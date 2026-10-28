namespace HanabePhotoManager.App.ViewModels;

/// <summary>主页“拍摄频率”条形图中的一天：按日期文件夹统计的照片组数量。</summary>
public sealed record ShootingDayBar(int Year, int Month, int Day, int Count, double BarHeight, bool IsLatest, bool ShowMonth = false)
{
    public string DayLabel => ShowMonth ? $"{Month}/{Day}" : Day.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string ToolTipText => $"{Year}.{Month:00}.{Day:00} · {Count:N0} 张";
}

public sealed partial class MainWindowViewModel
{
    private const int ShootingFrequencyDayLimit = 21;
    private const double ShootingFrequencyMaxBarHeight = 96;
    private const double ShootingFrequencyMinBarHeight = 6;
    public const double ShootingBarGapMin = 0;
    public const double ShootingBarGapMax = 16;
    private double _shootingBarGap = 2;

    /// <summary>拍摄频率条形之间的间距（像素），主页卡片右上角可拖动调整并保存到设置。</summary>
    public double ShootingBarGap
    {
        get => _shootingBarGap;
        set
        {
            var clamped = Math.Round(Math.Clamp(value, ShootingBarGapMin, ShootingBarGapMax));
            if (!SetProperty(ref _shootingBarGap, clamped)) return;
            OnPropertyChanged(nameof(ShootingBarMargin));
            if (_isInitialized) _ = SaveSettingsAsync();
        }
    }

    public System.Windows.Thickness ShootingBarMargin => new(_shootingBarGap / 2, 0, _shootingBarGap / 2, 0);

    /// <summary>最近若干个有内容的拍摄日（按日期文件夹），用于主页条形图；数量为 RAW/JPG 照片组数。</summary>
    public IReadOnlyList<ShootingDayBar> ShootingFrequencyBars
    {
        get
        {
            var days = FlattenDateNodes(LibraryDates)
                .Where(node => node.Date is not null)
                .Select(node => (Date: node.Date!.Value, node.TotalFiles))
                .OrderBy(day => day.Date.Year)
                .ThenBy(day => day.Date.Month)
                .ThenBy(day => day.Date.Day)
                .TakeLast(ShootingFrequencyDayLimit)
                .ToArray();
            if (days.Length == 0) return [];

            var max = Math.Max(1, days.Max(day => day.TotalFiles));
            return days
                .Select((day, index) => new ShootingDayBar(
                    day.Date.Year,
                    day.Date.Month,
                    day.Date.Day,
                    day.TotalFiles,
                    Math.Max(ShootingFrequencyMinBarHeight, ShootingFrequencyMaxBarHeight * day.TotalFiles / max),
                    index == days.Length - 1,
                    index == 0 || days[index - 1].Date.Month != day.Date.Month))
                .ToArray();
        }
    }

    public bool HasShootingFrequency => LibraryDates.Count > 0;

    public string ShootingFrequencySummary
    {
        get
        {
            var bars = ShootingFrequencyBars;
            if (bars.Count == 0) return "选择照片库后显示每天的拍摄数量";
            var total = bars.Sum(bar => bar.Count);
            var peak = bars.MaxBy(bar => bar.Count)!;
            return $"{bars.Count} 个拍摄日 · 共 {total:N0} 张 · 峰值 {peak.Month}.{peak.Day:00}";
        }
    }

    private void NotifyShootingFrequencyChanged()
    {
        OnPropertyChanged(nameof(ShootingFrequencyBars));
        OnPropertyChanged(nameof(HasShootingFrequency));
        OnPropertyChanged(nameof(ShootingFrequencySummary));
        OnPropertyChanged(nameof(HanabeDailyMessage));
    }
}
