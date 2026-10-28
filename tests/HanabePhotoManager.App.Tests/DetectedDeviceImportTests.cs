using System.IO;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class DetectedDeviceImportTests : IDisposable
{
    private readonly string _fixture = Path.Combine(@"D:\HanabePhoto", ".artifacts", "device-import-tests", Guid.NewGuid().ToString("N"));

    public DetectedDeviceImportTests() => Directory.CreateDirectory(_fixture);

    [Fact]
    public async Task ConfirmedDevice_OpensImportWithoutLibraryOrCachedDeviceEntry()
    {
        var source = Path.Combine(_fixture, "camera");
        var photos = Directory.CreateDirectory(Path.Combine(source, "DCIM"));
        var clips = Directory.CreateDirectory(Path.Combine(source, "M4ROOT", "CLIP"));
        var viewModel = new MainWindowViewModel { CurrentPage = "Preview" };
        viewModel.ConnectedDevices.Clear();

        await viewModel.OfferDeviceImportAsync(source);

        Assert.Equal("Import", viewModel.CurrentPage);
        Assert.Equal(new[] { photos.FullName, clips.FullName }, viewModel.SelectedImportSourceFolders);
        Assert.Empty(viewModel.ImportItems);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.HasLibraryRoot);
    }

    [Fact]
    public async Task LibraryInsideDevice_ShowsImportPageAndExplainsBlockedDestination()
    {
        var source = Directory.CreateDirectory(Path.Combine(_fixture, "camera")).FullName;
        var target = Directory.CreateDirectory(Path.Combine(source, "library")).FullName;
        var viewModel = new MainWindowViewModel { LibraryRoot = target, CurrentPage = "Home" };

        await viewModel.OfferDeviceImportAsync(source);

        Assert.Equal("Import", viewModel.CurrentPage);
        Assert.Contains("不能位于来源设备", viewModel.StatusMessage);
        Assert.Empty(viewModel.SelectedImportSourceFolders);
    }

    [Fact]
    public async Task SiblingLibraryWithSimilarName_DoesNotBlockDeviceImport()
    {
        var source = Directory.CreateDirectory(Path.Combine(_fixture, "camera")).FullName;
        var target = Directory.CreateDirectory(Path.Combine(_fixture, "camera-library")).FullName;
        var viewModel = new MainWindowViewModel { LibraryRoot = target, CurrentPage = "Home" };
        viewModel.ConnectedDevices.Clear();

        await viewModel.OfferDeviceImportAsync(source);

        Assert.Equal("Import", viewModel.CurrentPage);
        Assert.Equal(source, Assert.Single(viewModel.SelectedImportSourceFolders));
    }

    public void Dispose() => Directory.Delete(_fixture, recursive: true);
}
