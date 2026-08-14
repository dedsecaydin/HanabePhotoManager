using System.IO;

namespace HanabePhotoManager.App.WeChat;

public enum WeChatSendItemState
{
    Pending,
    Staging,
    ReadyToSend,
    Sending,
    Sent,
    Failed,
    Ambiguous,
    Changed,
    Canceled
}

public sealed record WeChatSendItem(
    Guid QueueItemId,
    string SourcePath,
    string DisplayName,
    long Length,
    DateTimeOffset LastWriteTime,
    WeChatSendItemState State = WeChatSendItemState.Pending,
    int RetryCount = 0,
    string Message = "")
{
    public static WeChatSendItem Create(
        string sourcePath,
        long length,
        DateTimeOffset lastWriteTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        return new(
            Guid.NewGuid(),
            Path.GetFullPath(sourcePath),
            Path.GetFileName(sourcePath),
            length,
            lastWriteTime);
    }
}

public sealed record WeChatSendBatch(int Number, IReadOnlyList<WeChatSendItem> Items);

public sealed record WeChatTarget(
    string RequestedName,
    string ResolvedTitle,
    string TargetType,
    string ConfirmationToken);

public enum WeChatEvidenceState
{
    Sent,
    Failed,
    Ambiguous
}

public sealed record WeChatItemEvidence(
    Guid QueueItemId,
    WeChatEvidenceState State,
    bool InputCleared,
    bool NewFileBubbleFound,
    bool UploadCompleted,
    bool FailureMarkerFound,
    bool TargetUnchanged,
    string Message = "")
{
    public bool IsVerifiedSuccess =>
        State == WeChatEvidenceState.Sent
        && InputCleared
        && NewFileBubbleFound
        && UploadCompleted
        && !FailureMarkerFound
        && TargetUnchanged;
}

public sealed record WeChatBatchSendResult(IReadOnlyList<WeChatItemEvidence> Items);

public sealed record WeChatQueueProgress(
    int Total,
    int Sent,
    int Failed,
    int Ambiguous,
    int CurrentBatch,
    string CurrentFile);

public sealed record WeChatQueueResult(
    IReadOnlyList<WeChatSendItem> Items,
    bool IsPaused,
    bool IsCanceled);

public sealed record WeChatGatewayStatus(
    bool IsSupported,
    bool IsReady,
    string Message,
    int? ProcessId = null);
