using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using HanabePhotoManager.App.ViewModels;

namespace HanabePhotoManager.App;

/// <summary>导入页（三步）的界面胶水：设置弹窗、分类与场次面板，以及让“日期与目标”随队列自动更新。</summary>
public partial class MainWindow
{
    private static readonly HashSet<string> InlinePlanItemProperties = new(StringComparer.Ordinal)
    {
        nameof(ImportPreviewItemViewModel.IsSelected),
        nameof(ImportPreviewItemViewModel.SelectedCategory),
        nameof(ImportPreviewItemViewModel.TargetDate),
        nameof(ImportPreviewItemViewModel.ImportSessionName),
    };

    private static readonly HashSet<string> InlinePlanViewModelProperties = new(StringComparer.Ordinal)
    {
        nameof(MainWindowViewModel.IsBusy),
        nameof(MainWindowViewModel.LibraryRoot),
        nameof(MainWindowViewModel.SourceFolder),
        nameof(MainWindowViewModel.SelectedTransferMode),
    };

    private bool _importPageHooked;

    /// <summary>在窗口构造完成后调用一次：监听队列变化，合并刷新日期方案。</summary>
    private void HookImportPage()
    {
        if (_importPageHooked) return;
        _importPageHooked = true;
        foreach (var item in _viewModel.ImportItems) item.PropertyChanged += ImportItem_PropertyChanged;
        _viewModel.ImportItems.CollectionChanged += ImportItems_CollectionChanged;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is { } name && InlinePlanViewModelProperties.Contains(name))
                _viewModel.ScheduleInlineDatePlanRefresh();
        };
    }

    private void ImportItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (ImportPreviewItemViewModel item in e.OldItems) item.PropertyChanged -= ImportItem_PropertyChanged;
        if (e.NewItems is not null)
            foreach (ImportPreviewItemViewModel item in e.NewItems) item.PropertyChanged += ImportItem_PropertyChanged;
        _viewModel.ScheduleInlineDatePlanRefresh();
    }

    private void ImportItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is { } name && InlinePlanItemProperties.Contains(name))
            _viewModel.ScheduleInlineDatePlanRefresh();
    }

    private void ImportSettingsButton_Click(object sender, RoutedEventArgs e) =>
        _viewModel.IsImportSettingsOpen = !_viewModel.IsImportSettingsOpen;

    private void ImportOrganizeToggle_Click(object sender, RoutedEventArgs e) =>
        _viewModel.IsImportOrganizePanelOpen = !_viewModel.IsImportOrganizePanelOpen;

    private void CloseImportOrganizePanel_Click(object sender, RoutedEventArgs e) =>
        _viewModel.IsImportOrganizePanelOpen = false;

    /// <summary>“查看全部素材”：展开 / 收起完整的逐张网格。</summary>
    private void ToggleImportQueue_Click(object sender, RoutedEventArgs e) =>
        _viewModel.IsImportQueueExpanded = !_viewModel.IsImportQueueExpanded;

    /// <summary>“去处理”：展开全部素材，需要确认的卡片带“需要确认”标记。</summary>
    private void ShowUnresolvedImportItems_Click(object sender, RoutedEventArgs e) =>
        _viewModel.IsImportQueueExpanded = true;
}
