using FluentAssertions;
using HanabePhotoManager.Core.Performance;

namespace HanabePhotoManager.Core.Tests.Performance;

public sealed class PreviewLoadingPolicyTests
{
    [Fact]
    public void SixThousandFiles_RequireFewerThanOneHundredUiDispatches()
    {
        PreviewLoadingPolicy.DispatcherBatchCount(6_000).Should().BeLessThan(100);
    }

    [Fact]
    public void ExpensivePreviewResources_AreStrictlyBounded()
    {
        PreviewLoadingPolicy.VisiblePageSize.Should().BeLessThanOrEqualTo(240);
        PreviewLoadingPolicy.HomeRecentItemLimit.Should().BeLessThanOrEqualTo(24);
        PreviewLoadingPolicy.ThumbnailConcurrency.Should().BeInRange(2, 4);
        PreviewLoadingPolicy.ThumbnailCacheLimit.Should().BeLessThanOrEqualTo(256);
    }
}
