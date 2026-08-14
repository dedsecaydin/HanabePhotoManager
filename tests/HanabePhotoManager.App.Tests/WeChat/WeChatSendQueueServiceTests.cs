using FluentAssertions;
using HanabePhotoManager.App.WeChat;
using Xunit;

namespace HanabePhotoManager.App.Tests.WeChat;

public sealed class WeChatSendQueueServiceTests
{
    [Fact]
    public async Task SendAsync_RetriesOnlyExplicitFailures()
    {
        var items = CreateItems(10);
        var gateway = new FakeGateway(
            Results(items.Take(9), failedName: "p5.jpg"),
            Results(items.Where(item => item.DisplayName == "p5.jpg")),
            Results(items.Skip(9)));
        var service = new WeChatSendQueueService(gateway, (_, _) => Task.CompletedTask);

        var result = await service.SendAsync(items, Target(), null, CancellationToken.None);

        gateway.Calls.Select(call => call.Select(item => item.DisplayName).ToArray()).Should().BeEquivalentTo(
        [
            items.Take(9).Select(item => item.DisplayName).ToArray(),
            ["p5.jpg"],
            ["p10.jpg"]
        ], options => options.WithStrictOrdering());
        result.Items.Should().OnlyContain(item => item.State == WeChatSendItemState.Sent);
    }

    [Fact]
    public async Task SendAsync_DoesNotRetryAmbiguousItems()
    {
        var items = CreateItems(1);
        var gateway = new FakeGateway(new WeChatBatchSendResult(
        [
            Evidence(items[0], WeChatEvidenceState.Ambiguous)
        ]));
        var service = new WeChatSendQueueService(gateway, (_, _) => Task.CompletedTask);

        var result = await service.SendAsync(items, Target(), null, CancellationToken.None);

        gateway.Calls.Should().HaveCount(1);
        result.IsPaused.Should().BeTrue();
        result.Items[0].State.Should().Be(WeChatSendItemState.Ambiguous);
    }

    [Fact]
    public async Task SendAsync_StopsAfterThreeRetries()
    {
        var items = CreateItems(1);
        var failed = Results(items, failedName: "p1.jpg");
        var gateway = new FakeGateway(failed, failed, failed, failed);
        var delays = new List<TimeSpan>();
        var service = new WeChatSendQueueService(gateway, (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        var result = await service.SendAsync(items, Target(), null, CancellationToken.None);

        gateway.Calls.Should().HaveCount(4);
        delays.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4));
        result.Items[0].State.Should().Be(WeChatSendItemState.Failed);
    }

    [Fact]
    public async Task SendAsync_CancellationPreventsNextBatch()
    {
        var items = CreateItems(10);
        using var cancellation = new CancellationTokenSource();
        var gateway = new FakeGateway(Results(items.Take(9)))
        {
            AfterSend = cancellation.Cancel
        };
        var service = new WeChatSendQueueService(gateway, (_, token) => Task.Delay(0, token));

        var action = () => service.SendAsync(items, Target(), null, cancellation.Token);

        var result = await action();
        result.IsCanceled.Should().BeTrue();
        gateway.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task SendAsync_LogsFilenameAndStatusWithoutSourcePath()
    {
        var items = CreateItems(1);
        var gateway = new FakeGateway(Results(items));
        var log = new FakeLog();
        var service = new WeChatSendQueueService(gateway, (_, _) => Task.CompletedTask, log);

        await service.SendAsync(items, Target(), null, CancellationToken.None);

        var entry = log.Entries.Should().ContainSingle().Subject;
        entry.FileName.Should().Be("p1.jpg");
        entry.TargetTitle.Should().Be("Alice");
        entry.Message.Should().NotContain(items[0].SourcePath);
    }

    private static WeChatTarget Target() => new("Alice", "Alice", "联系人", "confirmed");

    private static IReadOnlyList<WeChatSendItem> CreateItems(int count) =>
        Enumerable.Range(1, count)
            .Select(index => WeChatSendItem.Create($@"C:\photos\p{index}.jpg", 100, DateTimeOffset.UnixEpoch))
            .ToArray();

    private static WeChatBatchSendResult Results(
        IEnumerable<WeChatSendItem> items,
        string? failedName = null) =>
        new(items.Select(item => Evidence(
            item,
            item.DisplayName == failedName ? WeChatEvidenceState.Failed : WeChatEvidenceState.Sent)).ToArray());

    private static WeChatItemEvidence Evidence(WeChatSendItem item, WeChatEvidenceState state) =>
        new(
            item.QueueItemId,
            state,
            InputCleared: state != WeChatEvidenceState.Ambiguous,
            NewFileBubbleFound: state == WeChatEvidenceState.Sent,
            UploadCompleted: state == WeChatEvidenceState.Sent,
            FailureMarkerFound: state == WeChatEvidenceState.Failed,
            TargetUnchanged: true);

    private sealed class FakeGateway(params WeChatBatchSendResult[] results) : IWeChatDesktopGateway
    {
        private readonly Queue<WeChatBatchSendResult> _results = new(results);
        public List<IReadOnlyList<WeChatSendItem>> Calls { get; } = [];
        public Action? AfterSend { get; init; }

        public Task<WeChatGatewayStatus> EnsureReadyAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new WeChatGatewayStatus(true, true, "ready", 1));

        public Task<WeChatTarget?> LocateTargetAsync(string requestedName, CancellationToken cancellationToken) =>
            Task.FromResult<WeChatTarget?>(Target());

        public Task<WeChatBatchSendResult> SendBatchAsync(
            IReadOnlyList<WeChatSendItem> items,
            WeChatTarget target,
            CancellationToken cancellationToken)
        {
            Calls.Add(items.ToArray());
            var result = _results.Dequeue();
            AfterSend?.Invoke();
            return Task.FromResult(result);
        }
    }

    private sealed class FakeLog : IWeChatSendLog
    {
        public List<WeChatSendLogEntry> Entries { get; } = [];

        public Task AppendAsync(WeChatSendLogEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
