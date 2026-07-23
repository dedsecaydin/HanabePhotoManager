using System.Runtime.InteropServices;
using System.Security.Cryptography;
using HanabePhotoManager.Core.Imports;
using Microsoft.Win32.SafeHandles;

namespace HanabePhotoManager.Infrastructure.Files;

public sealed record VerifiedFileResult(PlannedFile File, string Sha256);

public sealed record GroupTransferResult(bool Success, string? Error, IReadOnlyList<VerifiedFileResult> VerifiedFiles);

public sealed class VerifiedFileTransfer(IFileHasher hasher)
{
    private readonly IFileHasher _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));

    public async Task<GroupTransferResult> TransferGroupAsync(
        ImportPlanItem item,
        bool deleteSourcesAfterVerify,
        CancellationToken cancellationToken)
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
                return Failure("A destination file has the same name but different content.", verifiedFiles);
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
                            return Failure($"Destination file is missing: {file.DestinationPath}", verifiedFiles);
                        }

                        var lease = sourceLeases.Single(lease => string.Equals(lease.Path, file.Source.FullPath, StringComparison.OrdinalIgnoreCase));
                        lease.Stream.Position = 0;
                        var sourceHash = await ComputeSha256Async(lease.Stream, cancellationToken).ConfigureAwait(false);
                        var destinationHash = await _hasher
                            .ComputeSha256Async(file.DestinationPath, cancellationToken)
                            .ConfigureAwait(false);

                        if (!string.Equals(sourceHash, destinationHash, StringComparison.OrdinalIgnoreCase))
                        {
                            return Failure($"Identical conflict changed before transfer: {file.DestinationPath}", verifiedFiles);
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

                        await using (var temporaryStream = new FileStream(
                                         file.TemporaryPath,
                                         FileMode.CreateNew,
                                         FileAccess.Write,
                                         FileShare.None,
                                         bufferSize: 1024 * 64,
                                         options: FileOptions.Asynchronous | FileOptions.SequentialScan))
                        {
                            await lease.Stream.CopyToAsync(temporaryStream, 1024 * 64, cancellationToken).ConfigureAwait(false);
                            await temporaryStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        }

                        var temporaryInfo = new FileInfo(file.TemporaryPath);
                        if (temporaryInfo.Length != file.Source.Length)
                        {
                            CleanupTemporaryFiles(copiedTemporaryFiles);
                            return Failure($"Temporary file length mismatch: {file.TemporaryPath}", verifiedFiles);
                        }

                        lease.Stream.Position = 0;
                        var sourceHash = await ComputeSha256Async(lease.Stream, cancellationToken).ConfigureAwait(false);
                        var temporaryHash = await _hasher
                            .ComputeSha256Async(file.TemporaryPath, cancellationToken)
                            .ConfigureAwait(false);

                        if (!string.Equals(sourceHash, temporaryHash, StringComparison.OrdinalIgnoreCase))
                        {
                            CleanupTemporaryFiles(copiedTemporaryFiles);
                            return Failure($"Temporary file hash mismatch: {file.TemporaryPath}", verifiedFiles);
                        }

                        verifiedFiles.Add(new VerifiedFileResult(file, sourceHash));
                        break;
                    }

                    default:
                        return Failure($"Unsupported conflict kind: {file.Conflict}", verifiedFiles);
                }
            }

            foreach (var file in item.Files.Where(file => file.Conflict == ConflictKind.None))
            {
                if (File.Exists(file.DestinationPath) || Directory.Exists(file.DestinationPath))
                {
                    CleanupTemporaryFiles(copiedTemporaryFiles);
                    return Failure($"Destination already exists before publish: {file.DestinationPath}", verifiedFiles);
                }
            }

            foreach (var file in item.Files.Where(file => file.Conflict == ConflictKind.None))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(file.TemporaryPath, file.DestinationPath, overwrite: false);
                publishedDestinations.Add(file.DestinationPath);
                copiedTemporaryFiles.Remove(file.TemporaryPath);
            }
            publishingCompleted = true;

            if (deleteSourcesAfterVerify)
            {
                await VerifySourcesUnchangedAsync(verifiedFiles, sourceLeases, cancellationToken).ConfigureAwait(false);
                deletingSources = true;
                DeleteSourceLeases(sourceLeases, cancellationToken);
            }

            return new GroupTransferResult(true, null, verifiedFiles.AsReadOnly());
        }
        catch (OperationCanceledException)
        {
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
            throw new ArgumentException("Transfer paths must be fully qualified.", nameof(file));
        }

        if (!string.Equals(
                Path.GetFullPath(file.TemporaryPath),
                Path.GetFullPath(file.DestinationPath + ".hanabe-part"),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("TemporaryPath must be DestinationPath plus .hanabe-part.", nameof(file));
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
                throw new IOException($"Source file changed after verification: {source.FullPath}");
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
            throw new IOException($"Unable to open source lease. Win32 error {Marshal.GetLastWin32Error()}.");
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
                throw new IOException($"Unable to delete source by handle. Win32 error {Marshal.GetLastWin32Error()}.");
            }
        }
    }

    private static SourceFileIdentity GetFileIdentity(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var information))
        {
            throw new IOException($"Unable to inspect file identity. Win32 error {Marshal.GetLastWin32Error()}.");
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
