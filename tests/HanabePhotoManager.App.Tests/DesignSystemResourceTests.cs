using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class DesignSystemResourceTests
{
    private static readonly string[] RequiredThemeKeys =
    [
        "Brush.Background.Canvas", "Brush.Surface.Default", "Brush.Surface.Subtle",
        "Brush.Border.Default", "Brush.Border.Focus", "Brush.Text.Primary",
        "Brush.Text.Secondary", "Brush.Accent.Default", "Brush.Status.Danger"
    ];

    [Fact]
    public void LightAndDarkThemes_ExposeTheSameSemanticBrushes()
    {
        var light = Read("Themes", "Colors", "Brushes.Light.xaml");
        var dark = Read("Themes", "Colors", "Brushes.Dark.xaml");

        foreach (var key in RequiredThemeKeys)
        {
            light.Should().Contain($"x:Key=\"{key}\"");
            dark.Should().Contain($"x:Key=\"{key}\"");
        }

        Keys(light).Should().BeEquivalentTo(Keys(dark));
    }

    [Fact]
    public void Typography_UsesTheApprovedSystemFontStack()
    {
        Read("Themes", "Typography", "FontFamilies.xaml")
            .Should().Contain("Segoe UI Variable, Microsoft YaHei UI")
            .And.NotContain("MiSans")
            .And.NotContain("HarmonyOS");
    }

    [Fact]
    public void App_LoadsTheLightThemeEntryPoint()
    {
        Read("App.xaml").Should().Contain("Themes/Themes/Dynamic.Light.xaml");
    }

    [Fact]
    public void AllSixThemes_ExposeTheSameColorAndBrushKeys()
    {
        var schemes = new[] { "Dynamic", "Forest", "Violet" };
        var modes = new[] { "Light", "Dark" };
        var colorFiles = schemes.SelectMany(s => modes.Select(m => (s, m)))
            .Select(p => Read("Themes", "Colors", $"Colors.{p.s}.{p.m}.xaml")).ToArray();

        foreach (var colorFile in colorFiles)
        {
            colorFile.Should().Contain("x:Key=\"Color.Primary\"");
            colorFile.Should().Contain("x:Key=\"Color.Surface.ContainerLow\"");
            colorFile.Should().Contain("x:Key=\"Color.OnSurface\"");
        }

        Read("Themes", "Colors", "Brushes.Light.xaml").Should().Contain("x:Key=\"Brush.Primary\"")
            .And.Contain("x:Key=\"Brush.SecondaryContainer\"")
            .And.Contain("x:Key=\"Brush.Surface.Container\"")
            .And.Contain("x:Key=\"Brush.OnSurfaceVariant\"");
    }

    [Fact]
    public void ComponentLibrary_DefinesRequiredSharedStyles()
    {
        var all = string.Join('\n', Directory.GetFiles(
            Path.Combine(SourceRoot(), "src", "HanabePhotoManager.App", "Themes", "Controls"), "*.xaml")
            .Select(File.ReadAllText));

        foreach (var key in new[]
        {
            "Button.Primary", "Button.Secondary", "Button.Ghost", "Button.Danger",
            "Button.Icon", "Button.Toolbar", "Button.Disclosure", "Card.Default",
            "Card.Subtle", "Card.Interactive", "Card.Selected"
        })
        {
            all.Should().Contain($"x:Key=\"{key}\"");
        }
    }

    [Theory]
    [InlineData("Icon.Import")]
    [InlineData("Icon.Library")]
    [InlineData("Icon.Map")]
    [InlineData("Icon.Compress")]
    [InlineData("Icon.Watermark")]
    [InlineData("Navigation.ReorderableItem")]
    [InlineData("Navigation.Segment")]
    [InlineData("Navigation.SegmentItem")]
    [InlineData("Input.SettingsComboBox")]
    [InlineData("Layout.SettingsGroup")]
    public void RequiredNavigationResourcesExist(string key)
    {
        var all = string.Join('\n', Directory.GetFiles(
            Path.Combine(SourceRoot(), "src", "HanabePhotoManager.App", "Themes"), "*.xaml", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

        all.Should().Contain($"x:Key=\"{key}\"");
    }

    [Fact]
    public void AppearanceAndCompressionSelectors_UseTheSharedThemedComboBoxTemplate()
    {
        var inputs = Read("Themes", "Controls", "Inputs.xaml");
        var settings = Read("SettingsCenterPage.xaml");
        var compression = Read("Compression", "CompressionPage.xaml");

        inputs.Should().Contain("x:Key=\"Input.SettingsComboBox\"")
            .And.Contain("<ControlTemplate TargetType=\"ComboBox\">")
            .And.Contain("<Trigger Property=\"Validation.HasError\" Value=\"True\"><Setter TargetName=\"Chrome\" Property=\"BorderBrush\" Value=\"{DynamicResource Brush.Status.Danger}\"/></Trigger>");
        settings.Should().Contain("Style=\"{StaticResource Input.SettingsComboBox}\" ItemsSource=\"{Binding BackgroundModes}\"")
            .And.Contain("Style=\"{StaticResource Input.SettingsComboBox}\" ItemsSource=\"{Binding BackgroundImageLayouts}\"");
        compression.Should().Contain("Style=\"{DynamicResource Input.SettingsComboBox}\" ItemsSource=\"{Binding TargetModes}\"")
            .And.Contain("Style=\"{DynamicResource Input.SettingsComboBox}\" ItemsSource=\"{Binding TargetUnits}\"")
            .And.Contain("HorizontalAlignment=\"Stretch\" MaxWidth=\"Infinity\"")
            .And.Contain("MinWidth=\"0\" MaxWidth=\"Infinity\" HorizontalAlignment=\"Stretch\"");
    }

    [Fact]
    public void CompressionPage_IsPresentedAsImageToolsWithCollageControls()
    {
        var xaml = Read("Compression", "CompressionPage.xaml");
        var watermark = Read("Watermark", "WatermarkPage.xaml");

        xaml.Should().Contain("x:Name=\"ImageToolModeTabs\"");
        xaml.Should().Contain("<WrapPanel");
        xaml.Should().Contain("Text=\"图片小工具\" Style=\"{DynamicResource Layout.PageTitle}\"");
        xaml.Should().NotContain("<TextBlock Text=\"批量压缩\" Style=\"{DynamicResource Layout.SectionTitle}\"");
        xaml.Should().NotContain("<TextBlock Text=\"拼图\" Style=\"{DynamicResource Layout.SectionTitle}\"");
        watermark.Should().NotContain("<TextBlock Text=\"批量水印\" Style=\"{DynamicResource Layout.PageTitle}\"");
        xaml.Should().Contain("DataTrigger.EnterActions");
        xaml.Split("Duration=\"0:0:0.18\"").Length.Should().BeGreaterThanOrEqualTo(4);
        xaml.Should().Contain("ItemsSource=\"{Binding ToolModes}\"");
        xaml.Should().Contain("纵向拼接");
        xaml.Should().Contain("横向拼接");
        xaml.Should().Contain("CollageLimitOutputSize");
        xaml.Should().Contain("watermark:WatermarkPage");
    }

    [Theory]
    [InlineData("DeleteConfirmationWindow.xaml")]
    [InlineData("RemarkPromptWindow.xaml")]
    [InlineData("Contest", "ContestPickerWindow.xaml")]
    public void Dialogs_UseSharedResourcesWithoutRawColors(params string[] parts)
    {
        var xaml = Read(parts);
        xaml.Should().Contain("Dialog.Window");
        xaml.Should().Contain("Button.Secondary");
        Regex.IsMatch(xaml, "#[0-9A-Fa-f]{6,8}").Should().BeFalse();
        Regex.IsMatch(xaml, "<Style(?![^>]*x:Key=)[^>]*TargetType=\"(?:\\{x:Type )?Button")
            .Should().BeFalse();
    }

    [Fact]
    public void ApplicationXaml_HasNoRawColorsOutsideThemeColorDictionaries()
    {
        var appRoot = Path.Combine(SourceRoot(), "src", "HanabePhotoManager.App");
        var offenders = Directory.GetFiles(appRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine("Themes", "Colors"), StringComparison.OrdinalIgnoreCase))
            .Where(path => Regex.IsMatch(File.ReadAllText(path), "#[0-9A-Fa-f]{6,8}"))
            .Select(path => Path.GetRelativePath(appRoot, path));
        offenders.Should().BeEmpty("raw color literals belong only in theme color dictionaries");
    }

    private static string[] Keys(string xaml) => Regex.Matches(xaml, "x:Key=\\\"([^\\\"]+)\\\"")
        .Select(match => match.Groups[1].Value).ToArray();

    private static string Read(params string[] parts) => File.ReadAllText(
        Path.Combine([SourceRoot(), "src", "HanabePhotoManager.App", .. parts]));

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
