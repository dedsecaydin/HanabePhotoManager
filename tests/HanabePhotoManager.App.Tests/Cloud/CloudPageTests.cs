using System.IO;
using System.Windows;
using FluentAssertions;
using HanabePhotoManager.App.Cloud;
using Xunit;

namespace HanabePhotoManager.App.Tests.Cloud;

public sealed class CloudPageTests
{
    [Fact]
    public void Page_CreatesWithoutException()
    {
        RunOnSta(() =>
        {
            var page = new CloudPage { InitialUrl = "https://pan.baidu.com" };
            page.Should().NotBeNull();
            page.Dispose();
        });
    }

    [Fact]
    public void Page_AcceptsDifferentInitialUrls()
    {
        RunOnSta(() =>
        {
            var baidu = new CloudPage { InitialUrl = "https://pan.baidu.com" };
            var quark = new CloudPage { InitialUrl = "https://pan.quark.cn" };

            baidu.InitialUrl.Should().Be("https://pan.baidu.com");
            quark.InitialUrl.Should().Be("https://pan.quark.cn");
            baidu.Dispose();
            quark.Dispose();
        });
    }

    [Fact]
    public void Page_ExposesDarkThemeDependencyProperty()
    {
        RunOnSta(() =>
        {
            var page = new CloudPage { IsDarkTheme = true };
            page.IsDarkTheme.Should().BeTrue();
            page.Dispose();
        });
    }

    [Fact]
    public void DarkFallback_IsReversibleAndDoesNotGloballyInvertMedia()
    {
        var source = File.ReadAllText(SourcePath("src", "HanabePhotoManager.App", "Cloud", "CloudPage.xaml.cs"));

        source.Should().Contain("hanabe-cloud-dark-style");
        source.Should().Contain("RemoveScriptToExecuteOnDocumentCreated");
        source.Should().Contain("PreferredColorScheme");
        source.Should().Contain("img");
        source.Should().Contain("video");
        source.Should().Contain("canvas");
        source.Should().Contain("iframe");
        source.Should().NotContain("filter: invert");
    }

    [Fact]
    public void MainWindow_UsesOneUnifiedCloudSurfaceWithTwoPreservedSessions()
    {
        var xaml = File.ReadAllText(SourcePath("src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("x:Name=\"CloudPageContainer\"");
        xaml.Should().Contain("Orientation=\"Horizontal\"");
        xaml.Should().Contain("Command=\"{Binding SelectCloudProviderCommand}\"");
        xaml.Should().Contain("InitialUrl=\"https://pan.baidu.com\"");
        xaml.Should().Contain("InitialUrl=\"https://pan.quark.cn\"");
        File.ReadAllText(SourcePath("src", "HanabePhotoManager.App", "MainWindow.xaml.cs"))
            .Should().Contain("TimeSpan.FromMilliseconds(180)");
    }

    private static string SourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            directory = directory.Parent;
        directory.Should().NotBeNull();
        return Path.Combine([directory!.FullName, .. parts]);
    }

    private static void RunOnSta(Action action)
    {
        var thread = new Thread(() => action());
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
