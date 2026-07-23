using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PhotoViewerViewModelTests
{
    [Fact]
    public void OpenAndNavigate_UsesCurrentFilteredOrderAndStopsAtBoundaries()
    {
        var reader = new StubReader();
        var viewer = new PhotoViewerViewModel(reader);

        viewer.Open(["a.jpg", "b.jpg", "c.jpg"], "b.jpg");
        viewer.IsOpen.Should().BeTrue();
        viewer.CurrentPath.Should().Be("b.jpg");
        viewer.Previous();
        viewer.CurrentPath.Should().Be("a.jpg");
        viewer.Previous();
        viewer.CurrentPath.Should().Be("a.jpg");
        viewer.Next();
        viewer.Next();
        viewer.Next();
        viewer.CurrentPath.Should().Be("c.jpg");
        viewer.Close();
        viewer.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Rating_IsClampedPersistedAndCanBeCleared()
    {
        var ratings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var viewer = new PhotoViewerViewModel(new StubReader(),
            path => ratings.GetValueOrDefault(path),
            (path, value) => ratings[path] = value,
            new StubRecycleBin());
        viewer.Open(["a.jpg"], "a.jpg");

        viewer.SetRating(8);
        viewer.Rating.Should().Be(5);
        ratings["a.jpg"].Should().Be(5);
        viewer.SetRating(0);
        viewer.Rating.Should().Be(0);
    }

    [Fact]
    public void DeleteCurrent_MovesToRecycleBinAndSelectsNextThenPrevious()
    {
        var recycle = new StubRecycleBin();
        var viewer = new PhotoViewerViewModel(new StubReader(), _ => 0, (_, _) => { }, recycle);
        viewer.Open(["a.jpg", "b.jpg", "c.jpg"], "b.jpg");

        viewer.DeleteCurrent();
        recycle.Paths.Should().ContainSingle().Which.Should().Be("b.jpg");
        viewer.CurrentPath.Should().Be("c.jpg");
        viewer.PositionText.Should().Be("2 / 2");

        viewer.DeleteCurrent();
        viewer.CurrentPath.Should().Be("a.jpg");
        viewer.PositionText.Should().Be("1 / 1");
    }

    [Fact]
    public void DeleteCurrent_WhenRecycleBinFails_KeepsCurrentPhoto()
    {
        var viewer = new PhotoViewerViewModel(new StubReader(), _ => 0, (_, _) => { }, new StubRecycleBin { Fail = true });
        viewer.Open(["a.jpg", "b.jpg"], "a.jpg");

        viewer.DeleteCurrent();

        viewer.CurrentPath.Should().Be("a.jpg");
        viewer.ErrorText.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Zoom_IsSmoothClampedAndResetsWhenChangingPhoto()
    {
        var viewer = new PhotoViewerViewModel(new StubReader());
        viewer.Open(["a.jpg", "b.jpg"], "a.jpg");

        viewer.AdjustZoom(1);
        viewer.ZoomScale.Should().BeApproximately(1.12, 0.001);
        for (var index = 0; index < 100; index++) viewer.AdjustZoom(1);
        viewer.ZoomScale.Should().Be(8);
        viewer.Next();
        viewer.ZoomScale.Should().Be(1);
        for (var index = 0; index < 100; index++) viewer.AdjustZoom(-1);
        viewer.ZoomScale.Should().Be(0.25);
        viewer.ResetZoom();
        viewer.ZoomScale.Should().Be(1);
    }

    private sealed class StubReader : IPhotoDetailMetadataReader
    {
        public PhotoDetailMetadata Read(string path) => PhotoDetailMetadata.Empty(path);
    }

    private sealed class StubRecycleBin : IRecycleBinFileService
    {
        public List<string> Paths { get; } = [];
        public bool Fail { get; init; }
        public void MoveToRecycleBin(string path)
        {
            if (Fail) throw new IOException("locked");
            Paths.Add(path);
        }
    }
}
