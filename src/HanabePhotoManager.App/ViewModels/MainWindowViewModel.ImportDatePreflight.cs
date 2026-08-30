using HanabePhotoManager.App.Imports;
using HanabePhotoManager.Core.Imports;
using System.Windows;

namespace HanabePhotoManager.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private readonly ImportDateFolderPreflightService _importDateFolderPreflightService = new();
    private bool _isImportDateFolderPreflightVisible;

    public bool IsImportDateFolderPreflightVisible
    {
        get => _isImportDateFolderPreflightVisible;
        private set
        {
            if (SetProperty(ref _isImportDateFolderPreflightVisible, value))
            {
                ConfirmImportDateFoldersCommand.NotifyCanExecuteChanged();
                BackFromImportDateFoldersCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsImportQueueVisible => !IsImportDateFolderPreflightVisible;

    private void PrepareImportDateFolderPreflight(IReadOnlyList<ImportPreviewItemViewModel> selectedItems)
    {
        ImportDateFolderDecisions.Clear();
        var groups = selectedItems
            .Where(item => item.TargetDate is not null)
            .GroupBy(item => item.TargetDate!.Value)
            .Select(group => (group.Key, group.Count()));
        foreach (var decision in _importDateFolderPreflightService.CreateDecisions(LibraryRoot, groups))
        {
            decision.PropertyChanged += (_, _) => ConfirmImportDateFoldersCommand.NotifyCanExecuteChanged();
            ImportDateFolderDecisions.Add(decision);
        }

        IsImportDateFolderPreflightVisible = ImportDateFolderDecisions.Count > 0;
        OnPropertyChanged(nameof(IsImportQueueVisible));
        ImportActionHint = "请批量确认每个日期的最终文件夹；确认前不会复制或移动任何媒体。";
        StatusMessage = "等待确认日期文件夹。";
        ConfirmImportDateFoldersCommand.NotifyCanExecuteChanged();
    }

    private bool CanConfirmImportDateFolders() =>
        IsImportDateFolderPreflightVisible && !IsBusy &&
        ImportDateFolderDecisions.Count > 0 && ImportDateFolderDecisions.All(decision => decision.IsValid);

    private async Task ConfirmImportDateFoldersAsync()
    {
        if (!CanConfirmImportDateFolders()) return;
        if (!_importDateFolderPreflightService.ApplyRequiredRenames(ImportDateFolderDecisions))
        {
            ConfirmImportDateFoldersCommand.NotifyCanExecuteChanged();
            StatusMessage = "日期文件夹确认失败，请检查行内提示。";
            return;
        }

        var selectedItems = ImportItems
            .Where(item => item.IsSelected)
            .Where(item => item.SelectedCategory.Category != MediaCategory.Unconfirmed)
            .Where(item => ConcreteCategoryFolders.ContainsKey(item.SelectedCategory.Category))
            .ToArray();
        var targets = ImportDateFolderDecisions.ToDictionary(decision => decision.Date, decision => decision.FinalDirectoryPath);
        var deleteSources = SelectedTransferMode == TransferMode.MoveAfterVerify;
        if (deleteSources)
        {
            var confirmation = new ImportMoveSafetyConfirmationWindow
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            if (confirmation.ShowDialog() != true) return;
        }

        IsImportDateFolderPreflightVisible = false;
        OnPropertyChanged(nameof(IsImportQueueVisible));
        await RunImportAsync(selectedItems, deleteSources, targets).ConfigureAwait(true);
    }

    private void BackFromImportDateFolders()
    {
        ClearImportDateFolderPreflight();
        ImportActionHint = "可调整来源、勾选或分类，然后重新进入日期文件夹确认。";
        StatusMessage = "已返回导入队列，尚未复制媒体。";
    }

    private void ClearImportDateFolderPreflight()
    {
        ImportDateFolderDecisions.Clear();
        IsImportDateFolderPreflightVisible = false;
        OnPropertyChanged(nameof(IsImportQueueVisible));
    }
}
