using HanabePhotoManager.App.Platform;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public class PlatformDialogTests
{
    [Fact]
    public async Task DefaultDialogCancelsWithoutUserInteraction()
    {
        IDialogService dialogs = new NullDialogService();
        Assert.False(await dialogs.ConfirmAsync("title", "message"));
        Assert.Null(await dialogs.PickFolderAsync("title"));
        Assert.Empty(await dialogs.PickFilesAsync("title", [], true));
        Assert.Null(await dialogs.SaveFileAsync("title", "file.jpg", []));
    }

    [Fact]
    public void MissingImportConfirmationRejectsMoveAndRename()
    {
        Assert.False(PlatformServices.ConfirmImportMove());
        Assert.False(PlatformServices.ConfirmImportRenames("rename"));
    }

    [Fact]
    public void MissingQuarantineWindowDoesNotReportRestoredFiles()
    {
        Assert.False(PlatformServices.ShowQuarantine("unused"));
    }

    [Fact]
    public async Task MissingDeleteConfirmationRejectsDeletion()
    {
        Assert.False(await PlatformServices.ConfirmDelete("test", 1, 1));
    }

    [Fact]
    public void MissingDuplicateDialogsDefaultToCancelAndSkip()
    {
        var dialogs = new NullDuplicateDialogs();
        Assert.Null(dialogs.ChooseScope());
        Assert.Equal(HanabePhotoManager.App.Duplicates.ImportDuplicateDecision.Skip, dialogs.DecideImport("a", "b", false));
    }

    [Fact]
    public void UnconfiguredTrashRejectsWithoutDeletingFile()
    {
        var directory = System.IO.Path.Combine(@"D:\HanabePhoto\.artifacts", "trash-test-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, "sample.txt");
        System.IO.File.WriteAllText(path, "fixture");
        try
        {
            Assert.Throws<PlatformNotSupportedException>(() => new HanabePhotoManager.App.Services.UnavailableRecycleBinFileService().MoveToRecycleBin(path));
            Assert.True(System.IO.File.Exists(path));
        }
        finally { System.IO.Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void UnconfiguredPlatformCapabilitiesDoNotModifySystem()
    {
        Assert.Empty(PlatformServices.ImportSourcePicker.PickFolders(""));
        Assert.False(PlatformServices.Startup.IsEnabled());
        Assert.Throws<PlatformNotSupportedException>(() => PlatformServices.Startup.SetEnabled(true));
        Assert.Null(PlatformServices.Wallpaper.GetCurrentWallpaperPath());
    }

    [Fact]
    public async Task UnconfiguredOriginMetadataReportsFailure()
    {
        var metadata = new HanabePhotoManager.App.Imports.FileOriginMetadata("a.jpg", 1, new string('A', 64));
        var result = await PlatformServices.OriginMetadata.WriteAndVerifyAsync("unused", metadata, CancellationToken.None);
        Assert.False(result.Success);
    }

    [Fact]
    public void UnconfiguredSecretProtectionNeverStoresPlaintext()
    {
        Assert.Equal(string.Empty, PlatformServices.Secrets.Protect(string.Empty));
        Assert.Throws<PlatformNotSupportedException>(() => PlatformServices.Secrets.Protect("test-secret"));
        Assert.Throws<PlatformNotSupportedException>(() => PlatformServices.Secrets.Unprotect("unavailable"));
    }

    [Fact]
    public async Task UnconfiguredRecoveryDoesNotOpenDevicesOrCreateOutput()
    {
        var recovery = new HanabePhotoManager.App.Recovery.UnavailableRecoveryPlatform();
        Assert.Empty(recovery.GetRemovableDevices());
        Assert.Throws<PlatformNotSupportedException>(() => recovery.CreateOutputPath("card.img", false));
        await Assert.ThrowsAsync<PlatformNotSupportedException>(() => recovery.ConnectDeviceAsync("device", CancellationToken.None));
    }

    [Fact]
    public void UnconfiguredStorageCapacityIsUnknown()
    {
        Assert.Null(PlatformServices.StorageCapacity.GetCapacity("unused"));
    }

    [Fact]
    public void UnconfiguredHeifDecoderFailsExplicitly()
    {
        Assert.Throws<PlatformNotSupportedException>(() => PlatformServices.DecodeHeif("unused", CancellationToken.None));
    }
}
