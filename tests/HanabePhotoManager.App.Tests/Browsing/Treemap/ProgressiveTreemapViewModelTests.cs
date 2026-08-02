using FluentAssertions;
using HanabePhotoManager.App.Browsing.Treemap;
using HanabePhotoManager.App.Models;
using HanabePhotoManager.Core.Browsing.Treemap;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests.Browsing.Treemap;

public sealed class ProgressiveTreemapViewModelTests
{
    [Fact]
    public void FirstBatch_IsPublishedImmediatelyAndDeduplicatesPaths()
    {
        using var viewModel = new ProgressiveTreemapViewModel(TimeSpan.FromMinutes(1));
        var generation = viewModel.BeginScan(@"D:\Photos\2026-08-03");

        viewModel.ApplyBatch(generation, Batch(
            Item(@"D:\Photos\2026-08-03\RAW\a.arw", "RAW生图", 300),
            Item(@"D:\Photos\2026-08-03\RAW\a.arw", "RAW生图", 300),
            Item(@"D:\Photos\2026-08-03\JPG\b.jpg", "JPG生图", 100)));

        viewModel.DiscoveredCount.Should().Be(2);
        viewModel.Items.Where(item => !item.IsContainer).Should().HaveCount(2);
        viewModel.Items.Where(item => item.IsContainer)
            .Select(item => item.Label)
            .Should().BeEquivalentTo("RAW生图", "JPG生图");
    }

    [Fact]
    public void WeightMode_SwitchesBetweenBytesAndPhotoCount()
    {
        using var viewModel = new ProgressiveTreemapViewModel(TimeSpan.FromMinutes(1));
        var generation = viewModel.BeginScan(@"D:\Photos");
        viewModel.ApplyBatch(generation, Batch(
            Item(@"D:\Photos\RAW\a.arw", "RAW生图", 300),
            Item(@"D:\Photos\RAW\b.arw", "RAW生图", 100)));

        viewModel.Items.Single(item => item.FullPath?.EndsWith("a.arw") == true)
            .Weight.Should().Be(300);

        viewModel.WeightMode = TreemapWeightMode.PhotoCount;

        viewModel.Items.Where(item => !item.IsContainer)
            .Should().OnlyContain(item => item.Weight == 1);
        viewModel.Items.Single(item => item.IsContainer).Weight.Should().Be(2);
    }

    [Fact]
    public void StaleGeneration_CannotReplaceCurrentScan()
    {
        using var viewModel = new ProgressiveTreemapViewModel(TimeSpan.FromMinutes(1));
        var staleGeneration = viewModel.BeginScan(@"D:\Old");
        var currentGeneration = viewModel.BeginScan(@"D:\Current");

        viewModel.ApplyBatch(staleGeneration, Batch(Item(@"D:\Old\a.jpg", "JPG生图", 10)));
        viewModel.ApplyBatch(currentGeneration, Batch(Item(@"D:\Current\b.jpg", "JPG生图", 20)));

        viewModel.DiscoveredCount.Should().Be(1);
        viewModel.Items.Should().Contain(item => item.FullPath == @"D:\Current\b.jpg");
        viewModel.Items.Should().NotContain(item => item.FullPath == @"D:\Old\a.jpg");
    }

    [Fact]
    public void ZoomToCategory_UpdatesBreadcrumbAndVisibleRoot()
    {
        using var viewModel = new ProgressiveTreemapViewModel(TimeSpan.FromMinutes(1));
        var generation = viewModel.BeginScan(@"D:\Photos");
        viewModel.ApplyBatch(generation, Batch(Item(@"D:\Photos\RAW\a.arw", "RAW生图", 10)));
        var category = viewModel.Items.Single(item => item.IsContainer);

        viewModel.ZoomTo(category.Key);

        viewModel.CurrentContainerKey.Should().Be(category.Key);
        viewModel.Breadcrumbs.Select(item => item.Label).Should().Equal("Photos", "RAW生图");
        viewModel.VisibleItems.Should().ContainSingle(item => !item.IsContainer);

        viewModel.NavigateToAncestor(null);
        viewModel.CurrentContainerKey.Should().BeNull();
    }

    [Fact]
    public async Task LaterBatches_AreCoalescedIntoOneLayoutPublication()
    {
        using var viewModel = new ProgressiveTreemapViewModel(TimeSpan.FromMilliseconds(40));
        var generation = viewModel.BeginScan(@"D:\Photos");
        viewModel.ApplyBatch(generation, Batch(Item(@"D:\Photos\a.jpg", "JPG生图", 10)));
        var firstRevision = viewModel.LayoutRevision;

        viewModel.ApplyBatch(generation, Batch(Item(@"D:\Photos\b.jpg", "JPG生图", 20)));
        viewModel.ApplyBatch(generation, Batch(Item(@"D:\Photos\c.jpg", "JPG生图", 30)));

        viewModel.LayoutRevision.Should().Be(firstRevision);
        await WaitUntilAsync(() => viewModel.LayoutRevision > firstRevision);
        viewModel.LayoutRevision.Should().Be(firstRevision + 1);
        viewModel.DiscoveredCount.Should().Be(3);
    }

    [Fact]
    public void Complete_PublishesPendingItemsAndPartialState()
    {
        using var viewModel = new ProgressiveTreemapViewModel(TimeSpan.FromMinutes(1));
        var generation = viewModel.BeginScan(@"D:\Photos");
        viewModel.ApplyBatch(generation, Batch(Item(@"D:\Photos\a.jpg", "JPG生图", 10)));
        viewModel.ApplyBatch(generation, Batch(Item(@"D:\Photos\b.jpg", "JPG生图", 20)));

        viewModel.Complete(generation, isPartial: true);

        viewModel.IsScanning.Should().BeFalse();
        viewModel.IsPartial.Should().BeTrue();
        viewModel.Items.Where(item => !item.IsContainer).Should().HaveCount(2);
    }

    private static LibraryDateSnapshotBatch Batch(params LibraryDateMediaItem[] items) =>
        new(items, items.Length, false);

    private static LibraryDateMediaItem Item(string path, string category, long length) =>
        new(path, Path.GetFileName(path), Path.GetExtension(path).TrimStart('.'), category, length, DateTime.UnixEpoch);

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        predicate().Should().BeTrue();
    }
}
