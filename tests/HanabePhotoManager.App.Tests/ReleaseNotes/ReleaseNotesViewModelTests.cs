using FluentAssertions;
using HanabePhotoManager.App.ReleaseNotes;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests.ReleaseNotes;

public sealed class ReleaseNotesViewModelTests
{
    [Fact]
    public void Catalog_SelectsCurrentVersionAndMarksNewerVersionAvailable()
    {
        var versions = new[]
        {
            new ReleaseVersionInfo("0.3.0-beta", new DateOnly(2026, 9, 1), ["新版"]),
            new ReleaseVersionInfo("0.2.0-alpha", new DateOnly(2026, 8, 3), ["当前版"]),
            new ReleaseVersionInfo("0.1.0-alpha", new DateOnly(2026, 7, 1), ["旧版"])
        };

        var viewModel = new ReleaseNotesViewModel(versions, "0.2.0-alpha");

        viewModel.SelectedVersion!.Version.Should().Be("0.2.0-alpha");
        viewModel.CurrentVersionLabel.Should().Be("当前版本 0.2.0-alpha");
        viewModel.Versions.Single(item => item.Version == "0.3.0-beta").StatusLabel.Should().Be("可更新");
        viewModel.Versions.Single(item => item.Version == "0.1.0-alpha").StatusLabel.Should().Be("历史版本");
    }

    [Fact]
    public void Selection_ChangesScrollableReleaseContent()
    {
        var viewModel = new ReleaseNotesViewModel(
            [
                new ReleaseVersionInfo("0.2.0", new DateOnly(2026, 8, 3), ["新增树图", "安装器"]),
                new ReleaseVersionInfo("0.1.0", new DateOnly(2026, 7, 1), ["项目基线"])
            ],
            "0.2.0");

        viewModel.SelectedVersion = viewModel.Versions.Single(item => item.Version == "0.1.0");

        viewModel.SelectedReleaseTitle.Should().Be("0.1.0 · 2026-07-01");
        viewModel.SelectedReleaseNotes.Should().Be("• 项目基线");
    }

    [Fact]
    public void Settings_UsesVersionTreeAndScrollableDetails()
    {
        var xaml = File.ReadAllText(ProjectFile("src", "HanabePhotoManager.App", "SettingsCenterPage.xaml"));

        xaml.Should().Contain("x:Name=\"ReleaseVersionTree\"");
        xaml.Should().Contain("ItemsSource=\"{Binding ReleaseNotes.Versions}\"");
        xaml.Should().Contain("SelectedItem=\"{Binding ReleaseNotes.SelectedVersion, Mode=TwoWay}\"");
        xaml.Should().Contain("x:Name=\"ReleaseNotesScrollViewer\"");
        xaml.Should().Contain("Text=\"{Binding ReleaseNotes.CurrentVersionLabel}\"");
    }

    private static string ProjectFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "HanabePhotoManager.sln")))
        {
            current = current.Parent;
        }

        current.Should().NotBeNull("tests must run from inside the repository");
        return Path.Combine([current!.FullName, .. segments]);
    }
}
