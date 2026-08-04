namespace HanabePhotoManager.App.WeChat;

public sealed class WeChatSendQueueService
{
    private readonly IWeChatDesktopGateway _gateway;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly IWeChatSendLog? _log;

    public WeChatSendQueueService(
        IWeChatDesktopGateway gateway,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        IWeChatSendLog? log = null)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _delay = delay ?? Task.Delay;
        _log = log;
    }

    public async Task<WeChatQueueResult> SendAsync(
        IReadOnlyList<WeChatSendItem> sourceItems,
        WeChatTarget target,
        IProgress<WeChatQueueProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceItems);
        ArgumentNullException.ThrowIfNull(target);
        var states = sourceItems.ToDictionary(item => item.QueueItemId);
        var batches = WeChatBatchPlanner.Create(sourceItems);

        try
        {
            foreach (var batch in batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var ready = await _gateway.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
                if (!ready.IsSupported || !ready.IsReady)
                    return Result(states, isPaused: true, isCanceled: false);

                IReadOnlyList<WeChatSendItem> pending = batch.Items
                    .Select(item => states[item.QueueItemId])
                    .ToArray();

                for (var attempt = 0; pending.Count > 0 && attempt <= 3; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (attempt > 0)
                    {
                        ready = await _gateway.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
                        if (!ready.IsSupported || !ready.IsReady)
                            return Result(states, isPaused: true, isCanceled: false);
                    }
                    pending = pending.Select(item => item with
                    {
                        State = WeChatSendItemState.Sending,
                        RetryCount = attempt
                    }).ToArray();
                    Store(states, pending);
                    Report(progress, states, batch.Number, pending[0].DisplayName);

                    var sendResult = await _gateway.SendBatchAsync(pending, target, cancellationToken)
                        .ConfigureAwait(false);
                    var evidenceById = sendResult.Items.ToDictionary(item => item.QueueItemId);
                    var retry = new List<WeChatSendItem>();
                    var hasAmbiguous = false;

                    foreach (var item in pending)
                    {
                        if (!evidenceById.TryGetValue(item.QueueItemId, out var evidence))
                        {
                            states[item.QueueItemId] = item with
                            {
                                State = WeChatSendItemState.Ambiguous,
                                Message = "无法取得本次发送结果"
                            };
                            hasAmbiguous = true;
                            continue;
                        }

                        if (evidence.IsVerifiedSuccess)
                        {
                            states[item.QueueItemId] = item with
                            {
                                State = WeChatSendItemState.Sent,
                                Message = evidence.Message
                            };
                        }
                        else if (evidence.State == WeChatEvidenceState.Failed && evidence.FailureMarkerFound)
                        {
                            var failed = item with
                            {
                                State = WeChatSendItemState.Failed,
                                Message = evidence.Message
                            };
                            states[item.QueueItemId] = failed;
                            retry.Add(failed);
                        }
                        else
                        {
                            states[item.QueueItemId] = item with
                            {
                                State = WeChatSendItemState.Ambiguous,
                                Message = evidence.Message
                            };
                            hasAmbiguous = true;
                        }

                        if (_log is not null)
                        {
                            await _log.AppendAsync(new(
                                DateTimeOffset.UtcNow,
                                item.QueueItemId,
                                item.DisplayName,
                                item.Length,
                                target.ResolvedTitle,
                                target.TargetType,
                                batch.Number,
                                attempt,
                                states[item.QueueItemId].State,
                                evidence.Message), cancellationToken).ConfigureAwait(false);
                        }
                    }

                    Report(progress, states, batch.Number, string.Empty);
                    if (hasAmbiguous)
                        return Result(states, isPaused: true, isCanceled: false);

                    if (retry.Count == 0)
                        break;

                    if (attempt == 3)
                        break;

                    await _delay(TimeSpan.FromSeconds(1 << attempt), cancellationToken)
                        .ConfigureAwait(false);
                    pending = retry;
                }
            }

            return Result(states, isPaused: false, isCanceled: false);
        }
        catch (OperationCanceledException)
        {
            foreach (var (id, item) in states.ToArray())
            {
                if (item.State is WeChatSendItemState.Pending or WeChatSendItemState.Staging)
                    states[id] = item with { State = WeChatSendItemState.Canceled };
            }

            return Result(states, isPaused: false, isCanceled: true);
        }
    }

    private static void Store(
        IDictionary<Guid, WeChatSendItem> states,
        IEnumerable<WeChatSendItem> items)
    {
        foreach (var item in items)
            states[item.QueueItemId] = item;
    }

    private static WeChatQueueResult Result(
        IReadOnlyDictionary<Guid, WeChatSendItem> states,
        bool isPaused,
        bool isCanceled) =>
        new(states.Values.ToArray(), isPaused, isCanceled);

    private static void Report(
        IProgress<WeChatQueueProgress>? progress,
        IReadOnlyDictionary<Guid, WeChatSendItem> states,
        int batch,
        string currentFile)
    {
        progress?.Report(new(
            states.Count,
            states.Values.Count(item => item.State == WeChatSendItemState.Sent),
            states.Values.Count(item => item.State == WeChatSendItemState.Failed),
            states.Values.Count(item => item.State == WeChatSendItemState.Ambiguous),
            batch,
            currentFile));
    }
}
