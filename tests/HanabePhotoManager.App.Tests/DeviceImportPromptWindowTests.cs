using System.Windows;
using HanabePhotoManager.App.Imports;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class DeviceImportPromptWindowTests
{
    [Theory]
    [InlineData(@"D:\Photos\照片库")]
    [InlineData(null)]
    public void Prompt_ShowsSourceDestinationAndExplicitActions(string? libraryRoot)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            DeviceImportPromptWindow? window = null;
            try
            {
                window = new DeviceImportPromptWindow(@"E:\", libraryRoot);
                Assert.Equal(@"E:\", window.RootPathText.Text);
                if (libraryRoot is null) Assert.Contains("尚未选择", window.DestinationPathText.Text);
                else Assert.Equal(libraryRoot, window.DestinationPathText.Text);
                Assert.True(window.ImportButton.IsDefault);
                Assert.True(window.LaterButton.IsCancel);
                Assert.Equal(SizeToContent.Height, window.SizeToContent);
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        Assert.Null(failure);
    }
}
