using System.Windows;
using FluentAssertions;
using HanabePhotoManager.App;
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
            baidu.InitialUrl.Should().Be("https://pan.baidu.com");
            baidu.Dispose();

            var quark = new CloudPage { InitialUrl = "https://pan.quark.cn" };
            quark.InitialUrl.Should().Be("https://pan.quark.cn");
            quark.Dispose();
        });
    }

    private static void RunOnSta(Action action)
    {
        var thread = new System.Threading.Thread(() => action());
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
