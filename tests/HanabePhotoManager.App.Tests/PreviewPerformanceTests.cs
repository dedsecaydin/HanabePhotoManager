using FluentAssertions;
using HanabePhotoManager.App.ViewModels;
using HanabePhotoManager.Core.Imports;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PreviewPerformanceTests
{
    [Fact]
    public void PreviewCards_ProvideExplicitSelectionModeAndLongPressSelection()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml.cs"));

        xaml.Should().Contain("x:Name=\"PreviewSelectionModeButton\"");
        xaml.Should().Contain("PreviewThumbnail_MouseLeftButtonUp");
        code.Should().Contain("TogglePreviewSelectionMode");
        code.Should().Contain("PreviewLongPressTimer_Tick");
        code.Should().Contain("IsPreviewSelectionMode");
    }

    [Theory]
    [InlineData("07.16_棚拍", "07.16_棚拍")]
    [InlineData("07.16", "07.16")]
    public void DateFolderDisplayName_PreservesSavedRemark(string folderName, string expected)
    {
        MainWindowViewModel.DateFolderDisplayName(folderName, new LibraryDate(2026, 7, 16))
            .Should().Be(expected);
    }

    [Fact]
    public void PhotoWalls_UseBoundedCollectionsWhileKeepingAllFeaturesInTemplate()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("ItemsSource=\"{Binding HomePreviewFiles}\"");
        xaml.Should().Contain("ItemsSource=\"{Binding VisiblePreviewSections}\"");
        xaml.Should().Contain("ItemsSource=\"{Binding Items}\"");
        xaml.Should().NotContain("ItemsSource=\"{Binding FilteredPreviewFiles}\"");
        xaml.Should().Contain("PreviewContextMenu_Rate5");
        xaml.Should().Contain("PreviewContextMenu_TagPortrait");
        xaml.Should().Contain("PreviewContextMenu_BatchCopy");
        xaml.Should().Contain("IsChecked=\"{Binding IsSelected, Mode=TwoWay}\"");
        xaml.Should().Contain("Command=\"{Binding DeleteSelectedFilesCommand}\"");
        xaml.Should().Contain("PreviewSelectionSurface_MouseLeftButtonDown");
        xaml.Should().Contain("PreviewSelectionRectangle");
        xaml.Should().NotContain("RepeatBehavior=\"Forever\"");
    }

    [Fact]
    public void DeletePairResolver_FindsMatchingRawAndJpegAcrossSiblingCategoryFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "HanabePairTest", Guid.NewGuid().ToString("N"));
        var raw = Path.Combine(root, "RAW生图");
        var jpeg = Path.Combine(root, "JPG生图");
        Directory.CreateDirectory(raw);
        Directory.CreateDirectory(jpeg);
        var rawPath = Path.Combine(raw, "JK0042.ARW");
        var jpegPath = Path.Combine(jpeg, "JK0042.JPG");
        var unrelated = Path.Combine(jpeg, "JK0043.JPG");
        File.WriteAllText(rawPath, "raw");
        File.WriteAllText(jpegPath, "jpeg");
        File.WriteAllText(unrelated, "other");

        try
        {
            MainWindowViewModel.ResolveRawJpegPairPaths(jpegPath)
                .Should().BeEquivalentTo(rawPath, jpegPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DeleteConfirmation_UsesApplicationGlassDialogInsteadOfSystemConfirmation()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "DeleteConfirmationWindow.xaml"));
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "ViewModels", "MainWindowViewModel.cs"));

        xaml.Should().Contain("Style=\"{StaticResource Dialog.Surface}\"");
        xaml.Should().Contain("x:Name=\"CancelButton\"");
        xaml.Should().Contain("移入回收站");
        viewModel.Should().Contain("DeleteConfirmationWindow.Confirm");
    }

    [Fact]
    public void PhotoViewer_DeleteButtonAndShortcut_RequireExistingConfirmationDialog()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "PhotoViewerWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "PhotoViewerWindow.xaml.cs"));

        xaml.Should().Contain("Click=\"Delete_Click\"");
        xaml.Should().NotContain("Command=\"{Binding DeleteCommand}\"");
        code.Should().Contain("DeleteConfirmationWindow.Confirm");
        code.Should().Contain("ConfirmDeleteCurrent");
        code.Should().Contain("_viewModel.DeleteCurrent()");

        var dialogXaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "DeleteConfirmationWindow.xaml"));
        dialogXaml.Should().Contain("Style=\"{StaticResource Dialog.Window}\"");
        var dialogStyles = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "Themes", "Controls", "Dialogs.xaml"));
        dialogStyles.Should().NotContain("<Setter Property=\"WindowStartupLocation\"");
    }

    [Fact]
    public void DiscoveredDateCount_CountsLeafDatesInsteadOfTopLevelYears()
    {
        var viewModel = new MainWindowViewModel();
        var dates = new[]
        {
            new LibraryDateNode("07.03", "C:\\photos\\07.03", new LibraryDate(2026, 7, 3)),
            new LibraryDateNode("07.11", "C:\\photos\\07.11", new LibraryDate(2026, 7, 11))
        };
        var month = new LibraryDateNode("7月", string.Empty, null, dates);
        viewModel.LibraryDates.Add(new LibraryDateNode("2026 年", string.Empty, null, [month]));

        viewModel.LibraryDates.Count.Should().Be(1);
        viewModel.DiscoveredDateCount.Should().Be(2);
    }

    [Theory]
    [InlineData("JK0063", "JK0063")]
    [InlineData("JK_02574-恢复的", "JK_02574")]
    [InlineData("JK_03018_ExHiRes", "JK_03018")]
    [InlineData("JK0329_noeffect", "JK0329")]
    public void RetouchedStemNormalization_MatchesRealLibraryNaming(string editedStem, string sourceStem)
    {
        MainWindowViewModel.NormalizeRetouchedStem(editedStem).Should().Be(sourceStem);
    }

    [Fact]
    public void BrowseSidebar_ReplacesRetouchProgressWithPeopleAlbums()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().NotContain("Text=\"修图进度\"");
        xaml.Should().Contain("PeopleAlbums.Albums");
        xaml.Should().Contain("PeopleAlbums.ScanCommand");
    }

    [Fact]
    public void RetouchedPreview_PrefersFinishedJpegOverPhotoshopProject()
    {
        var folder = Path.Combine(Path.GetTempPath(), "修后");
        var psd = Path.Combine(folder, "JK_02574.psd");
        var restoredJpeg = Path.Combine(folder, "JK_02574-恢复的.jpg");

        MainWindowViewModel.SelectPreferredRetouchedOutput([psd, restoredJpeg], "JK_02574")
            .Should().Be(restoredJpeg);
    }

    [Fact]
    public void PreviewDateSection_UsesMonthAndDateFolders()
    {
        var info = MainWindowViewModel.ResolvePreviewDateSection(
            "C:\\photos\\7月\\07.16_棚拍\\JPG生图\\JK0063.JPG");

        info.Title.Should().Be("7月 · 07.16_棚拍");
        info.Key.Should().EndWith("07.16_棚拍");
    }

    [Fact]
    public void PreviewWall_ProvidesPerDateExpandControlWithoutManualBatches()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("ItemsSource=\"{Binding VisiblePreviewSections}\"");
        xaml.Should().Contain("Visibility=\"{Binding IsExpanded, Converter={StaticResource BoolToVis}}\"");
        xaml.Should().Contain("Style=\"{StaticResource PreviewDateHeaderButton}\"");
        xaml.Should().Contain("Command=\"{Binding ToggleCommand}\"");
        xaml.Should().NotContain("<Expander IsExpanded=\"{Binding IsExpanded");
        xaml.Should().NotContain("Content=\"上一批\"");
        xaml.Should().NotContain("Content=\"下一批\"");
    }

    [Fact]
    public void PreviewDateSection_ToggleCommandChangesSingleSectionOnly()
    {
        var first = new PreviewDateSectionViewModel("a", "7月 · 07.16", [], true);
        var second = new PreviewDateSectionViewModel("b", "7月 · 07.17", [], true);

        first.ToggleCommand.Execute(null);

        first.IsExpanded.Should().BeFalse();
        first.ToggleLabel.Should().Be("展开");
        second.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void Calendar_EnablesOnlyDatesThatContainPhotos()
    {
        var available = new[] { new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 16) };

        var days = MainWindowViewModel.BuildCalendarDays(2026, 7, available, new DateOnly(2026, 7, 16));

        days.Single(day => day.Date == new DateOnly(2026, 7, 3)).IsAvailable.Should().BeTrue();
        days.Single(day => day.Date == new DateOnly(2026, 7, 4)).IsAvailable.Should().BeFalse();
        days.Single(day => day.Date == new DateOnly(2026, 7, 16)).IsSelected.Should().BeTrue();
        days.Should().HaveCount(42);
    }

    [Fact]
    public void PreviewSidebar_UsesClickableCalendar()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("ItemsSource=\"{Binding CalendarDays}\"");
        xaml.Should().Contain("IsEnabled=\"{Binding IsAvailable}\"");
        xaml.Should().Contain("SelectCalendarDayCommand");
    }

    [Fact]
    public void PreviewCardAncestorLookup_SupportsContentElementsWithoutCrashing()
    {
        var root = FindSourceRoot();
        var code = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml.cs"));

        code.Should().Contain("ContentOperations.GetParent(contentElement)")
            .And.Contain("FrameworkContentElement");
    }

    [Fact]
    public void PreviewToolbar_UsesReadableGlassFiltersInsteadOfEditableSystemCombos()
    {
        var viewModel = new MainWindowViewModel();
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        viewModel.PreviewSortChoices.Select(choice => choice.Label).Should().ContainInOrder(
            "默认顺序", "文件名 A–Z", "文件名 Z–A", "文件从小到大", "文件从大到小", "分类 A–Z", "分类 Z–A");
        viewModel.SetPreviewRetouchFilterCommand.Execute("已修");
        viewModel.PreviewRetouchFilter.Should().Be("已修");
        viewModel.PreviewSortChoices.Select(choice => choice.Label).Should().Contain(["评分从高到低", "评分从低到高"]);
        viewModel.RatingFilters.Should().Equal("全部评分", "未评分", "1★", "2★", "3★", "4★", "5★");
        xaml.Should().Contain("Style=\"{StaticResource PreviewSegmentButton}\"");
        xaml.Should().Contain("Style=\"{StaticResource PreviewSortComboBox}\"");
        xaml.Should().Contain("SelectedValue=\"{Binding PreviewSortMode, Mode=TwoWay}\"");
        xaml.Should().NotContain("Text=\"{Binding PreviewSortMode}\"");
    }

    [Fact]
    public async Task ResetBrowseConditions_RestoresEveryFilterToItsNeutralDefault()
    {
        var viewModel = new MainWindowViewModel
        {
            CurrentPreviewCategory = "修后",
            PreviewSearchText = "JK",
            PreviewRetouchFilter = "已修",
            RatingFilter = "5★",
            PreviewSortMode = 8,
            SmartCategoryFilter = "人像",
            IsBrowseConditionsExpanded = true
        };

        await viewModel.ResetBrowseConditionsCommand.ExecuteAsync(null);

        viewModel.CurrentPreviewCategory.Should().Be("全部");
        viewModel.PreviewSearchText.Should().BeEmpty();
        viewModel.PreviewRetouchFilter.Should().Be("全部");
        viewModel.RatingFilter.Should().Be("全部评分");
        viewModel.PreviewSortMode.Should().Be(0);
        viewModel.SmartCategoryFilter.Should().Be("全部智能类别");
        viewModel.IsBrowseConditionsExpanded.Should().BeFalse();
    }

    [Fact]
    public void PreviewFilters_AreGroupedInOneCollapsedBrowseConditionsPanel()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("x:Name=\"BrowseConditionsPanel\"");
        xaml.Should().Contain("Header=\"浏览条件\"");
        xaml.Should().Contain("IsExpanded=\"{Binding IsBrowseConditionsExpanded, Mode=TwoWay}\"");
        xaml.Should().Contain("Content=\"重置\"");
        xaml.Should().Contain("ResetBrowseConditionsCommand");
        xaml.Should().Contain("BrowseConditionsSummary");
    }

    [Fact]
    public void PreviewCard_PhotoBleedsToOuterEdgesAndUsesOnlyTopCornerMask()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("Tag=\"PreviewCard\" Margin=\"0,0,14,14\" Padding=\"0\" CornerRadius=\"22\"");
        xaml.Should().Contain("CornerRadius=\"21,21,0,0\" ClipToBounds=\"True\"");
        xaml.Should().Contain("<Border.Background><ImageBrush Stretch=\"UniformToFill\" ImageSource=\"{Binding Thumbnail}\" /></Border.Background>");
        xaml.Should().NotContain("<Rectangle>\n                                                  <Shape.Fill>");
    }

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HanabePhotoManager.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
