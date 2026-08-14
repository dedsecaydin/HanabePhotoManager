using FluentAssertions;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.Search;
using HanabePhotoManager.App.ViewModels;
using HanabePhotoManager.Core.Imports;
using System.IO;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class PreviewPerformanceTests
{
    [Fact]
    public void SemanticRanking_IntersectsExistingFilteredItemsAndKeepsClipOrder()
    {
        var first = new PreviewFileViewModel("first.jpg", "JPG生图", @"D:\photos\first.jpg", "1 KB", ".jpg", null);
        var second = new PreviewFileViewModel("second.jpg", "JPG生图", @"D:\photos\second.jpg", "1 KB", ".jpg", null);
        var excluded = new PreviewFileViewModel("excluded.jpg", "JPG生图", @"D:\photos\excluded.jpg", "1 KB", ".jpg", null);

        var ranked = SemanticBrowseRanking.Apply(
            [first, second],
            item => item.FullPath,
            [excluded.FullPath, second.FullPath, first.FullPath]);

        ranked.Should().Equal(second, first);
    }

    [Fact]
    public void SemanticRanking_WithNoActiveQueryLeavesExistingOrderUntouched()
    {
        var first = new PreviewFileViewModel("first.jpg", "JPG生图", @"D:\photos\first.jpg", "1 KB", ".jpg", null);
        var second = new PreviewFileViewModel("second.jpg", "JPG生图", @"D:\photos\second.jpg", "1 KB", ".jpg", null);

        SemanticBrowseRanking.Apply([first, second], item => item.FullPath, null)
            .Should().Equal(first, second);
    }

    [Fact]
    public void SemanticRanking_NeverExposesMoreThanTheTopFiftyCandidates()
    {
        var files = Enumerable.Range(0, 75)
            .Select(index => new PreviewFileViewModel(
                $"{index:D2}.jpg", "JPG生图", $@"D:\photos\{index:D2}.jpg", "1 KB", ".jpg", null))
            .ToArray();
        var rankedPaths = files.Reverse().Select(file => file.FullPath).ToArray();

        var ranked = SemanticBrowseRanking.Apply(files, file => file.FullPath, rankedPaths).ToArray();

        ranked.Should().HaveCount(SemanticSearchViewModel.ResultLimit);
        ranked.First().FullPath.Should().Be(@"D:\photos\74.jpg");
        ranked.Last().FullPath.Should().Be(@"D:\photos\25.jpg");
    }

    [Fact]
    public void RebuildRetouchStatistics_DoesNotRescanTheWholeLibraryForEveryDateNode()
    {
        var root = FindSourceRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "HanabePhotoManager.App", "ViewModels", "MainWindowViewModel.cs"));
        var methodStart = source.IndexOf("private void RecalcDateNodeStats()", StringComparison.Ordinal);
        var nextMethod = source.IndexOf("private ", methodStart + 1, StringComparison.Ordinal);
        var method = source[methodStart..nextMethod];
        var perNodeLoop = method[method.IndexOf("foreach (var node in flatNodes)", StringComparison.Ordinal)..];

        perNodeLoop.Should().NotContain("PreviewFiles.Where(");
        perNodeLoop.Should().NotContain("RetouchedFiles.Where(");
    }

    [Fact]
    public void PhotoWalls_UseBoundedCollectionsWhileKeepingAllFeaturesInTemplate()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("ItemsSource=\"{Binding HomePreviewFiles}\"");
        xaml.Should().Contain("ItemsSource=\"{Binding PreviewWallItems}\"");
        xaml.Should().Contain("controls:VirtualizingWrapPanel");
        xaml.Should().Contain("x:Name=\"PreviewWallItemsControl\"");
        xaml.Should().NotContain("ItemsSource=\"{Binding FilteredPreviewFiles}\"");
        xaml.Should().Contain("PreviewContextMenu_Rate5");
        xaml.Should().Contain("PreviewContextMenu_TagPortrait");
        xaml.Should().Contain("PreviewContextMenu_BatchCopy");
        xaml.Should().Contain("IsChecked=\"{Binding IsSelected, Mode=TwoWay}\"");
        xaml.Should().Contain("Command=\"{Binding DeleteSelectedFilesCommand}\"");
        xaml.Should().Contain("PreviewSelectionSurface_MouseLeftButtonDown");
        xaml.Should().Contain("PreviewSelectionRectangle");
        xaml.Should().NotContain("RepeatBehavior=\"Forever\"");

        var panelSource = File.ReadAllText(Path.Combine(
            root, "src", "HanabePhotoManager.App", "Controls", "VirtualizingWrapPanel.cs"));
        panelSource.Should().Contain("IScrollInfo");
        panelSource.Should().Contain("GetItemBounds");
        panelSource.Should().Contain("HeaderHeight");
        panelSource.Should().Contain("IWallSectionHeader");
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
    public void PreviewWall_ShowsTheLiveFilteredItemCountAtTheBottom()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("FilteredPreviewCount, Mode=OneWay");
        xaml.Should().Contain("StringFormat=共 {0} 项");
        xaml.Should().NotContain("<Border DockPanel.Dock=\"Bottom\" Padding=\"12,8\" Margin=\"0,10,0,0\" CornerRadius=\"10\"");
    }

    [Fact]
    public void SelectingAPerson_ExpandsAllMatchingDateGroupsAndClearingRestoresTheirState()
    {
        var viewModel = new MainWindowViewModel();
        var firstPath = Path.GetFullPath(@"C:\photos\7月\07.01\JPG生图\first.jpg");
        var secondPath = Path.GetFullPath(@"C:\photos\7月\07.02\JPG生图\second.jpg");
        viewModel.PreviewFiles.Add(new PreviewFileViewModel(
            "first.jpg", "JPG生图", firstPath, "1 KB", ".jpg", null));
        viewModel.PreviewFiles.Add(new PreviewFileViewModel(
            "second.jpg", "JPG生图", secondPath, "1 KB", ".jpg", null));
        viewModel.LibraryDates.Add(new LibraryDateNode("07.01", @"C:\photos\7月\07.01", new LibraryDate(2026, 7, 1)));
        viewModel.LibraryDates.Add(new LibraryDateNode("07.02", @"C:\photos\7月\07.02", new LibraryDate(2026, 7, 2)));
        viewModel.LibraryDates.Add(new LibraryDateNode("07.03", @"C:\photos\7月\07.03", new LibraryDate(2026, 7, 3)));
        viewModel.CurrentPreviewCategory = "JPG生图";
        viewModel.VisiblePreviewSections.Should().HaveCount(2);
        viewModel.VisiblePreviewSections[0].IsExpanded = true;
        viewModel.VisiblePreviewSections[1].IsExpanded = false;
        var person = new PersonAlbumItemViewModel(
            new PersonAlbum { Id = "person", Name = "A", PhotoPaths = [firstPath, secondPath] },
            new PeopleAlbumService(Path.Combine(Path.GetTempPath(), $"people-{Guid.NewGuid():N}.json")),
            _ => { });

        viewModel.PeopleAlbums.SelectedAlbum = person;

        viewModel.VisiblePreviewSections.Should().OnlyContain(section => section.IsExpanded);
        viewModel.CalendarMonthTitle.Should().Be("2026年 7月");
        viewModel.CalendarDays.Single(day => day.Date == new DateOnly(2026, 7, 1)).IsAvailable.Should().BeTrue();
        viewModel.CalendarDays.Single(day => day.Date == new DateOnly(2026, 7, 2)).IsAvailable.Should().BeTrue();
        viewModel.CalendarDays.Single(day => day.Date == new DateOnly(2026, 7, 3)).IsAvailable.Should().BeFalse();
        viewModel.PeopleAlbums.SelectedAlbum = null;
        viewModel.VisiblePreviewSections.Select(section => section.IsExpanded).Should().Equal(true, false);
    }

    [Fact]
    public void CompactBrowseLayout_KeepsThePhotoWallVisible()
    {
        var viewModel = new MainWindowViewModel { IsBrowseConditionsExpanded = true };

        viewModel.UpdateResponsiveBrowseLayout(1280, 760);

        viewModel.IsCompactBrowseLayout.Should().BeTrue();
        viewModel.IsBrowseConditionsExpanded.Should().BeFalse();
    }

    [Fact]
    public void BrowseSummary_IsCompactAndThumbnailControlsLiveAtTheSidebarBottom()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("x:Name=\"BrowseSummaryCard\"");
        xaml.Should().Contain("x:Name=\"BrowseSidebarThumbnailControls\"");
        xaml.Should().Contain("DockPanel.Dock=\"Bottom\"");
        xaml.Should().Contain("Text=\"{Binding ZoomableGridTileSize, StringFormat={}{0:N0}px}\"");
        xaml.Should().Contain("x:Name=\"BrowseSidebarThumbnailControls\"");
        xaml.Should().NotContain("x:Name=\"BrowseSidebarThumbnailControls\" DockPanel.Dock=\"Bottom\" Width=\"250\" Margin=\"0,10,14,4\" Visibility=");
        xaml.Should().NotContain("x:Name=\"BrowseSidebarThumbnailControls\" DockPanel.Dock=\"Bottom\" Width=\"250\" Padding=\"12,9\" Margin=\"0,10,14,0\" CornerRadius");
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

        xaml.Should().Contain("ItemsSource=\"{Binding PreviewWallItems}\"");
        xaml.Should().Contain("HeaderHeight=\"56\"");
        xaml.Should().Contain("Style=\"{StaticResource PreviewDateHeaderButton}\"");
        xaml.Should().Contain("Command=\"{Binding ToggleCommand}\"");
        xaml.Should().Contain("DataType=\"{x:Type vm:PreviewDateSectionViewModel}\"");
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
        viewModel.PreviewSortChoices.Select(choice => choice.Label).Should().Contain(["拍摄时间从新到旧", "拍摄时间从旧到新"]);
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
            UnifiedSearchText = "IMG_0001.JPG",
            PreviewSearchText = "JK",
            PreviewRetouchFilter = "已修",
            RatingFilter = "5★",
            PreviewSortMode = 8,
            SmartCategoryFilter = "人像",
            IsBrowseConditionsExpanded = true
        };
        viewModel.SemanticSearch.QueryText = "红色衣服";

        await viewModel.ResetBrowseConditionsCommand.ExecuteAsync(null);

        viewModel.CurrentPreviewCategory.Should().Be("全部");
        viewModel.UnifiedSearchText.Should().BeEmpty();
        viewModel.PreviewSearchText.Should().BeEmpty();
        viewModel.SemanticSearch.QueryText.Should().BeEmpty();
        viewModel.PreviewRetouchFilter.Should().Be("全部");
        viewModel.RatingFilter.Should().Be("全部评分");
        viewModel.PreviewSortMode.Should().Be(9);
        viewModel.SmartCategoryFilter.Should().Be("全部智能类别");
        viewModel.IsBrowseConditionsExpanded.Should().BeFalse();
    }

    [Fact]
    public void TimeSort_OrdersByCapturedAtNewestFirstAndOldestFirst()
    {
        var viewModel = new MainWindowViewModel();
        var older = new PreviewFileViewModel(
            "older.jpg", "JPG生图", @"D:\photos\older.jpg", "1 KB", ".jpg", null,
            0, new DateTime(2020, 5, 1, 12, 0, 0));
        var middle = new PreviewFileViewModel(
            "middle.jpg", "JPG生图", @"D:\photos\middle.jpg", "1 KB", ".jpg", null,
            0, new DateTime(2022, 5, 1, 12, 0, 0));
        var newer = new PreviewFileViewModel(
            "newer.jpg", "JPG生图", @"D:\photos\newer.jpg", "1 KB", ".jpg", null,
            0, new DateTime(2024, 5, 1, 12, 0, 0));
        viewModel.PreviewFiles.Add(older);
        viewModel.PreviewFiles.Add(middle);
        viewModel.PreviewFiles.Add(newer);

        // 拍摄时间从新到旧 (9): newest first
        viewModel.PreviewSortMode = 9;
        viewModel.FilteredPreviewFiles.Select(file => file.Name)
            .Should().Equal("newer.jpg", "middle.jpg", "older.jpg");

        // 拍摄时间从旧到新 (10): oldest first
        viewModel.PreviewSortMode = 10;
        viewModel.FilteredPreviewFiles.Select(file => file.Name)
            .Should().Equal("older.jpg", "middle.jpg", "newer.jpg");
    }

    [Fact]
    public async Task DefaultPreviewSort_IsCaptureTimeNewestFirst()
    {
        // The persisted default (settings "默认排序") must be 拍摄时间从新到旧 (9),
        // so a fresh install opens the browse page in time order.
        var store = new HanabePhotoManager.App.Services.AppSettingsStore(
            Path.Combine(Path.GetTempPath(), $"hpm-settings-{Guid.NewGuid():N}.json"));
        (await store.LoadAsync()).DefaultPreviewSort.Should().Be(9);
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
        xaml.Should().Contain("x:Name=\"BrowseSmartSearchBox\"");
        xaml.Should().Contain("Text=\"{Binding UnifiedSearchText, UpdateSourceTrigger=PropertyChanged}\"");
        xaml.Should().Contain("BrowseSearchModeChoices");
        xaml.Should().Contain("Style=\"{StaticResource Input.TextBox}\"");
        xaml.Should().Contain("Command=\"{Binding SemanticSearch.CancelCommand}\"");
        xaml.Should().NotContain("<search:SemanticSearchView");
    }

    [Fact]
    public void BrowseConditions_UseOneSmartSearchBoxAndHideManualAssignmentControls()
    {
        var viewModel = new MainWindowViewModel();
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        viewModel.BrowseSearchModeChoices.Select(choice => choice.Label)
            .Should().Contain(["智能", "文件名或路径", "语义描述"]);
        xaml.Should().Contain("x:Name=\"BrowseSmartSearchBox\"");
        xaml.Should().NotContain("应用到所选");
        xaml.Should().NotContain("添加到所选");
        xaml.Should().NotContain("手动类别");
        xaml.Should().NotContain("自定义标签");
    }

    [Fact]
    public void PreviewCard_PhotoBleedsToOuterEdgesAndUsesOnlyTopCornerMask()
    {
        var root = FindSourceRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "HanabePhotoManager.App", "MainWindow.xaml"));

        xaml.Should().Contain("Tag=\"PreviewCard\"");
        xaml.Should().Contain("Name=\"ThumbnailClip\"");
        xaml.Should().Contain("CornerRadius=\"12\" ClipToBounds=\"True\"");
        xaml.Should().Contain("ImageBrush Stretch=\"UniformToFill\" ImageSource=\"{Binding Thumbnail}\"");
        xaml.Should().NotContain("CornerRadius=\"21,21,0,0\" ClipToBounds=\"True\"");
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
