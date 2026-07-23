using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Compression;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class CompressionViewModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hanabe-compress-vm-{Guid.NewGuid():N}");

    [Fact]
    public void AddInputs_DeduplicatesAndUpdatesOriginalTotals()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "one.jpg");
        File.WriteAllBytes(path, new byte[321]);
        var viewModel = new CompressionViewModel();

        viewModel.AddInputs([path, path]);

        viewModel.Items.Should().ContainSingle(item => item.Path == Path.GetFullPath(path));
        viewModel.OriginalTotalBytes.Should().Be(321);
    }

    [Fact]
    public void CanStart_RequiresInputOutputAndPositiveTarget()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "one.jpg");
        File.WriteAllBytes(path, [1, 2, 3]);
        var viewModel = new CompressionViewModel();

        viewModel.CanStart.Should().BeFalse();
        viewModel.AddInputs([path]);
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
