using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class ControlThemeTests
{
    [Fact]
    public void SettingsChoiceComboBoxes_RenderLabelsInsteadOfRecordText()
    {
        var mainXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        mainXaml.Should().NotContain("ItemsSource=\"{Binding PreviewSortChoices}\" DisplayMemberPath=\"Label\"");
        mainXaml.Should().NotContain("ItemsSource=\"{Binding BrowseEntryModes}\" DisplayMemberPath=\"Label\"");
        mainXaml.Should().Contain("DataType=\"{x:Type vm:PreviewSortChoice}\"");
        mainXaml.Should().Contain("DataType=\"{x:Type vm:BrowseEntryChoice}\"");
    }

    [Fact]
    public void CloudPage_AlwaysProvidesLoadingEmptyAndErrorFeedbackWithRetry()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "Cloud", "CloudPage.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "Cloud", "CloudPage.xaml.cs"));

        xaml.Should().Contain("x:Name=\"CloudStatusPanel\"");
        xaml.Should().Contain("x:Name=\"CloudStatusTitle\"");
        xaml.Should().Contain("x:Name=\"CloudStatusDescription\"");
        xaml.Should().Contain("x:Name=\"CloudRetryButton\"");
        code.Should().Contain("NavigationCompleted");
        code.Should().Contain("ShowLoadingState");
        code.Should().Contain("ShowEmptyState");
        code.Should().Contain("ShowErrorState");
        code.Should().Contain("CloudRetry_Click");
    }

    [Fact]
    public void SharedTextAndButtonStyles_AreDpiSafeAndDisabledButtonsRemainReadable()
    {
        var root = FindSourceRoot();
        var buttons = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "Themes", "Controls", "Buttons.xaml"));
        var layout = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "Themes", "Controls", "Layout.xaml"));
        var status = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "Themes", "Controls", "Status.xaml"));
        var watermark = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "Watermark", "WatermarkPage.xaml"));

        buttons.Should().Contain("VerticalContentAlignment");
        buttons.Should().Contain("HorizontalContentAlignment");
        buttons.Should().Contain("TextElement.FontSize");
        buttons.Should().Contain("Brush.Surface.Subtle");
        buttons.Should().NotContain("<Setter Property=\"Opacity\" Value=\"0.72\"");
        layout.Should().Contain("TextOptions.TextFormattingMode");
        status.Should().Contain("TextOptions.TextFormattingMode");
        watermark.Should().Contain("TextWrapping=\"Wrap\"");
        watermark.Should().Contain("MinHeight=\"40\"");
        watermark.Should().Contain("x:Key=\"WatermarkButtonTextTemplate\"");
        watermark.Should().Contain("ContentTemplate=\"{StaticResource WatermarkButtonTextTemplate}\"");
    }

    [Fact]
    public void RadioButtonStyle_DoesNotInheritFromCheckBoxStyle()
    {
        var selectionXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "Themes", "Controls", "Selection.xaml"));

        selectionXaml.Should().NotContain(
            "TargetType=\"RadioButton\" BasedOn=\"{StaticResource Selection.CheckBox}\"",
            "WPF rejects a RadioButton style based on a CheckBox-targeted style at runtime");
    }

    [Fact]
    public void AppResources_DefineCompleteGlassTemplatesForSystemGreyControls()
    {
        var appXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "App.xaml"));

        foreach (var control in new[] { "ScrollBar", "Slider", "ProgressBar", "ComboBox" })
        {
            var typePattern = "(?:\\{x:Type )?" + Regex.Escape(control) + "\\}?";

            Regex.IsMatch(appXaml, "<Style[^>]*TargetType=\\\"" + typePattern + "\\\"")
                .Should().BeTrue($"{control} should have an implicit global style");
            Regex.IsMatch(appXaml, "<ControlTemplate[^>]*TargetType=\\\"" + typePattern + "\\\"")
                .Should().BeTrue($"{control} should replace the native Windows template");
        }

        appXaml.Should().Contain("x:Key=\"GlassControlTrack\"");
        appXaml.Should().Contain("x:Key=\"GlassControlFocus\"");
        appXaml.Should().Contain("x:Name=\"PART_Indicator\"");
        appXaml.Should().Contain("x:Name=\"PART_Popup\"");
    }

    [Fact]
    public void MainWindow_DoesNotOverrideGlobalSystemGreyControlTemplates()
    {
        var mainXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        var localImplicitControlStyle = new Regex(
            "<Style(?![^>]*x:Key=)[^>]*TargetType=\"(?:\\{x:Type )?(ComboBox|Slider|ScrollBar|ProgressBar)\\}?\"",
            RegexOptions.CultureInvariant);

        localImplicitControlStyle.IsMatch(mainXaml).Should().BeFalse(
            "the application-wide templates must remain the single source of truth");
    }

    [Fact]
    public void SidebarNavigation_UsesReorderableIconAwareNavigationAndStandardWidth()
    {
        var mainXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        mainXaml.Should().Contain("<ColumnDefinition Width=\"232\" />");
        mainXaml.Should().Contain("Style=\"{StaticResource Sidebar.Container}\"");
        mainXaml.Should().Contain("ItemsSource=\"{Binding NavigationItems}\"");
        mainXaml.Should().Contain("Style=\"{StaticResource Navigation.ReorderableItem}\"");
        mainXaml.Should().Contain("AutomationProperties.Name=\"{Binding Label}\"");
        mainXaml.Should().Contain("PrimaryNavigationItem_PreviewMouseMove");
        mainXaml.Should().Contain("x:Name=\"ThemeToggleButton\"");
        mainXaml.Should().NotContain("Win11NavButton");
    }

    [Fact]
    public void SidebarFooter_UsesAccessibleIconActionsAndArtworkOnlyBranding()
    {
        var mainXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        mainXaml.Should().Contain("AutomationProperties.Name=\"切换主题\"");
        mainXaml.Should().Contain("AutomationProperties.Name=\"设置\"");
        mainXaml.Should().NotContain("Text=\"Hanabe Photos\"");
        mainXaml.Should().Contain("Stretch=\"Uniform\"");
        mainXaml.Should().Contain("Tag=\"{StaticResource Icon.Theme}\"");
        mainXaml.Should().Contain("Tag=\"{StaticResource Icon.Settings}\"");
        mainXaml.Should().Contain("x:Key=\"SidebarFooterContentTemplate\"");
        mainXaml.Should().Contain("FooterNavigationLabel");
        mainXaml.Should().Contain("HorizontalContentAlignment=\"Stretch\" Content=\"深色模式\"");
        mainXaml.Should().Contain("HorizontalContentAlignment=\"Stretch\" Content=\"设置\"");
        mainXaml.Should().Contain("Width=\"104\" Height=\"104\"");
        mainXaml.Should().Contain("RectangleGeometry Rect=\"0,0,100,100\" RadiusX=\"12\" RadiusY=\"12\"");
        mainXaml.Should().Contain("Panel.ZIndex=\"3\"");
    }

    [Fact]
    public void GlobalScrollBar_AlwaysShowsTrackAndReadablePositionThumb()
    {
        var appXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "App.xaml"));

        appXaml.Should().Contain("x:Key=\"GlassScrollTrack\"");
        appXaml.Should().Contain("<Setter Property=\"Width\" Value=\"12\" />");
        appXaml.Should().Contain("<Setter Property=\"Height\" Value=\"12\" />");
        appXaml.Should().Contain("<Setter Property=\"MinHeight\" Value=\"28\" />");
        appXaml.Should().Contain("<Setter Property=\"MinWidth\" Value=\"28\" />");
        appXaml.Should().Contain("Value=\"6\"");
        appXaml.Should().Contain("Value=\"8\"");
        appXaml.Should().Contain("Background=\"{StaticResource GlassScrollTrack}\"");
    }

    [Fact]
    public void ReadOnlyAnalysisProgress_IsExplicitlyBoundOneWay()
    {
        var mainXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        mainXaml.Should().Contain("PhotoAnalysis.ProgressValue, Mode=OneWay");

        var mapXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "Map", "MapPage.xaml"));
        mapXaml.Should().Contain("UnlocatedPhotos.Count, Mode=OneWay");
        mapXaml.Should().Contain("LocatedPhotos.Count, Mode=OneWay");
    }

    [Fact]
    public void MapModes_UseSharedThemeAwareSegmentStyle()
    {
        var mapXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "Map", "MapPage.xaml"));

        mapXaml.Should().Contain("Style=\"{DynamicResource Navigation.Segment}\"");
        mapXaml.Should().Contain("Style=\"{DynamicResource Navigation.SegmentItem}\"");
        mapXaml.Should().Contain("Brush.Background.Canvas");
        mapXaml.Should().NotContain("MapModeTabItem");
    }

    [Fact]
    public void ConnectedDeviceCards_UseDedicatedSubtleHoverInsteadOfGlobalButtonChrome()
    {
        var mainXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        mainXaml.Should().Contain("x:Key=\"DeviceCardButton\"");
        mainXaml.Should().Contain("Style=\"{StaticResource DeviceCardButton}\"");
        mainXaml.Should().Contain("x:Name=\"DeviceCardInteractionSurface\"");
        mainXaml.Should().Contain("Property=\"UIElement.IsMouseOver\" Value=\"True\"");
        mainXaml.Should().Contain("Property=\"BorderBrush\" Value=\"{DynamicResource Brush.Border.Default}\"");
        mainXaml.Should().Contain("Property=\"Background\" Value=\"{DynamicResource Brush.Surface.Subtle}\"");
        mainXaml.Should().Contain("Property=\"UIElement.IsKeyboardFocused\" Value=\"True\"");
        mainXaml.Should().Contain("Brush.Border.Default");
    }

    [Fact]
    public void BrowseWorkspace_UsesOneCleanPanelWithoutInlineTagCreationOrAdjustLabel()
    {
        var mainXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        mainXaml.Should().Contain("x:Name=\"BrowseUnifiedWorkspace\"");
        mainXaml.Should().Contain("x:Name=\"BrowseSmartSearchBox\"");
        mainXaml.Split("ItemsSource=\"{Binding PreviewCategoryFilters}\"").Should().HaveCount(2);
        mainXaml.IndexOf("x:Name=\"BrowseUnifiedWorkspace\"", StringComparison.Ordinal)
            .Should().BeLessThan(mainXaml.IndexOf("ItemsSource=\"{Binding PreviewCategoryFilters}\"", StringComparison.Ordinal));
        mainXaml.Should().NotContain("BorderThickness=\"1,0,1,0\"");
        mainXaml.Should().NotContain("Text=\"{Binding NewCustomTagName");
        mainXaml.Should().NotContain("Command=\"{Binding CreateCustomTagCommand}");
        mainXaml.Should().NotContain("Text=\"调整\"");
    }

    [Fact]
    public void SettingsWorkspace_ConnectsTopBarTabsAndBottomEdgeWithoutOuterGaps()
    {
        var mainXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        mainXaml.Should().Contain("DataTrigger Binding=\"{Binding IsSettingsPage}\" Value=\"True\"");
        mainXaml.Should().Contain("Setter Property=\"Padding\" Value=\"0\"");
        mainXaml.Should().Contain("Setter Property=\"Padding\" Value=\"24,20,24,18\"");
        mainXaml.Should().Contain("Setter Property=\"Margin\" Value=\"0\"");
    }

    [Fact]
    public void ApplicationShell_OffersReplayableFeatureGuideAndFriendlyFaceChoiceLabels()
    {
        var settingsXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "SettingsCenterPage.xaml"));
        var mainXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        settingsXaml.Should().Contain("新手指南");
        settingsXaml.Should().Contain("ReplayOnboardingCommand");
        mainXaml.Should().Contain("Hanabe 新手指南");
        mainXaml.Should().Contain("IsOnboardingVisible");
        mainXaml.Should().Contain("PreviousOnboardingStepCommand");
        mainXaml.Should().Contain("NextOnboardingStepCommand");
        mainXaml.Should().Contain("PlacementTarget=\"{Binding ElementName=OnboardingAnalyzeButton}\"");
        mainXaml.Should().Contain("PlacementTarget=\"{Binding ElementName=OnboardingImportButton}\"");
        mainXaml.Should().Contain("选择图库根目录");
        mainXaml.Should().Contain("选择来源文件夹");
        mainXaml.Should().Contain("暂时不用介绍了");
        mainXaml.Should().Contain("请继续给我介绍");
        mainXaml.Should().Contain("ContinueOnboardingCommand");
        mainXaml.Should().Contain("StopOnboardingCommand");

        new FaceEngineChoice(FaceRecognitionEngineKind.YuNetSFace, "YuNet + SFace")
            .ToString().Should().Be("YuNet + SFace");
        new FaceProfileChoice(FaceRecognitionProfile.Balanced, "均衡")
            .ToString().Should().Be("均衡");
    }

    [Fact]
    public void WatermarkPreview_RendersSeparateSignatureAndTiledLayers()
    {
        var watermarkXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "Watermark", "WatermarkPage.xaml"));

        watermarkXaml.Should().Contain("Source=\"{Binding WatermarkPreviewImage}\"");
        watermarkXaml.Should().Contain("TileMode=\"Tile\"");
        watermarkXaml.Should().Contain("Viewport=\"{Binding PreviewTileViewport}\"");
        watermarkXaml.Should().Contain("Angle=\"{Binding PreviewTileAngle}\"");
        watermarkXaml.Should().Contain("Visibility=\"{Binding ShowSignatureSettings, Converter={StaticResource BoolToVis}}\"");
        watermarkXaml.Should().Contain("Visibility=\"{Binding ShowTileSettings, Converter={StaticResource BoolToVis}}\"");
        watermarkXaml.Should().Contain("DataTrigger Binding=\"{Binding HasItems}\" Value=\"True\"");
    }

    [Fact]
    public void FaceReferenceContent_IsClippedToItsRoundedFrame()
    {
        var mainXaml = File.ReadAllText(Path.Combine(
            FindSourceRoot(), "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        mainXaml.Should().Contain("x:Name=\"FaceReferenceClipSurface\"");
        mainXaml.Should().Contain("<Grid.OpacityMask>");
        mainXaml.Should().Contain("CornerRadius=\"21\"");
    }

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
