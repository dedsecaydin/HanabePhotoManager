using HanabePhotoManager.Core;

namespace HanabePhotoManager.Core.Cloud;

public enum CloudProviderKind
{
    Quark = 1,
    Baidu = 2,
    Simulated = 3
}

public enum CloudObjectKind
{
    Folder = 1,
    Image = 2,
    Raw = 3,
    Video = 4,
    Audio = 5,
    Other = 6
}

public enum CloudTransferPriority
{
    Required = 1,
    Opportunistic = 2
}

public enum CloudTransferState
{
    Pending = 1,
    Running = 2,
    Paused = 3,
    Verifying = 4,
    Completed = 5,
    Failed = 6,
    Canceled = 7
}

public sealed record CloudPath
{
    public CloudPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Cloud path is required.", nameof(value));
        }

        var normalized = value.Replace('\\', '/');
        if (CloudPathSyntax.HasAuthority(normalized) || CloudPathSyntax.HasDriveQualifier(normalized))
        {
            throw new ArgumentException("Cloud path cannot use Windows local path syntax.", nameof(value));
        }

        var parts = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && CloudPathSyntax.IsNtNamespace(parts[0]))
        {
            throw new ArgumentException("Cloud path cannot use an NT namespace.", nameof(value));
        }

        if (parts.Any(static part => part is "." or ".."))
        {
            throw new ArgumentException("Cloud path cannot contain traversal segments.", nameof(value));
        }

        Value = parts.Length == 0 ? "/" : "/" + string.Join('/', parts);
    }

    public string Value { get; }

    public CloudPath Combine(CloudRelativePath relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var prefix = Value == "/" ? string.Empty : Value;
        return new CloudPath($"{prefix}/{relativePath.Value}");
    }

    public override string ToString() => Value;
}

public sealed record CloudRelativePath
{
    public CloudRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Cloud relative path is required.", nameof(value));
        }

        var normalized = value.Replace('\\', '/');
        if (normalized[0] == '/' ||
            CloudPathSyntax.HasAuthority(normalized) ||
            CloudPathSyntax.HasDriveQualifier(normalized))
        {
            throw new ArgumentException("Cloud relative path must be truly relative.", nameof(value));
        }

        var parts = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            throw new ArgumentException("Cloud relative path cannot be the root path.", nameof(value));
        }

        if (CloudPathSyntax.IsNtNamespace(parts[0]))
        {
            throw new ArgumentException("Cloud relative path cannot use an NT namespace.", nameof(value));
        }

        if (parts.Any(static part => part is "." or ".."))
        {
            throw new ArgumentException("Cloud relative path cannot contain traversal segments.", nameof(value));
        }

        Value = string.Join('/', parts);
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record CloudAccountState
{
    public CloudAccountState(
        CloudProviderKind provider,
        bool isAuthenticated,
        string displayName,
        long usedBytes,
        long totalBytes,
        string statusText)
    {
        if (!Enum.IsDefined(provider))
        {
            throw new ArgumentOutOfRangeException(nameof(provider), provider, "Cloud provider is undefined.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Account display name is required.", nameof(displayName));
        }

        if (usedBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(usedBytes), usedBytes, "Used bytes cannot be negative.");
        }

        if (totalBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalBytes), totalBytes, "Total bytes cannot be negative.");
        }

        if (usedBytes > totalBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(usedBytes),
                usedBytes,
                "Used bytes cannot exceed total bytes.");
        }

        if (string.IsNullOrWhiteSpace(statusText))
        {
            throw new ArgumentException("Account status text is required.", nameof(statusText));
        }

        Provider = provider;
        IsAuthenticated = isAuthenticated;
        DisplayName = displayName;
        UsedBytes = usedBytes;
        TotalBytes = totalBytes;
        StatusText = statusText;
    }

    public CloudProviderKind Provider { get; }

    public bool IsAuthenticated { get; }

    public string DisplayName { get; }

    public long UsedBytes { get; }

    public long TotalBytes { get; }

    public string StatusText { get; }
}

