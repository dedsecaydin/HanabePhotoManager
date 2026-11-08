using FluentAssertions;
using HanabePhotoManager.App.ViewModels;
using HanabePhotoManager.Core.Imports;
using Xunit;

namespace HanabePhotoManager.App.Tests;

/// <summary>图库：常驻工具栏的状态、类型分段、信息面板开关与左侧日期列表。</summary>
public sealed class GalleryChromeTests
{
    [Fact]
    public void Toolbar_ShowsClearOnlyWhenAFilterIsActive()
    {
        var vm = new MainWindowViewModel();
        vm.HasActiveBrowseFilters.Should().BeFalse();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SetPreviewCategoryCommand.Execute("RAW生图");
        vm.HasActiveBrowseFilters.Should().BeTrue();
        raised.Should().Contain(nameof(MainWindowViewModel.HasActiveBrowseFilters));

        vm.SetPreviewCategoryCommand.Execute("全部");
        vm.HasActiveBrowseFilters.Should().BeFalse();
    }

    [Fact]
    public void CategoryChips_MapEveryFilterValueOnce()
    {
        var vm = new MainWindowViewModel();
        vm.PreviewCategoryChips.Select(chip => chip.Value).Should().Equal(vm.PreviewCategoryFilters);
        vm.PreviewCategoryChips.Single(chip => chip.Value == "action视频").Label.Should().Be("运动相机");
    }

    [Fact]
    public void Inspector_TogglesAndStartsOpen()
    {
        var vm = new MainWindowViewModel();
        vm.IsBrowseInspectorOpen.Should().BeTrue();
        vm.ToggleBrowseInspectorCommand.Execute(null);
        vm.IsBrowseInspectorOpen.Should().BeFalse();
    }

    [Fact]
    public void DateList_ShowsFolderNamesAndMarksAllDatesByDefault()
    {
        var vm = new MainWindowViewModel();
        vm.IsAllDatesSelected.Should().BeTrue();
        vm.GalleryCurrentDatePath.Should().BeEmpty();

        var day = new LibraryDateNode("2026/10月/10.21_婚礼", @"D:\lib\10月\10.21_婚礼", new LibraryDate(2026, 10, 21));
        day.ShortTitle.Should().Be("10.21_婚礼");
        new LibraryDateNode("10月", @"D:\lib\10月", null).ShortTitle.Should().Be("10月");
        vm.SelectLibraryDateCommand.Execute(new LibraryDateNode("10月", @"D:\lib\10月", null));
        vm.IsAllDatesSelected.Should().BeTrue("月份标题不能当作日期选中");
    }
}
