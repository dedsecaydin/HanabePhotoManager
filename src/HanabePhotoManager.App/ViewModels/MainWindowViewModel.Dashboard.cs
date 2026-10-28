using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>主页“日程安排”小板块的一条拍摄计划，保存在应用数据目录的 schedule.json。</summary>
public sealed partial class ScheduleItemViewModel : ObservableObject
{
    [ObservableProperty] private bool _isDone;

    public ScheduleItemViewModel(Guid id, DateOnly date, string title, bool isDone)
    {
        Id = id;
        Date = date;
        Title = title;
        _isDone = isDone;
    }

    public Guid Id { get; }
    public DateOnly Date { get; }
    public string Title { get; }

    public string DateText
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (Date == today) return "今天";
            if (Date == today.AddDays(1)) return "明天";
            if (Date == today.AddDays(2)) return "后天";
            var week = "日一二三四五六"[(int)Date.DayOfWeek];
            return $"{Date.Month}.{Date.Day} 周{week}";
        }
    }

    public bool IsOverdue => !IsDone && Date < DateOnly.FromDateTime(DateTime.Today);

    partial void OnIsDoneChanged(bool value) => OnPropertyChanged(nameof(IsOverdue));
}

public sealed partial class MainWindowViewModel
{
    private sealed record ScheduleRecord(Guid Id, DateOnly Date, string Title, bool IsDone);

    private static readonly Regex ScheduleDatePrefix = new(@"^\s*(?<m>\d{1,2})\s*[./月\-]\s*(?<d>\d{1,2})\s*日?\s*(?<t>.+)$", RegexOptions.Compiled);
    private ObservableCollection<ScheduleItemViewModel>? _scheduleItems;
    private string _newScheduleText = string.Empty;

    private static string SchedulePath => Path.Combine(AppDataPaths.Root, "schedule.json");

    public ObservableCollection<ScheduleItemViewModel> ScheduleItems => _scheduleItems ??= LoadSchedule();

    /// <summary>未完成的按日期排前，已完成的排后；主页只显示前 4 条。</summary>
    public IReadOnlyList<ScheduleItemViewModel> UpcomingSchedule => ScheduleItems
        .OrderBy(item => item.IsDone)
        .ThenBy(item => item.Date)
        .Take(4)
        .ToArray();

    public string ScheduleSummary
    {
        get
        {
            var open = ScheduleItems.Count(item => !item.IsDone);
            return open == 0 ? "暂无待办" : $"{open} 项待办";
        }
    }

    public bool HasNoSchedule => ScheduleItems.Count == 0;

    public string NewScheduleText
    {
        get => _newScheduleText;
        set => SetProperty(ref _newScheduleText, value ?? string.Empty);
    }

    /// <summary>输入如“11.2 漫展外拍”“明天 棚拍”，开头的日期可省略（默认今天）。</summary>
    [RelayCommand]
    private void AddSchedule()
    {
        var text = NewScheduleText.Trim();
        if (text.Length == 0) return;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var date = today;
        var title = text;
        if (text.StartsWith("今天", StringComparison.Ordinal)) { title = text[2..]; }
        else if (text.StartsWith("明天", StringComparison.Ordinal)) { date = today.AddDays(1); title = text[2..]; }
        else if (text.StartsWith("后天", StringComparison.Ordinal)) { date = today.AddDays(2); title = text[2..]; }
        else if (ScheduleDatePrefix.Match(text) is { Success: true } match &&
                 int.TryParse(match.Groups["m"].Value, out var month) && int.TryParse(match.Groups["d"].Value, out var day) &&
                 month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(today.Year, month))
        {
            date = new DateOnly(today.Year, month, day);
            if (date < today.AddDays(-30)) date = date.AddYears(1);
            title = match.Groups["t"].Value;
        }

        title = title.Trim();
        if (title.Length == 0) return;
        var item = new ScheduleItemViewModel(Guid.NewGuid(), date, title, false);
        item.PropertyChanged += (_, _) => { SaveSchedule(); NotifyScheduleChanged(); };
        ScheduleItems.Add(item);
        NewScheduleText = string.Empty;
        SaveSchedule();
        NotifyScheduleChanged();
    }

    [RelayCommand]
    private void ToggleSchedule(ScheduleItemViewModel? item)
    {
        if (item is not null) item.IsDone = !item.IsDone;
    }

    [RelayCommand]
    private void RemoveSchedule(ScheduleItemViewModel? item)
    {
        if (item is null || !ScheduleItems.Remove(item)) return;
        SaveSchedule();
        NotifyScheduleChanged();
    }

    private ObservableCollection<ScheduleItemViewModel> LoadSchedule()
    {
        var items = new ObservableCollection<ScheduleItemViewModel>();
        try
        {
            if (File.Exists(SchedulePath))
            {
                var records = JsonSerializer.Deserialize<List<ScheduleRecord>>(File.ReadAllText(SchedulePath)) ?? [];
                foreach (var record in records.Where(record => !string.IsNullOrWhiteSpace(record.Title)))
                {
                    var item = new ScheduleItemViewModel(record.Id, record.Date, record.Title, record.IsDone);
                    item.PropertyChanged += (_, _) => { SaveSchedule(); NotifyScheduleChanged(); };
                    items.Add(item);
                }
            }
        }
        catch (Exception)
        {
            // 日程文件损坏时从空列表开始，不覆盖原文件，直到用户添加新日程。
        }

        return items;
    }

    private void SaveSchedule()
    {
        try
        {
            Directory.CreateDirectory(AppDataPaths.Root);
            var records = ScheduleItems.Select(item => new ScheduleRecord(item.Id, item.Date, item.Title, item.IsDone)).ToList();
            var temp = SchedulePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, SchedulePath, overwrite: true);
        }
        catch (Exception)
        {
            // 写入失败不影响界面；下次修改会再次尝试保存。
        }
    }

    private void NotifyScheduleChanged()
    {
        OnPropertyChanged(nameof(UpcomingSchedule));
        OnPropertyChanged(nameof(ScheduleSummary));
        OnPropertyChanged(nameof(HasNoSchedule));
        OnPropertyChanged(nameof(HanabeDailyMessage));
    }

    /// <summary>角色小板块的问候语（按当前时间）。</summary>
    public string HanabeGreeting => DateTime.Now.Hour switch
    {
        < 5 => "夜深了，早点休息",
        < 11 => "早上好",
        < 14 => "中午好",
        < 18 => "下午好",
        _ => "晚上好",
    };

    /// <summary>角色小板块的提示：最近一次拍摄与下一个日程。</summary>
    public string HanabeDailyMessage
    {
        get
        {
            var parts = new List<string>();
            var latest = ShootingFrequencyBars.LastOrDefault();
            if (latest is not null)
                parts.Add($"最近一次拍摄在 {latest.Month}.{latest.Day}，共 {latest.Count.ToString("N0", CultureInfo.InvariantCulture)} 张。");
            var next = ScheduleItems.Where(item => !item.IsDone).OrderBy(item => item.Date).FirstOrDefault();
            parts.Add(next is null ? "还没有安排下一次拍摄，要不要写一个？" : $"下一个日程：{next.DateText} {next.Title}");
            return string.Join("\n", parts);
        }
    }
}
