using FluentAssertions;
using HanabePhotoManager.App.Browsing.Treemap;
using HanabePhotoManager.Core.Browsing.Treemap;
using System.IO;
using System.Windows;
using Xunit;

namespace HanabePhotoManager.App.Tests.Browsing.Treemap;

public sealed class PhotoTreemapControlTests
{
    [Fact]
    public void Control_IsOneDrawingSurfaceWithBindableInteractionProperties()
    {
        typeof(PhotoTreemapControl).Should().BeDerivedFrom<FrameworkElement>();
        PhotoTreemapControl.ItemsSourceProperty.OwnerType.Should().Be(typeof(PhotoTreemapControl));
        PhotoTreemapControl.RootKeyProperty.OwnerType.Should().Be(typeof(PhotoTreemapControl));
        PhotoTreemapControl.SelectedPathProperty.OwnerType.Should().Be(typeof(PhotoTreemapControl));
        PhotoTreemapControl.OpenItemCommandProperty.OwnerType.Should().Be(typeof(PhotoTreemapControl));
        PhotoTreemapControl.ZoomCommandProperty.OwnerType.Should().Be(typeof(PhotoTreemapControl));

        var source = File.ReadAllText(ProjectFile("src", "HanabePhotoManager.App", "Browsing", "Treemap", "PhotoTreemapControl.cs"));
        source.Should().Contain("DrawingContext");
        source.Should().NotContain("ItemsControl");
    }

    [Theory]
    [InlineData(200, 100, true)]
    [InlineData(120, 100, true)]
    [InlineData(119, 100, false)]
    [InlineData(300, 20, false)]
    public void ThumbnailPolicy_RequiresEnoughRenderedArea(double width, double height, bool expected)
    {
        PhotoTreemapControl.ShouldRequestThumbnail(width, height).Should().Be(expected);
    }

    [Fact]
    public void FindItemAt_ReturnsTopmostMatchingRegion()
    {
        var parent = Item("parent", true);
        var child = Item("child", false);
        var regions = new[]
        {
            new TreemapHitRegion(parent, new TreemapBounds(0, 0, 100, 100)),
            new TreemapHitRegion(child, new TreemapBounds(10, 10, 30, 30))
        };

        PhotoTreemapControl.FindItemAt(regions, 20, 20).Should().Be(child);
        PhotoTreemapControl.FindItemAt(regions, 80, 80).Should().Be(parent);
        PhotoTreemapControl.FindItemAt(regions, 101, 101).Should().BeNull();
    }

    [Fact]
    public void HitRegion_ProvidesStableAutomationName()
    {
        var region = new TreemapHitRegion(
            new TreemapItemViewModel("a", null, "夏日照片", 2048, false, @"D:\a.jpg", 2048, "JPG生图", "JPG"),
            new TreemapBounds(0, 0, 10, 10));

        region.AutomationName.Should().Be("夏日照片，JPG，2 KB");
    }

    private static TreemapItemViewModel Item(string key, bool container) =>
        new(key, null, key, 1, container, container ? null : $@"D:\{key}.jpg", 1, "JPG生图", "JPG");

    private static string ProjectFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "HanabePhotoManager.sln")))
        {
            current = current.Parent;
        }

        current.Should().NotBeNull("tests must run from inside the repository");
        return Path.Combine([current!.FullName, .. segments]);
    }
}
