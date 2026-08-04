namespace HanabePhotoManager.App.WeChat;

public interface IWeChatDesktopGateway
{
    Task<WeChatGatewayStatus> EnsureReadyAsync(CancellationToken cancellationToken);

    Task<WeChatTarget?> LocateTargetAsync(
        string requestedName,
        CancellationToken cancellationToken);

    Task<WeChatBatchSendResult> SendBatchAsync(
        IReadOnlyList<WeChatSendItem> items,
        WeChatTarget target,
        CancellationToken cancellationToken);
}