public sealed record CloudObject
{
    public CloudObject(
        CloudProviderKind provider,
        string remoteId,
        CloudPath path,
        string name,
        CloudObjectKind kind,
        long size,
        DateTimeOffset modifiedAt,
        string? thumbnailKey,
        bool isHanabeManaged)
    {
        if (!Enum.IsDefined(provider))
        {
            throw new ArgumentOutOfRangeException(nameof(provider), provider, "Cloud provider is undefined.");
        }

        if (string.IsNullOrWhiteSpace(remoteId))
        {
            throw new ArgumentException("Remote id is required.", nameof(remoteId));
        }

        ArgumentNullException.ThrowIfNull(path);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Object name is required.", nameof(name));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Cloud object kind is undefined.");
        }

        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "Object size cannot be negative.");
        }

        Provider = provider;
        RemoteId = remoteId;
        Path = path;
        Name = name;
        Kind = kind;
        Size = size;
        ModifiedAt = modifiedAt;
        ThumbnailKey = thumbnailKey;
        IsHanabeManaged = isHanabeManaged;
    }

    public CloudProviderKind Provider { get; }

    public string RemoteId { get; }

    public CloudPath Path { get; }

    public string Name { get; }

    public CloudObjectKind Kind { get; }

    public long Size { get; }

    public DateTimeOffset ModifiedAt { get; }

    public string? ThumbnailKey { get; }

    public bool IsHanabeManaged { get; }
}

public sealed record CloudTransferFile
{
    public CloudTransferFile(
        string localPath,
        CloudRelativePath relativePath,
        long size,
        string? contentHash,
        long uploadedBytes = 0,
        string? remoteId = null)
    {
        if (string.IsNullOrWhiteSpace(localPath))
        {
            throw new ArgumentException("Local path is required.", nameof(localPath));
        }

        if (!LocalPathSyntax.IsFullyQualified(localPath))
        {
            throw new ArgumentException("Local path must be an absolute file path.", nameof(localPath));
        }

        ArgumentNullException.ThrowIfNull(relativePath);
        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "File size cannot be negative.");
        }

        if (uploadedBytes < 0 || uploadedBytes > size)
        {
            throw new ArgumentOutOfRangeException(
                nameof(uploadedBytes),
                uploadedBytes,
                "Uploaded bytes must be between zero and the file size.");
        }

        if (remoteId is not null && string.IsNullOrWhiteSpace(remoteId))
        {
            throw new ArgumentException("Remote id cannot be blank when provided.", nameof(remoteId));
        }

        LocalPath = localPath;
        RelativePath = relativePath;
        Size = size;
        ContentHash = contentHash;
        UploadedBytes = uploadedBytes;
        RemoteId = remoteId;
    }

    public string LocalPath { get; }

    public CloudRelativePath RelativePath { get; }

    public long Size { get; }

    public string? ContentHash { get; }

    public long UploadedBytes { get; }

    public string? RemoteId { get; }

    public CloudTransferFile WithProgress(long uploadedBytes) =>
        new(LocalPath, RelativePath, Size, ContentHash, uploadedBytes, RemoteId);

    public CloudTransferFile WithRemoteId(string remoteId)
    {
        if (string.IsNullOrWhiteSpace(remoteId))
        {
            throw new ArgumentException("Remote id is required.", nameof(remoteId));
        }

        return new CloudTransferFile(LocalPath, RelativePath, Size, ContentHash, UploadedBytes, remoteId);
    }
}

public sealed record CloudFileVerification
{
    public CloudFileVerification(
        string remoteId,
        DateTimeOffset verifiedAt,
        bool isVerified,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(remoteId))
        {
            throw new ArgumentException("Verified remote id is required.", nameof(remoteId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Verification reason is required.", nameof(reason));
        }

        RemoteId = remoteId;
        VerifiedAt = verifiedAt;
        IsVerified = isVerified;
        Reason = reason;
    }

    public string RemoteId { get; }

    public DateTimeOffset VerifiedAt { get; }

    public bool IsVerified { get; }

    public string Reason { get; }
}

public sealed class CloudTransferJob
{
    public CloudTransferJob(
        Guid id,
        CloudProviderKind provider,
        CloudPath destination,
        CloudTransferPriority priority,
        CloudTransferState state,
        IReadOnlyList<CloudTransferFile> files,
        DateTimeOffset createdAt,
        IReadOnlyList<CloudFileVerification>? fileVerifications = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Job id cannot be empty.", nameof(id));
        }

