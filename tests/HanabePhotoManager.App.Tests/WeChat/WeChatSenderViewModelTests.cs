using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.WeChat;
using Xunit;

namespace HanabePhotoManager.App.Tests.WeChat;

public sealed class WeChatSenderViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hanabe-wechat-{Guid.NewGuid():N}");

    public WeChatSenderViewModelTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task ConfirmedTargetAndFiles_EnableStart()
    {
        var file = CreatePhoto("a.jpg");
        var gateway = new FakeGateway();
        var viewModel = new WeChatSenderViewModel(gateway);
        viewModel.AddInputs([file]);
        viewModel.TargetName = "Alice";

        await viewModel.DetectCommand.ExecuteAsync(null);
        await viewModel.LocateTargetCommand.ExecuteAsync(null);
        viewModel.ConfirmTargetCommand.Execute(null);

        viewModel.IsTargetConfirmed.Should().BeTrue();
        viewModel.StartCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task EditingTargetInvalidatesConfirmation()
    {
        var gateway = new FakeGateway();
        var viewModel = new WeChatSenderViewModel(gateway) { TargetName = "Alice" };
        await viewModel.DetectCommand.ExecuteAsync(null);
        await viewModel.LocateTargetCommand.ExecuteAsync(null);
        viewModel.ConfirmTargetCommand.Execute(null);

        viewModel.TargetName = "Bob";

        viewModel.IsTargetConfirmed.Should().BeFalse();
        viewModel.StartCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void CompressionAndWechatQueuesAreIndependent()
    {
        var compression = new HanabePhotoManager.App.ViewModels.CompressionViewModel();
        var file = CreatePhoto("a.jpg");

        compression.WeChatSender.AddInputs([file]);

        compression.Items.Should().BeEmpty();
        compression.WeChatSender.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task LocateFailure_EnablesRecoverableManualBatchMode()
    {
        var file = CreatePhoto("a.jpg");
        var clipboard = new FakeFileClipboard();
        var gateway = new FakeGateway { LocateResult = null };
        var viewModel = new WeChatSenderViewModel(gateway, clipboard: clipboard);
        viewModel.AddInputs([file]);
        viewModel.TargetName = "文件传输助手";

        await viewModel.DetectCommand.ExecuteAsync(null);
        await viewModel.LocateTargetCommand.ExecuteAsync(null);

        viewModel.IsManualFallbackAvailable.Should().BeTrue();
        viewModel.PrepareManualBatchCommand.CanExecute(null).Should().BeTrue();
        viewModel.StatusText.Should().Contain("手动打开");
    }

    [Fact]
    public async Task ManualBatch_CopiesNineAndOnlyAdvancesAfterUserAcknowledges()
    {
        var files = Enumerable.Range(1, 10)
            .Select(index => CreatePhoto($"{index}.jpg"))
            .ToArray();
        var clipboard = new FakeFileClipboard();
        var gateway = new FakeGateway { LocateResult = null };
        var viewModel = new WeChatSenderViewModel(gateway, clipboard: clipboard);
        viewModel.AddInputs(files);
        viewModel.TargetName = "文件传输助手";
        await viewModel.DetectCommand.ExecuteAsync(null);
        await viewModel.LocateTargetCommand.ExecuteAsync(null);

        await viewModel.PrepareManualBatchCommand.ExecuteAsync(null);

        clipboard.Files.Should().HaveCount(9);
        viewModel.HasPreparedManualBatch.Should().BeTrue();
        viewModel.SentCount.Should().Be(0);

        viewModel.ConfirmManualBatchSentCommand.Execute(null);

        viewModel.SentCount.Should().Be(9);
        viewModel.Items.Count(item => item.State == WeChatSendItemState.Pending).Should().Be(1);
        viewModel.PrepareManualBatchCommand.CanExecute(null).Should().BeTrue();
    }

    private string CreatePhoto(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, [1, 2, 3]);
        return path;
    }

    public void Dispose() => Directory.Delete(_root, true);

    private sealed class FakeGateway : IWeChatDesktopGateway
    {
        public WeChatTarget? LocateResult { get; init; } =
            new("Alice", "Alice", "联系人", "token");

        public Task<WeChatGatewayStatus> EnsureReadyAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new WeChatGatewayStatus(true, true, "微信已在前台", 42));

        public Task<WeChatTarget?> LocateTargetAsync(string requestedName, CancellationToken cancellationToken) =>
            Task.FromResult(LocateResult is null
                ? null
                : LocateResult with
                {
                    RequestedName = requestedName,
                    ResolvedTitle = requestedName
                });

        public Task<WeChatBatchSendResult> SendBatchAsync(
            IReadOnlyList<WeChatSendItem> items,
            WeChatTarget target,
            CancellationToken cancellationToken) =>
            Task.FromResult(new WeChatBatchSendResult(items.Select(item => new WeChatItemEvidence(
                item.QueueItemId, WeChatEvidenceState.Sent, true, true, true, false, true)).ToArray()));
    }

    private sealed class FakeFileClipboard : IWeChatFileClipboard
    {
        public IReadOnlyList<string> Files { get; private set; } = [];

        public void SetFiles(IReadOnlyList<string> files) => Files = files.ToArray();
    }
}
