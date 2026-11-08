using CommunityToolkit.Mvvm.ComponentModel;
using HanabePhotoManager.App.Imports;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>A projection of the shared preflight decision, never a second import plan.</summary>
public sealed class ImportDateGraphGroupViewModel : ObservableObject
{
    /// <summary>检查页日期卡片里分类比例条的宽度（像素）。</summary>
    public const double BreakdownBarWidth = 336;
    private const int StripSize = 5;
    private bool _isSelected;

    public ImportDateGraphGroupViewModel(ImportDateFolderDecision decision, IReadOnlyList<ImportPreviewItemViewModel> items)
    {
        Decision = decision;
        Items = items;
        Samples = items.Take(2).ToArray();
        // 缩略图条：尽量每个分类都露一张，再按顺序补满。
        Strip = items.GroupBy(item => item.SelectedCategory.Display).Select(group => group.First())
            .Concat(items).Distinct().Take(StripSize).ToArray();
        var total = Math.Max(1, items.Count);
        Breakdown = items.GroupBy(item => item.SelectedCategory.Display)
            .OrderByDescending(group => group.Count())
            .Select(group => new ImportCategoryShare(group.Key, group.Count(),
                Math.Max(6, Math.Round(BreakdownBarWidth * group.Count() / total)), ImportCategoryPalette.For(group.Key)))
            .ToArray();
        TotalBytes = items.Sum(item => item.TotalBytes);
    }

    public ImportDateFolderDecision Decision { get; }
    public IReadOnlyList<ImportPreviewItemViewModel> Items { get; }
    public IReadOnlyList<ImportPreviewItemViewModel> Samples { get; }
    public bool HasOverflow => Items.Count > Samples.Count;
    public string OverflowText => $"+{Items.Count - Samples.Count}";
    public string FullDateText => $"{Decision.Date.Year}.{Decision.Date.Month:00}.{Decision.Date.Day:00}";
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    /// <summary>日期卡片上的缩略图条（最多 6 张）。</summary>
    public IReadOnlyList<ImportPreviewItemViewModel> Strip { get; }
    public bool HasStripOverflow => Items.Count > Strip.Count;
    public string StripOverflowText => $"+{Items.Count - Strip.Count}";

    /// <summary>各分类的数量与比例（RAW / JPG / 视频 …），用于比例条和图例。</summary>
    public IReadOnlyList<ImportCategoryShare> Breakdown { get; }

    public long TotalBytes { get; }
    public string WeekdayText => new DateTime(Decision.Date.Year, Decision.Date.Month, Decision.Date.Day).ToString("dddd", new System.Globalization.CultureInfo("zh-CN"));
    public string SummaryText => $"{Items.Count:N0} 组 · {FormatBytes(TotalBytes)}";

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 40 => $"{bytes / (double)(1L << 40):0.##} TB",
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.##} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.#} MB",
        _ => $"{Math.Max(0, bytes) / 1024d:0} KB",
    };
}

public sealed record ImportDateGraphMonthViewModel(string Title, IReadOnlyList<ImportDateGraphGroupViewModel> Groups);

/// <summary>一个分类在某一天里的份额。</summary>
public sealed record ImportCategoryShare(string Name, int Count, double BarWidth, string Brush)
{
    public string Label => $"{Name} {Count:N0}";
}


