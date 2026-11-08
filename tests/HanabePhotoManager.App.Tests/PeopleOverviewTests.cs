using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.People;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

/// <summary>人物总览：搜索、排序、收起只出现 1–2 次的人物、区分已命名。</summary>
public sealed class PeopleOverviewTests
{
    private readonly PeopleAlbumService _service = new(
        Path.Combine(Path.GetTempPath(), $"hanabe-people-overview-{Guid.NewGuid():N}.json"),
        new FakeEmbeddingService(new Dictionary<string, float[]>()));

    private PersonAlbumItemViewModel Album(string name, int photos) => new(
        new PersonAlbum
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            CoverPath = string.Empty,
            PhotoPaths = Enumerable.Range(0, photos).Select(i => $@"C:\p\{name}\{i}.jpg").ToList(),
        },
        _service,
        _ => { });

    private PeopleAlbumViewModel CreateViewModel(params PersonAlbumItemViewModel[] albums)
    {
        var viewModel = new PeopleAlbumViewModel(_service, () => []);
        foreach (var album in albums) viewModel.Albums.Add(album);
        return viewModel;
    }

    [Fact]
    public void IsNamed_DistinguishesScanNamesFromUserNames()
    {
        Album("人物 12", 3).IsNamed.Should().BeFalse();
        Album("人物 12", 3).SortNumber.Should().Be(12);
        Album("小林", 3).IsNamed.Should().BeTrue();
        Album("小林", 3).SortNumber.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Overview_DefaultsToMostPhotosFirst_AndFoldsPeopleSeenOnlyOnceOrTwice()
    {
        var viewModel = CreateViewModel(Album("人物 1", 2), Album("人物 2", 40), Album("新娘", 25), Album("人物 3", 1), Album("人物 4", 5));

        viewModel.VisibleAlbums.Select(album => album.Name).Should().Equal("人物 2", "新娘", "人物 4");
        viewModel.HiddenSmallAlbumCount.Should().Be(2);
        viewModel.NamedAlbumCount.Should().Be(1);
        viewModel.OverviewSummaryText.Should().Be("显示 3 / 5 人 · 已命名 1 人");

        viewModel.ShowSmallAlbumsCommand.Execute(null);
        viewModel.VisibleAlbums.Should().HaveCount(5);
        viewModel.HasHiddenSmallAlbums.Should().BeFalse();
    }

    [Fact]
    public void Overview_SearchFindsSmallAlbumsToo_AndNamedFirstSortWorks()
    {
        var viewModel = CreateViewModel(Album("人物 7", 30), Album("伴娘小美", 1), Album("新郎", 12));

        viewModel.SearchText = "小美";
        viewModel.VisibleAlbums.Select(album => album.Name).Should().Equal("伴娘小美");
        viewModel.HiddenSmallAlbumCount.Should().Be(0, "搜索时不再收起");

        viewModel.SearchText = "不存在";
        viewModel.HasNoSearchResults.Should().BeTrue();

        viewModel.SearchText = string.Empty;
        viewModel.SortMode = "已命名优先";
        viewModel.VisibleAlbums.Select(album => album.Name).Should().Equal("新郎", "人物 7");
    }

    [Fact]
    public void RenamingRefreshesNamedCountAndOrder()
    {
        var unnamed = Album("人物 9", 8);
        var viewModel = CreateViewModel(Album("人物 3", 20), unnamed);
        viewModel.SortMode = "已命名优先";

        unnamed.Name = "摄影助理";

        viewModel.NamedAlbumCount.Should().Be(1);
        viewModel.VisibleAlbums.First().Should().BeSameAs(unnamed);
    }

    private sealed class FakeEmbeddingService(IReadOnlyDictionary<string, float[]> embeddings) : ILocalFaceEmbeddingService
    {
        public FaceModelIdentity ModelIdentity { get; } = FaceModelIdentity.YuNetSFaceLegacy;

        public Task<IReadOnlyList<DetectedFace>> DetectAsync(string path, CancellationToken cancellationToken)
        {
            IReadOnlyList<DetectedFace> result = embeddings.TryGetValue(path, out var embedding)
                ? [new DetectedFace(path, embedding, 0, 0, 10, 10)]
                : [];
            return Task.FromResult(result);
        }
    }
}
