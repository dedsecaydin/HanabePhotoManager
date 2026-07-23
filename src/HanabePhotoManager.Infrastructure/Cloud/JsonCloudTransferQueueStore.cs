using System.Text.Json;
using HanabePhotoManager.Core.Cloud;

namespace HanabePhotoManager.Infrastructure.Cloud;

public sealed class JsonCloudTransferQueueStore : ICloudTransferQueueStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _path;
    private readonly string _lockPath;

    public JsonCloudTransferQueueStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Queue path is required.", nameof(path));
        }

        _path = Path.GetFullPath(path);
        _lockPath = _path + ".lock";
    }

    public async Task<IReadOnlyList<CloudTransferJob>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        FileStream? lockStream = null;
        try
        {
            lockStream = await AcquireLockAsync(cancellationToken).ConfigureAwait(false);
            if (!File.Exists(_path))
            {
                return Array.Empty<CloudTransferJob>();
            }

            try
            {
                await using var stream = new FileStream(
                    _path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                var storedJobs = await JsonSerializer.DeserializeAsync<StoredCloudTransferJob?[]>(
                        stream,
                        SerializerOptions,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (storedJobs is null)
                {
                    throw new InvalidDataException($"Cloud transfer queue '{_path}' is not a JSON array.");
                }

                return Array.AsReadOnly(storedJobs.Select(ToDomain).ToArray());
            }
            catch (Exception exception) when (IsInvalidQueueData(exception))
            {
                throw new InvalidDataException(
                    $"Cloud transfer queue '{_path}' contains invalid data.",
                    exception);
            }
        }
        finally
        {
            if (lockStream is not null)
            {
                await lockStream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task SaveAsync(
        IReadOnlyCollection<CloudTransferJob> jobs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        if (jobs.Any(static job => job is null))
        {
            throw new ArgumentException("A queue cannot contain a null job.", nameof(jobs));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var storedJobs = jobs.Select(FromDomain).ToArray();

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        FileStream? lockStream = null;
        string? temporaryPath = null;
        Exception? primaryException = null;
        try
        {
            lockStream = await AcquireLockAsync(cancellationToken).ConfigureAwait(false);
            temporaryPath = CreateTemporaryPath();
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(
                        stream,
                        storedJobs,
                        SerializerOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, _path, overwrite: true);
        }
        catch (Exception exception)
        {
            primaryException = exception;
            throw;
        }
        finally
        {
            try
            {
                if (temporaryPath is not null && File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception cleanupException) when (primaryException is not null)
            {
                primaryException.Data["TemporaryCleanupException"] = cleanupException;
            }
            finally
            {
                if (lockStream is not null)
                {
                    await lockStream.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    private static bool IsInvalidQueueData(Exception exception) =>
        exception is JsonException or ArgumentException or InvalidOperationException or
            NotSupportedException or InvalidDataException;

    private async Task<FileStream> AcquireLockAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    _lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous);
            }
            catch (IOException exception) when (IsLockContention(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static bool IsLockContention(IOException exception)
    {
        var nativeErrorCode = exception.HResult & 0xFFFF;
        return nativeErrorCode is 32 or 33;
    }

    private string CreateTemporaryPath() =>
        Path.Combine(
            Path.GetDirectoryName(_path)!,
            $"{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");

    private static StoredCloudTransferJob FromDomain(CloudTransferJob job) =>
        new(
            job.Id,
            job.Provider,
            job.Destination.Value,
            job.Priority,
            job.State,
            job.Files.Select(static file => new StoredCloudTransferFile(
                file.LocalPath,
                file.RelativePath.Value,
                file.Size,
                file.ContentHash,
                file.UploadedBytes,
                file.RemoteId)).ToArray(),
            job.CreatedAt,
            job.FileVerifications.Select(static verification => new StoredCloudFileVerification(
                verification.RemoteId,
                verification.VerifiedAt,
                verification.IsVerified,
                verification.Reason)).ToArray());

    private static CloudTransferJob ToDomain(StoredCloudTransferJob? storedJob)
    {
        ArgumentNullException.ThrowIfNull(storedJob);
        var storedFiles = RequireReference(storedJob.Files, "files");
        var storedVerifications = RequireReference(storedJob.FileVerifications, "fileVerifications");

        var files = storedFiles.Select(static file =>
        {
            file = RequireReference(file, "files[]");
            return new CloudTransferFile(
                RequireReference(file.LocalPath, "files[].localPath"),
                new CloudRelativePath(RequireReference(file.RelativePath, "files[].relativePath")),
                RequireValue(file.Size, "files[].size"),
                file.ContentHash,
                RequireValue(file.UploadedBytes, "files[].uploadedBytes"),
                file.RemoteId);
        }).ToArray();
        var verifications = storedVerifications.Select(static verification =>
        {
            verification = RequireReference(verification, "fileVerifications[]");
            return new CloudFileVerification(
                RequireReference(verification.RemoteId, "fileVerifications[].remoteId"),
                RequireValue(verification.VerifiedAt, "fileVerifications[].verifiedAt"),
                RequireValue(verification.IsVerified, "fileVerifications[].isVerified"),
                RequireReference(verification.Reason, "fileVerifications[].reason"));
        }).ToArray();

        return new CloudTransferJob(
            RequireValue(storedJob.Id, "id"),
            RequireValue(storedJob.Provider, "provider"),
            new CloudPath(RequireReference(storedJob.Destination, "destination")),
            RequireValue(storedJob.Priority, "priority"),
            RequireValue(storedJob.State, "state"),
            files,
            RequireValue(storedJob.CreatedAt, "createdAt"),
            verifications);
    }

    private static T RequireReference<T>(T? value, string fieldName)
        where T : class =>
        value ?? throw new InvalidDataException($"Required queue field '{fieldName}' is missing.");

    private static T RequireValue<T>(T? value, string fieldName)
        where T : struct =>
        value ?? throw new InvalidDataException($"Required queue field '{fieldName}' is missing.");

    private sealed record StoredCloudTransferJob(
        Guid? Id,
        CloudProviderKind? Provider,
        string? Destination,
        CloudTransferPriority? Priority,
        CloudTransferState? State,
        StoredCloudTransferFile?[]? Files,
        DateTimeOffset? CreatedAt,
        StoredCloudFileVerification?[]? FileVerifications);

    private sealed record StoredCloudTransferFile(
        string? LocalPath,
        string? RelativePath,
        long? Size,
        string? ContentHash,
        long? UploadedBytes,
        string? RemoteId);

    private sealed record StoredCloudFileVerification(
        string? RemoteId,
        DateTimeOffset? VerifiedAt,
        bool? IsVerified,
        string? Reason);
}
