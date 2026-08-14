using System.IO;
using FluentAssertions;
using Xunit;

namespace HanabePhotoManager.App.Tests.WeChat;

public sealed class WeChatSenderViewStructureTests
{
    [Fact]
    public void View_ExposesTargetConfirmationProgressAndCancellation()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            root, "src", "HanabePhotoManager.App", "WeChat", "WeChatSenderView.xaml"));

        xaml.Should().Contain("ConfirmTargetCommand");
        xaml.Should().Contain("ProgressValue");
        xaml.Should().Contain("CancelCommand");
        xaml.Should().Contain("AmbiguousCount");
    }

    [Fact]
    public void ReadOnlyProgressProperties_AreBoundOneWay()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            root, "src", "HanabePhotoManager.App", "WeChat", "WeChatSenderView.xaml"));

        xaml.Should().Contain("{Binding CurrentBatch, Mode=OneWay}");
        xaml.Should().Contain("{Binding SentCount, Mode=OneWay}");
        xaml.Should().Contain("{Binding FailedCount, Mode=OneWay}");
        xaml.Should().Contain("{Binding AmbiguousCount, Mode=OneWay}");
        xaml.Should().Contain("{Binding ProgressValue, Mode=OneWay}");
        xaml.Should().Contain("{Binding Length, Mode=OneWay");
        xaml.Should().Contain("{Binding State, Mode=OneWay}");
        xaml.Should().Contain("{Binding RetryCount, Mode=OneWay}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
