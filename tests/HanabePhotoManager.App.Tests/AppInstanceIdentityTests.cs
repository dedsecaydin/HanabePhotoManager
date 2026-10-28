using HanabePhotoManager.App.Services;
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
