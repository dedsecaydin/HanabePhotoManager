using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Compression;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class CompressionViewModelTests : IDisposable
{
    [Fact]
    public void ImageTools_ExposeCompressionAndUnlimitedHorizontalOrVerticalCollageModes()
    {
        var viewModel = new CompressionViewModel();

        viewModel.ToolModes.Select(mode => mode.Label).Should().Equal("批量压缩", "拼图", "批量水印", "像素画");
        viewModel.CollageOrientations.Select(mode => mode.Label).Should().Equal("纵向拼接", "横向拼接");
        viewModel.CollageLimitOutputSize.Should().BeFalse();
    }
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hanabe-compress-vm-{Guid.NewGuid():N}");

    [Fact]
    public async Task AddInputsAsync_DeduplicatesAndUpdatesOriginalTotals()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "one.jpg");
        File.WriteAllBytes(path, new byte[321]);
        var viewModel = new CompressionViewModel();

        await viewModel.AddInputsAsync([path, path]);

        viewModel.Items.Should().ContainSingle(item => item.Path == Path.GetFullPath(path));
        viewModel.OriginalTotalBytes.Should().Be(321);
    }

    [Fact]
    public async Task AddInputsAsync_WhenCancelled_DoesNotModifyQueue()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "cancelled.jpg");
        File.WriteAllBytes(path, [1]);
        var viewModel = new CompressionViewModel();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => viewModel.AddInputsAsync([path], cancellationToken: cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        viewModel.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CanStart_RequiresInputOutputAndPositiveTarget()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "one.jpg");
        File.WriteAllBytes(path, [1, 2, 3]);
        var viewModel = new CompressionViewModel();

        viewModel.CanStart.Should().BeFalse();
        await viewModel.AddInputsAsync([path]);
        viewModel.OutputDirectory = Path.Combine(_root, "out");
        viewModel.TargetValue = "2";
        viewModel.TargetUnit = "MB";

        viewModel.CanStart.Should().BeTrue();
        viewModel.TargetBytes.Should().Be(2 * 1024 * 1024);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
