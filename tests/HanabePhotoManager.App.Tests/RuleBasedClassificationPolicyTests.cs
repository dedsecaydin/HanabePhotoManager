using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class RuleBasedClassificationPolicyTests
{
    [Fact]
    public void PortableModelPostprocessingMapsCategoriesAndRanksSimilarLabels()
    {
        var mapped = PortableClassificationPostprocessing.MapImageNetLabels([("golden retriever", .9)]);
        Assert.Contains(mapped, label => label.Label == "动物");
        var ranked = PortableClassificationPostprocessing.RankLabels([1, 0], new Dictionary<string, float[]> { ["match"] = [1, 0], ["other"] = [0, 1] });
        Assert.Equal("match", ranked[0].Label);
    }

    [Fact]
    public void DarkGreenPortraitProducesConsistentCategories()
    {
        var result = RuleBasedClassificationPolicy.Classify(40, .5, .05, true, string.Empty);
        Assert.Contains(result.Labels, label => label.Label == "人像");
        Assert.Contains(result.Labels, label => label.Label == "夜景");
        Assert.Contains(result.Labels, label => label.Label == "植物");
        Assert.Contains(result.Labels, label => label.Label == "自然风景");
    }
}
