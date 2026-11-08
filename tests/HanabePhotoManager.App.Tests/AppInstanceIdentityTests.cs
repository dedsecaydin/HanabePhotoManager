using HanabePhotoManager.App.Services;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class AppInstanceIdentityTests
{
    [Fact]
    public void ProcessingDataStaysOnProjectDriveAndHonorsIsolation()
    {
        Assert.Equal(@"D:\HanabePhoto\data", AppDataPaths.ResolveProcessingRoot(null));
        Assert.Equal(@"D:\HanabePhoto\.artifacts\qa\processing", AppDataPaths.ResolveProcessingRoot(@"D:\HanabePhoto\.artifacts\qa"));
    }

    [Fact]
    public void PlatformRootKeepsProcessingUnderConfiguredDataDirectory()
    {
        var root = Path.Combine(@"D:\HanabePhoto\.artifacts", "mac-root");
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "processing"), AppDataPaths.ResolveProcessingRoot(null, root));
        var isolated = Path.Combine(root, "isolated");
        Assert.Equal(Path.Combine(Path.GetFullPath(isolated), "processing"), AppDataPaths.ResolveProcessingRoot(isolated, root));
    }

    [Fact]
    public void DefaultInstanceRetainsExistingMutex()
    {
        Assert.Equal("HanabePhotoManager.SingleInstance", AppInstanceIdentity.MutexName(null));
        Assert.Equal(AppInstanceIdentity.MutexName(null), AppInstanceIdentity.MutexName(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HanabePhotoManager")));
    }

    [Fact]
    public void IsolatedDataRootsHaveIndependentMutexes()
    {
        Assert.NotEqual(AppInstanceIdentity.MutexName(null), AppInstanceIdentity.MutexName(@"D:\HanabePhoto\.artifacts\qa"));
        Assert.NotEqual(AppInstanceIdentity.MutexName(@"D:\HanabePhoto\.artifacts\qa"), AppInstanceIdentity.MutexName(@"D:\HanabePhoto\.artifacts\qa2"));
        Assert.Equal(AppInstanceIdentity.MutexName(@"D:\HanabePhoto\.artifacts\qa"), AppInstanceIdentity.MutexName(@"d:\hanabephoto\.artifacts\qa\"));
    }
}
