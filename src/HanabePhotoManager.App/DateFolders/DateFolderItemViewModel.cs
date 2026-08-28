using CommunityToolkit.Mvvm.ComponentModel;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.DateFolders;

/// <summary>日期目录的一行可编辑状态，并保留最后一次已保存的备注基线。</summary>
public sealed class DateFolderItemViewModel : ObservableObject
{
    private string _originalRemark;
    private string _editedRemark;
    private string _fullPath;
    private string _statusText = string.Empty;

    public DateFolderItemViewModel(DateFolderEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Month = entry.Month;
        Day = entry.Day;
        _originalRemark = entry.Remark;
        _editedRemark = entry.Remark;
        _fullPath = entry.FullPath;
    }

    public int Month { get; }
    public int Day { get; }
    public string OriginalRemark => _originalRemark;
    public string FullPath
    {
        get => _fullPath;
        private set => SetProperty(ref _fullPath, value);
    }

    public string EditedRemark
    {
        get => _editedRemark;
        set
        {
            if (SetProperty(ref _editedRemark, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    public bool IsDirty => !string.Equals(EditedRemark, OriginalRemark, StringComparison.Ordinal);

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    internal void MarkSaved(DateFolderRenameResult result)
    {
        FullPath = result.EffectivePath;
        _originalRemark = EditedRemark;
        OnPropertyChanged(nameof(OriginalRemark));
        OnPropertyChanged(nameof(IsDirty));
        StatusText = "已保存。";
    }

    internal void MarkSkipped(DateFolderRenameResult result)
    {
        FullPath = result.EffectivePath;
        _originalRemark = EditedRemark;
        OnPropertyChanged(nameof(OriginalRemark));
        OnPropertyChanged(nameof(IsDirty));
        StatusText = "无需保存。";
    }

    internal void MarkUnchanged()
    {
        StatusText = "无需保存。";
    }

    internal void MarkFailed(DateFolderRenameResult result)
    {
        StatusText = result.Status switch
        {
            DateFolderRenameStatus.TargetExists => "保存失败：目标文件夹已存在。",
            DateFolderRenameStatus.SourceMissing => "保存失败：源文件夹不存在。",
            _ when !string.IsNullOrWhiteSpace(result.ErrorMessage) => $"保存失败：{result.ErrorMessage}",
            _ => "保存失败。"
        };
    }

    internal void MarkFailed(Exception exception) =>
        StatusText = $"保存失败：{exception.Message}";
}
