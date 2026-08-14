using System.Runtime.CompilerServices;
using System.Text.Json;
using HanabePhotoManager.Core.Cloud;

namespace HanabePhotoManager.Infrastructure.Cloud;

/// <summary>
/// 夸克网盘（Quark）provider：通过官方 quark-drive.cjs CLI（NDJSON 协议）访问
/// 真实账户数据。CLI 自行管理凭据（config/accounts），本 provider 从不直接读取
/// token。未授权（-103）或 CLI 不可用时一律返回结构化"未登录"状态，不抛致命异常。
/// </summary>
public sealed class QuarkCloudProvider : ICloudProvider
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan UploadTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(15);

    private readonly string _cliPath;
    private readonly string _nodePath;
    private readonly string _downloadDirectory;

    public QuarkCloudProvider(string? cliPath = null, string? nodePath = null)
    {
        _cliPath = string.IsNullOrWhiteSpace(cliPath)
            ? QuarkCliRunner.ResolveDefaultCliPath()
            : cliPath;
        _nodePath = string.IsNullOrWhiteSpace(nodePath)
            ? QuarkCliRunner.NodeExecutable
            : nodePath;
        _downloadDirectory = Path.Combine(
            Path.GetTempPath(),
            "HanabePhotoManager",
            "QuarkCloud",
            "reads");
    }

    public CloudProviderKind Kind => CloudProviderKind.Quark;

    /// <summary>解析默认 CLI 脚本路径（供登录按钮等外部使用）。</summary>
    public static string ResolveDefaultCliPath() => QuarkCliRunner.ResolveDefaultCliPath();

    /// <summary>当前使用的 quark-drive.cjs 路径。</summary>
    public string CliPath => _cliPath;

    /// <summary>
    /// 执行 <c>quark-drive.cjs login</c>：启动本地授权服务器并自动打开浏览器完成
    /// OAuth，命令会阻塞直到授权完成或 CLI 内部超时。成功后 CLI 自行持久化凭据。
    /// </summary>
    /// <returns>授权成功返回 <see langword="true"/>，失败/取消/超时返回 <see langword="false"/>。</returns>
    public async Task<bool> LoginAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var output = await QuarkCliRunner.RunAsync(
                _cliPath,
                _nodePath,
                ["login"],
                LoginTimeout,
                cancellationToken).ConfigureAwait(false);
            return output.Result is { Code: 0 };
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<CloudAccountState> GetAccountStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var output = await QuarkCliRunner.RunAsync(
                _cliPath,
                _nodePath,
                ["get-user-info"],
                DefaultTimeout,
                cancellationToken).ConfigureAwait(false);

            var result = output.Result;
            if (result is null)
            {
                return NotAuthenticated(output.TimedOut ? "未登录 · CLI 调用超时" : "未登录 · CLI 无有效输出");
            }

            if (result.Code != 0)
            {
                // -103 = 未登录；其他负数为 CLI 错误，同样如实按未登录/不可用处理。
                var reason = result.Code == -103 ? "请先完成夸克网盘授权" : result.Message;
                return NotAuthenticated($"未登录 · {reason}");
            }

            long used = 0;
            long total = 0;
            if (QuarkCloudParsing.TryGetProperty(result.Data, "vipInfo", out var vip) &&
                vip.ValueKind == JsonValueKind.Object)
            {
                if (QuarkCloudParsing.TryGetInt64(vip, "used", out var usedValue))
                {
                    used = usedValue;
                }

                if (QuarkCloudParsing.TryGetInt64(vip, "capacity", out var capacityValue))
                {
                    total = capacityValue;
                }
            }

            if (total <= 0)
            {
                // 无容量信息：如实显示 0/0，UI 呈现"暂无容量信息"，不伪造数字。
                used = 0;
                total = 0;
            }
            else if (used > total)
            {
                // 防御：容量数据异常时钳制，避免违反 CloudAccountState 的约束。
                used = total;
            }

            var nickname = QuarkCloudParsing.TryGetProperty(result.Data, "userInfo", out var userInfo) &&
                           userInfo.ValueKind == JsonValueKind.Object &&
                           QuarkCloudParsing.TryGetProperty(userInfo, "nickname", out var nicknameProperty)
                ? nicknameProperty.GetString()
                : null;

            return new CloudAccountState(
                Kind,
                true,
                "夸克网盘",
                used,
                total,
                string.IsNullOrWhiteSpace(nickname) ? "已连接" : $"已连接 · {nickname}");
        }
        catch (Exception ex)
        {
            // CLI 不可用（node 缺失、脚本缺失等）：如实按未登录报告，不抛致命异常。
            return NotAuthenticated($"未登录 · 夸克 CLI 调用失败：{ex.Message}");
        }
    }

    public async IAsyncEnumerable<CloudObject> ListAsync(
        CloudPath directory,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var requested = directory.Value;

        // 官方 CLI 未提供 browse/目录列举命令，按 spec 用空关键词搜索作为尽力而为的列表：
        // 结果含真实 path 时按请求目录过滤，无 path 信息时仅根目录全量返回。
        QuarkCliOutput output;
        try
        {
            output = await QuarkCliRunner.RunAsync(
                _cliPath,
                _nodePath,
                ["search", "--keyword", "", "--size", "100", "--stdout-only"],
                DefaultTimeout,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 列表读取失败：返回空列表，不向调用方抛致命异常。
            yield break;
        }

        var result = output.Result;
        if (result is null || result.Code != 0)
        {
            yield break;
        }

        var items = new List<JsonElement>();
        if (QuarkCloudParsing.TryGetProperty(result.Data, "file_list", out var preview) &&
            preview.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in preview.EnumerateArray())
            {
                items.Add(entry);
            }
        }

        // search 的 NDJSON 只带最多 5 条预览，全量结果在 artifact jsonl 落盘文件里。
        foreach (var line in output.Lines)
        {
            if (line.Type != "artifact" ||
                !QuarkCloudParsing.TryGetProperty(line.Data, "file_path", out var filePathProperty))
            {
                continue;
            }

            var filePath = filePathProperty.GetString();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                continue;
            }

            foreach (var entry in QuarkCloudParsing.ReadArtifactEntries(filePath))
            {
                items.Add(entry);
            }

            break;
        }

        var emitted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in items)
        {
            if (QuarkCloudParsing.TryMap(entry, requested, out var mapped) &&
                emitted.Add(mapped.RemoteId))
            {
                yield return mapped;
            }
        }
    }

    public Task<Stream?> OpenThumbnailAsync(CloudObject item, CancellationToken cancellationToken)
    {
        // 夸克 CLI 没有缩略图读取接口：如实不支持。
        return Task.FromResult<Stream?>(null);
    }

    public async Task<Stream> OpenReadAsync(CloudObject item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        try
        {
            Directory.CreateDirectory(_downloadDirectory);
            var output = await QuarkCliRunner.RunAsync(
                _cliPath,
                _nodePath,
                ["download", "--fid", item.RemoteId, "--output-dir", _downloadDirectory, "--overwrite"],
                DefaultTimeout,
                cancellationToken).ConfigureAwait(false);

            var result = output.Result;
            if (result is null || result.Code != 0)
            {
                throw new IOException($"夸克网盘读取失败：{result?.Message ?? "CLI 无输出"}");
            }

            var path = QuarkCloudParsing.TryGetProperty(result.Data, "filePath", out var filePathProperty)
                ? filePathProperty.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"夸克网盘读取的文件不存在：{path ?? item.Name}",
                    path);
            }

            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException or UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new IOException($"夸克网盘读取失败：{ex.Message}", ex);
        }
    }

    public async Task<CloudObject> EnsureFolderAsync(CloudPath path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);

        QuarkCliOutput output;
        try
        {
            output = await QuarkCliRunner.RunAsync(
                _cliPath,
                _nodePath,
                ["create-folder", "--dir-path", path.Value],
                DefaultTimeout,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new IOException($"夸克网盘创建目录失败：{ex.Message}", ex);
        }

        var result = output.Result;
        if (result is null || result.Code != 0)
        {
            throw new IOException($"夸克网盘创建目录失败：{result?.Message ?? "CLI 无输出"}");
        }

        var fid = QuarkCloudParsing.TryGetProperty(result.Data, "fid", out var fidProperty)
            ? fidProperty.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(fid))
        {
            throw new IOException("夸克网盘创建目录成功但未返回 FID。");
        }

        var fullPath = QuarkCloudParsing.TryGetProperty(result.Data, "full_path", out var fullPathProperty)
            ? fullPathProperty.GetString()
            : null;
        var remotePath = path;
        if (!string.IsNullOrWhiteSpace(fullPath) &&
            QuarkCloudParsing.TryCreateCloudPath(QuarkCloudParsing.NormalizeQuarkPath(fullPath), out var mappedPath))
        {
            remotePath = mappedPath;
        }

        var name = path.Value.TrimEnd('/').Split('/').Last(static segment => segment.Length > 0);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "夸克网盘";
        }

        return new CloudObject(
            Kind,
            fid,
            remotePath,
            name,
            CloudObjectKind.Folder,
            0,
            DateTimeOffset.UtcNow,
            null,
            false);
    }

    public async Task<string> UploadAsync(
        string localPath,
        CloudPath destination,
        IProgress<CloudUploadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(localPath);
        ArgumentNullException.ThrowIfNull(destination);
        if (!File.Exists(localPath))
        {
            throw new FileNotFoundException("待上传文件不存在。", localPath);
        }

        var parentFid = await ResolveParentFidAsync(destination, cancellationToken).ConfigureAwait(false);

        var arguments = new List<string> { "upload", localPath };
        if (parentFid is not null)
        {
            arguments.Add("--parent-fid");
            arguments.Add(parentFid);
        }

        var fileName = Path.GetFileName(localPath);
        var totalBytes = new FileInfo(localPath).Length;

        QuarkCliOutput output;
        try
        {
            output = await QuarkCliRunner.RunAsync(
                _cliPath,
                _nodePath,
                arguments,
                UploadTimeout,
                cancellationToken,
                line => QuarkCloudParsing.ReportUploadProgress(line, progress, fileName)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new IOException($"夸克网盘上传失败：{ex.Message}", ex);
        }

        var result = output.Result;
        if (result is null || result.Code != 0)
        {
            throw new IOException($"夸克网盘上传失败：{result?.Message ?? "CLI 无输出"}");
        }

        var fid = QuarkCloudParsing.FindUploadedFid(output, fileName) ??
                  QuarkCloudParsing.ReadFirstFid(result.Data);
        if (string.IsNullOrWhiteSpace(fid))
        {
            throw new IOException("夸克网盘上传完成但未返回文件 FID。");
        }

        progress?.Report(new CloudUploadProgress(totalBytes, totalBytes, fileName));
        return fid;
    }

    public async Task<CloudVerificationResult> VerifyAsync(
        string remoteId,
        CloudTransferFile expected,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (string.IsNullOrWhiteSpace(remoteId))
        {
            return new CloudVerificationResult(false, "缺少远程文件 ID，无法校验。", null);
        }

        var remoteName = expected.RelativePath.Value
            .Split('/')
            .Last(static segment => segment.Length > 0);
        if (string.IsNullOrWhiteSpace(remoteName))
        {
            remoteName = Path.GetFileName(expected.LocalPath);
        }

        try
        {
            var output = await QuarkCliRunner.RunAsync(
                _cliPath,
                _nodePath,
                ["search", "--keyword", remoteName, "--size", "100", "--stdout-only"],
                DefaultTimeout,
                cancellationToken).ConfigureAwait(false);

            var result = output.Result;
            if (result is null || result.Code != 0)
            {
                return new CloudVerificationResult(
                    false,
                    $"夸克网盘校验失败：{result?.Message ?? "CLI 无输出"}",
                    remoteId);
            }

            var found = QuarkCloudParsing.SearchContainsFid(result.Data, remoteId);
            if (!found)
            {
                foreach (var line in output.Lines)
                {
                    if (line.Type != "artifact" ||
                        !QuarkCloudParsing.TryGetProperty(line.Data, "file_path", out var filePathProperty))
                    {
                        continue;
                    }

                    var filePath = filePathProperty.GetString();
                    if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    {
                        continue;
                    }

                    foreach (var entry in QuarkCloudParsing.ReadArtifactEntries(filePath))
                    {
                        if (string.Equals(
                                QuarkCloudParsing.GetString(entry, "fid"),
                                remoteId,
                                StringComparison.Ordinal))
                        {
                            found = true;
                            break;
                        }
                    }

                    break;
                }
            }

            return found
                ? new CloudVerificationResult(true, "夸克网盘搜索确认文件存在。", remoteId)
                : new CloudVerificationResult(false, "夸克网盘搜索未找到该文件。", remoteId);
        }
        catch (Exception ex)
        {
            return new CloudVerificationResult(false, $"夸克网盘校验失败：{ex.Message}", remoteId);
        }
    }

    private CloudAccountState NotAuthenticated(string statusText) =>
        new(Kind, false, "夸克网盘", 0, 0, statusText);

    private async Task<string?> ResolveParentFidAsync(
        CloudPath destination,
        CancellationToken cancellationToken)
    {
        if (destination.Value == "/")
        {
            // 根目录：交给 CLI 默认行为（不传 --parent-fid）。
            return null;
        }

        try
        {
            var output = await QuarkCliRunner.RunAsync(
                _cliPath,
                _nodePath,
                ["create-folder", "--dir-path", destination.Value],
                DefaultTimeout,
                cancellationToken).ConfigureAwait(false);

            var result = output.Result;
            if (result is null || result.Code != 0)
            {
                return null;
            }

            return QuarkCloudParsing.TryGetProperty(result.Data, "fid", out var fidProperty)
                ? fidProperty.GetString()
                : null;
        }
        catch (Exception)
        {
            // 目录解析失败时退化为 CLI 默认目录（尽力而为，不中断上传）。
            return null;
        }
    }
}
