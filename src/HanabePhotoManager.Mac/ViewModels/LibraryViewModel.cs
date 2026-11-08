using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.Mac.Services;

namespace HanabePhotoManager.Mac.ViewModels;

/// <summary>照片库里的一个日期文件夹（11月/11.04_婚礼）。</summary>
public sealed record DateFolderItem(string Title, string Remark, string FullPath, int Month, int Day)
{
    public string Subtitle => string.IsNullOrWhiteSpace(Remark) ? FullPath : Remark;
}

/// <summary>照片图库：左边日期文件夹，右边该日期的照片（按分类子文件夹）。</summary>
public sealed partial class LibraryViewModel : ObservableObject
{
    private const int PageSize = 300;
    private readonly MainViewModel _main;

    [ObservableProperty] private DateFolderItem? _selectedFolder;
    [ObservableProperty] private string _status = string.Empty;

    public LibraryViewModel(MainViewModel main)
    {
        _main = main;
        Refresh();
    }

    public ObservableCollection<DateFolderItem> Folders { get; } = [];
    public ObservableCollection<ThumbViewModel> Photos { get; } = [];
    public int TotalMedia { get; private set; }

    public void Refresh()
    {
        Folders.Clear();
        var root = _main.Settings.LibraryRoot;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            Status = "还没有选择照片库。去「设置」里选一个文件夹。";
            return;
        }

        try
        {
            foreach (var entry in LibraryDateFolderService.Scan(root)
                         .OrderByDescending(entry => entry.Month).ThenByDescending(entry => entry.Day))
            {
                Folders.Add(new DateFolderItem($"{entry.Month:00}.{entry.Day:00}", entry.Remark, entry.FullPath, entry.Month, entry.Day));
            }
            Status = Folders.Count == 0 ? "照片库里还没有日期文件夹，先去导入吧。" : $"{Folders.Count} 个拍摄日";
        }
        catch (IOException ex) { Status = "读不到照片库：" + ex.Message; }
        catch (UnauthorizedAccessException) { Status = "没有权限读取照片库。macOS 上请在「系统设置 → 隐私与安全性 → 文件与文件夹」里允许。"; }

        SelectedFolder ??= Folders.FirstOrDefault();
        OnPropertyChanged(nameof(TotalMedia));
    }

    partial void OnSelectedFolderChanged(DateFolderItem? value)
    {
        Photos.Clear();
        if (value is null) return;
        var files = Directory.EnumerateFiles(value.FullPath, "*", SearchOption.AllDirectories)
            .Where(path => MacImportEngine.ImportExtensions.Contains(Path.GetExtension(path)) && !Path.GetFileName(path).StartsWith("._", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var path in files.Take(PageSize)) Photos.Add(new ThumbViewModel(path, 200));
        Status = files.Length > PageSize ? $"{value.Title} · 共 {files.Length:N0} 个文件，先显示前 {PageSize} 个" : $"{value.Title} · {files.Length:N0} 个文件";
    }
}
