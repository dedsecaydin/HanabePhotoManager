using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Models;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class TagManagerViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hanabe-tags-{Guid.NewGuid():N}");

    [Fact]
    public async Task InitializeAndCreateTag_PersistsUniqueTrimmedCustomTags()
    {
        var store = CreateStore();
        var manager = new TagManagerViewModel(store);

        await manager.InitializeAsync();
        await manager.CreateTagAsync("  海边  ");
        await manager.CreateTagAsync("海边");

        manager.CustomTags.Should().Equal("海边");
        (await store.LoadAsync()).CustomTags.Should().Equal("海边");
        manager.AvailableCategories.Should().Contain(["人像", "自然风景", "城市风光", "建筑", "待分类"]);
    }

    [Fact]
    public async Task RenameTag_UpdatesDefinitionAndEveryMediaAssignment()
    {
        var store = CreateStore();
        await store.SaveAsync(new MediaMetadataSnapshot
        {
            CustomTags = ["旅行"],
            Entries =
            [
                new MediaMetadataEntry { Path = Path.Combine(_directory, "a.jpg"), ManualTags = ["旅行", "夏天"] },
                new MediaMetadataEntry { Path = Path.Combine(_directory, "b.jpg"), ManualTags = ["旅行"] }
            ]
        });
        var manager = new TagManagerViewModel(store);
        await manager.InitializeAsync();

        await manager.RenameTagAsync("旅行", "远行");

        var snapshot = await store.LoadAsync();
        snapshot.CustomTags.Should().Equal("远行");
        snapshot.Entries.Should().OnlyContain(entry => entry.ManualTags.Contains("远行"));
        snapshot.Entries.Should().OnlyContain(entry => !entry.ManualTags.Contains("旅行"));
    }

    [Fact]
    public async Task DeleteTag_RemovesDefinitionAndAssignments()
    {
        var store = CreateStore();
        await store.SaveAsync(new MediaMetadataSnapshot
        {
            CustomTags = ["临时"],
            Entries = [new MediaMetadataEntry { Path = Path.Combine(_directory, "a.jpg"), ManualTags = ["临时", "保留"] }]
        });
        var manager = new TagManagerViewModel(store);
        await manager.InitializeAsync();

        await manager.DeleteTagAsync("临时");

        var snapshot = await store.LoadAsync();
        snapshot.CustomTags.Should().BeEmpty();
        snapshot.Entries.Single().ManualTags.Should().Equal("保留");
    }

    [Fact]
    public async Task AssignOperations_AreCaseInsensitiveAndManualCategoryWins()
    {
        var pathA = Path.Combine(_directory, "a.jpg");
        var pathB = Path.Combine(_directory, "b.jpg");
        var store = CreateStore();
        var manager = new TagManagerViewModel(store);
        await manager.InitializeAsync();

        await manager.AssignTagAsync([pathA, pathB], "家庭");
        await manager.AssignTagAsync([pathA], "家庭");
        await manager.SetManualCategoryAsync([pathA, pathB], "人像");

        var snapshot = await store.LoadAsync();
        snapshot.Entries.Should().HaveCount(2);
        snapshot.Entries.Should().OnlyContain(entry => entry.ManualTags.Count(tag => tag == "家庭") == 1);
        snapshot.Entries.Should().OnlyContain(entry => entry.EffectiveCategory == "人像");
        snapshot.CustomTags.Should().ContainSingle("家庭");
    }

    private MediaMetadataStore CreateStore() => new(Path.Combine(_directory, "media-metadata.json"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