        if (!Enum.IsDefined(provider))
        {
            throw new ArgumentOutOfRangeException(nameof(provider), provider, "Cloud provider is undefined.");
        }

        ArgumentNullException.ThrowIfNull(destination);
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), priority, "Transfer priority is undefined.");
        }

        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Transfer state is undefined.");
        }

        ArgumentNullException.ThrowIfNull(files);
        if (files.Count == 0)
        {
            throw new ArgumentException("A job needs at least one file.", nameof(files));
        }

        if (files.Any(static file => file is null))
        {
            throw new ArgumentException("A job cannot contain a null file.", nameof(files));
        }

        var copiedFiles = files.ToArray();
        var copiedVerifications = fileVerifications?.ToArray() ?? Array.Empty<CloudFileVerification>();
        if (copiedVerifications.Any(static verification => verification is null))
        {
            throw new ArgumentException("Verification evidence cannot contain null entries.", nameof(fileVerifications));
        }

        if (state == CloudTransferState.Completed)
        {
            ValidateCompletedEvidence(copiedFiles, copiedVerifications, createdAt);
        }
        else if (copiedVerifications.Length > 0)
        {
            throw new ArgumentException(
                "Verification evidence is only valid for a completed job.",
                nameof(fileVerifications));
        }

        Id = id;
        Provider = provider;
        Destination = destination;
        Priority = priority;
        State = state;
        Files = Array.AsReadOnly(copiedFiles);
        CreatedAt = createdAt;
        FileVerifications = Array.AsReadOnly(copiedVerifications);
    }

    public Guid Id { get; }

    public CloudProviderKind Provider { get; }

    public CloudPath Destination { get; }

    public CloudTransferPriority Priority { get; }

    public CloudTransferState State { get; }

    public IReadOnlyList<CloudTransferFile> Files { get; }

    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyList<CloudFileVerification> FileVerifications { get; }

    public bool IsVerified =>
        State == CloudTransferState.Completed && FileVerifications.Count == Files.Count;

    public CloudTransferJob WithState(CloudTransferState state)
    {
        if (state == CloudTransferState.Completed)
        {
            throw new InvalidOperationException("Use MarkVerified to complete a transfer job.");
        }

        return new CloudTransferJob(Id, Provider, Destination, Priority, state, Files, CreatedAt);
    }

    public CloudTransferJob MarkVerified(
        IReadOnlyCollection<CloudVerificationResult> results,
        DateTimeOffset verifiedAt)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (Files.Any(static file =>
                file.UploadedBytes != file.Size || string.IsNullOrWhiteSpace(file.RemoteId)))
        {
            throw new InvalidOperationException(
                "All files must be fully uploaded with remote identifiers before verification can complete.");
        }

        if (results.Count != Files.Count || results.Any(static result => result is null || !result.IsVerified))
        {
            throw new InvalidOperationException("Every file requires one successful verification result.");
        }

        var resultsByRemoteId = new Dictionary<string, CloudVerificationResult>(StringComparer.Ordinal);
        foreach (var result in results)
        {
            if (result.RemoteId is null || !resultsByRemoteId.TryAdd(result.RemoteId, result))
            {
                throw new InvalidOperationException("Verification result remote ids must be unique and non-null.");
            }
        }

        var evidence = new List<CloudFileVerification>(Files.Count);
        foreach (var file in Files)
        {
            if (file.RemoteId is null || !resultsByRemoteId.TryGetValue(file.RemoteId, out var result))
            {
                throw new InvalidOperationException("Verification result remote ids must exactly match uploaded files.");
            }

            evidence.Add(new CloudFileVerification(file.RemoteId, verifiedAt, true, result.Reason));
        }

        return new CloudTransferJob(
            Id,
            Provider,
            Destination,
            Priority,
            CloudTransferState.Completed,
            Files,
            CreatedAt,
            evidence);
    }

    private static void ValidateCompletedEvidence(
        IReadOnlyList<CloudTransferFile> files,
        IReadOnlyList<CloudFileVerification> fileVerifications,
        DateTimeOffset createdAt)
    {
        if (files.Any(static file =>
                file.UploadedBytes != file.Size || string.IsNullOrWhiteSpace(file.RemoteId)))
        {
            throw new ArgumentException(
                "A completed job requires fully uploaded files with remote identifiers.",
                nameof(files));
        }

        if (fileVerifications.Count != files.Count ||
            fileVerifications.Any(static verification => !verification.IsVerified))
        {
            throw new ArgumentException(
                "A completed job requires one successful verification per file.",
                nameof(fileVerifications));
        }

        if (fileVerifications.Any(verification => verification.VerifiedAt < createdAt))
        {
            throw new ArgumentOutOfRangeException(
                nameof(fileVerifications),
                "Verification evidence cannot predate job creation.");
        }

        var fileRemoteIds = files.Select(static file => file.RemoteId!).ToArray();
        var evidenceRemoteIds = fileVerifications.Select(static verification => verification.RemoteId).ToArray();
        if (fileRemoteIds.Distinct(StringComparer.Ordinal).Count() != fileRemoteIds.Length ||
            evidenceRemoteIds.Distinct(StringComparer.Ordinal).Count() != evidenceRemoteIds.Length ||
            !fileRemoteIds.Order(StringComparer.Ordinal).SequenceEqual(
                evidenceRemoteIds.Order(StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Verification evidence remote ids must uniquely and exactly match job files.",
                nameof(fileVerifications));
        }
    }
}

