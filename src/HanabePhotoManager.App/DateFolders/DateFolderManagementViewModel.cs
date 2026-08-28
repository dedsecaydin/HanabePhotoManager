using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.DateFolders;

/// <summary>为照片库日期目录提供刷新和逐行连续保存的批量编辑协调。</summary>
public sealed class DateFolderManagementViewModel : ObservableObject
{
    private readonly IDateFolderService _dateFolderService;
    private string _libraryRoot = string.Empty;
    private string _summary = "请先选择照片库根目录。";
    private bool _isBusy;

    public DateFolderManagementViewModel(IDateFolderService? dateFolderService = null)
    {
        _dateFolderService = dateFolderService ?? new LibraryDateFolderServiceAdapter();
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanOperate);
        SaveAllCommand = new AsyncRelayCommand(SaveAllAsync, CanOperate);
    }

    public ObservableCollection<DateFolderItemViewModel> Items { get; } = [];
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand SaveAllCommand { get; }

    public string LibraryRoot
    {
        get => _libraryRoot;
        set
        {
            if (SetProperty(ref _libraryRoot, value ?? string.Empty))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                SaveAllCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                SaveAllCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(LibraryRoot))
        {
            Items.Clear();
            Summary = "请先选择照片库根目录。";
            return;
        }

        IsBusy = true;
        try
        {
            var entries = await Task.Run(() => _dateFolderService.Scan(LibraryRoot));
            Items.Clear();
            foreach (var entry in entries)
            {
                Items.Add(new DateFolderItemViewModel(entry));
            }

            Summary = $"已加载 {Items.Count} 个日期文件夹。";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Summary = $"刷新失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveAllAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(LibraryRoot))
        {
            Summary = "请先选择照片库根目录。";
            return;
        }

        IsBusy = true;
        var succeeded = 0;
        var skipped = 0;
        var failed = 0;

        try
        {
            foreach (var item in Items)
            {
                if (!item.IsDirty)
                {
                    item.MarkUnchanged();
                    skipped++;
                    continue;
                }

                try
                {
                    var result = await Task.Run(() =>
                        _dateFolderService.RenameRemark(item.FullPath, item.EditedRemark));
                    switch (result.Status)
                    {
                        case DateFolderRenameStatus.Success:
                            item.MarkSaved(result);
                            succeeded++;
                            break;
                        case DateFolderRenameStatus.NoChange:
                            item.MarkSkipped(result);
                            skipped++;
                            break;
                        default:
                            item.MarkFailed(result);
                            failed++;
                            break;
                    }
                }
                catch (Exception exception)
                {
                    item.MarkFailed(exception);
                    failed++;
                }
            }

            Summary = $"保存完成：成功 {succeeded}，跳过 {skipped}，失败 {failed}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanOperate() => !IsBusy;
}

/// <summary>将日期目录文件系统服务隔离在 ViewModel 外，便于在不访问真实图库时测试批量逻辑。</summary>
public interface IDateFolderService
{
    IReadOnlyList<DateFolderEntry> Scan(string libraryRoot);
    DateFolderRenameResult RenameRemark(string sourcePath, string remark);
}

internal sealed class LibraryDateFolderServiceAdapter : IDateFolderService
{
    public IReadOnlyList<DateFolderEntry> Scan(string libraryRoot) => LibraryDateFolderService.Scan(libraryRoot);

    public DateFolderRenameResult RenameRemark(string sourcePath, string remark) =>
        LibraryDateFolderService.RenameRemark(sourcePath, remark);
}
