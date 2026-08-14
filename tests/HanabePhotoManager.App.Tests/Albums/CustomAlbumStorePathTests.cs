using FluentAssertions;
using HanabePhotoManager.App.Services;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests.Albums;

/// <summary>
/// 自定义相册存储位置的回归护栏：默认必须固定在应用数据目录内
/// （%LOCALAPPDATA%\HanabePhotoManager\custom-albums.json），
/// 而不是用户选择的任意路径；用户仍可自行选择照片文件夹加入相册。
/// </summary>
public sealed class CustomAlbumStorePathTests
{
    [Fact]
    public void CustomAlbumsFile_ResidesInsideAppDataRoot()
    {
        var root = Path.GetFullPath(AppDataPaths.Root);
        var file = Path.GetFullPath(AppDataPaths.CustomAlbumsFile);

        file.Should().StartWith(root + Path.DirectorySeparatorChar);
        Path.GetFileName(file).Should().Be("custom-albums.json");
    }

    [Fact]
    public void MainWindowViewModel_WiresCustomAlbumStoreToTheAppDataFile()
    {
        var source = File.ReadAllText(Path.Combine(
            SourceRoot(), "src", "HanabePhotoManager.App", "ViewModels", "MainWindowViewModel.cs"));

        source.Should().Contain("new JsonCustomAlbumStore(AppDataPaths.CustomAlbumsFile)");
        source.Should().NotContain("new JsonCustomAlbumStore(Path.Combine(AppDataPaths.Root, \"custom-albums.json\"))");
    }

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
