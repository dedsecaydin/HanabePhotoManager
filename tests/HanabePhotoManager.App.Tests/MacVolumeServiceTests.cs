using HanabePhotoManager.Mac.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MacVolumeServiceTests
{
    [Fact]
    public void LibraryRootIsExcludedWhenItIsTheVolumeOrInsideIt()
    {
        Assert.True(VolumeService.IsInside(@"D:\HanabePhoto\.artifacts\volume", @"D:\HanabePhoto\.artifacts\volume"));
        Assert.True(VolumeService.IsInside(@"D:\HanabePhoto\.artifacts\volume\library", @"D:\HanabePhoto\.artifacts\volume"));
        Assert.False(VolumeService.IsInside(@"D:\HanabePhoto\.artifacts\volume-other", @"D:\HanabePhoto\.artifacts\volume"));
    }
}
