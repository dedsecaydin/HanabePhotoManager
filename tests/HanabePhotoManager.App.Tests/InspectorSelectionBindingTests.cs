using System.IO;
using System.Windows;
using FluentAssertions;
using Xunit;

namespace HanabePhotoManager.App.Tests;

/// <summary>
/// 回归测试：浏览页单击缩略图后右侧 Inspector 必须显示文件信息。
/// 历史根因（2026-08-14）：Inspector 单文件面板的 Visibility 用
/// <c>BooleanToVisibilityConverter</c> 绑定 <c>SelectedPreviewFile</c>（对象引用），
/// 该转换器只识别 bool/bool?，对对象引用永远返回 Collapsed → 面板永远隐藏、
/// 「未选择照片」空态永远显示（M3 定稿截图 --select-first 下仍为空态的铁证）。
/// 修复 = 改用 NullToVisibilityConverter（非 null → Visible）。
/// </summary>
public sealed class InspectorSelectionBindingTests
{
    [Fact]
    public void NullToVisibilityConverter_ShowsPanelForNonNullObjectReference()
    {
        // BooleanToVisibilityConverter 对非 bool 对象引用返回 Collapsed（旧 bug 根因）。
        var boolConverter = new System.Windows.Controls.BooleanToVisibilityConverter();
        boolConverter.Convert(new object(), typeof(Visibility), null, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Collapsed, "WPF 内置转换器只处理 bool/bool?，对象引用一律折叠");

        // 修复后使用的 NullToVisibilityConverter 必须对非 null 对象返回 Visible。
        var nullConverter = new NullToVisibilityConverter();
        nullConverter.Convert(new object(), typeof(Visibility), null, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Visible, "选中文件（SelectedPreviewFile 非 null）时单文件面板必须显示");
        nullConverter.Convert(null, typeof(Visibility), null, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Collapsed, "未选中时保持空态");
    }

    [Fact]
    public void InspectorSingleFilePanel_BindsSelectedPreviewFileWithNullToVis()
    {
        // 防回归：MainWindow.xaml 中 Inspector 单文件面板必须用 NullToVis（对象引用转换器），
        // 不得用 BoolToVis（BooleanToVisibilityConverter）——那是 2026-08-14 修复的根因。
        var xaml = File.ReadAllText(Path.Combine(FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain(
            "Visibility=\"{Binding SelectedPreviewFile, Converter={StaticResource NullToVis}}\"",
            "单击缩略图后右侧 Inspector 单文件面板必须随 SelectedPreviewFile 显示");
        xaml.Should().NotContain(
            "Visibility=\"{Binding SelectedPreviewFile, Converter={StaticResource BoolToVis}}\"",
            "BooleanToVisibilityConverter 对对象引用永远 Collapsed，会导致 Inspector 永远显示「未选择照片」空态");
    }

    [Fact]
    public void InspectorEmptyState_IsVisibleOnlyWhenNoPreviewFileIsSelected()
    {
        var xaml = File.ReadAllText(Path.Combine(FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain(
            "x:Name=\"InspectorEmptyState\" VerticalAlignment=\"Center\" HorizontalAlignment=\"Center\" Margin=\"28\" Visibility=\"{Binding SelectedPreviewFile, Converter={StaticResource NullToVis}, ConverterParameter=Invert}\"",
            "选中照片后空状态必须折叠，不能与文件详情同时显示");
    }

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("测试必须能从输出目录向上找到仓库根");
        return directory!.FullName;
    }
}
