using FluentAssertions;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

/// <summary>
/// 主窗口 DWM 亚克力材质 + 人物查找页黄色标识改造的回归护栏：
/// 锁设置页开关行、MainWindow.xaml.cs 的 DWM 优先/降级实现，
/// 以及人物查找页徽章不再使用黄色 TertiaryContainer。
/// </summary>
public sealed class WindowMaterialGuardsTests
{
    [Fact]
    public void SettingsPage_HasAcrylicMaterialToggleBoundToIsAcrylicEnabled()
    {
        var settings = Read("SettingsCenterPage.xaml");

        settings.Should().Contain("Text=\"亚克力材质\"");
        settings.Should().Contain("启用系统级亚克力背景模糊（DWM）。关闭后使用半透明降级方案。");
        settings.Should().Contain("IsChecked=\"{Binding IsAcrylicEnabled, Mode=TwoWay}\"");
        settings.Should().Contain("ToolTip=");
        settings.Should().Contain("AutomationProperties.Name=\"亚克力材质开关\"");
    }

    [Fact]
    public void MainWindowCodeBehind_ImplementsDwmFirstMaterialWithFallback()
    {
        var code = File.ReadAllText(Path.Combine(
            SourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml.cs"));

        // DWM 优先：Win11 DWMWA_SYSTEMBACKDROP_TYPE（亚克力 3 / Blur 2）
        code.Should().Contain("DwmwaSystemBackdropType = 38");
        code.Should().Contain("DwmSystemBackdropAcrylic = 3");
        code.Should().Contain("DwmSystemBackdropBlur = 2");
        // 降级：Win10 SetWindowCompositionAttribute ACCENT_ENABLE_ACRYLICBLURBEHIND
        code.Should().Contain("SetWindowCompositionAttribute");
        code.Should().Contain("AccentEnableAcrylicBlurBehind = 4");
        // 开关接线：属性变化重试应用材质
        code.Should().Contain("IsAcrylicEnabled");
        code.Should().Contain("ApplyWindowMaterial()");
    }

    [Fact]
    public void FaceSearchBadges_NoLongerUseYellowTertiaryContainer()
    {
        var xaml = Read("MainWindow.xaml");

        // 人物查找页 4 处黄色胶囊已改中性色（Surface.ContainerHigh + OnSurfaceVariant）
        xaml.Should().NotContain("Text=\"{Binding PeopleAlbums.RecognitionEngineText}\" Foreground=\"{DynamicResource Brush.OnTertiaryContainer}\"");
        xaml.Should().NotContain("Text=\"仅本机运行\" Foreground=\"{DynamicResource Brush.OnTertiaryContainer}\"");
        xaml.Should().NotContain("Text=\"🔒 仅在本机运行\" Foreground=\"{DynamicResource Brush.OnTertiaryContainer}\"");
        xaml.Should().NotContain("Text=\"{Binding PersonLabel}\" Foreground=\"{DynamicResource Brush.OnTertiaryContainer}\"");
    }

    private static string Read(string fileName) => File.ReadAllText(
        Path.Combine(SourceRoot(), "src", "HanabePhotoManager.App", fileName));

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
