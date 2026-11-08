using CommunityToolkit.Mvvm.ComponentModel;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>导入媒体类别的可展示选项。</summary>
public sealed record CategoryChoice(MediaCategory Category, string Display, string? FolderName = null)
{
    public override string ToString() => Display;
}

/// <summary>导入预览队列中的源媒体组、分类、日期、缩略图和启用状态。</summary>
public sealed class ImportPreviewItemViewModel : ObservableObject
{
    private bool _isOrganizeSelected;
    private string _importSessionName = "";
    public bool IsOrganizeSelected { get => _isOrganizeSelected; set => SetProperty(ref _isOrganizeSelected, value); }
    public string ImportSessionName { get => _importSessionName; set => SetProperty(ref _importSessionName, value); }
    public string SourceDirectory => Path.GetDirectoryName(PrimaryPath) ?? PrimaryPath;
    private bool _isSelected = true;
    private CategoryChoice _selectedCategory;
    private object? _thumbnail;

    public ImportPreviewItemViewModel(MediaGroup group, MediaCategory category, IReadOnlyList<CategoryChoice> categoryChoices, bool requiresConfirmation, LibraryDate? targetDate, object? thumbnail, string personLabel, int queueIndex, CategoryChoice? routedChoice = null)
    {
        Group = group;
        CategoryChoices = categoryChoices;
        RequiresConfirmation = requiresConfirmation;
        TargetDate = targetDate;
        _thumbnail = thumbnail;
        PersonLabel = personLabel;
        QueueIndex = queueIndex;
        _selectedCategory = routedChoice ?? categoryChoices.FirstOrDefault(choice => choice.Category == category)
            ?? categoryChoices.First(choice => choice.Category == MediaCategory.Unconfirmed);
    }

    public event Action? SelectionChanged;


    public MediaGroup Group { get; }

    public IReadOnlyList<CategoryChoice> CategoryChoices { get; }

    public bool RequiresConfirmation { get; }

    public bool NeedsAttention => SelectedCategory.Category == MediaCategory.Unconfirmed || TargetDate is null;

    public int QueueIndex { get; }

    public string QueueNumber => $"No.{QueueIndex:0000}";

    public LibraryDate? TargetDate { get; }

    public object? Thumbnail => _thumbnail;

    public string PersonLabel { get; }

    public bool HasPersonLabel => !string.IsNullOrWhiteSpace(PersonLabel);

    public bool HasThumbnail => Thumbnail is not null;

    public bool HasNoThumbnail => Thumbnail is null;

    public void SetThumbnail(object? thumbnail)
    {
        if (SetProperty(ref _thumbnail, thumbnail, nameof(Thumbnail)))
        {
            OnPropertyChanged(nameof(HasThumbnail));
            OnPropertyChanged(nameof(HasNoThumbnail));
        }
    }

    public string TargetDateText => TargetDate is { } date ? $"{date.Month}月\\{date.Month:00}.{date.Day:00}" : "需要确认日期";

    public string Name => Group.GroupKey;

    public string SourceFileName => Path.GetFileName(Group.Primary.FullPath);

    public string ExtensionBadge
    {
        get
        {
            var extension = Path.GetExtension(Group.Primary.FullPath).TrimStart('.').ToUpperInvariant();
            return string.IsNullOrWhiteSpace(extension) ? "FILE" : extension;
        }
    }

    public string PrimaryPath => Group.Primary.FullPath;

    public long TotalBytes => Group.Primary.Length + Group.Sidecars.Sum(file => file.Length);

    public string Detail => string.IsNullOrWhiteSpace(PersonLabel)
        ? $"{Path.GetFileName(Group.Primary.FullPath)} · 配套 {Group.Sidecars.Count} 个 · {FormatBytes(TotalBytes)}"
        : $"{PersonLabel} · {Path.GetFileName(Group.Primary.FullPath)} · 配套 {Group.Sidecars.Count} 个 · {FormatBytes(TotalBytes)}";

    public string SizeText => FormatBytes(TotalBytes);

    public string CategoryBadge => SelectedCategory.Display;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                SelectionChanged?.Invoke();
            }
        }
    }

    public CategoryChoice SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                OnPropertyChanged(nameof(CategoryBadge));
                OnPropertyChanged(nameof(NeedsAttention));
                SelectionChanged?.Invoke();
            }
        }
    }

    public MediaGroup ToMediaGroup()
    {
        return new MediaGroup(Group.GroupKey, SelectedCategory.Category, Group.Primary, Group.Sidecars, SelectedCategory.FolderName);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.##} {units[unit]}";
    }
}
