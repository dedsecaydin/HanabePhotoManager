using FluentAssertions;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class RecoveryPageUiTests
{
    [Fact]
    public void RecoveryTool_UsesDedicatedPageAndStatesSafetyBoundary()
    {
        var root = FindRoot();
        var page = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "Recovery", "RecoveryPage.xaml"));
        var tools = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "Compression", "CompressionPage.xaml"));
        page.Should().Contain("相机视频安全恢复助手").And.Contain("原卡与镜像只读").And.Contain("实验候选不会自动恢复");
        page.Should().Contain("ItemsSource=\"{Binding Candidates}\"").And.Contain("Command=\"{Binding RecoverCommand}\"");
        tools.Should().Contain("Tag=\"Recovery\"").And.Contain("存储卡视频恢复");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
