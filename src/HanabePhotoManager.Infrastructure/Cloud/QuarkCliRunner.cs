using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace HanabePhotoManager.Infrastructure.Cloud;

/// <summary>
/// One NDJSON line emitted by the quark-drive.cjs CLI on stdout.
/// Every CLI invocation produces a stream of these lines; the final line is
/// always a <c>result</c> (success or failure), long-running commands may emit
/// <c>progress</c> lines, and list-style commands emit <c>list</c> lines.
/// </summary>
internal sealed record QuarkCliLine(
    int Code,
    string Message,
    string Action,
    string Type,
    JsonElement Data);

/// <summary>
/// The complete captured output of one quark-drive.cjs invocation.
/// </summary>
internal sealed record QuarkCliOutput(
    IReadOnlyList<QuarkCliLine> Lines,
    string StdError,
    int ExitCode,
    bool TimedOut)
{
    /// <summary>The final <c>result</c> line, or <see langword="null"/> when none was emitted.</summary>
    public QuarkCliLine? Result => Lines.LastOrDefault(static line => line.Type == "result");
}

/// <summary>
/// Runs the official quark-drive.cjs CLI (夸克网盘命令行工具) as a child process and
/// parses its NDJSON stdout protocol. The CLI stores its own credentials in its
/// own config files — this runner never touches tokens directly.
/// </summary>
internal static class QuarkCliRunner
{
    /// <summary>Node.js executable name; resolved from PATH unless overridden.</summary>
    public const string NodeExecutable = "node";

    /// <summary>
    /// Well-known install location of the quark-drive.cjs skill script under the
    /// current user's profile (Hermes skill store).
    /// </summary>
    private const string DefaultCliRelativePath =
        @"AppData\Local\Hermes Agent CN Desktop\data\hermes-home\skills\quarkclouddrive\scripts\quark-drive.cjs";

    /// <summary>
    /// Resolves the CLI script path: an explicit <c>HANAHE_QUARK_CLI_PATH</c>
    /// environment variable wins, otherwise the well-known Hermes skill location
    /// under the current user profile is used.
    /// </summary>
    public static string ResolveDefaultCliPath()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("HANAHE_QUARK_CLI_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
        {
            return fromEnvironment;
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(profile, DefaultCliRelativePath);
    }

    /// <summary>
    /// Executes <c>node &lt;cliPath&gt; &lt;arguments...&gt;</c>, captures stdout NDJSON lines
    /// (also delivered to <paramref name="onLine"/> as they arrive), collects stderr and
    /// enforces a hard <paramref name="timeout"/> (the process tree is killed on expiry).
    /// Never throws for CLI-reported errors — those arrive as NDJSON <c>result</c> lines.
    /// </summary>
    public static async Task<QuarkCliOutput> RunAsync(
        string cliPath,
        string nodePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<QuarkCliLine>? onLine = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cliPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodePath);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo(nodePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetTempPath(),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add(cliPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return new QuarkCliOutput([], "无法启动 node 进程。", -1, false);
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new QuarkCliOutput([], $"无法启动 node 进程：{ex.Message}", -1, false);
        }

        var lines = new List<QuarkCliLine>();
        var stdoutTask = ReadStdoutAsync(process, lines, onLine);
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        var exited = false;
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            exited = true;
        }
        catch (OperationCanceledException)
        {
            // 超时或调用方取消：终止整个进程树，避免残留 node 进程。
            TryKill(process);
        }
        catch (InvalidOperationException)
        {
            // 罕见竞态：进程在开始等待前已经退出。
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        var exitCode = exited ? process.ExitCode : -1;

        return new QuarkCliOutput(stdout, stderr, exitCode, !exited);
    }

    private static async Task<IReadOnlyList<QuarkCliLine>> ReadStdoutAsync(
        Process process,
        List<QuarkCliLine> lines,
        Action<QuarkCliLine>? onLine)
    {
        try
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false)) is not null)
            {
                if (TryParseLine(line, out var parsed))
                {
                    lines.Add(parsed);
                    onLine?.Invoke(parsed);
                }
            }
        }
        catch (IOException)
        {
            // 进程被强制终止时输出流关闭：读取提前结束，属预期。
        }
        catch (ObjectDisposedException)
        {
        }

        return lines;
    }

    private static bool TryParseLine(string line, out QuarkCliLine parsed)
    {
        parsed = null!;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            var code = 0;
            if (root.TryGetProperty("code", out var codeProperty) && codeProperty.ValueKind == JsonValueKind.Number)
            {
                code = codeProperty.TryGetInt32(out var parsedCode) ? parsedCode : 0;
            }

            var message = root.TryGetProperty("msg", out var messageProperty)
                ? messageProperty.GetString() ?? ""
                : "";
            var action = root.TryGetProperty("action", out var actionProperty)
                ? actionProperty.GetString() ?? ""
                : "";
            var type = root.TryGetProperty("type", out var typeProperty)
                ? typeProperty.GetString() ?? ""
                : "";
            var data = root.TryGetProperty("data", out var dataProperty)
                ? dataProperty.Clone()
                : default;

            parsed = new QuarkCliLine(code, message, action, type, data);
            return true;
        }
        catch (JsonException)
        {
            // 非 NDJSON 行（如 banner/警告）直接跳过。
            return false;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
        }
    }
}
