using System.Diagnostics;
using System.IO;
using HanabePhotoManager.App.Imports;

namespace HanabePhotoManager.Mac.Services;

/// <summary>Store Hanabe's origin marker in a dedicated macOS extended attribute.</summary>
internal sealed class MacOriginMetadataStore(
    Func<string[], CancellationToken, Task<(int ExitCode, string Output, string Error)>>? run = null)
    : IFileOriginMetadataStore
{
    private const string Attribute = "com.hanabe.origin";
    private readonly Func<string[], CancellationToken, Task<(int ExitCode, string Output, string Error)>> _run = run ?? RunXattrAsync;

    public async Task<FileOriginMetadata?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var result = await _run(["-p", Attribute, Path.GetFullPath(path)], cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 && HanabeOriginCommentCodec.TryParse(result.Output, out var metadata) ? metadata : null;
    }

    public async Task<FileOriginMetadataWriteResult> WriteAndVerifyAsync(string path, FileOriginMetadata metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path)) return FileOriginMetadataWriteResult.Failure("目标文件不存在");
        try
        {
            var value = HanabeOriginCommentCodec.Merge(null, metadata);
            var result = await _run(["-w", Attribute, value, Path.GetFullPath(path)], cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0) return FileOriginMetadataWriteResult.Failure(result.Error);
            var readBack = await ReadAsync(path, cancellationToken).ConfigureAwait(false);
            return readBack == metadata ? FileOriginMetadataWriteResult.Completed : FileOriginMetadataWriteResult.Failure("来源标记写入后回读校验失败");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return FileOriginMetadataWriteResult.Failure(ex.Message);
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunXattrAsync(string[] args, CancellationToken token)
    {
        var info = new ProcessStartInfo("/usr/bin/xattr") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException("无法启动来源标记工具。");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            await process.WaitForExitAsync().ConfigureAwait(false);
            throw;
        }
        return (process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }
}
