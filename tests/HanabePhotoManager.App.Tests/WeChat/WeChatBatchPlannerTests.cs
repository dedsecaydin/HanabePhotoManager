using FluentAssertions;
using HanabePhotoManager.App.WeChat;
using Xunit;

namespace HanabePhotoManager.App.Tests.WeChat;

public sealed class WeChatBatchPlannerTests
{
    [Fact]
    public void Create_SplitsNineteenItemsIntoNineNineOne()
    {
        var batches = WeChatBatchPlanner.Create(CreateItems(19));

        batches.Select(batch => batch.Items.Count).Should().Equal(9, 9, 1);
    }

    [Fact]
    public void Create_DoesNotPlaceDuplicateDisplayNamesInOneBatch()
    {
        var batches = WeChatBatchPlanner.Create(
        [
            CreateItem(@"C:\a\same.jpg"),
            CreateItem(@"C:\b\same.jpg"),
            CreateItem(@"C:\c\other.jpg")
        ]);

        batches.Should().OnlyContain(batch =>
            batch.Items.Select(item => item.DisplayName)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() == batch.Items.Count);
        batches.SelectMany(batch => batch.Items).Should().HaveCount(3);
    }

    private static IReadOnlyList<WeChatSendItem> CreateItems(int count) =>
        Enumerable.Range(1, count).Select(index => CreateItem($@"C:\photos\p{index}.jpg")).ToArray();

    private static WeChatSendItem CreateItem(string path) =>
        WeChatSendItem.Create(path, 100, DateTimeOffset.UnixEpoch);
}
