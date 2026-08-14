using System.IO;
﻿using System.Text.Json;
using System.Text.Json.Serialization;
using HanabePhotoManager.App.Navigation;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.App.Services;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly string _settingsPath;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public AppSettingsStore(string? settingsPath = null)
    {
        var directory = settingsPath is null
            ? AppDataPaths.Root
            : Path.GetDirectoryName(Path.GetFullPath(settingsPath))!;
        Directory.CreateDirectory(directory);
        _settingsPath = settingsPath ?? Path.Combine(directory, "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            AppSettings settings;
            await using (var stream = File.OpenRead(_settingsPath))
            {
                settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options, cancellationToken).ConfigureAwait(false)
                           ?? new AppSettings();
            }

            await RepairLibraryRootAsync(settings, cancellationToken).ConfigureAwait(false);
            return settings;
        }
        catch (JsonException ex)
        {
            // 配置文件损坏（例如被外部脚本写入非法 JSON）：备份损坏文件、回退默认
            // 设置，让应用照常启动而不崩溃；下一次 SaveAsync 会写回合法 JSON。
            TryBackupCorruptSettings();
            LogRecovery("settings.json 解析失败，已备份损坏文件并回退默认设置", ex);
            return new AppSettings();
        }
        catch (IOException ex)
        {
            // 文件被占用/读取失败同样不应让启动崩溃。
            LogRecovery("settings.json 读取失败，已回退默认设置", ex);
            return new AppSettings();
        }
    }

    /// <summary>
    /// 启动自修复：settings.json 里的 LibraryRoot 可能是 "\Hanabe\拍照" 这种根相对路径
    /// （单反斜杠、无盘符，Path.IsPathFullyQualified=false），直接拼目标路径会产出
    /// 无盘符的非法目标（下游抛 "Transfer paths must be fully qualified."）。
    /// 加载时统一规范化为完全限定路径并回写 settings.json，用户无需手动改配置。
    /// 规范化优先按"丢失反斜杠的 UNC"识别：真实照片库是另一台电脑上的 UNC 共享
    /// "\\Hanabe\拍照"，补双反斜杠后可访问时按 UNC 保留，绝不转成本机 C 盘残留副本。
    /// </summary>
    private async Task RepairLibraryRootAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.LibraryRoot))
        {
            return;
        }

        var repaired = NormalizeLibraryRoot(settings.LibraryRoot);
        if (string.Equals(repaired, settings.LibraryRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        settings.LibraryRoot = repaired;
        await SaveAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 把 LibraryRoot 规范化为完全限定（含盘符/UNC）的绝对路径并去掉尾部分隔符。
    /// 已完全限定的路径（含 UNC "\\Hanabe\拍照"）原样保留；根相对路径（如 "\Hanabe\拍照"）
    /// 优先识别为丢失反斜杠的 UNC——补双反斜杠后若该共享可访问则返回 UNC，否则才
    /// GetFullPath 成当前盘符绝对路径。无法解析的非法路径原样返回，绝不让加载/保存流程崩溃。
    /// </summary>
    public static string? NormalizeLibraryRoot(string? path)
        => LibraryRootNormalizer.Normalize(path);

    /// <summary>测试用：注入目录探测器，确定性验证 UNC 分支（不依赖真实网络共享是否在线）。</summary>
    internal static string? NormalizeLibraryRoot(string? path, Func<string, bool>? directoryExists)
        => LibraryRootNormalizer.Normalize(path, directoryExists);

    private void TryBackupCorruptSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var backup = _settingsPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(_settingsPath, backup, overwrite: true);
            }
        }
        catch
        {
            // 备份失败绝不能让启动崩溃。
        }
    }

    private static void LogRecovery(string message, Exception exception)
    {
        try
        {
            var logDirectory = System.IO.Path.Combine(AppDataPaths.Root, "Logs");
            System.IO.Directory.CreateDirectory(logDirectory);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(logDirectory, "settings-recovery.log"),
                $"{DateTimeOffset.Now:O} {message}: {exception}{Environment.NewLine}");
        }
        catch
        {
            // 日志写入失败绝不能变成第二次异常。
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        // 保存前统一规范化 LibraryRoot：即使外部/旧逻辑传入了 "\Hanabe\拍照" 这种
        // 根相对路径，落盘前也转成完全限定路径（UNC 可访问时按 UNC 保留，否则当前盘符
        // 绝对路径；并去尾分隔符），防止坏值再次入盘。
        settings.LibraryRoot = NormalizeLibraryRoot(settings.LibraryRoot);
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var temporaryPath = _settingsPath + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            _saveGate.Release();
        }
    }

    public async Task UpdateAsync(Action<AppSettings> mutate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        var current = await LoadAsync(cancellationToken).ConfigureAwait(false);
        mutate(current);
        await SaveAsync(current, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class AppSettings
{
    public bool HasCompletedOnboarding { get; set; }

    public List<string> NavigationOrder { get; set; } = [];

    public NavigationDisplayMode NavigationDisplayMode { get; set; } = NavigationDisplayMode.IconAndText;

    public string? LibraryRoot { get; set; }

    public double DefaultThumbnailSize { get; set; } = 150;

    public double ZoomableGridTileSize { get; set; } = 150;

    public double TreemapZoom { get; set; } = 1.0;

    [System.Text.Json.Serialization.JsonPropertyName("ThumbnailSize")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? LegacyThumbnailSize
    {
        get => null;
        set
        {
            if (value is >= 96 and <= 260)
            {
                DefaultThumbnailSize = value.Value;
            }
        }
    }

    public double GlassIntensity { get; set; } = 0.62;

    /// <summary>
    /// 启用主窗口的 DWM 系统级亚克力/Blur 背景材质（DWMWA_SYSTEMBACKDROP_TYPE 优先，
    /// 失败时降级 SetWindowCompositionAttribute 亚克力，再降级为应用内半透明方案）。
    /// </summary>
    public bool IsAcrylicEnabled { get; set; } = true;

    public string BackgroundMode { get; set; } = "平衡玻璃";

    public string BackgroundImageLayout { get; set; } = "填充";

    public string ClassificationEngine { get; set; } = PhotoClassifierFactory.OnnxMode;
    public string InferenceDevice { get; set; } = "auto";
    public FaceRecognitionEngineKind FaceRecognitionEngine { get; set; } = FaceRecognitionEngineKind.YuNetSFace;
    public FaceRecognitionProfile FaceRecognitionProfile { get; set; } = FaceRecognitionProfile.Balanced;
    public string? ArcFaceDetectorModelPath { get; set; }
    public string? ArcFaceRecognizerModelPath { get; set; }
    public bool ArcFaceModelLicenseConfirmed { get; set; }
    public string? ArcFaceModelLicenseDescription { get; set; }
    public double ArcFaceMatchThreshold { get; set; } = FaceRecognitionDefaults.ArcFaceR100Threshold;
    public int SemanticMaxLabels { get; set; } = 3;
    public double SemanticSimilarityWindow { get; set; } = 0.10;
    public string DefaultRatingFilter { get; set; } = "全部评分";
    public int DefaultPreviewSort { get; set; } = 9;

    public string? CustomBackgroundPath { get; set; }

    public string? AppIconPath { get; set; }

    public bool LaunchAtStartup { get; set; }

    public double WindowWidth { get; set; } = 1600;

    public double WindowHeight { get; set; } = 980;

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public string WindowState { get; set; } = "Normal";

    public bool RestoreWindowState { get; set; } = true;

    public bool EnablePersonRecognition { get; set; }

    public string BrowseEntryMode { get; set; } = nameof(global::HanabePhotoManager.App.Services.BrowseEntryMode.SessionRestore);

    public BrowseSnapshot? BrowseSnapshot { get; set; }

    public string BrowseDisplayMode { get; set; } = nameof(global::HanabePhotoManager.App.ViewModels.BrowseDisplayMode.Grid);

    public string TreemapWeightMode { get; set; } = nameof(global::HanabePhotoManager.Core.Browsing.Treemap.TreemapWeightMode.FileSize);

    public bool IsTreemapBorderless { get; set; } = true;

    public bool ShowPsdFiles { get; set; } = false;

    public List<string> SelectedFileTypeFilters { get; set; } = [];

    /// <summary>
    /// Whether the browse page's advanced filter section is expanded. Defaults to
    /// collapsed so the filter panel stays compact for the 90% high-frequency use.
    /// </summary>
    public bool IsAdvancedFiltersExpanded { get; set; }

    /// <summary>
    /// User-supplied Baidu Open Platform AppKey. The secret counterpart is stored
    /// separately as <see cref="BaiduAppSecretProtected"/> (DPAPI-encrypted).
    /// </summary>
    public string? BaiduAppKey { get; set; }

    /// <summary>
    /// Base64-encoded DPAPI-encrypted Baidu AppSecret. Never store the raw secret.
    /// </summary>
    public string? BaiduAppSecretProtected { get; set; }

    /// <summary>
    /// Optional path to the user's local Quark netdisk client (or shortcut), so
    /// the app can launch it on demand even though Quark has no public API.
    /// </summary>
    public string? QuarkClientPath { get; set; }

    /// <summary>
    /// 导入时是否对来源文件做全库 SHA-256 哈希查重并弹窗确认。
    /// 默认关闭（导入更快、不做跨库去重）；开启后每次导入前检查重复并弹窗。
    /// </summary>
    public bool CheckDuplicatesOnImport { get; set; } = false;
}
