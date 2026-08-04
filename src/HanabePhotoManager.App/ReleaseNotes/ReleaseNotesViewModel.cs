using CommunityToolkit.Mvvm.ComponentModel;
using System.Reflection;

namespace HanabePhotoManager.App.ReleaseNotes;

public sealed record ReleaseVersionInfo(
    string Version,
    DateOnly ReleaseDate,
    IReadOnlyList<string> Notes);

public sealed record ReleaseVersionItemViewModel(
    string Version,
    string DateText,
    string StatusLabel,
    string BranchGlyph,
    IReadOnlyList<string> Notes);

public sealed class ReleaseNotesViewModel : ObservableObject
{
    private ReleaseVersionItemViewModel? _selectedVersion;

    public ReleaseNotesViewModel(
        IReadOnlyList<ReleaseVersionInfo>? versions = null,
        string? currentVersion = null)
    {
        var resolvedCurrent = string.IsNullOrWhiteSpace(currentVersion)
            ? ResolveCurrentVersion()
            : currentVersion;
        var ordered = (versions ?? ReleaseNotesCatalog.Versions)
            .OrderByDescending(item => ParseVersion(item.Version))
            .ThenByDescending(item => item.ReleaseDate)
            .ToArray();
        Versions = ordered
            .Select((item, index) => new ReleaseVersionItemViewModel(
                item.Version,
                item.ReleaseDate.ToString("yyyy-MM-dd"),
                GetStatus(item.Version, resolvedCurrent),
                index == ordered.Length - 1 ? "└─" : "├─",
                item.Notes))
            .ToArray();
        CurrentVersionLabel = $"当前版本 {resolvedCurrent}";
        HasAvailableUpdate = Versions.Any(item => item.StatusLabel == "可更新");
        SelectedVersion = Versions.FirstOrDefault(item => item.StatusLabel == "当前版本")
            ?? Versions.FirstOrDefault();
    }

    public IReadOnlyList<ReleaseVersionItemViewModel> Versions { get; }

    public string CurrentVersionLabel { get; }

    public bool HasAvailableUpdate { get; }

    public ReleaseVersionItemViewModel? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (SetProperty(ref _selectedVersion, value))
            {
                OnPropertyChanged(nameof(SelectedReleaseTitle));
                OnPropertyChanged(nameof(SelectedReleaseNotes));
            }
        }
    }

    public string SelectedReleaseTitle => SelectedVersion is null
        ? "暂无版本信息"
        : $"{SelectedVersion.Version} · {SelectedVersion.DateText}";

    public string SelectedReleaseNotes => SelectedVersion is null
        ? string.Empty
        : string.Join(Environment.NewLine, SelectedVersion.Notes.Select(note => $"• {note}"));

    private static string ResolveCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(ReleaseNotesViewModel).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational.Split('+', 2)[0];
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static string GetStatus(string version, string currentVersion)
    {
        var comparison = ParseVersion(version).CompareTo(ParseVersion(currentVersion));
        return comparison switch
        {
            > 0 => "可更新",
            0 => "当前版本",
            _ => "历史版本"
        };
    }

    private static Version ParseVersion(string version)
    {
        var numeric = version.Split('-', 2)[0];
        return Version.TryParse(numeric, out var parsed) ? parsed : new Version(0, 0, 0);
    }
}

public static class ReleaseNotesCatalog
{
    public static IReadOnlyList<ReleaseVersionInfo> Versions { get; } =
    [
        new(
            "0.2.0-alpha.1",
            new DateOnly(2026, 8, 3),
            [
                "浏览页新增渐进式照片空间树图，可按文件大小或照片数量分配面积。",
                "扫描照片时持续更新矩形结构，并保留原有网格浏览方式。",
                "设置页新增版本树、当前版本标记和可滚动更新日志。",
                "新增 Windows 安装与升级流程，桌面快捷方式始终打开当前版本。"
            ]),
        new(
            "0.1.0-alpha",
            new DateOnly(2026, 7, 29),
            [
                "建立照片管理、分类、导入和本地媒体预览基础功能。",
                "提供主题、开机自启动及基础设置管理。"
            ])
    ];
}
