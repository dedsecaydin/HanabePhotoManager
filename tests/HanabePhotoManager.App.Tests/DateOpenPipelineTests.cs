using System.IO;
using FluentAssertions;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class DateOpenPipelineTests
{
    [Fact]
    public void SelectedDatePipeline_UsesSnapshotCancellationAndRangeUpdates()
    {
        var source = File.ReadAllText(SourcePath(
            "src", "HanabePhotoManager.App", "ViewModels", "MainWindowViewModel.cs"));

        source.Should().Contain("LibraryDateSnapshotService");
        source.Should().Contain("_dateLoadCancellation");
        source.Should().Contain("_dateLoadGeneration");
        source.Should().Contain("_libraryDateSnapshotService");
        source.Should().Contain(".LoadAsync(node.FullPath");
        source.Should().Contain("RangeObservableCollection<PreviewFileViewModel>");
        source.Should().Contain("PreviewFiles.AddRange");
        source.Should().Contain("CreatePreviewFile(LibraryDateMediaItem");
    }

    [Fact]
    public void SelectedDatePipeline_DoesNotAwaitCapacityBeforeShowingContent()
    {
        var source = File.ReadAllText(SourcePath(
            "src", "HanabePhotoManager.App", "ViewModels", "MainWindowViewModel.cs"));
        var start = source.IndexOf(
            "private async Task SelectDateAsync",
            StringComparison.Ordinal);
        var end = source.IndexOf(
            "private static IEnumerable<string> EnumerateLibraryPreviewFiles",
            start,
            StringComparison.Ordinal);

        start.Should().BeGreaterThanOrEqualTo(0);
        end.Should().BeGreaterThan(start);
        var method = source[start..end];
        method.Should().Contain("StartSelectedDateCapacityRefresh");
        method.Should().NotContain("await RefreshCapacityAsync");
        method.Should().NotContain("new FileInfo");
    }

    private static string SourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        return Path.Combine([directory!.FullName, .. parts]);
    }
}
