using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using HanabePhotoManager.Core.Imports;
using Microsoft.Win32.SafeHandles;

namespace HanabePhotoManager.Infrastructure.Files;

/// <summary>已完成内容校验的计划文件及其源内容哈希。</summary>
public sealed record VerifiedFileResult(PlannedFile File, string Sha256);

/// <summary>一个媒体组的原子传输结果。</summary>
public sealed record GroupTransferResult(bool Success, string? Error, IReadOnlyList<VerifiedFileResult> VerifiedFiles);

/// <summary>
/// 在源文件租约保护下复制、校验并发布整个媒体组；失败时清理未发布的临时文件和目标文件。
/// </summary>
public sealed class VerifiedFileTransfer(IFileHasher hasher)
{
    private readonly IFileHasher _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));

    /// <summary>
    /// 传输计划项中的主文件和附属文件。仅在全部目标校验并发布成功后才按请求删除源文件。
    /// </summary>
    public async Task<GroupTransferResult> TransferGroupAsync(
        ImportPlanItem item,
        bool deleteSourcesAfterVerify,
        CancellationToken cancellationToken,
        Action<IReadOnlyList<VerifiedFileResult>>? beforeSourceDeletion = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        var verifiedFiles = new List<VerifiedFileResult>();
        var copiedTemporaryFiles = new List<string>();
        var publishedDestinations = new List<string>();
        var publishingCompleted = false;
        var deletingSources = false;
        IReadOnlyList<SourceLease> sourceLeases = Array.Empty<SourceLease>();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var file in item.Files)
            {
                ValidatePlannedFile(file);
            }

            if (item.Files.Any(file => file.Conflict == ConflictKind.SameNameDifferentContent))
            {
                return Failure("目标位置已有同名但内容不同的文件。", verifiedFiles);
            }

            sourceLeases = OpenSourceLeases(item);

            foreach (var file in item.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (file.Conflict)
                {
                    case ConflictKind.Identical:
                    {
                        if (!File.Exists(file.DestinationPath))
                        {
                            return Failure($"目标文件不存在：{file.DestinationPath}", verifiedFiles);
                        }

                        var lease = sourceLeases.Single(lease => string.Equals(lease.Path, file.Source.FullPath, StringComparison.OrdinalIgnoreCase));
                        lease.Stream.Position = 0;
                        var sourceHash = await ComputeSha256Async(lease.Stream, cancellationToken).ConfigureAwait(false);
                        var destinationHash = await _hasher
                            .ComputeSha256Async(file.DestinationPath, cancellationToken)
                            .ConfigureAwait(false);

                        if (!string.Equals(sourceHash, destinationHash, StringComparison.OrdinalIgnoreCase))
                        {
                            return Failure($"重复检测后文件发生变化：{file.DestinationPath}", verifiedFiles);
                        }

                        verifiedFiles.Add(new VerifiedFileResult(file, sourceHash));
                        break;
                    }

                    case ConflictKind.None:
                    {
                        var lease = sourceLeases.Single(lease => string.Equals(lease.Path, file.Source.FullPath, StringComparison.OrdinalIgnoreCase));
                        lease.Stream.Position = 0;
                        var destinationDirectory = Path.GetDirectoryName(file.DestinationPath);
                        if (!string.IsNullOrEmpty(destinationDirectory))
                        {
                            Directory.CreateDirectory(destinationDirectory);
                        }

                        var temporaryDirectory = Path.GetDirectoryName(file.TemporaryPath);
                        if (!string.IsNullOrEmpty(temporaryDirectory))
                        {
                            Directory.CreateDirectory(temporaryDirectory);
                        }

                        copiedTemporaryFiles.Add(file.TemporaryPath);
                        if (File.Exists(file.TemporaryPath))
                        {
                            File.Delete(file.TemporaryPath);
                        }

                        string sourceHash;
                        await using (var temporaryStream = new FileStream(
                                         file.TemporaryPath,
                                         FileMode.CreateNew,
                                         FileAccess.Write,
                                         FileShare.None,
                                         bufferSize: 1024 * 1024,
                                         options: FileOptions.Asynchronous | FileOptions.SequentialScan))
                        {
                            sourceHash = await CopyAndComputeSha256Async(
                                    lease.Stream,
                                    temporaryStream,
                                    cancellationToken)
                                .ConfigureAwait(false);
                            await temporaryStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        }

                        var temporaryInfo = new FileInfo(file.TemporaryPath);
                        if (temporaryInfo.Length != file.Source.Length)
                        {
                            CleanupTemporaryFiles(copiedTemporaryFiles);
                            return Failure($"临时文件大小不匹配：{file.TemporaryPath}", verifiedFiles);
                        }

                        var temporaryHash = await _hasher
                            .ComputeSha256Async(file.TemporaryPath, cancellationToken)
                            .ConfigureAwait(false);

                        if (!string.Equals(sourceHash, temporaryHash, StringComparison.OrdinalIgnoreCase))
                        {
                            CleanupTemporaryFiles(copiedTemporaryFiles);
                            return Failure($"临时文件校验不一致：{file.TemporaryPath}", verifiedFiles);
                        }

                        verifiedFiles.Add(new VerifiedFileResult(file, sourceHash));
                        break;
                    }

                    default:
                        return Failure($"不支持的冲突类型：{file.Conflict}", verifiedFiles);
                }
            }

            // 发布前再次检查全部目标，确保扫描计划与复制期间出现的外部文件不会被覆盖。
            foreach (var file in item.Files.Where(file => file.Conflict == ConflictKind.None))
            {
                if (File.Exists(file.DestinationPath) || Directory.Exists(file.DestinationPath))
                {
                    CleanupTemporaryFiles(copiedTemporaryFiles);
                    return Failure($"发布前目标文件已存在：{file.DestinationPath}", verifiedFiles);
                }
            }

            // 所有临时文件均验证成功后才进入发布阶段，保持媒体组尽可能接近原子提交。
            foreach (var file in item.Files.Where(file => file.Conflict == ConflictKind.None))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(file.TemporaryPath, file.DestinationPath, overwrite: false);
                publishedDestinations.Add(file.DestinationPath);
                copiedTemporaryFiles.Remove(file.TemporaryPath);
            }
            publishingCompleted = true;
            // 恢复凭据必须先可靠落盘，随后才允许删除任何源文件。
            beforeSourceDeletion?.Invoke(verifiedFiles.AsReadOnly());

            if (deleteSourcesAfterVerify)
            {
                // 删除前重新计算租约内容，避免校验后被外部进程修改的源文件遭到误删。
                await VerifySourcesUnchangedAsync(verifiedFiles, sourceLeases, cancellationToken).ConfigureAwait(false);
                deletingSources = true;
                DeleteSourceLeases(sourceLeases, cancellationToken);
            }

            return new GroupTransferResult(true, null, verifiedFiles.AsReadOnly());
        }
        catch (OperationCanceledException)
        {
            CleanupTemporaryFiles(copiedTemporaryFiles);
            if (!deletingSources && !publishingCompleted)
            {
                CleanupPublishedDestinations(publishedDestinations);
            }
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            CleanupTemporaryFiles(copiedTemporaryFiles);
            if (!deletingSources && !publishingCompleted)
            {
                CleanupPublishedDestinations(publishedDestinations);
            }

            return Failure(exception.Message, verifiedFiles);
        }
        finally
        {
            DisposeLeases(sourceLeases.Select(lease => lease.Stream));
        }
    }

    private static GroupTransferResult Failure(string error, List<VerifiedFileResult> verifiedFiles)
    {
        return new GroupTransferResult(false, error, verifiedFiles.AsReadOnly());
    }

    private static void ValidatePlannedFile(PlannedFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(file.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(file.Source.FullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(file.DestinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(file.TemporaryPath);

        if (!Path.IsPathFullyQualified(file.Source.FullPath) ||
            !Path.IsPathFullyQualified(file.DestinationPath) ||
            !Path.IsPathFullyQualified(file.TemporaryPath))
        {
            throw new ArgumentException("传输路径必须是完整路径。", nameof(file));
        }

        if (!string.Equals(
                Path.GetFullPath(file.TemporaryPath),
                Path.GetFullPath(file.DestinationPath + ".hanabe-part"),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("临时路径格式不正确。", nameof(file));
        }
    }

    private async Task VerifySourcesUnchangedAsync(
        IEnumerable<VerifiedFileResult> verifiedFiles,
        IReadOnlyList<SourceLease> sourceLeases,
        CancellationToken cancellationToken)
    {
        foreach (var result in verifiedFiles.DistinctBy(result => result.File.Source.FullPath, StringComparer.OrdinalIgnoreCase))
        {
            var source = result.File.Source;
            var lease = sourceLeases.Single(lease => string.Equals(lease.Path, source.FullPath, StringComparison.OrdinalIgnoreCase));
            lease.Stream.Position = 0;
            var currentHash = await ComputeSha256Async(lease.Stream, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(currentHash, result.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException($"校验后源文件发生变化：{source.FullPath}");
            }
        }
    }

    private static IReadOnlyList<SourceLease> OpenSourceLeases(ImportPlanItem item)
    {
        var sourceLeases = new List<SourceLease>();
        try
        {
            foreach (var sourcePath in item.Files.Select(file => file.Source.FullPath).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var stream = OpenSourceLeaseStream(sourcePath);
                try
                {
                    sourceLeases.Add(new SourceLease(stream, GetFileIdentity(stream.SafeFileHandle), sourcePath));
                }
                catch
                {
                    stream.Dispose();
                    throw;
                }
            }

            return sourceLeases;
        }
        catch
        {
            DisposeLeases(sourceLeases.Select(lease => lease.Stream));
            throw;
        }
    }

    private static void DisposeLeases(IEnumerable<FileStream> leases)
    {
        foreach (var lease in leases)
        {
            lease.Dispose();
        }
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static async Task<string> CopyAndComputeSha256Async(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
        try
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            while (true)
            {
                var bytesRead = await source
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                hasher.AppendData(buffer, 0, bytesRead);
                await destination
                    .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                    .ConfigureAwait(false);
            }

            return Convert.ToHexString(hasher.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static FileStream OpenSourceLeaseStream(string sourcePath)
    {
        var handle = CreateFile(
            sourcePath,
            GenericRead | DeleteAccess,
            FileShareRead,
            IntPtr.Zero,
            OpenExisting,
            FileFlagSequentialScan,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            throw new IOException($"无法打开源文件（Win32 错误 {Marshal.GetLastWin32Error()}）。");
        }

        return new FileStream(handle, FileAccess.Read, bufferSize: 1024 * 64, isAsync: false);
    }

    private static void DeleteSourceLeases(IEnumerable<SourceLease> sourceLeases, CancellationToken cancellationToken)
    {
        foreach (var lease in sourceLeases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var disposition = new FileDispositionInformation { DeleteFile = true };
            if (!SetFileInformationByHandle(
                    lease.Stream.SafeFileHandle,
                    FileInformationByHandleClass.FileDispositionInfo,
                    ref disposition,
                    (uint)Marshal.SizeOf<FileDispositionInformation>()))
            {
                throw new IOException($"无法删除源文件（Win32 错误 {Marshal.GetLastWin32Error()}）。");
            }
        }
    }

    private static SourceFileIdentity GetFileIdentity(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var information))
        {
            throw new IOException($"无法读取文件信息（Win32 错误 {Marshal.GetLastWin32Error()}）。");
        }

        return new SourceFileIdentity(
            information.VolumeSerialNumber,
            information.FileIndexHigh,
            information.FileIndexLow);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public FileTime CreationTime;
        public FileTime LastAccessTime;
        public FileTime LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct SourceFileIdentity(uint VolumeSerialNumber, uint FileIndexHigh, uint FileIndexLow);

    private sealed record SourceLease(FileStream Stream, SourceFileIdentity Identity, string Path);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInformation
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool DeleteFile;
    }

    private enum FileInformationByHandleClass
    {
        FileDispositionInfo = 4
    }

    private const uint GenericRead = 0x80000000;
    private const uint DeleteAccess = 0x00010000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagSequentialScan = 0x08000000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out ByHandleFileInformation lpFileInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle hFile,
        FileInformationByHandleClass fileInformationClass,
        ref FileDispositionInformation lpFileInformation,
        uint dwBufferSize);

    private static void CleanupTemporaryFiles(IEnumerable<string> temporaryPaths)
    {
        foreach (var path in temporaryPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void CleanupPublishedDestinations(IEnumerable<string> destinationPaths)
    {
        foreach (var path in destinationPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