public sealed record CloudUploadProgress
{
    public CloudUploadProgress(long bytesTransferred, long totalBytes, string currentFile)
    {
        if (bytesTransferred < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bytesTransferred),
                bytesTransferred,
                "Transferred bytes cannot be negative.");
        }

        if (totalBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalBytes), totalBytes, "Total bytes cannot be negative.");
        }

        if (bytesTransferred > totalBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bytesTransferred),
                bytesTransferred,
                "Transferred bytes cannot exceed total bytes.");
        }

        if (string.IsNullOrWhiteSpace(currentFile))
        {
            throw new ArgumentException("Current file is required.", nameof(currentFile));
        }

        BytesTransferred = bytesTransferred;
        TotalBytes = totalBytes;
        CurrentFile = currentFile;
    }

    public long BytesTransferred { get; }

    public long TotalBytes { get; }

    public string CurrentFile { get; }
}

public sealed record CloudVerificationResult
{
    public CloudVerificationResult(bool isVerified, string reason, string? remoteId)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Verification reason is required.", nameof(reason));
        }

        if (remoteId is not null && string.IsNullOrWhiteSpace(remoteId))
        {
            throw new ArgumentException("Remote id cannot be blank when provided.", nameof(remoteId));
        }

        if (isVerified && remoteId is null)
        {
            throw new ArgumentException("A successful verification requires a remote id.", nameof(remoteId));
        }

        IsVerified = isVerified;
        Reason = reason;
        RemoteId = remoteId;
    }

    public bool IsVerified { get; }

    public string Reason { get; }

    public string? RemoteId { get; }
}

file static class CloudPathSyntax
{
    public static bool HasAuthority(string normalized)
    {
        return normalized.StartsWith("//", StringComparison.Ordinal) &&
            normalized.AsSpan().Trim('/').Length > 0;
    }

    public static bool HasDriveQualifier(string normalized)
    {
        var candidate = normalized.AsSpan().TrimStart('/');
        return candidate.Length >= 2 && char.IsAsciiLetter(candidate[0]) && candidate[1] == ':';
    }

    public static bool IsNtNamespace(string firstSegment)
    {
        return firstSegment.Equals("??", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("Device", StringComparison.OrdinalIgnoreCase) ||
            firstSegment.Equals("GLOBALROOT", StringComparison.OrdinalIgnoreCase);
    }
}
