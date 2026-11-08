using CommunityToolkit.Mvvm.ComponentModel;
using System.Reflection;

namespace HanabePhotoManager.App.ReleaseNotes;

/// <summary>一个版本的标题、日期和变更条目。</summary>
public sealed record ReleaseVersionInfo(
    string Version,
    DateOnly ReleaseDate,
    IReadOnlyList<string> Notes);

/// <summary>版本说明列表中的可展示项。</summary>
public sealed record ReleaseVersionItemViewModel(
    string Version,
    string DateText,
    string StatusLabel,
    string BranchGlyph,
    IReadOnlyList<string> Notes);

/// <summary>提供版本筛选、选择和当前发布说明。</summary>
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

/// <summary>内置版本说明的只读目录。</summary>
public static class ReleaseNotesCatalog
{
    public static IReadOnlyList<ReleaseVersionInfo> Versions { get; } =
    [
        new(
            "0.3.2-alpha.21",
            new DateOnly(2026, 8, 31),
            [
                "Hanabe 增加相机机械、Q 版与混合三套任务状态音效，并提供独立音效设置分区。",
                "重复检测可在需要处理时唤回主窗口，重复清理完成后显示独立结果报告。",
                "移动导入安全确认改为与应用主题一致的 Hanabe 模态窗口。"
            ]),
        new(
            "0.3.2-alpha.20",
            new DateOnly(2026, 8, 26),
            [
                "分类、修图状态、评分分类和多选开关调整到同一行，常用筛选保持连续。",
                "高级筛选移动到下一行并单独靠左，与常用筛选建立清晰层级。",
                "视频引擎改为后台预热并在窗口内复用播放器，启用硬件解码和缓存/迟帧策略，减少首次打开卡顿并改善大视频播放流畅度。"
            ]),
        new(
            "0.3.2-alpha.19",
            new DateOnly(2026, 8, 26),
            [
                "修正图库筛选区多选开关和高级筛选按钮的位置，组成右对齐操作组并与同排筛选控件底部对齐。",
                "两个控件统一为 36px 高度并增加固定间距，避免边界挤压和上下错位。"
            ]),
        new(
            "0.3.2-alpha.18",
            new DateOnly(2026, 8, 26),
            [
                "图库顶部多选模式改为 Switch 开关，明确表达模式的开启与关闭。",
                "照片卡片、导入队列、水印列表及重复内容复查统一使用深色选中层与勾选框，不再将项目选择显示成 Switch。",
                "保留统一内外圆角及可辨识的比例滚动进度。"
            ]),
        new(
            "0.3.2-alpha.17",
            new DateOnly(2026, 8, 26),
            [
                "主窗口外部与内部工作区统一使用同一容器圆角令牌，四角曲率保持一致。",
                "继承 alpha.16 的可辨识滚动进度、alpha.15 的最小窗口布局保护，以及 alpha.14 的图库清理。",
                "全量 644 项自动化测试通过。"
            ]),
        new(
            "0.3.2-alpha.16",
            new DateOnly(2026, 8, 26),
            [
                "图库滚动条改为中性浅色轨道与深绿色比例滑块，当前位置和滚动进度清晰可见。",
                "滑块长度对应当前可视内容比例，支持拖动、点击轨道与鼠标滚轮。"
            ]),
        new(
            "0.3.2-alpha.15",
            new DateOnly(2026, 8, 26),
            [
                "主窗口最小尺寸设为 1440 × 900，避免三栏工作区缩小时发生重合和错位。",
                "一级菜单保留深色悬停反馈，移除左侧额外选中条，使选中状态只保留一个视觉层级。"
            ]),
        new(
            "0.3.2-alpha.14",
            new DateOnly(2026, 8, 26),
            [
                "LRF 继续作为相机伴随文件参与导入归组，但不再显示于图库内容。",
                "图库主滚动条改为常显，移除右下角悬浮加号导入按钮。",
                "调整一级导航选中提示，避免与菜单项表面重合。"
            ]),
        new(
            "0.3.2-alpha.13",
            new DateOnly(2026, 8, 26),
            [
                "水印支持在真实图片区域直接拖动；调整签名大小时保持中心位置不变。",
                "多个铺满水印提升至最大密度；拼图新增可选的虚化玻璃质感补边背景。",
                "补齐新应用页面切换动画、图库完整日期分节、导入页间距与固定高度提示卡切换。"
            ]),
        new(
            "0.3.0-alpha",
            new DateOnly(2026, 8, 14),
            [
                "全面 Material Design 3 视觉重构：动态色彩 6 套主题（靛蓝/森林绿/紫罗兰 × 浅/深）、Navigation Rail 导航、大圆角 Surface 与彩色状态层。",
                "功能页重新设计：人物页（人物相册 + 按脸查找双 Tab，照片虚拟化）、相册页（卡片流 + 详情 + 网格/列表切换）、导入页（三栏 Lightroom 导入模块）、设置页（分区导航 + 主题色卡实时换肤）、工具/地图页全新工作台。",
                "修复网格角标对比度、视频首帧缩略图提取、Inspector 单击 EXIF 不显示、工具卡片圆角等问题。",
                "开源准备：三语 README（英/中/日）、MIT 许可证、原创应用图标、微信赞赏 + 爱发电赞助入口。"
            ]),
        new(
            "0.2.0-alpha.3",
            new DateOnly(2026, 8, 6),
            [
                "持续重构照片图库筛选与空间树图数据流，统一日期、修后目录与文件类型筛选管线。",
                "新增文件类型多选筛选（RAW/JPG/PNG/视频），PSD 默认排除，支持组合筛选取交集。",
                "空间树图分类内部引入 Justified Gallery 自动拼贴，根据图片宽高比生成动态矩形。",
                "优化子树导航、项目数量动态统计和视口优先缩略图加载流程。",
                "修复修后子文件夹递归扫描、日期切换异步竞态、已修筛选稳定性等问题。",
                "当前自动拼贴、大图库完整浏览和部分筛选稳定性仍在持续优化。"
            ]),
        new(
            "0.2.0-alpha.2",
            new DateOnly(2026, 8, 4),
            [
                "导入后自动进行内容级重复检测：比对文件哈希与视觉指纹，识别内容重复的照片。",
                "新增重复复查面板：罗列重复内容，支持单选或多选进行合并或删除。",
                "合并或删除后按自然顺序重新排列序列号，去掉重复项后序号保持连续。",
                "照片网格支持以指针为中心、按住 Ctrl + 滚轮连续放大或缩小的苹果图库式交互。",
                "网格照片块统一为正方形，缩略图使用 UniformToFill 裁切铺满方块、不留白。",
                "缩放时渐进加载更清晰的缩略图：放大逐步加载高清图，缩小减少细节避免卡顿。",
                "网格顶部新增面包屑导航，可点击返回上一级分类，分类入口保持不变。",
                "放大后支持滚动条或按住鼠标中键拖动画布平移，并设定最小/最大尺寸避免缩成看不见的点。"
            ]),
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
