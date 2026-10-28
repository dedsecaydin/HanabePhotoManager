using FluentAssertions;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.Core.Tests.Imports;

public sealed class ImportRoutingRuleTests
{
    [Theory]
    [InlineData(ImportRuleMatchKind.Extension, "cr3", "portrait.CR3", true)]
    [InlineData(ImportRuleMatchKind.Extension, "CR3", "portrait.ARW", false)]
    [InlineData(ImportRuleMatchKind.NameStartsWith, "dji_", "DJI_0042.MP4", true)]
    [InlineData(ImportRuleMatchKind.NameContains, "修后", "客户修后01.JPG", true)]
    [InlineData(ImportRuleMatchKind.NameEndsWith, "edit", "portrait_EDIT.JPG", true)]
    [InlineData(ImportRuleMatchKind.NameEndsWith, "edit", "portrait_EDIT2.JPG", false)]
    public void Matches_UsesPrimaryFileNameAndExtension(ImportRuleMatchKind kind, string pattern, string fileName, bool expected)
    {
        new ImportRoutingRule(kind, pattern, "目标").Matches(fileName).Should().Be(expected);
    }

    [Fact]
    public void FirstMatchingFolder_UsesRuleOrder()
    {
        var rules = new[]
        {
            new ImportRoutingRule(ImportRuleMatchKind.NameStartsWith, "DJI_", "action视频"),
            new ImportRoutingRule(ImportRuleMatchKind.Extension, "MP4", "视频")
        };

        ImportRoutingRule.FirstMatchingFolder(rules, "DJI_0042.MP4").Should().Be("action视频");
        ImportRoutingRule.FirstMatchingFolder(rules, "C0001.MP4").Should().Be("视频");
        ImportRoutingRule.FirstMatchingFolder(rules, "photo.ARW").Should().BeNull();
    }
}
