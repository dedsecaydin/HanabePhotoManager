using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Models;
using HanabePhotoManager.App.Navigation;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.Watermark;
using HanabePhotoManager.Core.Imports;
using HanabePhotoManager.Core.Performance;
using HanabePhotoManager.Infrastructure.Files;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace HanabePhotoManager.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private static readonly string[] CategoryFolderNames =
    [
        "RAW生图",
        "JPG生图",
        "修后",
        "视频",
        "action视频",
        "素材"
    ];

    private static readonly IReadOnlyDictionary<MediaCategory, string> ConcreteCategoryFolders =
        new Dictionary<MediaCategory, string>
        {
            [MediaCategory.Raw] = "RAW生图",
            [MediaCategory.Jpeg] = "JPG生图",
            [MediaCategory.Edited] = "修后",
            [MediaCategory.Video] = "视频",
            [MediaCategory.ActionVideo] = "action视频",
            [MediaCategory.Material] = "素材"
        };

    private static readonly IReadOnlyList<string> ImportExtensions =
    [
        ".arw", ".cr2", ".cr3", ".jpg", ".jpeg", ".mp4", ".xml", ".lrf", ".aac"
    ];

    private static readonly IReadOnlyList<string> LibraryPreviewExtensions =
    [
        ".arw", ".cr2", ".cr3", ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic",
        ".mp4", ".mov", ".xml", ".lrf", ".aac"
    ];

    private static readonly IReadOnlyList<string> DefaultNavigationOrder =
    [
        "Home",
        "Import",
        "Preview",
        "FaceSearch",
        "MapPhotos",
        "Compression",
        "BaiduCloud",
        "QuarkCloud",
        "ContestOpen",
        "ContestJudged"
    ];

    private static readonly HashSet<string> WpfImageExtensions = new(
        [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ThumbnailCandidateExtensions = new(
        [".arw", ".cr2", ".cr3", ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".heic", ".psd", ".psb", ".mp4", ".mov"],
        StringComparer.OrdinalIgnoreCase);

    // RAW formats that Windows Shell cannot thumbnail without a third-party codec. Skipping
    // the Shell fallback for these avoids a multi-second probe per file every time the
    // preview page is reloaded; the WPF decoder (or a generic icon) is used instead.
    private static readonly HashSet<string> RawExtensions = new(
        [".arw", ".cr2", ".cr3", ".nef", ".raf", ".rw2", ".orf", ".dng", ".raw"],
        StringComparer.OrdinalIgnoreCase);

    // Per-app thumbnail cache keyed by (path + size + mtime) so the same file isn't
    // re-decoded every time the preview page re-renders.
    private static readonly ConcurrentDictionary<string, ImageSource> ThumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentQueue<string> ThumbnailCacheOrder = new();

    private readonly AppSettingsStore _settingsStore = new();
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly CloudConnectionSettingsService _cloudConnectionService = new();
    private readonly CameraFolderDateResolver _dateResolver = new();
    private readonly MediaGroupBuilder _groupBuilder = new(new MediaClassifier(["ARW", "CR2", "CR3"]));
    private readonly ImportPlanBuilder _planBuilder = new(new DestinationProbe(new Sha256FileHasher()));
    private readonly LibraryDirectoryInitializer _directoryInitializer = new();
    private readonly VerifiedFileTransfer _transfer = new(new Sha256FileHasher());
    private readonly LocalPersonClusterer _personClusterer = new();
    private readonly LibraryMaintenanceService _libraryMaintenanceService = new();
    private readonly RetouchedMediaIndex _retouchedMediaIndex = new();
    private readonly BrowseStatePolicy _browseStatePolicy = new();
    private readonly IMediaMetadataStore _mediaMetadataStore;
    private readonly IWindowsWallpaperService _wallpaperService;
    private readonly PersistentAssetStore _assetStore = new(Path.Combine(AppDataPaths.Root, "Assets"));

    private string _libraryRoot = string.Empty;
    private string _sourceFolder = string.Empty;
    private string _statusMessage = "请选择 Hanabe 拍照库位置，或者先选择一个本地测试目录。";
    private string _selectedDateTitle = "未选择日期";
    private string _selectedDatePath = string.Empty;
    private string _selectedFolderSize = "--";
    private string _selectedFolderPercent = "--";
    private string _importReport = "等待选择来源文件夹。";
    private string _progressLabel = "空闲";
    private string _targetDateText = "--";
    private string _backgroundMode = "平衡玻璃";
    private string _backgroundImageLayout = "填充";
    private string _currentPage = "Home";
    private string _currentPreviewCategory = "全部";
    private string _customBackgroundPath = string.Empty;
    private string _windowsWallpaperPath = string.Empty;
    private string _customAppIconPath = string.Empty;
    private string _selectedDeviceTitle = "选择一个设备查看内容";
    private string _selectedDeviceSummary = "点击设备组中的磁盘、相机或照片库后，这里会显示文件夹和媒体文件概览。";
    private string _importActionHint = "先选择照片库根目录，再选择设备或来源文件夹。";
    private IReadOnlyList<string> _sourceScanPaths = Array.Empty<string>();
    private LibraryDate? _targetDate;
    private LibraryDateNode? _selectedDate;
    private int _previewScanVersion;
    private string _exifSummary = string.Empty;
    private PreviewFileViewModel? _selectedPreviewFile;
    private CancellationTokenSource? _activeTaskCancellation;
    private CancellationTokenSource? _importThumbnailCancellation;
    private CancellationTokenSource? _previewThumbnailCancellation;
    private ActiveTaskKind _activeTaskKind = ActiveTaskKind.None;
    private double _progressValue;
    private DateTimeOffset _operationStartedAt;
    private double _thumbnailSize = 150;
    private double _defaultThumbnailSize = 150;
    private string _defaultRatingFilter = "全部评分";
    private int _defaultPreviewSort;
    private double _glassIntensity = 0.62;
    private double _windowWidth = 1600;
    private double _windowHeight = 980;
    private bool _isBusy;
    private bool _isProgressIndeterminate;
    private bool _launchAtStartup;
    private bool _restoreWindowState = true;
    private double? _windowLeft;
    private double? _windowTop;
    private string _savedWindowState = "Normal";
    private bool _changingStartupRegistration;
    private bool _enablePersonRecognition;
    private bool _previewHasLoaded;
    private bool _isBrowseConditionsExpanded;
    private bool _isInitialized;
    private bool _isOnboardingVisible;
    private int _onboardingStep;
    private NavigationDisplayMode _navigationDisplayMode = NavigationDisplayMode.Text;
    private int _previewPage;
    private readonly Dictionary<string, bool> _previewDateExpansion = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressPreviewSectionRefresh;
    private bool _personFilterOwnsExpansion;
    private bool _skipPreviewExpansionCaptureOnce;
    private bool _isCompactBrowseLayout;
    private DateTime _calendarDisplayMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateOnly? _calendarSelectedDate;
    private TransferMode _selectedTransferMode = TransferMode.CopyKeepSource;
    private BrowseEntryMode _browseEntryModeSetting = BrowseEntryMode.SessionRestore;
    private BrowseSnapshot? _persistedBrowseSnapshot;
    private BrowseSnapshot? _sessionBrowseSnapshot;

    private string _baiduAppKey = string.Empty;
    private string _baiduAppSecret = string.Empty;
    private string _baiduAuthCode = string.Empty;
    private string _baiduStatus = "未连接";
    private string _quarkStatus = "等待官方 API";
    private string _quarkClientPath = string.Empty;
    private string _diagnosticsText = "⏱ 库扫描 · 等待触发扫描…";
    private bool _isBaiduBusy;
    private bool _isBaiduAuthorized;
    private bool _hasSavedBaiduCredentials;
    private string? _pendingBaiduAuthorizeUri;
    private string? _pendingBaiduState;

    public MainWindowViewModel(
        IWindowsWallpaperService? wallpaperService = null,
        IMediaMetadataStore? mediaMetadataStore = null,
        IStartupRegistrationService? startupRegistrationService = null)
    {
        _startupRegistrationService = startupRegistrationService ?? new WindowsStartupRegistrationService();
        _wallpaperService = wallpaperService ?? new WindowsWallpaperService();
        _mediaMetadataStore = mediaMetadataStore ?? new MediaMetadataStore();
        TagManager = new TagManagerViewModel(_mediaMetadataStore);
        PhotoAnalysis = new PhotoAnalysisViewModel(_mediaMetadataStore);
        PhotoAnalysis.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PhotoAnalysisViewModel.SelectedEngine) && _isInitialized)
                _ = SaveSettingsAsync();
        };
        PeopleAlbums = new PeopleAlbumViewModel(
            new PeopleAlbumService(),
            () => PreviewFiles.Select(file => file.FullPath));
        MapPhotos = new MapPhotosViewModel(
            _mediaMetadataStore,
            () => PreviewFiles.Select(file => file.FullPath));
        Compression = new CompressionViewModel();
        Watermark = new WatermarkViewModel();
        PhotoViewer = new PhotoViewerViewModel();
        PhotoViewer.PhotoDeleted += RemoveDeletedViewerPhoto;
        PeopleAlbums.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PeopleAlbumViewModel.SelectedAlbum))
            {
                if (PeopleAlbums.SelectedAlbum is not null)
                {
                    foreach (var section in VisiblePreviewSections)
                        _previewDateExpansion[section.Key] = section.IsExpanded;
                    _personFilterOwnsExpansion = true;
                }
                else
                {
                    _personFilterOwnsExpansion = false;
                    _skipPreviewExpansionCaptureOnce = true;
                }
                RefreshFilteredCache(resetPage: true);
                RebuildCalendarDays();
            }
        };
        FaceSearch = new FaceSearchViewModel(() => LibraryRoot);
        BrowseLibraryCommand = new AsyncRelayCommand(BrowseLibraryAsync, CanRunCommand);
        BrowseSourceCommand = new AsyncRelayCommand(BrowseSourceAsync, CanRunCommand);
        AnalyzeSourceCommand = new AsyncRelayCommand(AnalyzeSourceAsync, CanAnalyzeSource);
        ImportSelectedCommand = new AsyncRelayCommand(ImportSelectedAsync, CanImportSelected);
        RefreshLibraryCommand = new AsyncRelayCommand(RefreshLibraryAsync, CanRunCommand);
        OpenSelectedDateCommand = new RelayCommand(OpenSelectedDate, () => Directory.Exists(SelectedDatePath));
        RefreshDevicesCommand = new RelayCommand(RefreshConnectedDevices);
        InspectDeviceCommand = new RelayCommand<ConnectedDeviceViewModel>(InspectDevice);
        ImportFromDeviceCommand = new AsyncRelayCommand<ConnectedDeviceViewModel>(ImportFromDeviceAsync, CanImportFromDevice);
        CancelCurrentTaskCommand = new RelayCommand(CancelCurrentTask, CanCancelCurrentTask);
        ChooseBackgroundCommand = new RelayCommand(ChooseCustomBackground);
        ClearBackgroundCommand = new RelayCommand(ClearCustomBackground);
        ChooseAppIconCommand = new RelayCommand(ChooseCustomAppIcon);
        ClearAppIconCommand = new RelayCommand(ClearCustomAppIcon);
        ShowHomeCommand = new RelayCommand(() => CurrentPage = "Home");
        ShowImportCommand = new RelayCommand(() => CurrentPage = "Import");
        ShowPreviewCommand = new RelayCommand(() => _ = ShowPreviewAsync());
        ShowFaceSearchCommand = new RelayCommand(() => CurrentPage = "FaceSearch");
        ShowMapPhotosCommand = new RelayCommand(() => CurrentPage = "MapPhotos");
        ShowCompressionCommand = new RelayCommand(() => CurrentPage = "Compression");
        ShowWatermarkCommand = new RelayCommand(() =>
        {
            Compression.SelectedToolMode = ImageToolMode.Watermark;
            CurrentPage = "Compression";
        });
        ShowBaiduCloudCommand = new RelayCommand(() => CurrentPage = "BaiduCloud");
        ShowQuarkCloudCommand = new RelayCommand(() => CurrentPage = "QuarkCloud");
        DeleteSelectedFilesCommand = new RelayCommand(DeleteSelectedFiles, CanDeleteSelectedFiles);
        ShowContestOpenCommand = new RelayCommand(() => CurrentPage = "ContestOpen");
        ShowContestJudgedCommand = new RelayCommand(() => CurrentPage = "ContestJudged");
        ShowSettingsCommand = new RelayCommand(() => CurrentPage = "Settings");
        ResetNavigationItems(null);
        SetPreviewCategoryCommand = new RelayCommand<string>(category => CurrentPreviewCategory = category!);
        SetPreviewRetouchFilterCommand = new RelayCommand<string>(filter => PreviewRetouchFilter = filter ?? "全部");
        OpenQuarkOfficialCommand = new RelayCommand(OpenQuarkOfficial);
        OpenBaiduConsoleCommand = new RelayCommand(OpenBaiduConsole);
        SaveBaiduCredentialsCommand = new AsyncRelayCommand(SaveBaiduCredentialsAsync, CanSaveBaiduCredentials);
        StartBaiduAuthorizationCommand = new AsyncRelayCommand(StartBaiduAuthorizationAsync, CanStartBaiduAuthorization);
        CompleteBaiduAuthorizationCommand = new AsyncRelayCommand(CompleteBaiduAuthorizationAsync, CanCompleteBaiduAuthorization);
        DisconnectBaiduCommand = new AsyncRelayCommand(DisconnectBaiduAsync, () => IsBaiduAuthorized && !IsBaiduBusy);
        NextPreviewPageCommand = new RelayCommand(ShowNextPreviewPage, () => HasNextPreviewPage);
        PreviousPreviewPageCommand = new RelayCommand(ShowPreviousPreviewPage, () => HasPreviousPreviewPage);
        ExpandAllPreviewDatesCommand = new RelayCommand(() => SetAllPreviewDateSectionsExpanded(true));
        CollapseAllPreviewDatesCommand = new RelayCommand(() => SetAllPreviewDateSectionsExpanded(false));
        PreviousCalendarMonthCommand = new RelayCommand(() => ChangeCalendarMonth(-1));
        NextCalendarMonthCommand = new RelayCommand(() => ChangeCalendarMonth(1));
        SelectCalendarDayCommand = new RelayCommand<CalendarDayViewModel>(SelectCalendarDay);
        ShowAllDatesCommand = new AsyncRelayCommand(ShowAllDatesAsync, () => HasLibraryRoot && !IsBusy);
        ResetBrowseConditionsCommand = new AsyncRelayCommand(ResetBrowseConditionsAsync);
        CreateCustomTagCommand = new AsyncRelayCommand(CreateCustomTagAsync);
        AssignCategoryToSelectedCommand = new AsyncRelayCommand(AssignCategoryToSelectedAsync);
        AssignTagToSelectedCommand = new AsyncRelayCommand(AssignTagToSelectedAsync);
        AnalyzeSelectedPhotosCommand = new AsyncRelayCommand(AnalyzeSelectedPhotosAsync);
        AnalyzeCurrentScopeCommand = new AsyncRelayCommand(AnalyzeCurrentScopeAsync);
        DismissOnboardingCommand = new AsyncRelayCommand(DismissOnboardingAsync);
        ReplayOnboardingCommand = new RelayCommand(ReplayOnboarding);
        PreviousOnboardingStepCommand = new RelayCommand(ShowPreviousOnboardingStep);
        NextOnboardingStepCommand = new AsyncRelayCommand(ShowNextOnboardingStepAsync);
        StopOnboardingCommand = new AsyncRelayCommand(DismissOnboardingAsync);
        ContinueOnboardingCommand = new RelayCommand(ContinueOnboarding);
    }

    public ObservableCollection<LibraryDateNode> LibraryDates { get; } = [];

    public FaceSearchViewModel FaceSearch { get; }

    public TagManagerViewModel TagManager { get; }

    public PhotoAnalysisViewModel PhotoAnalysis { get; }

    public PeopleAlbumViewModel PeopleAlbums { get; }

    public MapPhotosViewModel MapPhotos { get; }

    public CompressionViewModel Compression { get; }

    public WatermarkViewModel Watermark { get; }

    public PhotoViewerViewModel PhotoViewer { get; }

    public IAsyncRelayCommand DismissOnboardingCommand { get; }
    public IRelayCommand ReplayOnboardingCommand { get; }
    public IRelayCommand PreviousOnboardingStepCommand { get; }
    public IAsyncRelayCommand NextOnboardingStepCommand { get; }
    public IAsyncRelayCommand StopOnboardingCommand { get; }
    public IRelayCommand ContinueOnboardingCommand { get; }

    public int OnboardingStep
    {
        get => _onboardingStep;
        private set
        {
            if (!SetProperty(ref _onboardingStep, Math.Clamp(value, 0, OnboardingStepCount - 1))) return;
            OnPropertyChanged(nameof(OnboardingTitle));
            OnPropertyChanged(nameof(OnboardingDescription));
            OnPropertyChanged(nameof(OnboardingStepText));
            OnPropertyChanged(nameof(OnboardingProgress));
            OnPropertyChanged(nameof(OnboardingPrimaryActionText));
            OnPropertyChanged(nameof(IsFirstOnboardingStep));
            OnPropertyChanged(nameof(IsLastOnboardingStep));
            OnPropertyChanged(nameof(CanGoToPreviousOnboardingStep));
            OnPropertyChanged(nameof(IsOnboardingLibraryStep));
            OnPropertyChanged(nameof(IsOnboardingSourceStep));
            OnPropertyChanged(nameof(IsOnboardingImportStep));
            OnPropertyChanged(nameof(IsOnboardingContinuationChoiceStep));
            OnPropertyChanged(nameof(ShowStandardOnboardingNavigation));
            OnPropertyChanged(nameof(IsOnboardingLivePageStep));
        }
    }

    public bool IsOnboardingVisible
    {
        get => _isOnboardingVisible;
        private set
        {
            if (SetProperty(ref _isOnboardingVisible, value))
            {
                OnPropertyChanged(nameof(IsOnboardingImportStep));
                OnPropertyChanged(nameof(IsOnboardingContinuationChoiceStep));
                OnPropertyChanged(nameof(IsOnboardingLivePageStep));
            }
        }
    }

    public int OnboardingStepCount => 15;
    public string OnboardingTitle => OnboardingStep switch
    {
        0 => "第一步：设置图库根目录",
        1 => "第二步：选择来源文件夹",
        2 => "第三步：分析与导入",
        3 => "主页：查看照片库概况",
        4 => "照片图库：浏览与筛选",
        5 => "人物查找与人物相册",
        6 => "主要功能介绍完成，要继续吗？",
        7 => "图片小工具",
        8 => "批量水印",
        9 => "地图照片",
        10 => "投稿项目：开放投稿",
        11 => "投稿项目：已评选作品",
        12 => "百度网盘",
        13 => "夸克网盘",
        _ => "设置、外观与高级选项"
    };

    public string OnboardingDescription => OnboardingStep switch
    {
        0 => "图库根目录是整理后照片的唯一存放位置。请在本步骤直接选择或更改目录，然后再继续。",
        1 => "来源文件夹是相机、存储卡或待整理照片所在的位置。请直接选择来源，应用会进入导入页面。",
        2 => "现在直接操作导入页面。箭头会提示“开始分析与分类”和“手动开始 / 继续导入”；先检查右侧队列，再执行导入。",
        3 => "主页汇总照片库容量、日期和近期照片，是进入常用工作流的起点。",
        4 => "照片图库支持按日期、分类、评分、标签和已修状态筛选；双击照片可进入查看器。",
        5 => "人物查找使用清晰参考人脸搜索相似照片；人物相册在本机扫描聚类，不同模型向量不会混用。",
        6 => "图库建立、导入、主页、浏览和人物功能已经介绍完毕。你可以先开始使用，也可以继续了解其他工具。",
        7 => "图片小工具支持批量压缩，以及不限张数的纵向或横向拼图；任务支持取消。",
        8 => "批量水印支持签名水印和满屏平铺，可预览位置、透明度、旋转角度并批量导出。",
        9 => "地图照片读取本地照片的位置元数据，在地图上按地点浏览；没有定位的照片会单独列出。",
        10 => "开放投稿用于准备和管理待提交作品，适合按项目组织照片。",
        11 => "已评选作品用于整理评选结果，并与开放投稿项目分开管理。",
        12 => "百度网盘页面用于网页登录与浏览；应用设置中还可配置本地加密保存的授权信息。",
        13 => "夸克网盘通过独立页面进入；没有公开 API 的能力会明确提示，不会模拟不存在的接口。",
        _ => "设置集中管理启动、图库、AI、人脸引擎、主题背景和诊断。完成后可从“设置 → 常规”再次打开指南。"
    };

    public string OnboardingStepText => $"{OnboardingStep + 1} / {OnboardingStepCount}";
    public int OnboardingProgress => OnboardingStep + 1;
    public string OnboardingPrimaryActionText => IsLastOnboardingStep ? "完成指南" : "下一步";
    public bool IsFirstOnboardingStep => OnboardingStep == 0;
    public bool IsLastOnboardingStep => OnboardingStep == OnboardingStepCount - 1;
    public bool CanGoToPreviousOnboardingStep => !IsFirstOnboardingStep;
    public bool IsOnboardingLibraryStep => OnboardingStep == 0;
    public bool IsOnboardingSourceStep => OnboardingStep == 1;
    public bool IsOnboardingImportStep => OnboardingStep == 2 && IsOnboardingVisible;
    public bool IsOnboardingContinuationChoiceStep => OnboardingStep == 6 && IsOnboardingVisible;
    public bool ShowStandardOnboardingNavigation => !IsOnboardingContinuationChoiceStep;
    public bool IsOnboardingLivePageStep =>
        IsOnboardingVisible && OnboardingStep >= 2 && !IsOnboardingContinuationChoiceStep;

    public void OpenPhotoViewer(PreviewFileViewModel file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!WpfImageExtensions.Contains(Path.GetExtension(file.PreviewPath))) return;
        var paths = VisiblePreviewFiles
            .Select(item => item.PreviewPath)
            .Where(path => WpfImageExtensions.Contains(Path.GetExtension(path)))
            .ToArray();
        PhotoViewer.Open(paths, file.PreviewPath);
    }

    public void RemoveDeletedViewerPhoto(string path)
    {
        var removed = PreviewFiles.Where(file =>
            string.Equals(file.FullPath, path, StringComparison.OrdinalIgnoreCase)
            || string.Equals(file.PreviewPath, path, StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var item in removed)
        {
            item.Thumbnail = null;
            PreviewFiles.Remove(item);
            HomePreviewFiles.Remove(item);
            VisiblePreviewFiles.Remove(item);
            RetouchedFiles.Remove(item);
        }
        RemoveThumbnailCacheEntries(path);
        RefreshFilteredCache(resetPage: false);
        StatusMessage = $"已移入回收站：{Path.GetFileName(path)}";
    }

    public ObservableCollection<CategorySummaryViewModel> CategorySummaries { get; } = [];

    public ObservableCollection<PreviewFileViewModel> PreviewFiles { get; } = [];

    public ObservableCollection<PreviewFileViewModel> VisiblePreviewFiles { get; } = [];

    public ObservableCollection<PreviewDateSectionViewModel> VisiblePreviewSections { get; } = [];

    public ObservableCollection<CalendarDayViewModel> CalendarDays { get; } = [];

    public ObservableCollection<PreviewFileViewModel> HomePreviewFiles { get; } = [];

    public ObservableCollection<PreviewFileViewModel> RetouchedFiles { get; } = [];

    public bool HasRetouchedFiles => RetouchedFiles.Count > 0;

    public int RetouchedGroupCount => CountPhotoGroups(RetouchedFiles);

    public int TotalPhotoGroupCount => CountPhotoGroups(PreviewFiles);

    public string ExifSummary
    {
        get => _exifSummary;
        private set => SetProperty(ref _exifSummary, value);
    }

    private CancellationTokenSource? _exifCts;

    public PreviewFileViewModel? SelectedPreviewFile
    {
        get => _selectedPreviewFile;
        set
        {
            if (SetProperty(ref _selectedPreviewFile, value))
            {
                _ = LoadExifForSelectedAsync();
            }
        }
    }

    private async Task LoadExifForSelectedAsync()
    {
        _exifCts?.Cancel();
        _exifCts = new CancellationTokenSource();
        var ct = _exifCts.Token;

        var file = _selectedPreviewFile;
        if (file is null || !File.Exists(file.PreviewPath))
        {
            ExifSummary = "选择照片查看元数据";
            return;
        }

        var path = file.PreviewPath;
        ExifSummary = "读取元数据中…";

        try
        {
            var text = await Task.Run(() => ReadExifCore(path), ct);
            if (ct.IsCancellationRequested) return;
            ExifSummary = string.IsNullOrWhiteSpace(text) ? "该文件无可用 EXIF 数据" : text;
        }
        catch (OperationCanceledException) { }
        catch
        {
            ExifSummary = "无法读取元数据（可能非标准图像格式）";
        }
    }

    private static string ReadExifCore(string path)
    {
        var dirs = MetadataExtractor.ImageMetadataReader.ReadMetadata(path);
        var sb = new StringBuilder();
        foreach (var directory in dirs)
        {
            foreach (var tag in directory.Tags)
            {
                var name = tag.Name;
                if (name == "File Name" || name == "File Size" || name == "Image Width" || name == "Image Height"
                    || name.Contains("Make") || name.Contains("Model") || (name.Contains("Aperture") && !name.Contains("Max"))
                    || name.Contains("Shutter") || name.Contains("ISO") || name.Contains("Focal Length")
                    || name.Contains("Date") || name.Contains("Exposure") || name.Contains("White Balance")
                    || name.Contains("Lens") || name.Contains("Flash"))
                {
                    sb.AppendLine($"{name}：{tag.Description}");
                }
            }
            if (directory.Name.Contains("GPS"))
                foreach (var tag in directory.Tags.Where(t => t.Name.Contains("GPS")))
                    sb.AppendLine($"{tag.Name}：{tag.Description}");
        }
        return sb.ToString().TrimEnd();
    }

    public ObservableCollection<ImportPreviewItemViewModel> ImportItems { get; } = [];

    public ObservableCollection<ImportCategorySectionViewModel> ImportSections { get; } = [];

    public ObservableCollection<ConnectedDeviceViewModel> ConnectedDevices { get; } = [];

    public ObservableCollection<DeviceGroupViewModel> DeviceGroups { get; } = [];

    public ObservableCollection<DeviceContentItemViewModel> SelectedDeviceContents { get; } = [];

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; } = [];

    public IReadOnlyList<NavigationDisplayMode> NavigationDisplayModes { get; } =
        Enum.GetValues<NavigationDisplayMode>();

    public NavigationDisplayMode NavigationDisplayMode
    {
        get => _navigationDisplayMode;
        set
        {
            if (SetProperty(ref _navigationDisplayMode, value))
            {
                _ = SaveSettingsAsync();
            }
        }
    }

    public IReadOnlyList<CategoryChoice> CategoryChoices { get; } =
    [
        new(MediaCategory.Unconfirmed, "待确认"),
        new(MediaCategory.Raw, "RAW生图"),
        new(MediaCategory.Jpeg, "JPG生图"),
        new(MediaCategory.Video, "视频"),
        new(MediaCategory.ActionVideo, "action视频"),
        new(MediaCategory.Edited, "修后"),
        new(MediaCategory.Material, "素材")
    ];

    public IReadOnlyList<TransferModeChoice> TransferModes { get; } =
    [
        new(TransferMode.CopyKeepSource, "复制并保留源文件"),
        new(TransferMode.MoveAfterVerify, "校验后移动/删除源文件")
    ];

    public IReadOnlyList<string> BackgroundModes { get; } = ["平衡玻璃", "内置渐变", "自定义图片", "跟随 Windows 壁纸"];

    public IReadOnlyList<string> BackgroundImageLayouts { get; } = ["填充", "居中", "保持长宽比", "拉伸"];

    public IReadOnlyList<BrowseEntryChoice> BrowseEntryModes { get; } =
    [
        new(BrowseEntryMode.CrossLaunchRestore, "跨启动恢复", "重新打开应用后继续上次日期和筛选"),
        new(BrowseEntryMode.SessionRestore, "仅本次运行恢复", "切换栏目后保留，重启后回到全部日期"),
        new(BrowseEntryMode.AlwaysAllDates, "始终全部日期", "每次进入浏览页都清除日期选择")
    ];

    public IReadOnlyList<string> PreviewCategoryFilters { get; } =
        ["全部", "RAW生图", "JPG生图", "修后", "视频", "action视频", "素材"];

    public IReadOnlyList<PreviewSortChoice> PreviewSortChoices { get; } =
    [
        new(0, "默认顺序"),
        new(1, "文件名 A–Z"),
        new(2, "文件名 Z–A"),
        new(3, "文件从小到大"),
        new(4, "文件从大到小"),
        new(5, "分类 A–Z"),
        new(6, "分类 Z–A"),
        new(7, "评分从高到低"),
        new(8, "评分从低到高")
    ];

    public IReadOnlyList<string> RatingFilters { get; } =
        ["全部评分", "未评分", "1★", "2★", "3★", "4★", "5★"];
    public IReadOnlyList<InferenceDeviceChoice> InferenceDevices { get; } = [new("auto", "自动"), new("cpu", "CPU")];
    public IReadOnlyList<FaceEngineChoice> FaceRecognitionEngines { get; } =
    [
        new(FaceRecognitionEngineKind.YuNetSFace, "YuNet + SFace（兼容默认）"),
        new(FaceRecognitionEngineKind.ArcFaceR100, "ArcFace R100（用户模型）")
    ];
    public IReadOnlyList<FaceProfileChoice> FaceRecognitionProfiles { get; } =
    [
        new(FaceRecognitionProfile.Speed, "速度"),
        new(FaceRecognitionProfile.Balanced, "均衡"),
        new(FaceRecognitionProfile.HighAccuracy, "高精度")
    ];
    public FaceRecognitionEngineKind FaceRecognitionEngine
    {
        get => FaceRecognitionRuntimeOptions.Current.Engine;
        set
        {
            if (value == FaceRecognitionEngineKind.ArcFaceR100 && !IsArcFaceAvailable)
            {
                OnPropertyChanged(nameof(ArcFaceAvailabilityReason));
                return;
            }
            FaceRecognitionRuntimeOptions.Current.Engine = value;
            NotifyFaceSettingsChanged();
        }
    }
    public FaceRecognitionProfile FaceRecognitionProfile
    {
        get => FaceRecognitionRuntimeOptions.Current.Profile;
        set { FaceRecognitionRuntimeOptions.Current.Profile = value; NotifyFaceSettingsChanged(); }
    }
    public string ArcFaceDetectorModelPath
    {
        get => FaceRecognitionRuntimeOptions.Current.DetectorModelPath ?? string.Empty;
        set { FaceRecognitionRuntimeOptions.Current.DetectorModelPath = value; NotifyFaceSettingsChanged(); }
    }
    public string ArcFaceRecognizerModelPath
    {
        get => FaceRecognitionRuntimeOptions.Current.RecognizerModelPath ?? string.Empty;
        set { FaceRecognitionRuntimeOptions.Current.RecognizerModelPath = value; NotifyFaceSettingsChanged(); }
    }
    public bool ArcFaceModelLicenseConfirmed
    {
        get => FaceRecognitionRuntimeOptions.Current.ModelLicenseConfirmed;
        set { FaceRecognitionRuntimeOptions.Current.ModelLicenseConfirmed = value; NotifyFaceSettingsChanged(); }
    }
    public string ArcFaceModelLicenseDescription
    {
        get => FaceRecognitionRuntimeOptions.Current.ModelLicenseDescription ?? string.Empty;
        set { FaceRecognitionRuntimeOptions.Current.ModelLicenseDescription = value; NotifyFaceSettingsChanged(); }
    }
    public double ArcFaceMatchThreshold
    {
        get => FaceRecognitionRuntimeOptions.Current.MatchThreshold;
        set { FaceRecognitionRuntimeOptions.Current.MatchThreshold = Math.Clamp(value, .2, .9); NotifyFaceSettingsChanged(); }
    }
    public bool IsArcFaceAvailable => new FaceRecognitionOptions
    {
        Engine = FaceRecognitionEngineKind.ArcFaceR100,
        DetectorModelPath = ArcFaceDetectorModelPath,
        RecognizerModelPath = ArcFaceRecognizerModelPath,
        ModelLicenseConfirmed = ArcFaceModelLicenseConfirmed,
        ModelLicenseDescription = ArcFaceModelLicenseDescription
    }.EvaluateAvailability().IsAvailable;
    public string ArcFaceAvailabilityReason
    {
        get
        {
            var availability = new FaceRecognitionOptions
            {
                Engine = FaceRecognitionEngineKind.ArcFaceR100,
                DetectorModelPath = ArcFaceDetectorModelPath,
                RecognizerModelPath = ArcFaceRecognizerModelPath,
                ModelLicenseConfirmed = ArcFaceModelLicenseConfirmed,
                ModelLicenseDescription = ArcFaceModelLicenseDescription
            }.EvaluateAvailability();
            return availability.IsAvailable ? "ArcFace 已启用；模型向量与 YuNet/SFace 人物库完全隔离。" : availability.Reason;
        }
    }
    public IReadOnlyList<int> SemanticLabelCounts { get; } = [1, 2, 3, 4, 5];
    public string DefaultRatingFilter { get => _defaultRatingFilter; set { if (SetProperty(ref _defaultRatingFilter, RatingFilters.Contains(value) ? value : "全部评分")) _ = SaveSettingsAsync(); } }
    public int DefaultPreviewSort { get => _defaultPreviewSort; set { if (SetProperty(ref _defaultPreviewSort, Math.Clamp(value, 0, 8))) _ = SaveSettingsAsync(); } }
    public double DefaultThumbnailSize { get => _defaultThumbnailSize; set { if (SetProperty(ref _defaultThumbnailSize, Math.Clamp(value, 96, 260))) _ = SaveSettingsAsync(); } }
    public string InferenceDevice
    {
        get => string.Equals(MobileClipRuntimeOptions.DevicePreference, "CPU", StringComparison.OrdinalIgnoreCase) ? "cpu" : "auto";
        set
        {
            MobileClipRuntimeOptions.DevicePreference = string.Equals(value, "cpu", StringComparison.OrdinalIgnoreCase) ? "CPU" : "自动（NVIDIA 优先）";
            OnPropertyChanged();
            OnPropertyChanged(nameof(InferenceProviderStatus));
            _ = SaveSettingsAsync();
        }
    }
    public bool IsGpuProviderAvailable => File.Exists(Path.Combine(AppContext.BaseDirectory, "onnxruntime_providers_cuda.dll"));
    public string InferenceProviderStatus => IsGpuProviderAvailable
        ? "检测到可用 GPU Provider；自动模式将优先使用它。"
        : InferenceDevice == "auto" ? "未检测到 GPU Provider；自动模式已回退 CPU。" : "当前固定使用 CPU。";
    public int SemanticMaxLabels { get => MobileClipRuntimeOptions.MaximumLabels; set { MobileClipRuntimeOptions.MaximumLabels = Math.Clamp(value, 1, 5); OnPropertyChanged(); _ = SaveSettingsAsync(); } }
    public double SemanticSimilarityWindow { get => MobileClipRuntimeOptions.SimilarityWindow; set { MobileClipRuntimeOptions.SimilarityWindow = Math.Clamp(value, .02, .30); OnPropertyChanged(); _ = SaveSettingsAsync(); } }

    private void NotifyFaceSettingsChanged()
    {
        if (FaceRecognitionRuntimeOptions.Current.Engine == FaceRecognitionEngineKind.ArcFaceR100
            && !IsArcFaceAvailable)
            FaceRecognitionRuntimeOptions.Current.Engine = FaceRecognitionEngineKind.YuNetSFace;
        OnPropertyChanged(nameof(FaceRecognitionEngine));
        OnPropertyChanged(nameof(FaceRecognitionProfile));
        OnPropertyChanged(nameof(ArcFaceDetectorModelPath));
        OnPropertyChanged(nameof(ArcFaceRecognizerModelPath));
        OnPropertyChanged(nameof(ArcFaceModelLicenseConfirmed));
        OnPropertyChanged(nameof(ArcFaceModelLicenseDescription));
        OnPropertyChanged(nameof(ArcFaceMatchThreshold));
        OnPropertyChanged(nameof(IsArcFaceAvailable));
        OnPropertyChanged(nameof(ArcFaceAvailabilityReason));
        PeopleAlbums.RefreshRecognitionStatus();
        _ = SaveSettingsAsync();
    }

    public IAsyncRelayCommand BrowseLibraryCommand { get; }

    public IAsyncRelayCommand BrowseSourceCommand { get; }

    public IAsyncRelayCommand AnalyzeSourceCommand { get; }

    public IAsyncRelayCommand ImportSelectedCommand { get; }

    public IAsyncRelayCommand RefreshLibraryCommand { get; }

    public IRelayCommand OpenSelectedDateCommand { get; }

    public IRelayCommand RefreshDevicesCommand { get; }

    public IRelayCommand<ConnectedDeviceViewModel> InspectDeviceCommand { get; }

    public IAsyncRelayCommand<ConnectedDeviceViewModel> ImportFromDeviceCommand { get; }

    public IRelayCommand CancelCurrentTaskCommand { get; }

    public string DiskWarningText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LibraryRoot)) return "";
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(LibraryRoot));
                if (root is null) return "";
                var drive = new DriveInfo(root);
                if (drive.IsReady && drive.TotalSize > 0)
                {
                    var pct = (drive.AvailableFreeSpace * 100.0) / drive.TotalSize;
                    if (pct < 5) return $"⚠ 磁盘空间不足：{pct:F1}% ({FormatBytes(drive.AvailableFreeSpace)} 可用)";
                    if (pct < 15) return $"磁盘空间：{pct:F1}% ({FormatBytes(drive.AvailableFreeSpace)} 可用)";
                }
            }
            catch { }
            return "";
        }
    }

    public IRelayCommand ChooseBackgroundCommand { get; }

    public IRelayCommand ClearBackgroundCommand { get; }

    public IRelayCommand ChooseAppIconCommand { get; }

    public IRelayCommand ClearAppIconCommand { get; }

    public IRelayCommand ShowHomeCommand { get; }

    public IRelayCommand ShowImportCommand { get; }

    public IRelayCommand ShowPreviewCommand { get; }

    public IRelayCommand ShowFaceSearchCommand { get; }

    public IRelayCommand ShowMapPhotosCommand { get; }

    public IRelayCommand ShowCompressionCommand { get; }

    public IRelayCommand ShowWatermarkCommand { get; }

    public IRelayCommand ShowBaiduCloudCommand { get; }
    public IRelayCommand ShowQuarkCloudCommand { get; }
    public IRelayCommand DeleteSelectedFilesCommand { get; }
    public IRelayCommand ShowContestOpenCommand { get; }
    public IRelayCommand ShowContestJudgedCommand { get; }

    public IRelayCommand ShowSettingsCommand { get; }

    public IRelayCommand<string> SetPreviewCategoryCommand { get; }

    public IRelayCommand<string> SetPreviewRetouchFilterCommand { get; }

    public IRelayCommand NextPreviewPageCommand { get; }

    public IRelayCommand PreviousPreviewPageCommand { get; }

    public IRelayCommand ExpandAllPreviewDatesCommand { get; }

    public IRelayCommand CollapseAllPreviewDatesCommand { get; }

    public IRelayCommand PreviousCalendarMonthCommand { get; }

    public IRelayCommand NextCalendarMonthCommand { get; }

    public IRelayCommand<CalendarDayViewModel> SelectCalendarDayCommand { get; }

    public IAsyncRelayCommand ShowAllDatesCommand { get; }

    public IAsyncRelayCommand ResetBrowseConditionsCommand { get; }

    public bool IsBrowseConditionsExpanded
    {
        get => _isBrowseConditionsExpanded;
        set => SetProperty(ref _isBrowseConditionsExpanded, value);
    }

    public bool IsCompactBrowseLayout
    {
        get => _isCompactBrowseLayout;
        private set => SetProperty(ref _isCompactBrowseLayout, value);
    }

    public void UpdateResponsiveBrowseLayout(double width, double height)
    {
        var compact = width < 1950 || height < 900;
        if (compact == IsCompactBrowseLayout) return;
        IsCompactBrowseLayout = compact;
        if (compact) IsBrowseConditionsExpanded = false;
    }

    public string BrowseConditionsSummary =>
        $"{(SelectedDate is null ? "全部日期" : SelectedDateTitle)} · {CurrentPreviewCategory} · {PreviewRetouchFilter} · {RatingFilter}";

    public BrowseEntryMode BrowseEntryModeSetting
    {
        get => _browseEntryModeSetting;
        set
        {
            if (SetProperty(ref _browseEntryModeSetting, value)) _ = SaveSettingsAsync();
        }
    }

    public IAsyncRelayCommand CreateCustomTagCommand { get; }

    public IAsyncRelayCommand AssignCategoryToSelectedCommand { get; }

    public IAsyncRelayCommand AssignTagToSelectedCommand { get; }

    public IAsyncRelayCommand AnalyzeSelectedPhotosCommand { get; }

    public IAsyncRelayCommand AnalyzeCurrentScopeCommand { get; }

    public string LibraryRoot
    {
        get => _libraryRoot;
        set
        {
            if (SetProperty(ref _libraryRoot, value))
            {
                OnPropertyChanged(nameof(HasLibraryRoot));
                OnPropertyChanged(nameof(LibraryHealthText));
                FaceSearch.NotifyLibraryRootChanged();
                RefreshConnectedDevices();
                NotifyCommandStates();
            }
        }
    }

    public string SourceFolder
    {
        get => _sourceFolder;
        set
        {
            if (SetProperty(ref _sourceFolder, value))
            {
                NotifyCommandStates();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string SelectedDeviceTitle
    {
        get => _selectedDeviceTitle;
        set => SetProperty(ref _selectedDeviceTitle, value);
    }

    public string SelectedDeviceSummary
    {
        get => _selectedDeviceSummary;
        set => SetProperty(ref _selectedDeviceSummary, value);
    }

    public string SelectedDateTitle
    {
        get => _selectedDateTitle;
        set => SetProperty(ref _selectedDateTitle, value);
    }

    public string SelectedDatePath
    {
        get => _selectedDatePath;
        set
        {
            if (SetProperty(ref _selectedDatePath, value))
            {
                OpenSelectedDateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string SelectedFolderSize
    {
        get => _selectedFolderSize;
        set => SetProperty(ref _selectedFolderSize, value);
    }

    public string SelectedFolderPercent
    {
        get => _selectedFolderPercent;
        set => SetProperty(ref _selectedFolderPercent, value);
    }

    public string ImportReport
    {
        get => _importReport;
        set => SetProperty(ref _importReport, value);
    }

    public string ProgressLabel
    {
        get => _progressLabel;
        set => SetProperty(ref _progressLabel, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        set
        {
            if (SetProperty(ref _progressValue, value))
            {
                OnPropertyChanged(nameof(EstimatedTimeRemaining));
            }
        }
    }

    public string EstimatedTimeRemaining
    {
        get
        {
            if (_isProgressIndeterminate || _progressValue <= 0 || _progressValue >= 100)
            {
                return string.Empty;
            }

            var elapsed = DateTimeOffset.UtcNow - _operationStartedAt;
            if (elapsed <= TimeSpan.Zero)
            {
                return string.Empty;
            }

            var total = elapsed.TotalSeconds / (_progressValue / 100.0);
            var remaining = TimeSpan.FromSeconds(total - elapsed.TotalSeconds);
            return remaining switch
            {
                { TotalMinutes: >= 1 } => $"剩余约 {remaining.TotalMinutes:0} 分钟",
                { TotalSeconds: >= 5 } => $"剩余约 {remaining.TotalSeconds:0} 秒",
                _ => "即将完成"
            };
        }
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NotifyCommandStates();
                CancelCurrentTaskCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public double ThumbnailSize
    {
        get => _thumbnailSize;
        set
        {
            var clamped = Math.Clamp(value, 96, 260);
            if (SetProperty(ref _thumbnailSize, clamped))
            {
                _ = SaveSettingsAsync();
            }
        }
    }

    public double GlassIntensity
    {
        get => _glassIntensity;
        set
        {
            var clamped = Math.Clamp(value, 0.25, 0.95);
            if (SetProperty(ref _glassIntensity, clamped))
            {
                OnPropertyChanged(nameof(PanelOpacity));
                OnPropertyChanged(nameof(BackgroundOverlayOpacity));
                _ = SaveSettingsAsync();
            }
        }
    }

    public double PanelOpacity => Math.Clamp(0.46 + (GlassIntensity * 0.42), 0.56, 0.86);

    public double BackgroundOverlayOpacity => Math.Clamp(0.18 + (GlassIntensity * 0.45), 0.29, 0.61);

    public string EffectiveBackgroundPath => BackgroundMode switch
    {
        "自定义图片" when File.Exists(CustomBackgroundPath) => CustomBackgroundPath,
        "跟随 Windows 壁纸" => _windowsWallpaperPath,
        _ => string.Empty
    };

    public bool HasEffectiveBackground => !string.IsNullOrWhiteSpace(EffectiveBackgroundPath);

    public string BackgroundMode
    {
        get => _backgroundMode;
        set
        {
            if (SetProperty(ref _backgroundMode, value))
            {
                if (value == "跟随 Windows 壁纸") RefreshWindowsWallpaper();
                OnPropertyChanged(nameof(UsesCustomBackground));
                NotifyEffectiveBackgroundChanged();
                _ = SaveSettingsAsync();
            }
        }
    }

    public string CustomBackgroundPath
    {
        get => _customBackgroundPath;
        set
        {
            if (SetProperty(ref _customBackgroundPath, value))
            {
                OnPropertyChanged(nameof(BackgroundImage));
                OnPropertyChanged(nameof(UsesCustomBackground));
                NotifyEffectiveBackgroundChanged();
                _ = SaveSettingsAsync();
            }
        }
    }

    public string BackgroundImageLayout
    {
        get => _backgroundImageLayout;
        set
        {
            if (SetProperty(ref _backgroundImageLayout, value))
            {
                OnPropertyChanged(nameof(BackgroundImageStretch));
                OnPropertyChanged(nameof(BackgroundImageHorizontalAlignment));
                OnPropertyChanged(nameof(BackgroundImageVerticalAlignment));
                _ = SaveSettingsAsync();
            }
        }
    }

    public ImageSource? BackgroundImage => LoadBackgroundImage(CustomBackgroundPath);

    public bool UsesCustomBackground => BackgroundMode == "自定义图片" && File.Exists(CustomBackgroundPath);

    public ImageSource? EffectiveBackgroundImage => LoadBackgroundImage(EffectiveBackgroundPath);

    public void RefreshWindowsWallpaper()
    {
        var path = _wallpaperService.GetCurrentWallpaperPath() ?? string.Empty;
        if (string.Equals(_windowsWallpaperPath, path, StringComparison.OrdinalIgnoreCase)) return;
        _windowsWallpaperPath = path;
        NotifyEffectiveBackgroundChanged();
    }

    private void NotifyEffectiveBackgroundChanged()
    {
        OnPropertyChanged(nameof(EffectiveBackgroundPath));
        OnPropertyChanged(nameof(EffectiveBackgroundImage));
        OnPropertyChanged(nameof(HasEffectiveBackground));
    }

    public Stretch BackgroundImageStretch => BackgroundImageLayout switch
    {
        "居中" => Stretch.None,
        "保持长宽比" => Stretch.Uniform,
        "拉伸" => Stretch.Fill,
        _ => Stretch.UniformToFill
    };

    public System.Windows.HorizontalAlignment BackgroundImageHorizontalAlignment => System.Windows.HorizontalAlignment.Center;

    public System.Windows.VerticalAlignment BackgroundImageVerticalAlignment => System.Windows.VerticalAlignment.Center;

    public string CustomAppIconPath
    {
        get => _customAppIconPath;
        set
        {
            if (SetProperty(ref _customAppIconPath, value))
            {
                OnPropertyChanged(nameof(AppIconImage));
                OnPropertyChanged(nameof(HasCustomAppIcon));
                _ = SaveSettingsAsync();
            }
        }
    }

    public ImageSource? AppIconImage => LoadImage(CustomAppIconPath, 256);

    public bool HasCustomAppIcon => File.Exists(CustomAppIconPath);

    public bool LaunchAtStartup
    {
        get => _launchAtStartup;
        set
        {
            if (!_changingStartupRegistration && SetProperty(ref _launchAtStartup, value))
            {
                _ = ApplyStartupRegistrationAsync(value);
            }
        }
    }

    public bool RestoreWindowState { get => _restoreWindowState; set { if (SetProperty(ref _restoreWindowState, value)) _ = SaveSettingsAsync(); } }
    public double? WindowLeft => _windowLeft;
    public double? WindowTop => _windowTop;
    public string SavedWindowState => _savedWindowState;
    public string WindowStateSummary => RestoreWindowState
        ? $"将恢复 {_windowWidth:0} × {_windowHeight:0}，状态：{_savedWindowState}"
        : "下次启动使用安全的默认窗口位置";

    public double WindowWidth
    {
        get => _windowWidth;
        set
        {
            var clamped = Math.Clamp(value, 1180, 2600);
            if (SetProperty(ref _windowWidth, clamped))
            {
                _ = SaveSettingsAsync();
            }
        }
    }

    public double WindowHeight
    {
        get => _windowHeight;
        set
        {
            var clamped = Math.Clamp(value, 760, 1600);
            if (SetProperty(ref _windowHeight, clamped))
            {
                _ = SaveSettingsAsync();
            }
        }
    }

    public bool EnablePersonRecognition
    {
        get => _enablePersonRecognition;
        set => SetProperty(ref _enablePersonRecognition, value);
    }

    public string CurrentPreviewCategory
    {
        get => _currentPreviewCategory;
        set
        {
            if (SetProperty(ref _currentPreviewCategory, value))
            {
                RefreshFilteredCache(resetPage: true);
                OnPropertyChanged(nameof(BrowseConditionsSummary));
            }
        }
    }

    private void RefreshFilteredCache(bool resetPage = false)
    {
        var categoryItems = CurrentPreviewCategory == "全部"
            ? PreviewFiles
            : PreviewFiles.Where(file => string.Equals(file.Category, CurrentPreviewCategory, StringComparison.OrdinalIgnoreCase));
        _filteredCache = ApplyFilters(categoryItems).ToList();
        if (resetPage)
        {
            _previewPage = 0;
        }

        RebuildVisiblePreviewPage();
        OnPropertyChanged(nameof(FilteredPreviewFiles));
        NotifyPreviewCountsChanged();
    }

    private int _previewSort;
    public int PreviewSortMode
    {
        get => _previewSort;
        set
        {
            if (SetProperty(ref _previewSort, value))
            {
                RefreshFilteredCache(resetPage: true);
                OnPropertyChanged(nameof(BrowseConditionsSummary));
            }
        }
    }

    private string _previewSearchText = string.Empty;
    public string PreviewSearchText
    {
        get => _previewSearchText;
        set
        {
            if (SetProperty(ref _previewSearchText, value ?? string.Empty))
            {
                RefreshFilteredCache(resetPage: true);
                OnPropertyChanged(nameof(BrowseConditionsSummary));
            }
        }
    }

    private string _previewRetouchFilter = "全部";
    public string PreviewRetouchFilter
    {
        get => _previewRetouchFilter;
        set
        {
            if (SetProperty(ref _previewRetouchFilter, value))
            {
                RefreshFilteredCache(resetPage: true);
                OnPropertyChanged(nameof(BrowseConditionsSummary));
            }
        }
    }

    private string _ratingFilter = "全部评分";
    public string RatingFilter
    {
        get => _ratingFilter;
        set
        {
            if (SetProperty(ref _ratingFilter, value ?? "全部评分"))
            {
                RefreshFilteredCache(resetPage: true);
                OnPropertyChanged(nameof(BrowseConditionsSummary));
            }
        }
    }

    public IEnumerable<PreviewFileViewModel> FilteredPreviewFiles => _filteredCache;

    private string _newCustomTagName = string.Empty;
    public string NewCustomTagName
    {
        get => _newCustomTagName;
        set => SetProperty(ref _newCustomTagName, value ?? string.Empty);
    }

    private string _selectedManualCategory = "待分类";
    public string SelectedManualCategory
    {
        get => _selectedManualCategory;
        set => SetProperty(ref _selectedManualCategory, value ?? "待分类");
    }

    private string? _selectedCustomTag;
    public string? SelectedCustomTag
    {
        get => _selectedCustomTag;
        set => SetProperty(ref _selectedCustomTag, value);
    }

    public IReadOnlyList<string> SmartCategoryFilters => ["全部智能类别", .. TagManager.AvailableCategories];

    private string _smartCategoryFilter = "全部智能类别";
    public string SmartCategoryFilter
    {
        get => _smartCategoryFilter;
        set
        {
            if (SetProperty(ref _smartCategoryFilter, value ?? "全部智能类别"))
                RefreshFilteredCache(resetPage: true);
        }
    }

    private List<PreviewFileViewModel> _filteredCache = [];

    private IEnumerable<PreviewFileViewModel> ApplyFilters(IEnumerable<PreviewFileViewModel> source)
    {
        var result = source;

        // Retouch filter
        if (PreviewRetouchFilter == "已修")
            result = result.Where(f => f.IsRetouched);
        else if (PreviewRetouchFilter == "未修")
            result = result.Where(f => !f.IsRetouched);

        // Text search
        if (!string.IsNullOrWhiteSpace(PreviewSearchText))
        {
            var search = PreviewSearchText.Trim();
            result = result.Where(f => f.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || f.Extension.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (SmartCategoryFilter != "全部智能类别")
            result = result.Where(file => string.Equals(file.SmartCategory, SmartCategoryFilter, StringComparison.OrdinalIgnoreCase));
        if (RatingFilter == "未评分") result = result.Where(file => file.Rating == 0);
        else if (RatingFilter.Length > 0 && char.IsDigit(RatingFilter[0]))
            result = result.Where(file => file.Rating == RatingFilter[0] - '0');
        if (PeopleAlbums.SelectedAlbum is { } person)
            result = result.Where(file => person.PhotoPaths.Contains(Path.GetFullPath(file.FullPath)));

        // Sort
        result = PreviewSortMode switch
        {
            1 => result.OrderBy(f => f.Name),                          // Name
            2 => result.OrderByDescending(f => f.Name),                // Name ↓
            3 => result.OrderBy(f => new FileInfo(f.FullPath).Length), // Size
            4 => result.OrderByDescending(f => new FileInfo(f.FullPath).Length), // Size ↓
            5 => result.OrderBy(f => f.Category),                      // Category
            6 => result.OrderByDescending(f => f.Category),            // Category ↓
            7 => result.OrderByDescending(f => f.Rating).ThenBy(f => f.Name),
            8 => result.OrderBy(f => f.Rating).ThenBy(f => f.Name),
            _ => result // Default: unsorted (by discovery order)
        };

        return result;
    }

    public int FilteredPreviewCount => _filteredCache.Count;

    public bool HasPreviousPreviewPage => _previewPage > 0;

    public bool HasNextPreviewPage => (_previewPage + 1) * PreviewLoadingPolicy.VisiblePageSize < FilteredPreviewCount;

    public string PreviewPageText
    {
        get
        {
            if (FilteredPreviewCount == 0)
            {
                return "暂无媒体";
            }

            var first = _previewPage * PreviewLoadingPolicy.VisiblePageSize + 1;
            var last = Math.Min(first + VisiblePreviewFiles.Count - 1, FilteredPreviewCount);
            return $"显示 {first:N0}–{last:N0} / {FilteredPreviewCount:N0}";
        }
    }

    public string PreviewSummaryText =>
        CurrentPreviewCategory == "全部"
            ? $"当前范围：{SelectedDateTitle} · {FilteredPreviewCount:N0} 个媒体文件"
            : $"当前范围：{SelectedDateTitle} · {CurrentPreviewCategory} · {FilteredPreviewCount:N0} 个媒体文件";

    public TransferMode SelectedTransferMode
    {
        get => _selectedTransferMode;
        set => SetProperty(ref _selectedTransferMode, value);
    }

    public string TargetDateText
    {
        get => _targetDateText;
        set => SetProperty(ref _targetDateText, value);
    }

    public string ImportActionHint
    {
        get => _importActionHint;
        set => SetProperty(ref _importActionHint, value);
    }

    public LibraryDateNode? SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (value?.Date is null)
            {
                return;
            }

            if (SetProperty(ref _selectedDate, value))
            {
                _calendarSelectedDate = new DateOnly(value.Date.Value.Year, value.Date.Value.Month, value.Date.Value.Day);
                _calendarDisplayMonth = new DateTime(value.Date.Value.Year, value.Date.Value.Month, 1);
                OnPropertyChanged(nameof(CalendarMonthTitle));
                RebuildCalendarDays();
                _ = SelectDateAsync(value);
            }
        }
    }

    public bool HasLibraryRoot => !string.IsNullOrWhiteSpace(LibraryRoot);

    public string LibraryHealthText => HasLibraryRoot ? "已连接照片库" : "未选择照片库";

    public int DiscoveredDateCount => FlattenDateNodes(LibraryDates).Count;

    public string CalendarMonthTitle => $"{_calendarDisplayMonth:yyyy年 M月}";

    public async Task InitializeAsync()
    {
        var settings = await _settingsStore.LoadAsync().ConfigureAwait(true);
        IsOnboardingVisible = !settings.HasCompletedOnboarding;
        ResetNavigationItems(settings.NavigationOrder);
        NavigationDisplayMode = settings.NavigationDisplayMode;
        LibraryRoot = settings.LibraryRoot ?? string.Empty;
        _defaultThumbnailSize = Math.Clamp(settings.DefaultThumbnailSize, 96, 260);
        _thumbnailSize = _defaultThumbnailSize;
        OnPropertyChanged(nameof(DefaultThumbnailSize));
        OnPropertyChanged(nameof(ThumbnailSize));
        GlassIntensity = settings.GlassIntensity;
        BackgroundMode = settings.BackgroundMode;
        BackgroundImageLayout = string.IsNullOrWhiteSpace(settings.BackgroundImageLayout) ? "填充" : settings.BackgroundImageLayout;
        PhotoAnalysis.SelectedEngine = string.IsNullOrWhiteSpace(settings.ClassificationEngine)
            ? PhotoClassifierFactory.OnnxMode
            : settings.ClassificationEngine;
        MobileClipRuntimeOptions.DevicePreference = string.Equals(settings.InferenceDevice, "cpu", StringComparison.OrdinalIgnoreCase)
            || string.Equals(settings.InferenceDevice, "CPU", StringComparison.OrdinalIgnoreCase) ? "CPU" : "自动（NVIDIA 优先）";
        MobileClipRuntimeOptions.MaximumLabels = Math.Clamp(settings.SemanticMaxLabels, 1, 5);
        MobileClipRuntimeOptions.SimilarityWindow = Math.Clamp(settings.SemanticSimilarityWindow, .02, .30);
        FaceRecognitionRuntimeOptions.Current = new FaceRecognitionOptions
        {
            Engine = settings.FaceRecognitionEngine,
            Profile = settings.FaceRecognitionProfile,
            DetectorModelPath = settings.ArcFaceDetectorModelPath,
            RecognizerModelPath = settings.ArcFaceRecognizerModelPath,
            ModelLicenseConfirmed = settings.ArcFaceModelLicenseConfirmed,
            ModelLicenseDescription = settings.ArcFaceModelLicenseDescription,
            MatchThreshold = Math.Clamp(settings.ArcFaceMatchThreshold, .2, .9)
        };
        _defaultRatingFilter = RatingFilters.Contains(settings.DefaultRatingFilter) ? settings.DefaultRatingFilter : "全部评分";
        _defaultPreviewSort = settings.DefaultPreviewSort is >= 0 and <= 8 ? settings.DefaultPreviewSort : 0;
        RatingFilter = _defaultRatingFilter;
        PreviewSortMode = _defaultPreviewSort;
        OnPropertyChanged(nameof(DefaultRatingFilter));
        OnPropertyChanged(nameof(DefaultPreviewSort));
        OnPropertyChanged(nameof(InferenceDevice));
        OnPropertyChanged(nameof(SemanticMaxLabels));
        OnPropertyChanged(nameof(SemanticSimilarityWindow));
        OnPropertyChanged(nameof(FaceRecognitionEngine));
        OnPropertyChanged(nameof(FaceRecognitionProfile));
        OnPropertyChanged(nameof(ArcFaceDetectorModelPath));
        OnPropertyChanged(nameof(ArcFaceRecognizerModelPath));
        OnPropertyChanged(nameof(ArcFaceModelLicenseConfirmed));
        OnPropertyChanged(nameof(ArcFaceModelLicenseDescription));
        OnPropertyChanged(nameof(ArcFaceMatchThreshold));
        OnPropertyChanged(nameof(IsArcFaceAvailable));
        OnPropertyChanged(nameof(ArcFaceAvailabilityReason));
        CustomBackgroundPath = PersistExistingAsset(settings.CustomBackgroundPath, "background");
        CustomAppIconPath = PersistExistingAsset(settings.AppIconPath, "avatar");
        try { _launchAtStartup = _startupRegistrationService.IsEnabled(); }
        catch (Exception ex) { _launchAtStartup = settings.LaunchAtStartup; StatusMessage = $"读取开机自启动状态失败：{ex.Message}"; }
        OnPropertyChanged(nameof(LaunchAtStartup));
        WindowWidth = settings.WindowWidth;
        WindowHeight = settings.WindowHeight;
        _windowLeft = settings.WindowLeft;
        _windowTop = settings.WindowTop;
        _savedWindowState = settings.WindowState;
        _restoreWindowState = settings.RestoreWindowState;
        OnPropertyChanged(nameof(RestoreWindowState));
        OnPropertyChanged(nameof(WindowStateSummary));
        EnablePersonRecognition = false;
        BrowseEntryModeSetting = Enum.TryParse<BrowseEntryMode>(settings.BrowseEntryMode, out var browseMode)
            ? browseMode
            : BrowseEntryMode.SessionRestore;
        _persistedBrowseSnapshot = settings.BrowseSnapshot;
        BaiduAppKey = settings.BaiduAppKey ?? string.Empty;
        QuarkClientPath = settings.QuarkClientPath ?? string.Empty;
        RefreshConnectedDevices();
        await RefreshCloudConnectionAsync().ConfigureAwait(true);
        await TagManager.InitializeAsync().ConfigureAwait(true);
        PeopleAlbums.RefreshRecognitionStatus();
        await PeopleAlbums.InitializeAsync().ConfigureAwait(true);

        if (!string.IsNullOrWhiteSpace(LibraryRoot) && Directory.Exists(LibraryRoot))
        {
            ResetPreviewState("浏览未加载");
            StatusMessage = $"正在预读取照片库：{LibraryRoot}";
            // Kick off the scan in the background so the UI stays responsive
            // and the user doesn't need to click anything to start.
            _ = RefreshLibraryAsync().ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(LibraryRoot))
        {
            ResetPreviewState("浏览未加载");
            StatusMessage = "照片库路径无效。请在主界面设置正确的库根目录。";
        }

        _isInitialized = true;
        await SaveSettingsAsync().ConfigureAwait(true);
    }

    public void AdjustThumbnailSize(bool larger)
    {
        ThumbnailSize += larger ? 22 : -22;
        StatusMessage = $"预览大小：{ThumbnailSize:0}px";
    }

    private void SyncCalendarWithLibrary()
    {
        var dates = FlattenDateNodes(LibraryDates)
            .Where(node => node.Date is not null)
            .ToArray();
        if (dates.Length > 0 && _calendarSelectedDate is null)
        {
            var latest = dates
                .Select(node => node.Date!.Value)
                .OrderBy(date => date.Year)
                .ThenBy(date => date.Month)
                .ThenBy(date => date.Day)
                .Last();
            _calendarDisplayMonth = new DateTime(latest.Year, latest.Month, 1);
            OnPropertyChanged(nameof(CalendarMonthTitle));
        }
        RebuildCalendarDays();
    }

    private void ChangeCalendarMonth(int offset)
    {
        _calendarDisplayMonth = _calendarDisplayMonth.AddMonths(offset);
        OnPropertyChanged(nameof(CalendarMonthTitle));
        RebuildCalendarDays();
    }

    private void SelectCalendarDay(CalendarDayViewModel? day)
    {
        if (day is null || !day.IsAvailable || day.Date is null) return;
        var date = day.Date.Value;
        var node = FlattenDateNodes(LibraryDates).FirstOrDefault(candidate =>
            candidate.Date is { } value &&
            value.Year == date.Year && value.Month == date.Month && value.Day == date.Day);
        if (node is not null)
        {
            SelectedDate = node;
        }
    }

    private void RebuildCalendarDays()
    {
        var availableNodes = FlattenDateNodes(LibraryDates)
            .Where(node => node.Date is not null)
            .GroupBy(node => new DateOnly(node.Date!.Value.Year, node.Date.Value.Month, node.Date.Value.Day))
            .ToDictionary(group => group.Key, group => group.First());
        if (PeopleAlbums.SelectedAlbum is { } person)
        {
            availableNodes = availableNodes
                .Where(entry => person.PhotoPaths.Any(path => IsPathInside(path, entry.Value.FullPath)))
                .ToDictionary(entry => entry.Key, entry => entry.Value);
        }
        var selected = _calendarSelectedDate;
        var days = BuildCalendarDays(_calendarDisplayMonth.Year, _calendarDisplayMonth.Month, availableNodes.Keys, selected);
        CalendarDays.Clear();
        foreach (var day in days) CalendarDays.Add(day);
    }

    private static bool IsPathInside(string filePath, string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(directoryPath)) return false;
        var directory = Path.GetFullPath(directoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(filePath).StartsWith(directory, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<CalendarDayViewModel> BuildCalendarDays(
        int year,
        int month,
        IEnumerable<DateOnly> availableDates,
        DateOnly? selectedDate = null)
    {
        var available = new HashSet<DateOnly>(availableDates);
        var first = new DateOnly(year, month, 1);
        var mondayOffset = ((int)first.DayOfWeek + 6) % 7;
        var gridStart = first.AddDays(-mondayOffset);
        return Enumerable.Range(0, 42)
            .Select(index =>
            {
                var date = gridStart.AddDays(index);
                var inCurrentMonth = date.Month == month && date.Year == year;
                return new CalendarDayViewModel(
                    date,
                    date.Day.ToString(CultureInfo.InvariantCulture),
                    inCurrentMonth && available.Contains(date),
                    inCurrentMonth,
                    selectedDate == date);
            })
            .ToArray();
    }

    private async Task ShowPreviewAsync()
    {
        CurrentPage = "Preview";
        if (!HasLibraryRoot)
        {
            StatusMessage = "还没有选择照片库根目录。";
            return;
        }

        if (_previewHasLoaded || IsBusy)
        {
            if (_previewHasLoaded) await ApplyBrowseEntryPolicyAsync().ConfigureAwait(true);
            return;
        }

        StatusMessage = "进入预览页，开始按需读取照片缩略图。";
        await RefreshLibraryAsync().ConfigureAwait(true);
        await ApplyBrowseEntryPolicyAsync().ConfigureAwait(true);
    }

    private async Task ShowAllDatesAsync()
    {
        if (!HasLibraryRoot || IsBusy) return;
        _calendarSelectedDate = null;
        await RefreshLibraryAsync().ConfigureAwait(true);
        RebuildCalendarDays();
        StatusMessage = "已返回全部日期。";
        OnPropertyChanged(nameof(BrowseConditionsSummary));
    }

    private async Task ResetBrowseConditionsAsync()
    {
        CurrentPreviewCategory = "全部";
        PreviewSearchText = string.Empty;
        PreviewRetouchFilter = "全部";
        RatingFilter = "全部评分";
        PreviewSortMode = 0;
        SmartCategoryFilter = "全部智能类别";

        if (HasLibraryRoot && !IsBusy)
        {
            await ShowAllDatesAsync().ConfigureAwait(true);
        }
        else
        {
            _calendarSelectedDate = null;
            SetProperty(ref _selectedDate, null, nameof(SelectedDate));
            RebuildCalendarDays();
        }

        IsBrowseConditionsExpanded = false;
        OnPropertyChanged(nameof(BrowseConditionsSummary));
        StatusMessage = "浏览条件已重置。";
    }

    private BrowseSnapshot CaptureBrowseSnapshot()
    {
        var dateKey = SelectedDate?.Date is { } date
            ? $"{date.Year:D4}-{date.Month:D2}-{date.Day:D2}"
            : null;
        return new BrowseSnapshot(dateKey, CurrentPreviewCategory, PreviewSearchText, PreviewSortMode, ThumbnailSize, SelectedPreviewFile?.FullPath);
    }

    private async Task ApplyBrowseEntryPolicyAsync()
    {
        var resolved = _browseStatePolicy.ResolveOnEntry(
            BrowseEntryModeSetting,
            _persistedBrowseSnapshot,
            _sessionBrowseSnapshot,
            new BrowseDefaults(DefaultRatingFilter, DefaultPreviewSort, DefaultThumbnailSize));
        RatingFilter = DefaultRatingFilter;
        CurrentPreviewCategory = string.IsNullOrWhiteSpace(resolved.Category) ? "全部" : resolved.Category;
        PreviewSearchText = resolved.SearchText ?? string.Empty;
        PreviewSortMode = resolved.SortIndex;
        ThumbnailSize = resolved.ThumbnailSize;
        if (resolved.DateKey is null)
        {
            if (SelectedDate is not null) await ShowAllDatesAsync().ConfigureAwait(true);
            return;
        }

        if (DateOnly.TryParseExact(resolved.DateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var target))
        {
            var node = FlattenDateNodes(LibraryDates).FirstOrDefault(candidate => candidate.Date is { } date
                && date.Year == target.Year && date.Month == target.Month && date.Day == target.Day);
            if (node is not null) SelectedDate = node;
            else if (SelectedDate is not null) await ShowAllDatesAsync().ConfigureAwait(true);
        }
    }

    private void ResetPreviewState(string title)
    {
        _previewHasLoaded = false;
        ++_previewScanVersion;
        SetProperty(ref _selectedDate, null, nameof(SelectedDate));
        LibraryDates.Clear();
        OnPropertyChanged(nameof(DiscoveredDateCount));
        CalendarDays.Clear();
        _calendarSelectedDate = null;
        CategorySummaries.Clear();
        PreviewFiles.Clear();
        VisiblePreviewFiles.Clear();
        VisiblePreviewSections.Clear();
        HomePreviewFiles.Clear();
        CancelPreviewThumbnailLoading();
        _previewPage = 0;
        RetouchedFiles.Clear();
        SelectedDateTitle = title;
        SelectedDatePath = LibraryRoot;
        SelectedFolderSize = "--";
        SelectedFolderPercent = "--";
        ProgressValue = 0;
        ProgressLabel = "浏览未加载";
        RefreshFilteredCache();
        OnPropertyChanged(nameof(FilteredPreviewFiles));
        NotifyPreviewCountsChanged();
    }

    private async Task RebuildRetouchTrackingAsync()
    {
        // Run the heavy IO on a background thread so the UI stays responsive.
        var before = PreviewFiles.ToArray();
        var retouchMap = await Task.Run(() =>
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var standalone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Group by date directory so each date builds exactly one consistent index.
            var byRetouchDir = before
                .GroupBy(f =>
                {
                    var dir = Path.GetDirectoryName(f.FullPath);
                    return dir is not null ? Path.GetDirectoryName(dir) : null;
                })
                .Where(g => !string.IsNullOrWhiteSpace(g.Key));
            foreach (var group in byRetouchDir)
            {
                if (group.Key is null) continue;
                var snapshot = _retouchedMediaIndex.Build(
                    group.Key,
                    group.Where(IsRawOrJpegPreview).Select(file => file.FullPath).ToArray());
                foreach (var pair in snapshot.RetouchedByOriginal)
                {
                    map[pair.Key] = pair.Value;
                }
                standalone.UnionWith(snapshot.StandaloneRetouchedFiles);
            }
            return (Map: map, Standalone: standalone);
        }).ConfigureAwait(true);

        RetouchedFiles.Clear();
        for (var i = 0; i < PreviewFiles.Count; i++)
        {
            var file = PreviewFiles[i];
            var previousPreviewPath = file.PreviewPath;
            var isStandaloneOutput = retouchMap.Standalone.Contains(file.FullPath) ||
                                     file.Category.Equals("修后", StringComparison.OrdinalIgnoreCase);
            var retouched = retouchMap.Map.TryGetValue(file.FullPath, out var retouchedPath) || isStandaloneOutput;
            if (isStandaloneOutput) retouchedPath = file.FullPath;
            file.RetouchedPath = retouchedPath;
            file.IsRetouched = retouched;
            if (!string.Equals(previousPreviewPath, file.PreviewPath, StringComparison.OrdinalIgnoreCase))
            {
                file.Thumbnail = null;
            }
            if (retouched)
            {
                RetouchedFiles.Add(file);
            }
        }

        OnPropertyChanged(nameof(HasRetouchedFiles));
        OnPropertyChanged(nameof(RetouchedGroupCount));
        OnPropertyChanged(nameof(TotalPhotoGroupCount));
        RecalcDateNodeStats();
        RefreshFilteredCache();
        if (IsHomePage)
        {
            StartPreviewThumbnailLoading(HomePreviewFiles);
        }
    }

    private static bool IsRawOrJpegPreview(PreviewFileViewModel file) =>
        file.Category.Equals("RAW生图", StringComparison.OrdinalIgnoreCase) ||
        file.Category.Equals("JPG生图", StringComparison.OrdinalIgnoreCase);

    private static bool IsRetouchOutputFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".psd", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".psb", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeRetouchedStem(string stem)
    {
        if (string.IsNullOrWhiteSpace(stem)) return string.Empty;
        return Regex.Replace(
            stem.Trim(),
            "(?:-恢复的|_ExHiRes|_noeffect)$",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static string? SelectPreferredRetouchedOutput(IEnumerable<string> paths, string sourceStem)
    {
        return paths
            .Where(IsRetouchOutputFile)
            .Where(path => string.Equals(
                NormalizeRetouchedStem(Path.GetFileNameWithoutExtension(path)),
                sourceStem,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(path => RetouchOutputScore(path, sourceStem))
            .ThenByDescending(path =>
            {
                try { return File.GetLastWriteTimeUtc(path); }
                catch { return DateTime.MinValue; }
            })
            .FirstOrDefault();
    }

    private static int RetouchOutputScore(string path, string sourceStem)
    {
        var extension = Path.GetExtension(path);
        var raster = extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase) ||
                     extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
        var exactName = Path.GetFileNameWithoutExtension(path).Equals(sourceStem, StringComparison.OrdinalIgnoreCase);
        return (raster ? 100 : 10) + (exactName ? 20 : 0);
    }

    private static int CountPhotoGroups(IEnumerable<PreviewFileViewModel> files) => files
        .Where(IsRawOrJpegPreview)
        .Select(file =>
        {
            var categoryDirectory = Path.GetDirectoryName(file.FullPath);
            var dateDirectory = string.IsNullOrWhiteSpace(categoryDirectory)
                ? string.Empty
                : Directory.GetParent(categoryDirectory)?.FullName ?? categoryDirectory;
            return $"{dateDirectory}|{Path.GetFileNameWithoutExtension(file.FullPath)}";
        })
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    private void RecalcDateNodeStats()
    {
        var flatNodes = FlattenDateNodes(LibraryDates);
        foreach (var node in flatNodes)
        {
            if (node.Date is null || !Directory.Exists(node.FullPath)) continue;
            var total = CountPhotoGroups(PreviewFiles.Where(f => f.FullPath.StartsWith(node.FullPath, StringComparison.OrdinalIgnoreCase)));
            var retouched = CountPhotoGroups(RetouchedFiles.Where(f => f.FullPath.StartsWith(node.FullPath, StringComparison.OrdinalIgnoreCase)));
            node.TotalFiles = total;
            node.RetouchedFiles = retouched;
        }
    }

    private CancellationTokenSource BeginCancelableTask(ActiveTaskKind taskKind)
    {
        _activeTaskCancellation?.Cancel();
        _activeTaskCancellation?.Dispose();
        _activeTaskCancellation = new CancellationTokenSource();
        _activeTaskKind = taskKind;
        _operationStartedAt = DateTimeOffset.UtcNow;
        CancelCurrentTaskCommand.NotifyCanExecuteChanged();
        return _activeTaskCancellation;
    }

    private void EndCancelableTask(CancellationTokenSource cancellation)
    {
        if (ReferenceEquals(_activeTaskCancellation, cancellation))
        {
            _activeTaskCancellation.Dispose();
            _activeTaskCancellation = null;
            _activeTaskKind = ActiveTaskKind.None;
            CancelCurrentTaskCommand.NotifyCanExecuteChanged();
        }
    }

    private void CancelCurrentTask()
    {
        CancelPreviewThumbnailLoading();
        if (_activeTaskCancellation is null || _activeTaskCancellation.IsCancellationRequested)
        {
            return;
        }

        _activeTaskCancellation.Cancel();
        CancelCurrentTaskCommand.NotifyCanExecuteChanged();
        ++_previewScanVersion;
        IsProgressIndeterminate = false;
        ProgressValue = 0;
        ProgressLabel = "已请求停止";
        ImportReport = "正在停止当前任务…如果刚好在读取系统目录，可能需要几秒释放。";
        ImportActionHint = _activeTaskKind == ActiveTaskKind.Import
            ? "已请求停止。传输会在当前文件的安全点停止，避免留下损坏文件。"
            : "已请求停止。后台读取正在退出，完成后可立即重新选择来源。";
        StatusMessage = "已请求停止当前任务，会在当前文件安全点停止。";

    }

    public async Task ImportLooseFilesAsync(IEnumerable<string> paths, MediaCategory category)
    {
        if (!HasLibraryRoot || _selectedDate is null)
        {
            System.Windows.MessageBox.Show("请先选择照片库和目标日期。", "Hanabe", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var files = paths.Where(File.Exists).ToArray();
        if (files.Length == 0)
        {
            return;
        }

        var groups = files
            .Select(path => new SourceMediaFile(path, new FileInfo(path).Length, new FileInfo(path).LastWriteTimeUtc))
            .Select(file => new MediaGroup(Path.GetFileNameWithoutExtension(file.FullPath), category, file, Array.Empty<SourceMediaFile>()))
            .ToArray();

        var date = _selectedDate.Date!.Value;
        var items = groups
            .Select((group, index) => new ImportPreviewItemViewModel(group, category, CategoryChoices, false, date, TryLoadThumbnail(group.Primary.FullPath), string.Empty, index + 1))
            .ToArray();
        await RunImportAsync(items, deleteSourcesAfterVerify: false).ConfigureAwait(true);
    }

    public async Task AutoImportDroppedSourceAsync(IEnumerable<string> paths)
    {
        if (!HasLibraryRoot)
        {
            System.Windows.MessageBox.Show("请先选择 Hanabe 拍照库根目录。", "Hanabe", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (IsBusy)
        {
            StatusMessage = "当前还有任务在进行，稍等完成后再拖入。";
            return;
        }

        var droppedPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (droppedPaths.Length == 0)
        {
            return;
        }

        SourceFolder = ResolveDroppedSourceDisplayPath(droppedPaths);
        StatusMessage = "已接收拖拽内容，开始自动分析并导入。";

        var sourceFiles = await AnalyzeSourcePathsAsync(droppedPaths, SourceFolder).ConfigureAwait(true);
        if (sourceFiles.Length == 0)
        {
            ImportReport = "拖入内容里没有找到可导入的照片/视频文件。";
            StatusMessage = "拖拽导入已停止：没有可导入文件。";
            return;
        }

        var blockingItems = ImportItems
            .Where(item => item.IsSelected && (item.SelectedCategory.Category == MediaCategory.Unconfirmed || item.TargetDate is null))
            .Select(item => item.Name)
            .ToArray();

        if (blockingItems.Length > 0)
        {
            StatusMessage = "已完成拖拽分析，但有日期或分类需要你确认，我先不乱导入。";
            ImportReport += Environment.NewLine + "需要确认后再导入：" +
                            $"待确认项目 {blockingItems.Length} 个";
            return;
        }

        await ImportSelectedAsync().ConfigureAwait(true);
    }

    private async Task BrowseLibraryAsync()
    {
        var selected = PickFolder("选择 Hanabe 拍照库根目录", LibraryRoot);
        if (selected is null)
        {
            return;
        }

        LibraryRoot = selected;
        await SaveSettingsAsync().ConfigureAwait(true);
        ResetPreviewState("预览未加载");
        StatusMessage = "根目录已保存。不会自动加载照片；进入预览页或点刷新时才读取缩略图。";
    }

    private Task BrowseSourceAsync()
    {
        var selected = PickFolder("选择相机来源文件夹", SourceFolder);
        if (selected is null)
        {
            return Task.CompletedTask;
        }

        SourceFolder = selected;
        _sourceScanPaths = [selected];
        CancelImportThumbnailLoading();
        ImportItems.Clear();
        ImportSections.Clear();
        TargetDateText = "等待分析日期";
        ImportReport = "来源已选择，尚未开始分析。";
        ImportActionHint = "先决定是否启用本地 AI 人物识别，然后点击“开始分析与分类”。";
        StatusMessage = "来源文件夹已选择，等待你开始分析。";
        ProgressValue = 0;
        ProgressLabel = "等待开始";
        NotifyCommandStates();
        return Task.CompletedTask;
    }

    private async Task AnalyzeSourceAsync()
    {
        if (!Directory.Exists(SourceFolder))
        {
            StatusMessage = "来源文件夹不存在。";
            return;
        }

        var paths = _sourceScanPaths.Count > 0 ? _sourceScanPaths : [SourceFolder];
        await AnalyzeSourcePathsAsync(paths, SourceFolder).ConfigureAwait(true);
    }

    private async Task<SourceMediaFile[]> AnalyzeSourcePathsAsync(IReadOnlyList<string> paths, string dateHintPath)
    {
        CancelImportThumbnailLoading();
        using var cancellation = BeginCancelableTask(ActiveTaskKind.Analysis);
        var cancellationToken = cancellation.Token;
        IsBusy = true;
        IsProgressIndeterminate = false;
        ProgressValue = 0;
        ProgressLabel = "正在读取来源文件夹…";
        StatusMessage = "正在分析来源，请稍候…";
        ImportItems.Clear();
        ImportSections.Clear();
        ImportActionHint = "正在分析来源，识别完成后会显示可导入内容。";
        ImportReport = "正在分析文件…";

        try
        {
            var uiScanProgress = new Progress<ImportFileScanProgress>(progress =>
            {
                if (cancellationToken.IsCancellationRequested || !ReferenceEquals(_activeTaskCancellation, cancellation))
                {
                    return;
                }

                ProgressLabel = $"正在读取来源文件夹… 已发现 {progress.MatchedFiles:N0} 个可导入文件";
                ImportReport = $"正在扫描：{progress.CurrentFolder}";
            });
            var scanProgress = new ThrottledProgress<ImportFileScanProgress>(uiScanProgress, TimeSpan.FromMilliseconds(125));
            var fileInfos = await Task.Run(
                () => EnumerateImportFileInfos(paths, cancellationToken, scanProgress).ToArray(),
                cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            ProgressValue = 35;

            var sourceFiles = fileInfos
                .Select(info => new SourceMediaFile(info.FullName, info.Length, info.LastWriteTimeUtc))
                .ToArray();

            var resolvedDateHintPath = ResolveCameraDateHintPath(dateHintPath, fileInfos);
            var resolution = _dateResolver.Resolve(Path.GetFileName(Path.TrimEndingDirectorySeparator(resolvedDateHintPath)), fileInfos.Select(info => info.LastWriteTime.Year).ToArray());
            _targetDate = resolution.Date;
            TargetDateText = resolution.Date is { } date ? FormatDate(date) : "需要手动确认日期";

            ProgressValue = 58;
            ProgressLabel = "正在自动分类…";
            var groups = _groupBuilder.Build(sourceFiles);
            IReadOnlyDictionary<string, string> personLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            PersonClusteringResult? personResult = null;
            if (EnablePersonRecognition)
            {
                ProgressLabel = "正在识别 JPG 人物/造型…";
                var uiPersonProgress = new Progress<PersonClusteringProgress>(progress =>
                {
                    if (cancellationToken.IsCancellationRequested || !ReferenceEquals(_activeTaskCancellation, cancellation))
                    {
                        return;
                    }

                    ProgressValue = 58 + progress.Processed * 22d / Math.Max(progress.Total, 1);
                    ProgressLabel = $"正在识别 JPG 人物/造型… {progress.Processed}/{progress.Total}";
                });
                var personProgress = new ThrottledProgress<PersonClusteringProgress>(uiPersonProgress, TimeSpan.FromMilliseconds(125));
                personResult = await _personClusterer.ClusterAsync(groups, personProgress, cancellationToken).ConfigureAwait(true);
                personLabels = personResult.LabelsByPrimaryPath;
            }

            ProgressValue = 82;
            ProgressLabel = "正在生成导入队列…";
            var queueIndex = 1;
            foreach (var group in groups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var category = group.Category;
                var itemDate = ResolveGroupDate(group, resolution.Date);
                var requiresConfirmation = group.Category == MediaCategory.Unconfirmed || itemDate is null;
                var item = new ImportPreviewItemViewModel(
                    group,
                    category,
                    CategoryChoices,
                    requiresConfirmation,
                    itemDate,
                    null,
                    personLabels.TryGetValue(group.Primary.FullPath, out var personLabel) ? personLabel : string.Empty,
                    queueIndex++);
                item.SelectionChanged += () =>
                {
                    NotifyCommandStates();
                    RebuildImportSections();
                };
                ImportItems.Add(item);
            }

            RebuildImportSections();
            StartImportThumbnailLoading();
            ProgressValue = 100;
            var unconfirmedCount = ImportItems.Count(item => item.RequiresConfirmation);
            var dateCount = ImportItems.Select(item => item.TargetDate).Where(date => date is not null).Distinct().Count();
            if (dateCount > 1)
            {
                TargetDateText = $"多日期导入：{dateCount} 个目标日期";
            }
            ImportReport = $"已识别 {ImportItems.Count} 组媒体，{unconfirmedCount} 组需要你确认分类/日期，目标日期 {dateCount} 个。";
            if (personResult is not null)
            {
                ImportReport += Environment.NewLine + $"本地人物识别：读取 {personResult.RecognizedImages} 张可分析图片，分成 {personResult.ClusterCount} 组人物/造型。";
            }
            if (!string.Equals(Path.GetFullPath(dateHintPath), Path.GetFullPath(resolvedDateHintPath), StringComparison.OrdinalIgnoreCase))
            {
                ImportReport += Environment.NewLine + $"已从子文件夹识别日期：{Path.GetFileName(Path.TrimEndingDirectorySeparator(resolvedDateHintPath))}";
            }
            if (resolution.Warnings.Count > 0)
            {
                ImportReport += Environment.NewLine + string.Join(Environment.NewLine, resolution.Warnings);
            }

            StatusMessage = "来源分析完成。";
            ImportActionHint = HasLibraryRoot ? "可以开始导入；复制/校验时进度条会实时显示。" : "导入按钮不可用：请先选择照片库根目录。";
            NotifyCommandStates();

            // Prompt for date remarks right after analysis, before the import copies any files.
            if (HasLibraryRoot && dateCount > 0)
            {
                var detectedDates = ImportItems
                    .Select(item => item.TargetDate)
                    .Where(date => date is not null)
                    .Select(date => date!.Value)
                    .Distinct()
                    .ToArray();
                if (detectedDates.Length > 0)
                {
                    await AskForDateRemarksAsync(detectedDates).ConfigureAwait(true);
                }
            }

            return sourceFiles;
        }
        catch (OperationCanceledException)
        {
            ImportItems.Clear();
            ImportSections.Clear();
            ImportReport = "已停止分析。";
            ImportActionHint = "来源分析已停止。你可以重新选择来源或重新分析。";
            StatusMessage = "来源分析已停止。";
            ProgressLabel = "已停止";
            ProgressValue = 0;
            return [];
        }
        catch (Exception ex)
        {
            ImportReport = "分析失败：" + ex.Message;
            StatusMessage = "分析来源时遇到问题。";
            return [];
        }
        finally
        {
            EndCancelableTask(cancellation);
            IsProgressIndeterminate = false;
            ProgressValue = 0;
            if (ProgressLabel != "已停止")
            {
                ProgressLabel = "分析完成";
            }
            IsBusy = false;
        }
    }

    private async Task ImportSelectedAsync()
    {
        if (!HasLibraryRoot)
        {
            System.Windows.MessageBox.Show("请先选择 Hanabe 拍照库根目录。", "Hanabe", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var selectedItems = ImportItems
            .Where(item => item.IsSelected)
            .Where(item => item.SelectedCategory.Category != MediaCategory.Unconfirmed)
            .Where(item => ConcreteCategoryFolders.ContainsKey(item.SelectedCategory.Category))
            .ToArray();

        var unresolved = ImportItems
            .Where(item => item.IsSelected && (item.SelectedCategory.Category == MediaCategory.Unconfirmed || item.TargetDate is null))
            .Select(item => item.Name)
            .ToArray();

        if (unresolved.Length > 0)
        {
            System.Windows.MessageBox.Show(
                "还有待确认的项目，请先改成具体类别再导入：\n" + string.Join("\n", unresolved.Take(12)),
                "Hanabe",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (selectedItems.Length == 0)
        {
            System.Windows.MessageBox.Show("没有勾选可导入的文件。", "Hanabe", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var deleteSources = SelectedTransferMode == TransferMode.MoveAfterVerify;
        if (deleteSources)
        {
            var answer = System.Windows.MessageBox.Show(
                "移动模式会在哈希校验成功后删除来源文件。确认继续吗？",
                "Hanabe 安全确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }
        }

        await RunImportAsync(selectedItems, deleteSources).ConfigureAwait(true);
    }

    private async Task RunImportAsync(IReadOnlyList<ImportPreviewItemViewModel> items, bool deleteSourcesAfterVerify)
    {
        using var cancellation = BeginCancelableTask(ActiveTaskKind.Import);
        var cancellationToken = cancellation.Token;
        IsBusy = true;
        IsProgressIndeterminate = false;
        ProgressValue = 0;
        ProgressLabel = "准备导入…";
        StatusMessage = "正在导入，进度条会持续更新。";

        var success = 0;
        var failed = 0;
        var skipped = 0;
        var stopped = false;
        var lines = new List<string>();

        try
        {
            var dateGroups = items
                .Where(item => item.TargetDate is not null)
                .GroupBy(item => item.TargetDate!.Value)
                .OrderBy(group => group.Key.Year)
                .ThenBy(group => group.Key.Month)
                .ThenBy(group => group.Key.Day)
                .ToArray();

            var completedDateGroups = 0;
            foreach (var dateGroup in dateGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await RunImportDateAsync(
                    dateGroup.Select(item => item.ToMediaGroup()).ToArray(),
                    dateGroup.Key,
                    deleteSourcesAfterVerify,
                    cancellationToken).ConfigureAwait(true);

                success += result.Success;
                skipped += result.Skipped;
                failed += result.Failed;
                lines.AddRange(result.Lines);
                completedDateGroups++;
                ProgressValue = dateGroups.Length == 0 ? 100 : completedDateGroups * 100d / dateGroups.Length;
            }

            ProgressValue = 100;
            ProgressLabel = "导入完成";
            ImportReport = $"导入完成：成功 {success}，跳过 {skipped}，失败 {failed}" + Environment.NewLine + string.Join(Environment.NewLine, lines.Take(100));
            StatusMessage = "导入流程结束。";
            EndCancelableTask(cancellation);
            IsProgressIndeterminate = false;
            IsBusy = false;
            var lastDate = dateGroups.LastOrDefault()?.Key;
            if (CurrentPage == "Preview")
            {
                await RefreshLibraryAsync().ConfigureAwait(true);
                if (lastDate is { } date)
                {
                    SelectDateByValue(date);
                }
            }
            else
            {
                ResetPreviewState("预览未加载");
                StatusMessage = "导入完成。预览已标记为待刷新；进入预览页时再读取缩略图。";
            }
        }
        catch (OperationCanceledException)
        {
            stopped = true;
            ProgressValue = Math.Clamp(ProgressValue, 0, 100);
            ProgressLabel = "已停止";
            ImportReport = $"导入已停止：成功 {success}，跳过 {skipped}，失败 {failed}" + Environment.NewLine + string.Join(Environment.NewLine, lines.Take(100));
            StatusMessage = "传输已停止。已完成的文件会保留，未完成的临时文件已尽量清理。";
        }
        catch (Exception ex)
        {
            ImportReport = "导入中断：" + ex.Message;
            StatusMessage = "导入中断，已保留可见报告。";
        }
        finally
        {
            EndCancelableTask(cancellation);
            IsProgressIndeterminate = false;
            IsBusy = false;
            if (!stopped)
            {
                CancelCurrentTaskCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private async Task<ImportRunResult> RunImportDateAsync(IReadOnlyList<MediaGroup> groups, LibraryDate date, bool deleteSourcesAfterVerify, CancellationToken cancellationToken)
    {
        var success = 0;
        var failed = 0;
        var skipped = 0;
        var lines = new List<string>();

        _directoryInitializer.EnsureDateTree(LibraryRoot, date);
        var plan = await _planBuilder.BuildAsync(
            LibraryRoot,
            date,
            deleteSourcesAfterVerify ? TransferMode.MoveAfterVerify : TransferMode.CopyKeepSource,
            groups,
            cancellationToken).ConfigureAwait(true);

        for (var index = 0; index < plan.Items.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = plan.Items[index];
            ProgressLabel = $"正在处理 {date.Month:00}.{date.Day:00} · {index + 1}/{plan.Items.Count}：{item.Group.GroupKey}";
            ProgressValue = plan.Items.Count == 0 ? 100 : index * 100d / plan.Items.Count;

            if (item.Conflict == ConflictKind.SameNameDifferentContent)
            {
                failed++;
                lines.Add($"冲突：{date.Month:00}.{date.Day:00} / {item.Group.GroupKey} 已有同名但内容不同的文件，已跳过。");
                continue;
            }

            var result = await _transfer.TransferGroupAsync(item, deleteSourcesAfterVerify, cancellationToken).ConfigureAwait(true);
            if (result.Success)
            {
                if (item.Conflict == ConflictKind.Identical)
                {
                    skipped++;
                    lines.Add($"已存在：{date.Month:00}.{date.Day:00} / {item.Group.GroupKey} 内容相同，跳过复制。");
                }
                else
                {
                    success++;
                    lines.Add($"完成：{date.Month:00}.{date.Day:00} / {item.Group.GroupKey}");
                }
            }
            else
            {
                failed++;
                lines.Add($"失败：{date.Month:00}.{date.Day:00} / {item.Group.GroupKey} - {result.Error}");
            }
        }

        return new ImportRunResult(success, skipped, failed, lines);
    }

    private async Task AskForDateRemarksAsync(IReadOnlyList<LibraryDate> dates)
    {
        foreach (var date in dates.Distinct().OrderBy(date => date.Year).ThenBy(date => date.Month).ThenBy(date => date.Day))
        {
            var window = new RemarkPromptWindow($"{date.Month:00}.{date.Day:00}")
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (window.ShowDialog() != true)
            {
                continue;
            }

            var remark = SanitizeRemark(window.Remark, date);
            if (string.IsNullOrWhiteSpace(remark))
            {
                continue;
            }

            try
            {
                RenameDateFolderWithRemark(date, remark);
                StatusMessage = $"已给 {date.Month:00}.{date.Day:00} 添加备注：{remark}";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"备注保存失败：{ex.Message}", "Hanabe", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await Task.Yield();
        }
    }

    private void RenameDateFolderWithRemark(LibraryDate date, string remark)
    {
        var monthDirectory = Path.Combine(LibraryRoot, $"{date.Month}月");
        if (!Directory.Exists(monthDirectory))
        {
            return;
        }

        var prefix = $"{date.Month:00}.{date.Day:00}";
        var current = Directory.EnumerateDirectories(monthDirectory, prefix + "*", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path).Equals(prefix, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .FirstOrDefault();

        if (current is null)
        {
            return;
        }

        var target = Path.Combine(monthDirectory, $"{prefix}_{remark}");
        if (string.Equals(Path.GetFullPath(current), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Directory.Exists(target))
        {
            throw new IOException($"目标文件夹已存在：{target}");
        }

        Directory.Move(current, target);
    }

    private static string SanitizeRemark(string input, LibraryDate date)
    {
        var remark = input.Trim();
        var prefix = $"{date.Month:00}.{date.Day:00}";
        if (remark.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            remark = remark[prefix.Length..].TrimStart('_', '-', ' ', '\\', '/');
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            remark = remark.Replace(invalid, '_');
        }

        return remark.Trim().Trim('_');
    }

    private void RebuildImportSections()
    {
        ImportSections.Clear();
        foreach (var group in ImportItems
                     .Where(item => item.IsSelected)
                     .GroupBy(item => new ImportSectionKey(item.NeedsAttention, item.SelectedCategory.Category))
                     .OrderBy(group => group.Key.NeedsAttention ? 0 : 1)
                     .ThenBy(group => CategoryOrder(group.Key.Category)))
        {
            var items = group.ToArray();
            var statusText = group.Key.NeedsAttention ? "需要确认" : "已自动分类";
            var statusHint = group.Key.NeedsAttention ? "请检查分类/日期后再导入" : "可直接导入";
            ImportSections.Add(new ImportCategorySectionViewModel(
                $"{statusText} · {CategoryDisplayName(group.Key.Category)}",
                group.Key.NeedsAttention ? "!" : CategoryIcon(group.Key.Category),
                $"{statusHint} · {items.Length} 组 · {FormatBytes(items.Sum(item => item.TotalBytes))}",
                group.Key.NeedsAttention ? "#D97706" : CategoryAccent(group.Key.Category),
                items));
        }
    }

    private static LibraryDate? ResolveGroupDate(MediaGroup group, LibraryDate? fallbackDate)
    {
        return ResolveFileDate(group.Primary) ?? fallbackDate;
    }

    private static LibraryDate? ResolveFileDate(SourceMediaFile file)
    {
        var directory = Path.GetDirectoryName(file.FullPath);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var folderName = Path.GetFileName(directory);
            var match = Regex.Matches(folderName ?? string.Empty, "[0-9]{4,}")
                .LastOrDefault();
            if (match is not null)
            {
                var monthDay = match.Value[^4..];
                if (int.TryParse(monthDay[..2], CultureInfo.InvariantCulture, out var month) &&
                    int.TryParse(monthDay[2..], CultureInfo.InvariantCulture, out var day) &&
                    month is >= 1 and <= 12 &&
                    day is >= 1 and <= 31)
                {
                    try
                    {
                        return new LibraryDate(file.LastWriteTime.Year, month, day);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                    }
                }
            }

            directory = Path.GetDirectoryName(directory);
        }

        var localTime = file.LastWriteTime.LocalDateTime;
        try
        {
            return new LibraryDate(localTime.Year, localTime.Month, localTime.Day);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static int CategoryOrder(MediaCategory category) => category switch
    {
        MediaCategory.Raw => 0,
        MediaCategory.Jpeg => 1,
        MediaCategory.Video => 2,
        MediaCategory.ActionVideo => 3,
        MediaCategory.Edited => 4,
        MediaCategory.Material => 5,
        _ => 99
    };

    private static string CategoryDisplayName(MediaCategory category) => category switch
    {
        MediaCategory.Raw => "RAW生图",
        MediaCategory.Jpeg => "JPG生图",
        MediaCategory.Video => "视频",
        MediaCategory.ActionVideo => "action视频",
        MediaCategory.Edited => "修后",
        MediaCategory.Material => "素材",
        _ => "待确认"
    };

    private static string CategoryIcon(MediaCategory category) => category switch
    {
        MediaCategory.Raw => "◈",
        MediaCategory.Jpeg => "▧",
        MediaCategory.Video => "▶",
        MediaCategory.ActionVideo => "DJI",
        MediaCategory.Edited => "✦",
        MediaCategory.Material => "◇",
        _ => "?"
    };

    private static string CategoryAccent(MediaCategory category) => category switch
    {
        MediaCategory.Raw => "#4F46E5",
        MediaCategory.Jpeg => "#06B6D4",
        MediaCategory.Video => "#F97316",
        MediaCategory.ActionVideo => "#A855F7",
        MediaCategory.Edited => "#22C55E",
        MediaCategory.Material => "#EAB308",
        _ => "#64748B"
    };

    private async Task RefreshLibraryAsync()
    {
        if (string.IsNullOrWhiteSpace(LibraryRoot))
        {
            StatusMessage = "照片库位置为空。";
            return;
        }

        using var cancellation = BeginCancelableTask(ActiveTaskKind.Preview);
        var cancellationToken = cancellation.Token;
        IsBusy = true;
        _previewHasLoaded = false;
        IsProgressIndeterminate = false;
        var scanVersion = ++_previewScanVersion;
        ProgressValue = 0;
        ProgressLabel = "正在预读取库根目录…";
        PreviewFiles.Clear();
        VisiblePreviewFiles.Clear();
        VisiblePreviewSections.Clear();
        HomePreviewFiles.Clear();
        CancelPreviewThumbnailLoading();
        _previewPage = 0;
        NotifyPreviewCountsChanged();
        CategorySummaries.Clear();
        SetProperty(ref _selectedDate, null, nameof(SelectedDate));
        SelectedDateTitle = "全部照片";
        SelectedDatePath = LibraryRoot;
        NotifyPreviewCountsChanged();

        try
        {
            var totalSw = System.Diagnostics.Stopwatch.StartNew();
            var dateSw = System.Diagnostics.Stopwatch.StartNew();
            LibraryDates.Clear();
            ProgressValue = 2;
            ProgressLabel = "正在清理空日期目录…";
            var maintenance = await _libraryMaintenanceService
                .RemoveEmptyDateDirectoriesAsync(LibraryRoot, cancellationToken)
                .ConfigureAwait(true);
            ProgressValue = 5;
            foreach (var node in DiscoverDates(LibraryRoot))
            {
                LibraryDates.Add(node);
            }
            OnPropertyChanged(nameof(DiscoveredDateCount));
            SyncCalendarWithLibrary();
            dateSw.Stop();

            ProgressValue = 15;
            ProgressLabel = "正在可视化扫描媒体文件…";
            StatusMessage = "正在扫描：缩略图会边发现边出现在主界面和预览页。";
            var streamSw = System.Diagnostics.Stopwatch.StartNew();
            await StreamLibraryPreviewAsync(LibraryRoot, scanVersion, cancellationToken).ConfigureAwait(true);
            await ApplyStoredMetadataAsync(cancellationToken).ConfigureAwait(true);
            streamSw.Stop();
            var capSw = System.Diagnostics.Stopwatch.StartNew();
            await RefreshCapacityAsync(LibraryRoot).ConfigureAwait(true);
            capSw.Stop();
            _ = RebuildRetouchTrackingAsync().ConfigureAwait(true);
            totalSw.Stop();

            var dateCount = FlattenDateNodes(LibraryDates).Count;
            var mediaCount = PreviewFiles.Count;
            DiagnosticsText = $"⏱ 库扫描 · 日期发现 {dateCount} 个用 {dateSw.ElapsedMilliseconds}ms · 媒体扫描 {mediaCount} 个用 {streamSw.ElapsedMilliseconds}ms · 容量刷新 {capSw.ElapsedMilliseconds}ms · 总计 {totalSw.ElapsedMilliseconds}ms";

            StatusMessage = $"已可视化预读取 {PreviewFiles.Count} 个媒体文件" +
                            (dateCount == 0 ? "，暂未发现日期文件夹。" : $"，发现 {dateCount} 个日期文件夹。") +
                            (maintenance.Deleted.Count > 0 ? $" 已删除 {maintenance.Deleted.Count} 个空日期目录。" : string.Empty) +
                            (maintenance.Failures.Count > 0 ? $" {maintenance.Failures.Count} 个空目录无法删除。" : string.Empty);
            RefreshFilteredCache(resetPage: true);
            NotifyPreviewCountsChanged();
            _previewHasLoaded = true;
            if (IsHomePage)
            {
                StartPreviewThumbnailLoading(HomePreviewFiles);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "照片库预读取已停止。";
            ProgressLabel = "已停止";
            ProgressValue = 0;
            _previewHasLoaded = false;
        }
        catch (Exception ex)
        {
            LibraryDates.Clear();
            OnPropertyChanged(nameof(DiscoveredDateCount));
            CalendarDays.Clear();
            PreviewFiles.Clear();
            VisiblePreviewFiles.Clear();
            VisiblePreviewSections.Clear();
            HomePreviewFiles.Clear();
            CancelPreviewThumbnailLoading();
            NotifyPreviewCountsChanged();
            StatusMessage = $"读取照片库失败：{ex.Message}";
            _previewHasLoaded = false;
        }
        finally
        {
            EndCancelableTask(cancellation);
            IsProgressIndeterminate = false;
            if (ProgressLabel != "已停止")
            {
                ProgressValue = 100;
                ProgressLabel = "预读取完成";
            }
            IsBusy = false;
        }
    }

    private async Task SelectDateAsync(LibraryDateNode node)
    {
        if (node.Date is null || !Directory.Exists(node.FullPath))
        {
            return;
        }

        ++_previewScanVersion;
        SelectedDateTitle = node.Title;
        SelectedDatePath = node.FullPath;
        CategorySummaries.Clear();
        PreviewFiles.Clear();
        VisiblePreviewFiles.Clear();
        VisiblePreviewSections.Clear();
        HomePreviewFiles.Clear();
        CancelPreviewThumbnailLoading();
        _previewPage = 0;
        NotifyPreviewCountsChanged();
        ProgressValue = 0;
        ProgressLabel = "正在读取所选日期…";

        foreach (var category in CategoryFolderNames)
        {
            var path = Path.Combine(node.FullPath, category);
            var files = Directory.Exists(path)
                ? Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly)
                    .Where(IsLibraryPreviewFile)
                    .ToArray()
                : [];
            var totalSize = files.Sum(file => new FileInfo(file).Length);
            CategorySummaries.Add(new CategorySummaryViewModel(category, path, files.Length, FormatBytes(totalSize)));

            foreach (var file in files)
            {
                AddPreviewFile(file, category);
            }

            ProgressValue += 100d / CategoryFolderNames.Length;
            await Task.Yield();
        }

        ProgressValue = 100;
        ProgressLabel = $"日期读取完成：{PreviewFiles.Count:N0} 个媒体文件";
        foreach (var preview in PreviewFiles.Take(PreviewLoadingPolicy.HomeRecentItemLimit))
        {
            HomePreviewFiles.Add(preview);
        }
        await RebuildRetouchTrackingAsync().ConfigureAwait(true);
        await ApplyStoredMetadataAsync().ConfigureAwait(true);
        NotifyPreviewCountsChanged();
        await RefreshCapacityAsync(node.FullPath).ConfigureAwait(true);
    }

    private static IEnumerable<string> EnumerateLibraryPreviewFiles(string root)
    {
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(IsLibraryPreviewFile);
    }

    private async Task StreamLibraryPreviewAsync(string root, int scanVersion, CancellationToken cancellationToken)
    {
        var scanned = 0;
        var categoryStats = new Dictionary<string, (int Count, long Size)>(StringComparer.OrdinalIgnoreCase);

        IsProgressIndeterminate = true;
        await Task.Run(async () =>
        {
            var batch = new List<PreviewFileViewModel>(PreviewLoadingPolicy.ScanBatchSize);
            foreach (var file in EnumerateLibraryPreviewFiles(root))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (scanVersion != _previewScanVersion)
                {
                    break;
                }

                PreviewFileViewModel? preview;
                try
                {
                    preview = CreatePreviewFile(file, GuessCategoryFromPath(file));
                }
                catch
                {
                    continue;
                }

                if (preview is null)
                {
                    continue;
                }

                scanned++;
                var category = preview.Category;
                var size = TryGetFileLength(file);
                var previous = categoryStats.TryGetValue(category, out var stat) ? stat : (Count: 0, Size: 0L);
                categoryStats[category] = (previous.Count + 1, previous.Size + size);
                batch.Add(preview);

                if (batch.Count < PreviewLoadingPolicy.ScanBatchSize)
                {
                    continue;
                }

                var ready = batch.ToArray();
                batch.Clear();
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    AddPreviewMetadataBatch(ready, scanned, scanVersion));
            }

            if (batch.Count > 0)
            {
                var ready = batch.ToArray();
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    AddPreviewMetadataBatch(ready, scanned, scanVersion));
            }
        }).ConfigureAwait(true);

        IsProgressIndeterminate = false;
        ProgressValue = 100;
        ProgressLabel = scanned == 0 ? "没有发现可预览文件" : $"预读取完成：{scanned} 个媒体文件";
        RebuildCategorySummaries(root, categoryStats);
        RefreshFilteredCache(resetPage: true);
        NotifyPreviewCountsChanged();
    }

    private void AddPreviewMetadataBatch(
        IReadOnlyList<PreviewFileViewModel> batch,
        int scanned,
        int scanVersion)
    {
        if (scanVersion != _previewScanVersion)
        {
            return;
        }

        foreach (var preview in batch)
        {
            PreviewFiles.Add(preview);
            if (HomePreviewFiles.Count < PreviewLoadingPolicy.HomeRecentItemLimit)
            {
                HomePreviewFiles.Add(preview);
            }
        }

        ProgressLabel = $"正在扫描媒体文件：已找到 {scanned:N0} 个";
        NotifyPreviewCountsChanged();
    }

    private void NotifyPreviewCountsChanged()
    {
        OnPropertyChanged(nameof(FilteredPreviewCount));
        OnPropertyChanged(nameof(PreviewSummaryText));
        OnPropertyChanged(nameof(HasPreviousPreviewPage));
        OnPropertyChanged(nameof(HasNextPreviewPage));
        OnPropertyChanged(nameof(PreviewPageText));
        PreviousPreviewPageCommand.NotifyCanExecuteChanged();
        NextPreviewPageCommand.NotifyCanExecuteChanged();
    }

    private void RebuildCategorySummaries(string root, IReadOnlyDictionary<string, (int Count, long Size)> categoryStats)
    {
        CategorySummaries.Clear();
        foreach (var item in categoryStats.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            CategorySummaries.Add(new CategorySummaryViewModel(item.Key, root, item.Value.Count, FormatBytes(item.Value.Size)));
        }
    }

    private async Task PreloadLibraryPreviewAsync(string root, IReadOnlyList<string> files)
    {
        if (files.Count == 0)
        {
            ProgressValue = 100;
            ProgressLabel = "没有发现可预览文件";
            return;
        }

        foreach (var group in files.GroupBy(GuessCategoryFromPath).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var totalSize = group.Sum(file => new FileInfo(file).Length);
            CategorySummaries.Add(new CategorySummaryViewModel(group.Key, root, group.Count(), FormatBytes(totalSize)));
        }

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            AddPreviewFile(file, GuessCategoryFromPath(file));

            if (index % 12 == 0 || index == files.Count - 1)
            {
                ProgressValue = files.Count == 0 ? 100 : 25 + ((index + 1) * 72d / files.Count);
                ProgressLabel = files.Count == 0
                    ? "没有发现可预览文件"
                    : $"正在生成预览 {index + 1}/{files.Count}";
                await Task.Yield();
            }
        }
    }

    public string CurrentPage
    {
        get => _currentPage;
        set
        {
            if (_currentPage == "Preview" && value != "Preview") _sessionBrowseSnapshot = CaptureBrowseSnapshot();
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(IsHomePage));
                OnPropertyChanged(nameof(IsImportPage));
                OnPropertyChanged(nameof(IsPreviewPage));
                OnPropertyChanged(nameof(IsFaceSearchPage));
                OnPropertyChanged(nameof(IsMapPhotosPage));
                OnPropertyChanged(nameof(IsCompressionPage));
                OnPropertyChanged(nameof(IsWatermarkPage));
                OnPropertyChanged(nameof(IsBaiduCloudPage));
                OnPropertyChanged(nameof(IsQuarkCloudPage));
                OnPropertyChanged(nameof(IsContestOpenPage));
                OnPropertyChanged(nameof(IsContestJudgedPage));
                OnPropertyChanged(nameof(IsSettingsPage));
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(PageSubtitle));

                if (IsPreviewPage)
                {
                    StartPreviewThumbnailLoading(VisiblePreviewFiles);
                }
                else
                {
                    CancelPreviewThumbnailLoading();
                    foreach (var item in VisiblePreviewFiles.Where(item => !HomePreviewFiles.Contains(item)))
                    {
                        item.Thumbnail = null;
                    }
                }
            }
        }
    }

    public bool IsHomePage => CurrentPage == "Home";

    public bool IsImportPage => CurrentPage == "Import";

    public bool IsPreviewPage => CurrentPage == "Preview";

    public bool IsFaceSearchPage => CurrentPage == "FaceSearch";

    public bool IsMapPhotosPage => CurrentPage == "MapPhotos";

    public bool IsCompressionPage => CurrentPage == "Compression";

    public bool IsWatermarkPage => CurrentPage == "Watermark";

    public bool IsBaiduCloudPage => CurrentPage == "BaiduCloud";
    public bool IsQuarkCloudPage => CurrentPage == "QuarkCloud";
    public bool HasSelectedFiles => PreviewFiles.Any(f => f.IsSelected);
    public bool IsContestOpenPage => CurrentPage == "ContestOpen";
    public bool IsContestJudgedPage => CurrentPage == "ContestJudged";

    public string DiagnosticsText
    {
        get => _diagnosticsText;
        private set => SetProperty(ref _diagnosticsText, value);
    }

    public bool IsSettingsPage => CurrentPage == "Settings";

    public string BaiduAppKey
    {
        get => _baiduAppKey;
        set
        {
            if (SetProperty(ref _baiduAppKey, value ?? string.Empty))
            {
                StartBaiduAuthorizationCommand.NotifyCanExecuteChanged();
                SaveBaiduCredentialsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string BaiduAppSecret
    {
        get => _baiduAppSecret;
        set
        {
            if (SetProperty(ref _baiduAppSecret, value ?? string.Empty))
            {
                StartBaiduAuthorizationCommand.NotifyCanExecuteChanged();
                SaveBaiduCredentialsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string BaiduAuthCode
    {
        get => _baiduAuthCode;
        set
        {
            if (SetProperty(ref _baiduAuthCode, value ?? string.Empty))
            {
                CompleteBaiduAuthorizationCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string BaiduStatus
    {
        get => _baiduStatus;
        private set => SetProperty(ref _baiduStatus, value);
    }

    public string QuarkStatus
    {
        get => _quarkStatus;
        private set => SetProperty(ref _quarkStatus, value);
    }

    public string QuarkClientPath
    {
        get => _quarkClientPath;
        set
        {
            if (SetProperty(ref _quarkClientPath, value ?? string.Empty))
            {
                _settingsStore.UpdateAsync(s => s.QuarkClientPath = value ?? string.Empty)
                    .ConfigureAwait(false);
            }
        }
    }

    public bool IsBaiduAuthorized
    {
        get => _isBaiduAuthorized;
        private set
        {
            if (SetProperty(ref _isBaiduAuthorized, value))
            {
                DisconnectBaiduCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsBaiduBusy
    {
        get => _isBaiduBusy;
        private set
        {
            if (SetProperty(ref _isBaiduBusy, value))
            {
                SaveBaiduCredentialsCommand.NotifyCanExecuteChanged();
                StartBaiduAuthorizationCommand.NotifyCanExecuteChanged();
                CompleteBaiduAuthorizationCommand.NotifyCanExecuteChanged();
                DisconnectBaiduCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string BaiduConnectionLabel => IsBaiduAuthorized ? "已连接" : "未连接";

    public IRelayCommand OpenQuarkOfficialCommand { get; }
    public IRelayCommand OpenBaiduConsoleCommand { get; }
    public IAsyncRelayCommand SaveBaiduCredentialsCommand { get; }
    public IAsyncRelayCommand StartBaiduAuthorizationCommand { get; }
    public IAsyncRelayCommand CompleteBaiduAuthorizationCommand { get; }
    public IAsyncRelayCommand DisconnectBaiduCommand { get; }

    public string PageTitle => CurrentPage switch
    {
        "Import" => "导入",
        "Preview" => "浏览",
        "FaceSearch" => "人物查找",
        "MapPhotos" => "地图照片",
        "Compression" => "图片小工具",
        "Watermark" => "批量水印",
        "BaiduCloud" => "百度网盘",
        "QuarkCloud" => "夸克网盘",
        "ContestOpen" => "投稿项目",
        "ContestJudged" => "欣赏项目",
        "Settings" => "设置",
        _ => "主界面"
    };

    public string PageSubtitle => CurrentPage switch
    {
        "Import" => "把相机文件夹拖进来，它会自动分类、建目录、导入。",
        "Preview" => "照片墙、分类筛选、缩略图缩放，专心看内容。",
        "FaceSearch" => "放入一张参考人脸，在本机照片库中寻找相似人物。",
        "MapPhotos" => "按 EXIF 或手动位置浏览照片；照片与位置索引始终保存在本机。",
        "Compression" => "批量压缩，或按原始尺寸纵向、横向拼接图片。",
        "Watermark" => "批量添加 PNG 签名或铺满水印，保持原格式与原始像素尺寸。",
        "BaiduCloud" => "百度网盘内嵌浏览器，会话自动保持，登录后直接浏览文件。",
        "QuarkCloud" => "夸克网盘内嵌浏览器，会话自动保持，登录后直接浏览文件。",
        "ContestOpen" => "征稿中的摄影大赛，点击右侧打开投稿页面。",
        "ContestJudged" => "已评奖的摄影大赛获奖作品，支持批量下载到本地。",
        "Settings" => "玻璃效果、背景、自启动、窗口大小都在这里。",
        _ => "设备连接、照片库状态和常用入口。"
    };

    private void AddPreviewFile(string file, string category)
    {
        try
        {
            var preview = CreatePreviewFile(file, category);
            if (preview is not null)
            {
                PreviewFiles.Add(preview);
                NotifyPreviewCountsChanged();
            }
        }
        catch
        {
        }
    }

    private void ShowNextPreviewPage()
    {
        if (!HasNextPreviewPage) return;
        _previewPage++;
        RebuildVisiblePreviewPage();
        NotifyPreviewCountsChanged();
    }

    private void ShowPreviousPreviewPage()
    {
        if (!HasPreviousPreviewPage) return;
        _previewPage--;
        RebuildVisiblePreviewPage();
        NotifyPreviewCountsChanged();
    }

    private void RebuildVisiblePreviewPage()
    {
        CancelPreviewThumbnailLoading();
        var oldItems = VisiblePreviewFiles.ToArray();
        foreach (var item in oldItems.Where(item => !HomePreviewFiles.Contains(item)))
        {
            item.Thumbnail = null;
        }

        RebuildVisiblePreviewSections();
    }

    private void RebuildVisiblePreviewSections()
    {
        if (!_personFilterOwnsExpansion && !_skipPreviewExpansionCaptureOnce)
        {
            foreach (var section in VisiblePreviewSections)
                _previewDateExpansion[section.Key] = section.IsExpanded;
        }
        _skipPreviewExpansionCaptureOnce = false;

        VisiblePreviewSections.Clear();
        var sectionIndex = 0;
        foreach (var group in _filteredCache.GroupBy(file => ResolvePreviewDateSection(file.FullPath)))
        {
            var first = group.Key;
            var isSingleSelectedDate = _calendarSelectedDate is not null;
            var expanded = _personFilterOwnsExpansion || isSingleSelectedDate || (_previewDateExpansion.TryGetValue(first.Key, out var remembered)
                ? remembered
                : sectionIndex == 0);
            VisiblePreviewSections.Add(new PreviewDateSectionViewModel(
                first.Key,
                first.Title,
                group.ToArray(),
                expanded,
                OnPreviewDateSectionExpansionChanged,
                showHeader: !isSingleSelectedDate));
            sectionIndex++;
        }

        RebuildExpandedPreviewFiles();
    }

    private void OnPreviewDateSectionExpansionChanged(PreviewDateSectionViewModel section, bool expanded)
    {
        _previewDateExpansion[section.Key] = expanded;
        if (!_suppressPreviewSectionRefresh)
        {
            RebuildExpandedPreviewFiles();
        }
    }

    private void RebuildExpandedPreviewFiles()
    {
        CancelPreviewThumbnailLoading();
        var oldItems = VisiblePreviewFiles.ToArray();
        VisiblePreviewFiles.Clear();
        var expandedItems = VisiblePreviewSections
            .Where(section => section.IsExpanded)
            .SelectMany(section => section.Items)
            .ToArray();
        foreach (var item in expandedItems)
        {
            VisiblePreviewFiles.Add(item);
        }

        foreach (var item in oldItems.Except(expandedItems).Where(item => !HomePreviewFiles.Contains(item)))
        {
            item.Thumbnail = null;
        }

        if (IsPreviewPage)
        {
            StartPreviewThumbnailLoading(VisiblePreviewFiles);
        }
    }

    private void SetAllPreviewDateSectionsExpanded(bool expanded)
    {
        _suppressPreviewSectionRefresh = true;
        try
        {
            foreach (var section in VisiblePreviewSections)
            {
                section.IsExpanded = expanded;
                _previewDateExpansion[section.Key] = expanded;
            }
        }
        finally
        {
            _suppressPreviewSectionRefresh = false;
        }
        RebuildExpandedPreviewFiles();
    }

    public static PreviewDateSectionInfo ResolvePreviewDateSection(string filePath)
    {
        try
        {
            var categoryDirectory = Path.GetDirectoryName(Path.GetFullPath(filePath));
            var dateDirectory = string.IsNullOrWhiteSpace(categoryDirectory)
                ? null
                : Directory.GetParent(categoryDirectory);
            if (dateDirectory is null)
            {
                return new PreviewDateSectionInfo("other", "其他日期");
            }

            var month = dateDirectory.Parent?.Name;
            var date = dateDirectory.Name;
            var title = string.IsNullOrWhiteSpace(month) ? date : $"{month} · {date}";
            return new PreviewDateSectionInfo(dateDirectory.FullName, title);
        }
        catch
        {
            return new PreviewDateSectionInfo("other", "其他日期");
        }
    }

    private void StartPreviewThumbnailLoading(IEnumerable<PreviewFileViewModel>? source = null)
    {
        CancelPreviewThumbnailLoading();
        var unloaded = (source ?? VisiblePreviewFiles).Where(item => item.Thumbnail is null).ToArray();
        if (unloaded.Length == 0) return;

        _previewThumbnailCancellation = new CancellationTokenSource();
        _ = LoadPreviewThumbnailsAsync(unloaded, _previewThumbnailCancellation.Token);
    }

    private static async Task LoadPreviewThumbnailsAsync(
        IReadOnlyList<PreviewFileViewModel> items,
        CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(PreviewLoadingPolicy.ThumbnailConcurrency);
        var tasks = items.Select(async item =>
        {
            var entered = false;
            try
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                entered = true;
                var thumbnail = await Task.Run(() => TryLoadThumbnail(item.PreviewPath), cancellationToken)
                    .WaitAsync(TimeSpan.FromSeconds(3), cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => item.Thumbnail = thumbnail);
            }
            catch (TimeoutException) { }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                if (entered) gate.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private void CancelPreviewThumbnailLoading()
    {
        _previewThumbnailCancellation?.Cancel();
        _previewThumbnailCancellation?.Dispose();
        _previewThumbnailCancellation = null;
    }

    private static PreviewFileViewModel? CreatePreviewFile(string file, string category)
    {
        var info = new FileInfo(file);
        if (!info.Exists)
        {
            return null;
        }

        var extension = Path.GetExtension(info.Name).TrimStart('.').ToUpperInvariant();
        return new PreviewFileViewModel(
            info.Name,
            category,
            info.FullName,
            FormatBytes(info.Length),
            extension,
            null); // load thumbnail async later
    }

    private void DeleteSelectedFiles()
    {
        var selected = PreviewFiles.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0) return;

        DeletePreviewFiles(selected, $"确定删除勾选的 {selected.Count} 组照片吗？");
    }

    public void DeletePreviewFile(PreviewFileViewModel file)
    {
        ArgumentNullException.ThrowIfNull(file);
        DeletePreviewFiles([file], $"确定把 {file.Name} 及其同名 RAW/JPG 配对文件移入回收站吗？");
    }

    public void NotifyPreviewSelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectedFiles));
        DeleteSelectedFilesCommand.NotifyCanExecuteChanged();
    }

    private IReadOnlyList<string> SelectedMetadataPaths()
    {
        var selected = PreviewFiles.Where(file => file.IsSelected).Select(file => file.FullPath).ToArray();
        if (selected.Length > 0) return selected;
        return SelectedPreviewFile is null ? [] : [SelectedPreviewFile.FullPath];
    }

    private async Task CreateCustomTagAsync()
    {
        var name = NewCustomTagName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "请输入标签名称。";
            return;
        }

        await TagManager.CreateTagAsync(name).ConfigureAwait(true);
        SelectedCustomTag = TagManager.CustomTags.FirstOrDefault(tag =>
            string.Equals(tag, name, StringComparison.OrdinalIgnoreCase));
        NewCustomTagName = string.Empty;
        StatusMessage = $"已创建标签：{name}";
    }

    private async Task AssignCategoryToSelectedAsync()
    {
        var paths = SelectedMetadataPaths();
        if (paths.Count == 0)
        {
            StatusMessage = "请先勾选照片，或打开一张照片。";
            return;
        }

        await TagManager.SetManualCategoryAsync(paths, SelectedManualCategory).ConfigureAwait(true);
        await ApplyStoredMetadataAsync().ConfigureAwait(true);
        StatusMessage = $"已将 {paths.Count} 张照片归入“{SelectedManualCategory}”。";
    }

    private async Task AssignTagToSelectedAsync()
    {
        var paths = SelectedMetadataPaths();
        if (paths.Count == 0)
        {
            StatusMessage = "请先勾选照片，或打开一张照片。";
            return;
        }
        if (string.IsNullOrWhiteSpace(SelectedCustomTag))
        {
            StatusMessage = "请先选择一个标签。";
            return;
        }

        await TagManager.AssignTagAsync(paths, SelectedCustomTag).ConfigureAwait(true);
        await ApplyStoredMetadataAsync().ConfigureAwait(true);
        StatusMessage = $"已为 {paths.Count} 张照片添加“{SelectedCustomTag}”。";
    }

    private async Task AnalyzeSelectedPhotosAsync()
    {
        var paths = SelectedMetadataPaths().Where(IsClassifiableImage).ToArray();
        if (paths.Length == 0)
        {
            StatusMessage = "请先勾选可识别的 JPG、PNG、BMP、TIFF 或 WEBP 照片。";
            return;
        }
        await Task.WhenAll(PhotoAnalysis.AnalyzeAsync(paths), PeopleAlbums.ScanPathsAsync(paths)).ConfigureAwait(true);
        await ApplyStoredMetadataAsync().ConfigureAwait(true);
        StatusMessage = PhotoAnalysis.StatusText;
        await SaveSettingsAsync().ConfigureAwait(true);
    }

    private async Task AnalyzeCurrentScopeAsync()
    {
        var paths = PreviewFiles.Select(file => file.FullPath).Where(IsClassifiableImage).ToArray();
        if (paths.Length == 0)
        {
            StatusMessage = "当前范围没有可识别照片。";
            return;
        }
        await Task.WhenAll(PhotoAnalysis.AnalyzeAsync(paths), PeopleAlbums.ScanPathsAsync(paths)).ConfigureAwait(true);
        await ApplyStoredMetadataAsync().ConfigureAwait(true);
        StatusMessage = PhotoAnalysis.StatusText;
        await SaveSettingsAsync().ConfigureAwait(true);
    }

    private static bool IsClassifiableImage(string path) =>
        WpfImageExtensions.Contains(Path.GetExtension(path));

    public async Task AssignQuickTagAsync(string tag)
    {
        var paths = SelectedMetadataPaths();
        if (paths.Count == 0) return;
        await TagManager.AssignTagAsync(paths, tag).ConfigureAwait(true);
        await ApplyStoredMetadataAsync().ConfigureAwait(true);
        StatusMessage = $"已为 {paths.Count} 张照片添加“{tag}”。";
    }

    private async Task ApplyStoredMetadataAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _mediaMetadataStore.LoadAsync(cancellationToken).ConfigureAwait(true);
        var lookup = (snapshot.Entries ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
            .GroupBy(entry => Path.GetFullPath(entry.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        foreach (var file in PreviewFiles)
        {
            if (lookup.TryGetValue(Path.GetFullPath(file.FullPath), out var entry))
            {
                file.SmartCategory = entry.EffectiveCategory;
                file.ManualTagsDisplay = string.Join(" · ", entry.ManualTags ?? []);
            }
            else
            {
                file.SmartCategory = "待分类";
                file.ManualTagsDisplay = string.Empty;
            }
        }
        RefreshFilteredCache(resetPage: false);
    }

    private void DeletePreviewFiles(
        IReadOnlyCollection<PreviewFileViewModel> selected,
        string confirmationText)
    {
        var targetPaths = selected
            .SelectMany(file => ResolveRawJpegPairPaths(file.FullPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (targetPaths.Length == 0) return;

        if (!HanabePhotoManager.App.DeleteConfirmationWindow.Confirm(
                System.Windows.Application.Current?.MainWindow,
                confirmationText,
                selected.Count,
                targetPaths.Length))
        {
            return;
        }

        var removedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failures = new List<string>();
        foreach (var path in targetPaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                        path,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
                removedPaths.Add(path);
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(path)}：{ex.Message}");
            }
        }

        var removedItems = PreviewFiles
            .Where(file => removedPaths.Contains(file.FullPath))
            .ToArray();
        foreach (var item in removedItems)
        {
            item.Thumbnail = null;
            PreviewFiles.Remove(item);
            HomePreviewFiles.Remove(item);
            VisiblePreviewFiles.Remove(item);
            RetouchedFiles.Remove(item);
        }

        foreach (var path in removedPaths)
        {
            RemoveThumbnailCacheEntries(path);
        }

        if (SelectedPreviewFile is not null && removedPaths.Contains(SelectedPreviewFile.FullPath))
        {
            SelectedPreviewFile = null;
        }

        RefreshFilteredCache();
        OnPropertyChanged(nameof(RetouchedGroupCount));
        OnPropertyChanged(nameof(TotalPhotoGroupCount));
        NotifyPreviewSelectionChanged();
        StatusMessage = failures.Count == 0
            ? $"已移入回收站：{removedPaths.Count} 个 RAW/JPG 文件，图库已刷新。"
            : $"已删除 {removedPaths.Count} 个文件，{failures.Count} 个失败。";

        if (failures.Count > 0)
        {
            System.Windows.MessageBox.Show(
                string.Join(Environment.NewLine, failures.Take(8)),
                "部分文件删除失败",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    public static IReadOnlyList<string> ResolveRawJpegPairPaths(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return [];

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory)) return File.Exists(fullPath) ? [fullPath] : [];

        var selectedExtension = Path.GetExtension(fullPath);
        var isRawOrJpeg = RawExtensions.Contains(selectedExtension) ||
                          selectedExtension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                          selectedExtension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
        if (!isRawOrJpeg) return File.Exists(fullPath) ? [fullPath] : [];

        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { directory };
        var category = Path.GetFileName(directory);
        if (category.Equals("RAW生图", StringComparison.OrdinalIgnoreCase) ||
            category.Equals("JPG生图", StringComparison.OrdinalIgnoreCase))
        {
            var dateRoot = Directory.GetParent(directory)?.FullName;
            if (!string.IsNullOrWhiteSpace(dateRoot))
            {
                directories.Add(Path.Combine(dateRoot, "RAW生图"));
                directories.Add(Path.Combine(dateRoot, "JPG生图"));
            }
        }

        var baseName = Path.GetFileNameWithoutExtension(fullPath);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidateDirectory in directories.Where(Directory.Exists))
        {
            try
            {
                foreach (var candidate in Directory.EnumerateFiles(candidateDirectory, baseName + ".*", SearchOption.TopDirectoryOnly))
                {
                    var extension = Path.GetExtension(candidate);
                    if (RawExtensions.Contains(extension) ||
                        extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(Path.GetFullPath(candidate));
                    }
                }
            }
            catch
            {
            }
        }

        if (File.Exists(fullPath)) result.Add(fullPath);
        return result.ToArray();
    }

    private static void RemoveThumbnailCacheEntries(string path)
    {
        var prefix = Path.GetFullPath(path) + "|";
        foreach (var key in ThumbnailCache.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            ThumbnailCache.TryRemove(key, out _);
        }
    }

    private bool CanDeleteSelectedFiles() => HasSelectedFiles;

    public void BatchCopyFilesTo(string targetFolder)
    {
        var selected = PreviewFiles.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0) return;

        int copied = 0;
        foreach (var f in selected)
        {
            try
            {
                var dest = Path.Combine(targetFolder, Path.GetFileName(f.FullPath));
                File.Copy(f.FullPath, dest, overwrite: false);
                copied++;
            }
            catch { }
        }
        StatusMessage = $"已复制 {copied}/{selected.Count} 个文件";
    }

    public void BatchMoveFilesTo(string targetFolder)
    {
        var selected = PreviewFiles.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0) return;

        int moved = 0;
        foreach (var f in selected.ToList())
        {
            try
            {
                var dest = Path.Combine(targetFolder, Path.GetFileName(f.FullPath));
                File.Move(f.FullPath, dest, overwrite: false);
                PreviewFiles.Remove(f);
                moved++;
            }
            catch { }
        }
        RefreshFilteredCache();
        OnPropertyChanged(nameof(FilteredPreviewFiles));
        OnPropertyChanged(nameof(HasSelectedFiles));
        StatusMessage = $"已移动 {moved}/{selected.Count} 个文件";
    }

    private static long TryGetFileLength(string file)
    {
        try
        {
            return new FileInfo(file).Length;
        }
        catch
        {
            return 0;
        }
    }

    private void StartImportThumbnailLoading()
    {
        CancelImportThumbnailLoading();
        var items = ImportSections
            .SelectMany(section => section.VisibleItems)
            .Distinct()
            .ToArray();
        if (items.Length == 0)
        {
            return;
        }

        _importThumbnailCancellation = new CancellationTokenSource();
        _ = LoadImportThumbnailsAsync(items, _importThumbnailCancellation.Token);
    }

    private async Task LoadImportThumbnailsAsync(
        IReadOnlyList<ImportPreviewItemViewModel> items,
        CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var loading = Task.Run(() => TryLoadThumbnail(item.PrimaryPath));
                var thumbnail = await loading
                    .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken)
                    .ConfigureAwait(true);
                if (!cancellationToken.IsCancellationRequested)
                {
                    item.SetThumbnail(thumbnail);
                }
            }
            catch (TimeoutException)
            {
                // 异常或损坏的单个媒体文件不应阻塞整个导入队列。
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // 保留可读占位卡片，继续加载后续缩略图。
            }
        }
    }

    private void CancelImportThumbnailLoading()
    {
        _importThumbnailCancellation?.Cancel();
        _importThumbnailCancellation?.Dispose();
        _importThumbnailCancellation = null;
    }

    private static ImageSource? TryLoadThumbnail(string path)
    {
        var extension = Path.GetExtension(path);
        if (!ThumbnailCandidateExtensions.Contains(extension))
        {
            return null;
        }

        // Cache check — keyed on path + mtime + size so edits from other apps invalidate.
        var cacheKey = TryGetThumbnailCacheKey(path);
        if (cacheKey is not null && ThumbnailCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var wpfThumbnail = TryLoadWpfThumbnail(path);
        if (wpfThumbnail is not null)
        {
            if (cacheKey is not null) CacheThumbnail(cacheKey, wpfThumbnail);
            return wpfThumbnail;
        }

        // For RAW files, try to reuse the embedded-JPG or sidecar JPG thumbnail
        // first before falling back to the generic Shell provider (which is slow
        // and usually returns nothing without a third-party codec).
        if (RawExtensions.Contains(extension))
        {
            var pairedJpg = TryFindPairedJpg(path);
            if (pairedJpg is not null)
            {
                var jpgThumbnail = TryLoadWpfThumbnail(pairedJpg);
                if (jpgThumbnail is not null)
                {
                    if (cacheKey is not null) CacheThumbnail(cacheKey, jpgThumbnail);
                    return jpgThumbnail;
                }
            }

            // Also try embedded JPEG (same filename as RAW but with .jpg extension
            // in the same directory, common on Sony cameras).
            return null;
        }

        var shellThumbnail = ShellThumbnailProvider.TryGetThumbnail(path);
        if (shellThumbnail is not null && cacheKey is not null)
        {
            CacheThumbnail(cacheKey, shellThumbnail);
        }
        return shellThumbnail;
    }

    private static void CacheThumbnail(string key, ImageSource thumbnail)
    {
        if (ThumbnailCache.TryAdd(key, thumbnail))
        {
            ThumbnailCacheOrder.Enqueue(key);
        }
        else
        {
            ThumbnailCache[key] = thumbnail;
        }

        while (ThumbnailCache.Count > PreviewLoadingPolicy.ThumbnailCacheLimit &&
               ThumbnailCacheOrder.TryDequeue(out var oldest))
        {
            ThumbnailCache.TryRemove(oldest, out _);
        }
    }

    private static string? TryFindPairedJpg(string rawPath)
    {
        // Most RAW+JPG pairings: IMG_0001.ARW → IMG_0001.JPG (same dir, different extension).
        var directory = Path.GetDirectoryName(rawPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var bareName = Path.GetFileNameWithoutExtension(rawPath);
        var jpgCandidates = new[]
        {
            Path.Combine(directory, bareName + ".jpg"),
            Path.Combine(directory, bareName + ".JPG"),
            Path.Combine(directory, bareName + ".jpeg"),
            Path.Combine(directory, bareName + ".JPEG")
        };

        foreach (var candidate in jpgCandidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? TryGetThumbnailCacheKey(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return null;
            }

            return $"{path}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? TryLoadWpfThumbnail(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = 260;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsLibraryPreviewFile(string path)
    {
        return LibraryPreviewExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }

    private static string GuessCategoryFromPath(string path)
    {
        var parts = Path.GetFullPath(path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Reverse()
            .ToArray();

        foreach (var category in CategoryFolderNames)
        {
            if (parts.Any(part => string.Equals(part, category, StringComparison.OrdinalIgnoreCase)))
            {
                return category;
            }
        }

        var extension = Path.GetExtension(path);
        if (extension.Equals(".ARW", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".CR2", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".CR3", StringComparison.OrdinalIgnoreCase))
        {
            return "RAW生图";
        }

        if (extension.Equals(".JPG", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".JPEG", StringComparison.OrdinalIgnoreCase))
        {
            return "JPG生图";
        }

        if (extension.Equals(".MP4", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".MOV", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileNameWithoutExtension(path).StartsWith("DJI_", StringComparison.OrdinalIgnoreCase)
                ? "action视频"
                : "视频";
        }

        return "素材";
    }

    private async Task RefreshCapacityAsync(string selectedPath)
    {
        SelectedFolderSize = "计算中…";
        SelectedFolderPercent = "计算中…";
        var size = await Task.Run(() => Directory.Exists(selectedPath) ? GetDirectorySize(selectedPath) : 0).ConfigureAwait(true);
        SelectedFolderSize = FormatBytes(size);
        SelectedFolderPercent = TryGetVolumePercent(selectedPath, size);
    }

    private void SelectDateByValue(LibraryDate date)
    {
        var found = FlattenDateNodes(LibraryDates)
            .FirstOrDefault(node => node.Date is { } nodeDate &&
                                    nodeDate.Year == date.Year &&
                                    nodeDate.Month == date.Month &&
                                    nodeDate.Day == date.Day);
        if (found is not null)
        {
            SelectedDate = found;
        }
    }

    private void OpenSelectedDate()
    {
        if (string.IsNullOrWhiteSpace(SelectedDatePath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = SelectedDatePath,
            UseShellExecute = true
        });
    }

    private static IReadOnlyList<LibraryDateNode> DiscoverDates(string root)
    {
        var days = new List<LibraryDateNode>();
        var currentYear = DateTime.Now.Year;

        foreach (var monthDirectory in Directory.EnumerateDirectories(root))
        {
            var monthName = Path.GetFileName(monthDirectory);
            if (TryParseMonthName(monthName, out var month))
            {
                AddDayDirectories(days, monthDirectory, currentYear, month);
                continue;
            }

            if (int.TryParse(monthName, out var year))
            {
                foreach (var nestedMonthDirectory in Directory.EnumerateDirectories(monthDirectory))
                {
                    if (TryParseMonthName(Path.GetFileName(nestedMonthDirectory), out var nestedMonth))
                    {
                        AddDayDirectories(days, nestedMonthDirectory, year, nestedMonth);
                    }
                }
            }
        }

        return days
            .GroupBy(node => node.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(node => node.Date!.Value.Year)
            .ThenBy(node => node.Date!.Value.Month)
            .ThenBy(node => node.Date!.Value.Day)
            .GroupBy(node => node.Date!.Value.Year)
            .Select(yearGroup => new LibraryDateNode(
                $"{yearGroup.Key} 年",
                string.Empty,
                null,
                yearGroup
                    .GroupBy(node => node.Date!.Value.Month)
                    .Select(monthGroup => new LibraryDateNode(
                        $"{monthGroup.Key}月",
                        string.Empty,
                        null,
                        monthGroup.ToArray()))
                    .ToArray()))
            .ToArray();
    }

    private static IReadOnlyList<LibraryDateNode> FlattenDateNodes(IEnumerable<LibraryDateNode> nodes)
    {
        var result = new List<LibraryDateNode>();
        foreach (var node in nodes)
        {
            if (node.Date is not null)
            {
                result.Add(node);
            }

            if (node.Children.Count > 0)
            {
                result.AddRange(FlattenDateNodes(node.Children));
            }
        }

        return result;
    }

    private static void AddDayDirectories(List<LibraryDateNode> nodes, string monthDirectory, int year, int month)
    {
        foreach (var discoveredDirectory in Directory.EnumerateDirectories(monthDirectory).ToArray())
        {
            var dayName = Path.GetFileName(discoveredDirectory);
            if (!LibraryDateFolderService.TryParseName(dayName, month, out var parsed))
            {
                continue;
            }

            try
            {
                var effectiveDirectory = LibraryDateFolderService.NormalizeDirectoryName(discoveredDirectory, parsed);
                var date = new LibraryDate(year, parsed.Month, parsed.Day);
                nodes.Add(new LibraryDateNode(FormatDate(date), effectiveDirectory, date));
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }
    }

    private static bool TryParseMonthName(string monthName, out int month)
    {
        month = 0;
        if (string.IsNullOrWhiteSpace(monthName) || !monthName.EndsWith("月", StringComparison.Ordinal))
        {
            return false;
        }

        var token = monthName[..^1].Trim();
        if (int.TryParse(token, out month) && month is >= 1 and <= 12)
        {
            return true;
        }

        month = token switch
        {
            "一" => 1,
            "二" => 2,
            "三" => 3,
            "四" => 4,
            "五" => 5,
            "六" => 6,
            "七" => 7,
            "八" => 8,
            "九" => 9,
            "十" => 10,
            "十一" => 11,
            "十二" => 12,
            _ => 0
        };

        return month is >= 1 and <= 12;
    }

    private async Task DismissOnboardingAsync()
    {
        IsOnboardingVisible = false;
        await _settingsStore.UpdateAsync(settings => settings.HasCompletedOnboarding = true).ConfigureAwait(true);
    }

    private void ReplayOnboarding()
    {
        OnboardingStep = 0;
        CurrentPage = "Settings";
        IsOnboardingVisible = true;
    }

    private void ShowPreviousOnboardingStep()
    {
        if (IsFirstOnboardingStep) return;
        OnboardingStep--;
        NavigateToOnboardingPage();
    }

    private async Task ShowNextOnboardingStepAsync()
    {
        if (IsLastOnboardingStep)
        {
            await DismissOnboardingAsync().ConfigureAwait(true);
            return;
        }
        if (IsOnboardingContinuationChoiceStep) return;
        OnboardingStep++;
        NavigateToOnboardingPage();
    }

    private void ContinueOnboarding()
    {
        if (!IsOnboardingContinuationChoiceStep) return;
        OnboardingStep++;
        NavigateToOnboardingPage();
    }

    private void NavigateToOnboardingPage()
    {
        if (OnboardingStep == 7)
            Compression.SelectedToolMode = ImageToolMode.Compression;
        else if (OnboardingStep == 8)
        {
            Compression.SelectedToolMode = ImageToolMode.Watermark;
            CurrentPage = "Compression";
            return;
        }
        CurrentPage = OnboardingStep switch
        {
            0 => "Settings",
            1 or 2 => "Import",
            3 => "Home",
            4 => "Preview",
            5 => "FaceSearch",
            7 => "Compression",
            9 => "MapPhotos",
            10 => "ContestOpen",
            11 => "ContestJudged",
            12 => "BaiduCloud",
            13 => "QuarkCloud",
            14 => "Settings",
            _ => CurrentPage
        };
    }

    private async Task SaveSettingsAsync()
    {
        if (!_isInitialized)
        {
            return;
        }

        try
        {
            await _settingsStore.UpdateAsync(settings =>
            {
            settings.NavigationOrder = NavigationItems.Select(item => item.Key).ToList();
            settings.HasCompletedOnboarding = !IsOnboardingVisible;
            settings.NavigationDisplayMode = NavigationDisplayMode;
            settings.LibraryRoot = LibraryRoot;
            settings.DefaultThumbnailSize = DefaultThumbnailSize;
            settings.GlassIntensity = GlassIntensity;
            settings.BackgroundMode = BackgroundMode;
            settings.BackgroundImageLayout = BackgroundImageLayout;
            settings.ClassificationEngine = PhotoAnalysis.SelectedEngine;
            settings.InferenceDevice = InferenceDevice;
            settings.SemanticMaxLabels = SemanticMaxLabels;
            settings.SemanticSimilarityWindow = SemanticSimilarityWindow;
            settings.FaceRecognitionEngine = FaceRecognitionEngine;
            settings.FaceRecognitionProfile = FaceRecognitionProfile;
            settings.ArcFaceDetectorModelPath = ArcFaceDetectorModelPath;
            settings.ArcFaceRecognizerModelPath = ArcFaceRecognizerModelPath;
            settings.ArcFaceModelLicenseConfirmed = ArcFaceModelLicenseConfirmed;
            settings.ArcFaceModelLicenseDescription = ArcFaceModelLicenseDescription;
            settings.ArcFaceMatchThreshold = ArcFaceMatchThreshold;
            settings.DefaultRatingFilter = DefaultRatingFilter;
            settings.DefaultPreviewSort = DefaultPreviewSort;
            settings.CustomBackgroundPath = CustomBackgroundPath;
            settings.AppIconPath = CustomAppIconPath;
            settings.LaunchAtStartup = LaunchAtStartup;
            settings.WindowWidth = WindowWidth;
            settings.WindowHeight = WindowHeight;
            settings.WindowLeft = _windowLeft;
            settings.WindowTop = _windowTop;
            settings.WindowState = _savedWindowState;
            settings.RestoreWindowState = RestoreWindowState;
            settings.BrowseEntryMode = BrowseEntryModeSetting.ToString();
            settings.BrowseSnapshot = CaptureBrowseSnapshot();
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                StatusMessage = $"设置保存失败：{ex.Message}");
        }
    }

    public void MoveNavigationItem(string sourceKey, string targetKey, bool insertAfter = false)
    {
        if (string.IsNullOrWhiteSpace(sourceKey) || string.IsNullOrWhiteSpace(targetKey))
        {
            return;
        }

        var sourceIndex = FindNavigationItemIndex(sourceKey);
        var targetIndex = FindNavigationItemIndex(targetKey);
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return;
        }

        var item = NavigationItems[sourceIndex];
        NavigationItems.RemoveAt(sourceIndex);
        targetIndex = FindNavigationItemIndex(targetKey);
        var insertionIndex = insertAfter ? targetIndex + 1 : targetIndex;
        NavigationItems.Insert(Math.Clamp(insertionIndex, 0, NavigationItems.Count), item);
        UpdateNavigationItemOrders();
        _ = SaveSettingsAsync();
    }

    private void ResetNavigationItems(IEnumerable<string>? storedOrder)
    {
        var order = NavigationOrderPolicy.Normalize(storedOrder, DefaultNavigationOrder)
            .Where(key => !string.Equals(key, "Watermark", StringComparison.Ordinal));
        NavigationItems.Clear();
        foreach (var key in order)
        {
            NavigationItems.Add(CreateNavigationItem(key, NavigationItems.Count));
        }
    }

    private NavigationItemViewModel CreateNavigationItem(string key, int order) => key switch
    {
        "Home" => new(key, "主页", "Icon.Home", ShowHomeCommand, order),
        "Import" => new(key, "导入照片", "Icon.Import", ShowImportCommand, order),
        "Preview" => new(key, "照片图库", "Icon.Library", ShowPreviewCommand, order),
        "FaceSearch" => new(key, "人物查找", "Icon.People", ShowFaceSearchCommand, order),
        "MapPhotos" => new(key, "地图照片", "Icon.Map", ShowMapPhotosCommand, order),
        "Compression" => new(key, "图片小工具", "Icon.Compression", ShowCompressionCommand, order),
        "BaiduCloud" => new(key, "百度网盘", "Icon.BaiduCloud", ShowBaiduCloudCommand, order),
        "QuarkCloud" => new(key, "夸克网盘", "Icon.QuarkCloud", ShowQuarkCloudCommand, order),
        "ContestOpen" => new(key, "投稿项目", "Icon.ContestOpen", ShowContestOpenCommand, order),
        "ContestJudged" => new(key, "欣赏项目", "Icon.ContestJudged", ShowContestJudgedCommand, order),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown navigation destination.")
    };

    private int FindNavigationItemIndex(string key)
    {
        for (var index = 0; index < NavigationItems.Count; index++)
        {
            if (string.Equals(NavigationItems[index].Key, key, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private void UpdateNavigationItemOrders()
    {
        for (var index = 0; index < NavigationItems.Count; index++)
        {
            NavigationItems[index].Order = index;
        }
    }

    public void RememberWindowSize(double width, double height)
    {
        if (double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0)
        {
            return;
        }

        _windowWidth = Math.Clamp(width, 1180, 2600);
        _windowHeight = Math.Clamp(height, 760, 1600);
        _ = SaveSettingsAsync();
    }

    public void RememberWindowState(double left, double top, double width, double height, string state)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top) || !double.IsFinite(width) || !double.IsFinite(height)) return;
        _windowLeft = left;
        _windowTop = top;
        _windowWidth = Math.Clamp(width, 800, 3840);
        _windowHeight = Math.Clamp(height, 600, 2160);
        _savedWindowState = state == "Maximized" ? "Maximized" : "Normal";
        OnPropertyChanged(nameof(WindowStateSummary));
        _ = SaveSettingsAsync();
    }

    private async Task ApplyStartupRegistrationAsync(bool enabled)
    {
        try
        {
            _startupRegistrationService.SetEnabled(enabled);
            await SaveSettingsAsync().ConfigureAwait(true);
            StatusMessage = enabled ? "已启用开机自启动。" : "已关闭开机自启动。";
        }
        catch (Exception ex)
        {
            _changingStartupRegistration = true;
            _launchAtStartup = !enabled;
            OnPropertyChanged(nameof(LaunchAtStartup));
            _changingStartupRegistration = false;
            StatusMessage = $"开机自启动修改失败，已回滚：{ex.Message}";
        }
    }

    private Task ImportFromDeviceAsync(ConnectedDeviceViewModel? device)
    {
        if (device is null || string.IsNullOrWhiteSpace(device.Path) || !Directory.Exists(device.Path))
        {
            StatusMessage = "设备暂时不可读取。";
            return Task.CompletedTask;
        }

        CurrentPage = "Import";
        var importRoots = FindDeviceImportRoots(device.Path);
        _sourceScanPaths = importRoots;
        CancelImportThumbnailLoading();
        SourceFolder = importRoots.Length == 1 ? importRoots[0] : device.Path;
        ImportItems.Clear();
        ImportSections.Clear();
        TargetDateText = "等待分析日期";
        ImportReport = importRoots.Length > 1
            ? $"已选择 {device.Name}，分析时会包含：{string.Join("、", importRoots.Select(Path.GetFileName))}。"
            : $"已选择 {device.Name}：{SourceFolder}";
        ImportActionHint = "先决定是否启用本地 AI 人物识别，然后点击“开始分析与分类”。";
        StatusMessage = $"已选择设备 {device.Name}，尚未开始扫描。";
        ProgressValue = 0;
        ProgressLabel = "等待开始";
        NotifyCommandStates();
        return Task.CompletedTask;
    }

    private static string[] FindDeviceImportRoots(string deviceRoot)
    {
        if (!Directory.Exists(deviceRoot))
        {
            return [deviceRoot];
        }

        var roots = new List<string>();
        var dcim = Path.Combine(deviceRoot, "DCIM");
        if (Directory.Exists(dcim))
        {
            roots.Add(dcim);
        }

        var m4RootClip = Path.Combine(deviceRoot, "M4ROOT", "CLIP");
        if (Directory.Exists(m4RootClip))
        {
            roots.Add(m4RootClip);
        }

        var privateClip = Path.Combine(deviceRoot, "PRIVATE");
        if (Directory.Exists(privateClip))
        {
            roots.Add(privateClip);
        }

        return roots.Count == 0 ? [deviceRoot] : roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private void RefreshConnectedDevices()
    {
        ConnectedDevices.Clear();
        DeviceGroups.Clear();

        if (HasLibraryRoot)
        {
            var kind = LibraryRoot.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase) ? "网络照片库" : "照片库";
            ConnectedDevices.Add(new ConnectedDeviceViewModel("Hanabe 拍照库", kind, LibraryRoot, Directory.Exists(LibraryRoot), "📷", "照片库", "已配置", LibraryRoot));
        }

        foreach (var drive in DriveInfo.GetDrives().OrderBy(drive => drive.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var isReady = drive.IsReady;
                if (!isReady && drive.DriveType != DriveType.Removable && drive.DriveType != DriveType.Network)
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.Name : $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})";
                var detail = isReady
                    ? $"{FormatDriveType(drive.DriveType)} · 可用 {FormatBytes(drive.AvailableFreeSpace)} / {FormatBytes(drive.TotalSize)}"
                    : $"{FormatDriveType(drive.DriveType)} · 未就绪";
                var brand = DetectCameraBrand(drive);
                ConnectedDevices.Add(new ConnectedDeviceViewModel(
                    name,
                    FormatDriveType(drive.DriveType),
                    $"{detail} · {brand.Reason}",
                    isReady,
                    brand.Icon ?? DriveIcon(drive.DriveType),
                    brand.DisplayName,
                    brand.BadgeText,
                    drive.RootDirectory.FullName));
            }
            catch
            {
            }
        }

        if (ConnectedDevices.Count == 0)
        {
            ConnectedDevices.Add(new ConnectedDeviceViewModel("暂未检测到设备", "空", "连接移动硬盘/相机卡或选择 Hanabe 照片库后会显示在这里。", false, "○", "未识别", "等待连接", string.Empty));
        }

        RebuildDeviceGroups();
    }

    private void RebuildDeviceGroups()
    {
        DeviceGroups.Clear();

        foreach (var group in ConnectedDevices.GroupBy(DeviceGroupKey).OrderBy(group => DeviceGroupOrder(group.Key)))
        {
            var devices = group.ToArray();
            DeviceGroups.Add(new DeviceGroupViewModel(
                DeviceGroupTitle(group.Key),
                DeviceGroupIcon(group.Key),
                $"{devices.Length} 个子设备",
                devices));
        }
    }

    private void InspectDevice(ConnectedDeviceViewModel? device)
    {
        SelectedDeviceContents.Clear();

        if (device is null || string.IsNullOrWhiteSpace(device.Path) || !Directory.Exists(device.Path))
        {
            SelectedDeviceTitle = "无法读取设备";
            SelectedDeviceSummary = "这个设备暂时不可访问，可能已断开或系统没有权限。";
            return;
        }

        SelectedDeviceTitle = $"{device.Brand} · {device.Name}";
        StatusMessage = $"正在读取 {device.Name} 的内容…";

        try
        {
            var directories = Directory.EnumerateDirectories(device.Path, "*", SearchOption.TopDirectoryOnly)
                .Take(80)
                .Select(path => new DirectoryInfo(path))
                .ToArray();
            var files = Directory.EnumerateFiles(device.Path, "*", SearchOption.TopDirectoryOnly)
                .Take(120)
                .Select(path => new FileInfo(path))
                .ToArray();

            var mediaFiles = files.Count(info => IsLibraryPreviewFile(info.FullName));
            SelectedDeviceSummary = $"{device.Path} · 文件夹 {directories.Length} 个 · 顶层文件 {files.Length} 个 · 顶层媒体 {mediaFiles} 个";

            foreach (var directory in directories)
            {
                SelectedDeviceContents.Add(new DeviceContentItemViewModel(
                    directory.Name,
                    "文件夹",
                    directory.FullName,
                    "打开查看子内容",
                    "📁"));
            }

            foreach (var file in files)
            {
                SelectedDeviceContents.Add(new DeviceContentItemViewModel(
                    file.Name,
                    Path.GetExtension(file.Name).TrimStart('.').ToUpperInvariant(),
                    file.FullName,
                    FormatBytes(file.Length),
                    IsLibraryPreviewFile(file.FullName) ? "🖼" : "📄"));
            }

            if (SelectedDeviceContents.Count == 0)
            {
                SelectedDeviceContents.Add(new DeviceContentItemViewModel("没有可显示内容", "空", device.Path, "设备根目录为空或内容被系统隐藏", "○"));
            }

            StatusMessage = $"已读取 {device.Name}。";
        }
        catch (Exception ex)
        {
            SelectedDeviceSummary = "读取失败：" + ex.Message;
            SelectedDeviceContents.Add(new DeviceContentItemViewModel("读取失败", "错误", device.Path, ex.Message, "⚠"));
            StatusMessage = "读取设备内容失败。";
        }
    }

    private static string DeviceGroupKey(ConnectedDeviceViewModel device)
    {
        if (device.Brand.Contains("索尼", StringComparison.OrdinalIgnoreCase) || device.BadgeText.Equals("Sony", StringComparison.OrdinalIgnoreCase))
        {
            return "sony";
        }

        if (device.Brand.Contains("大疆", StringComparison.OrdinalIgnoreCase) || device.BadgeText.Equals("DJI", StringComparison.OrdinalIgnoreCase))
        {
            return "dji";
        }

        if (device.Brand.Contains("照片库", StringComparison.OrdinalIgnoreCase))
        {
            return "library";
        }

        if (device.Kind == "本机磁盘")
        {
            return "computer";
        }

        if (device.Kind == "网络设备")
        {
            return "network";
        }

        return "storage";
    }

    private static int DeviceGroupOrder(string key) => key switch
    {
        "computer" => 0,
        "sony" => 1,
        "dji" => 2,
        "library" => 3,
        "network" => 4,
        _ => 5
    };

    private static string DeviceGroupTitle(string key) => key switch
    {
        "computer" => "这台电脑",
        "sony" => "索尼相机 / Sony",
        "dji" => "大疆设备 / DJI",
        "library" => "Hanabe 照片库",
        "network" => "网络设备",
        _ => "其他存储设备"
    };

    private static string DeviceGroupIcon(string key) => key switch
    {
        "computer" => "🖥",
        "sony" => "α",
        "dji" => "DJI",
        "library" => "📷",
        "network" => "⌁",
        _ => "▣"
    };

    private static CameraBrandGuess DetectCameraBrand(DriveInfo drive)
    {
        var nameProbe = $"{drive.Name} {SafeVolumeLabel(drive)}".ToUpperInvariant();
        if (ContainsAny(nameProbe, "PMHOME", "SONY", "ALPHA", "ILCE", "DSC", "MSDCF"))
        {
            return new CameraBrandGuess("索尼设备", "Sony", "卷标/盘符包含 Sony 相机特征", "α", "Sony");
        }

        if (ContainsAny(nameProbe, "DJI", "OSMO", "ACTION"))
        {
            return new CameraBrandGuess("大疆设备", "DJI", "卷标/盘符包含 DJI / Osmo 特征", "DJI", "DJI");
        }

        if (!drive.IsReady || drive.DriveType == DriveType.Fixed)
        {
            return new CameraBrandGuess("普通存储", "未识别", "未发现相机品牌特征", null, "存储");
        }

        try
        {
            var root = drive.RootDirectory.FullName;
            var directories = Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.ToUpperInvariant())
                .ToArray();

            var sonyScore = 0;
            var djiScore = 0;
            var reasons = new List<string>();

            if (directories.Any(name => name is "PMHOME" or "AVF_INFO" or "M4ROOT" or "PRIVATE" or "XDROOT"))
            {
                sonyScore += 3;
                reasons.Add("发现 Sony 常见目录");
            }

            if (directories.Any(name => name is "DJI" or "DJI_PRO" or "MISC"))
            {
                djiScore += 3;
                reasons.Add("发现 DJI 常见目录");
            }

            var samplePaths = EnumerateCameraSamples(root, maxItems: 260).ToArray();
            foreach (var sample in samplePaths)
            {
                var fileName = Path.GetFileName(sample).ToUpperInvariant();
                var extension = Path.GetExtension(sample).ToUpperInvariant();
                var full = sample.ToUpperInvariant();

                if (extension is ".ARW" or ".ARQ" || full.Contains("100MSDCF") || fileName.StartsWith("DSC", StringComparison.Ordinal))
                {
                    sonyScore += 2;
                }

                if (fileName.StartsWith("DJI_", StringComparison.Ordinal) ||
                    full.Contains("100MEDIA") ||
                    extension is ".LRF" or ".SRT" or ".AAC")
                {
                    djiScore += 2;
                }
            }

            if (sonyScore >= djiScore && sonyScore > 0)
            {
                return new CameraBrandGuess("索尼设备", "Sony", reasons.FirstOrDefault() ?? "发现 Sony 原片/目录特征", "α", "Sony");
            }

            if (djiScore > 0)
            {
                return new CameraBrandGuess("大疆设备", "DJI", reasons.FirstOrDefault() ?? "发现 DJI 视频/目录特征", "DJI", "DJI");
            }
        }
        catch
        {
        }

        return new CameraBrandGuess("普通存储", "未识别", "未发现相机品牌特征", null, "存储");
    }

    private static IEnumerable<string> EnumerateCameraSamples(string root, int maxItems)
    {
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));
        var yielded = 0;

        while (queue.Count > 0 && yielded < maxItems)
        {
            var (current, depth) = queue.Dequeue();
            IEnumerable<string> files = [];
            IEnumerable<string> directories = [];

            try
            {
                files = Directory.EnumerateFiles(current, "*", SearchOption.TopDirectoryOnly).Take(80);
                if (depth < 3)
                {
                    directories = Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly).Take(40);
                }
            }
            catch
            {
            }

            foreach (var file in files)
            {
                yield return file;
                yielded++;
                if (yielded >= maxItems)
                {
                    yield break;
                }
            }

            foreach (var directory in directories)
            {
                queue.Enqueue((directory, depth + 1));
            }
        }
    }

    private static string SafeVolumeLabel(DriveInfo drive)
    {
        try
        {
            return drive.IsReady ? drive.VolumeLabel : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private void ChooseCustomBackground()
    {
        using var dialog = new WinForms.OpenFileDialog
        {
            Title = "选择背景图片",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.webp|所有文件|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            try
            {
                CustomBackgroundPath = _assetStore.Import(dialog.FileName, "background");
                BackgroundMode = "自定义图片";
                SaveSettingsImmediately();
                StatusMessage = "背景已复制并永久保存在 Hanabe 内部。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"背景保存失败：{ex.Message}";
            }
        }
    }

    private void ClearCustomBackground()
    {
        _assetStore.Delete("background");
        CustomBackgroundPath = string.Empty;
        if (BackgroundMode == "自定义图片")
        {
            BackgroundMode = "平衡玻璃";
        }
        SaveSettingsImmediately();
        StatusMessage = "已清除自定义背景。";
    }

    private void ChooseCustomAppIcon()
    {
        using var dialog = new WinForms.OpenFileDialog
        {
            Title = "选择应用头像 / 图标",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.ico|所有文件|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            try
            {
                CustomAppIconPath = _assetStore.Import(dialog.FileName, "avatar");
                SaveSettingsImmediately();
                StatusMessage = "头像已复制并永久保存在 Hanabe 内部。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"头像保存失败：{ex.Message}";
            }
        }
    }

    private void ClearCustomAppIcon()
    {
        _assetStore.Delete("avatar");
        CustomAppIconPath = string.Empty;
        SaveSettingsImmediately();
        StatusMessage = "已恢复默认“花”头像。";
    }

    private string PersistExistingAsset(string? path, string assetName)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return _assetStore.Find(assetName) ?? string.Empty;
        }

        try
        {
            return _assetStore.Import(path, assetName);
        }
        catch
        {
            return path;
        }
    }

    private void SaveSettingsImmediately()
    {
        SaveSettingsAsync().GetAwaiter().GetResult();
    }

    private async Task RefreshCloudConnectionAsync()
    {
        try
        {
            var snapshot = await _cloudConnectionService.LoadAsync().ConfigureAwait(true);
            BaiduAppKey = snapshot.BaiduAppKey ?? string.Empty;
            _hasSavedBaiduCredentials = snapshot.BaiduCredentialsConfigured;
            IsBaiduAuthorized = snapshot.BaiduAuthorized;
            BaiduStatus = snapshot.BaiduAuthorized
                ? $"已连接 · 凭据由百度官方授权 · {snapshot.BaiduToken!.ExpiresAt.LocalDateTime:yyyy-MM-dd HH:mm} 到期"
                : snapshot.BaiduCredentialsConfigured
                    ? "已配置 AppKey / AppSecret · 待完成 OAuth 授权"
                    : "未配置 AppKey / AppSecret";
            QuarkStatus = "未连接 · 等待夸克官方 API 授权";
            _pendingBaiduState = null;
            _pendingBaiduAuthorizeUri = null;
            BaiduAuthCode = string.Empty;
        }
        catch (Exception ex)
        {
            BaiduStatus = $"读取云盘设置失败：{ex.Message}";
        }
        finally
        {
            NotifyBaiduCommandStates();
        }
    }

    private void NotifyBaiduCommandStates()
    {
        SaveBaiduCredentialsCommand.NotifyCanExecuteChanged();
        StartBaiduAuthorizationCommand.NotifyCanExecuteChanged();
        CompleteBaiduAuthorizationCommand.NotifyCanExecuteChanged();
        DisconnectBaiduCommand.NotifyCanExecuteChanged();
    }

    private bool CanSaveBaiduCredentials() => !IsBaiduBusy
        && !string.IsNullOrWhiteSpace(BaiduAppKey)
        && !string.IsNullOrWhiteSpace(BaiduAppSecret);

    private bool CanStartBaiduAuthorization() => !IsBaiduBusy
        && !string.IsNullOrWhiteSpace(BaiduAppKey);

    private bool CanCompleteBaiduAuthorization() => !IsBaiduBusy
        && !string.IsNullOrWhiteSpace(BaiduAuthCode)
        && !string.IsNullOrWhiteSpace(BaiduAppKey)
        && _hasSavedBaiduCredentials;

    private void OpenQuarkOfficial()
    {
        _cloudConnectionService.OpenInBrowser(new Uri(CloudConnectionSettingsService.QuarkOfficialUrl));
        QuarkStatus = "已为你打开夸克网盘官网 · 登录后即可上传/下载 · 官方 API 暂未开放";
    }

    private void OpenBaiduConsole()
    {
        _cloudConnectionService.OpenInBrowser(new Uri(CloudConnectionSettingsService.BaiduHelpUrl));
        BaiduStatus = "已为你打开百度网盘开放者中心 · 在「应用管理」中创建/查看 AppKey 与 AppSecret";
    }

    private async Task SaveBaiduCredentialsAsync()
    {
        try
        {
            IsBaiduBusy = true;
            await _cloudConnectionService.SaveBaiduCredentialsAsync(BaiduAppKey, BaiduAppSecret).ConfigureAwait(true);
            // Clear the in-memory secret field once persisted so it never lingers in the UI.
            BaiduAppSecret = string.Empty;
            _hasSavedBaiduCredentials = true;
            BaiduStatus = "已保存 AppKey 与 AppSecret（AppSecret 已用 Windows DPAPI 加密）";
            CompleteBaiduAuthorizationCommand.NotifyCanExecuteChanged();
            await RefreshCloudConnectionAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            BaiduStatus = $"保存百度网盘凭据失败：{ex.Message}";
        }
        finally
        {
            IsBaiduBusy = false;
        }
    }

    private async Task StartBaiduAuthorizationAsync()
    {
        try
        {
            IsBaiduBusy = true;
            var (authorizeUri, state) = _cloudConnectionService.StartBaiduAuthorization(BaiduAppKey);
            _pendingBaiduAuthorizeUri = authorizeUri.AbsoluteUri;
            _pendingBaiduState = state;
            _cloudConnectionService.OpenInBrowser(authorizeUri);
            BaiduStatus = "已在浏览器中打开百度授权页 · 登录并同意后，复制页面上的授权码粘贴到下方输入框";
            await Task.CompletedTask.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            BaiduStatus = $"打开授权页失败：{ex.Message}";
            _pendingBaiduAuthorizeUri = null;
            _pendingBaiduState = null;
        }
        finally
        {
            IsBaiduBusy = false;
        }
    }

    private async Task CompleteBaiduAuthorizationAsync()
    {
        if (!_hasSavedBaiduCredentials)
        {
            BaiduStatus = "请先点「保存凭据」把 AppKey 与 AppSecret 加密存到本机";
            return;
        }

        if (string.IsNullOrWhiteSpace(_pendingBaiduState))
        {
            BaiduStatus = "请先点「打开授权页」在浏览器中授权，然后把页面上的授权码粘贴到这里";
            return;
        }

        var appSecret = _cloudConnectionService.TryReadBaiduAppSecret();
        if (string.IsNullOrWhiteSpace(appSecret))
        {
            BaiduStatus = "未找到本地保存的 AppSecret，请先保存凭据";
            return;
        }

        try
        {
            IsBaiduBusy = true;
            await _cloudConnectionService.CompleteBaiduAuthorizationAsync(
                BaiduAuthCode,
                BaiduAppKey,
                appSecret,
                _pendingBaiduState!).ConfigureAwait(true);
            BaiduAuthCode = string.Empty;
            _pendingBaiduState = null;
            _pendingBaiduAuthorizeUri = null;
            await RefreshCloudConnectionAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            BaiduStatus = $"完成授权失败：{ex.Message}";
        }
        finally
        {
            IsBaiduBusy = false;
        }
    }

    private async Task DisconnectBaiduAsync()
    {
        try
        {
            IsBaiduBusy = true;
            await _cloudConnectionService.DisconnectBaiduAsync().ConfigureAwait(true);
            await RefreshCloudConnectionAsync().ConfigureAwait(true);
            BaiduStatus = "已退出百度网盘授权";
        }
        catch (Exception ex)
        {
            BaiduStatus = $"退出百度网盘失败：{ex.Message}";
        }
        finally
        {
            IsBaiduBusy = false;
        }
    }

    private static ImageSource? LoadBackgroundImage(string path)
    {
        return LoadImage(path, 2200);
    }

    private static ImageSource? LoadImage(string path, int decodePixelWidth)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = decodePixelWidth;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static void SetLaunchAtStartup(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key is null)
            {
                return;
            }

            const string valueName = "HanabePhotoManager";
            if (enabled)
            {
                key.SetValue(valueName, $"\"{Environment.ProcessPath}\"");
            }
            else
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }
        }
        catch
        {
        }
    }

    private static bool IsLaunchAtStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            return key?.GetValue("HanabePhotoManager") is string value && value.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatDriveType(DriveType driveType) => driveType switch
    {
        DriveType.Removable => "可移动设备",
        DriveType.Network => "网络设备",
        DriveType.CDRom => "光盘/读卡器",
        DriveType.Fixed => "本机磁盘",
        _ => "存储设备"
    };

    private static string DriveIcon(DriveType driveType) => driveType switch
    {
        DriveType.Removable => "▣",
        DriveType.Network => "⌁",
        DriveType.CDRom => "◉",
        DriveType.Fixed => "▰",
        _ => "◆"
    };

    private bool CanRunCommand() => !IsBusy;

    private bool CanCancelCurrentTask() => IsBusy && _activeTaskCancellation is { IsCancellationRequested: false };

    private bool CanAnalyzeSource() => !IsBusy && Directory.Exists(SourceFolder);

    private bool CanImportSelected() => !IsBusy && HasLibraryRoot && ImportItems.Any(item => item.IsSelected);

    private bool CanImportFromDevice(ConnectedDeviceViewModel? device) => !IsBusy && device is { IsConnected: true } && Directory.Exists(device.Path);

    private void NotifyCommandStates()
    {
        BrowseLibraryCommand.NotifyCanExecuteChanged();
        BrowseSourceCommand.NotifyCanExecuteChanged();
        AnalyzeSourceCommand.NotifyCanExecuteChanged();
        ImportSelectedCommand.NotifyCanExecuteChanged();
        RefreshLibraryCommand.NotifyCanExecuteChanged();
        ImportFromDeviceCommand.NotifyCanExecuteChanged();
        ImportActionHint = !HasLibraryRoot
            ? "导入按钮不可用：请先选择照片库根目录。"
            : ImportItems.Any(item => item.IsSelected)
                ? "可以导入；复制/校验时进度条会实时显示。"
                : "请选择至少一个要导入的文件组。";
    }

    private static string? PickFolder(string description, string initialPath)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true,
            SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            ShowNewFolderButton = true
        };

        return dialog.ShowDialog() == WinForms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    private static IEnumerable<string> EnumerateImportFiles(string path)
    {
        if (File.Exists(path))
        {
            yield return path;
            yield break;
        }

        if (!Directory.Exists(path))
        {
            yield break;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
        {
            yield return file;
        }
    }

    private static IEnumerable<FileInfo> EnumerateImportFileInfos(
        IEnumerable<string> paths,
        CancellationToken cancellationToken,
        IProgress<ImportFileScanProgress>? progress)
    {
        var matched = 0;
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(path))
            {
                if (ImportExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                {
                    matched++;
                    progress?.Report(new ImportFileScanProgress(Path.GetDirectoryName(path) ?? path, matched));
                    yield return new FileInfo(path);
                }

                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            var pending = new Stack<string>();
            pending.Push(path);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = pending.Pop();
                progress?.Report(new ImportFileScanProgress(current, matched));

                string[] files;
                try
                {
                    files = Directory.GetFiles(current, "*", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    files = [];
                }

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!ImportExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    matched++;
                    if (matched % 50 == 0)
                    {
                        progress?.Report(new ImportFileScanProgress(current, matched));
                    }

                    FileInfo info;
                    try
                    {
                        info = new FileInfo(file);
                    }
                    catch
                    {
                        continue;
                    }

                    yield return info;
                }

                string[] directories;
                try
                {
                    directories = Directory.GetDirectories(current, "*", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    directories = [];
                }

                for (var index = directories.Length - 1; index >= 0; index--)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pending.Push(directories[index]);
                }
            }
        }
    }

    private static string ResolveCameraDateHintPath(string dateHintPath, IReadOnlyCollection<FileInfo> fileInfos)
    {
        if (fileInfos.Count == 0)
        {
            return dateHintPath;
        }

        var root = Directory.Exists(dateHintPath)
            ? Path.GetFullPath(dateHintPath)
            : Path.GetDirectoryName(Path.GetFullPath(dateHintPath));

        var candidates = new Dictionary<string, (string Path, int FileCount)>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in fileInfos)
        {
            var directory = file.Directory;
            while (directory is not null)
            {
                if (IsCameraDateFolderName(directory.Name))
                {
                    var current = candidates.TryGetValue(directory.FullName, out var existing)
                        ? existing
                        : (Path: directory.FullName, FileCount: 0);
                    candidates[directory.FullName] = (Path: directory.FullName, FileCount: current.FileCount + 1);
                }

                if (!string.IsNullOrWhiteSpace(root) &&
                    string.Equals(Path.GetFullPath(directory.FullName), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                directory = directory.Parent;
            }
        }

        return candidates.Values
            .OrderByDescending(candidate => candidate.FileCount)
            .ThenByDescending(candidate => candidate.Path.Length)
            .Select(candidate => candidate.Path)
            .FirstOrDefault() ?? dateHintPath;
    }

    private static bool IsCameraDateFolderName(string folderName)
    {
        var match = Regex.Matches(folderName ?? string.Empty, "[0-9]{4,}")
            .LastOrDefault();
        if (match is null)
        {
            return false;
        }

        var monthDay = match.Value[^4..];
        return int.TryParse(monthDay[..2], CultureInfo.InvariantCulture, out var month)
            && int.TryParse(monthDay[2..], CultureInfo.InvariantCulture, out var day)
            && month is >= 1 and <= 12
            && day is >= 1 and <= 31;
    }

    private static string ResolveDroppedSourceDisplayPath(IReadOnlyList<string> paths)
    {
        var singleDirectory = paths.Count == 1 && Directory.Exists(paths[0]);
        if (singleDirectory)
        {
            return paths[0];
        }

        var parentDirectories = paths
            .Select(path =>
            {
                if (Directory.Exists(path))
                {
                    return path;
                }

                return File.Exists(path) ? Path.GetDirectoryName(path) : null;
            })
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .ToArray();

        return FindCommonDirectory(parentDirectories) ?? parentDirectories.FirstOrDefault() ?? paths[0];
    }

    private static string? FindCommonDirectory(IReadOnlyList<string> directories)
    {
        if (directories.Count == 0)
        {
            return null;
        }

        var commonParts = directories[0]
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var directory in directories.Skip(1))
        {
            var parts = directory
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var count = 0;
            while (count < commonParts.Length &&
                   count < parts.Length &&
                   string.Equals(commonParts[count], parts[count], StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }

            commonParts = commonParts.Take(count).ToArray();
            if (commonParts.Length == 0)
            {
                return null;
            }
        }

        return string.Join(Path.DirectorySeparatorChar, commonParts);
    }

    private static long GetDirectorySize(string path)
    {
        try
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Sum(file =>
                {
                    try
                    {
                        return new FileInfo(file).Length;
                    }
                    catch
                    {
                        return 0L;
                    }
                });
        }
        catch
        {
            return 0;
        }
    }

    private static string TryGetVolumePercent(string path, long selectedSize)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(root))
            {
                return "无法读取磁盘容量";
            }

            var drive = new DriveInfo(root);
            if (drive.TotalSize <= 0)
            {
                return "无法读取磁盘容量";
            }

            return $"{selectedSize * 100d / drive.TotalSize:0.000}% / 磁盘总容量 {FormatBytes(drive.TotalSize)}";
        }
        catch
        {
            return "网络共享容量暂不可读";
        }
    }

    private static string FormatDate(LibraryDate date) => $"{date.Year}/{date.Month}月/{date.Month:00}.{date.Day:00}";

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

public sealed class LibraryDateNode
{
    public LibraryDateNode(string title, string fullPath, LibraryDate? date, IReadOnlyList<LibraryDateNode>? children = null)
    {
        Title = title;
        FullPath = fullPath;
        Date = date;
        Children = new ObservableCollection<LibraryDateNode>(children ?? Array.Empty<LibraryDateNode>());
    }

    public string Title { get; }

    public string FullPath { get; }

    public LibraryDate? Date { get; }

    public ObservableCollection<LibraryDateNode> Children { get; }

    public bool IsSelectable => Date is not null;

    private int _totalFiles;
    public int TotalFiles
    {
        get => _totalFiles;
        set { _totalFiles = value; OnStatChanged(); }
    }

    private int _retouchedFiles;
    public int RetouchedFiles
    {
        get => _retouchedFiles;
        set { _retouchedFiles = value; OnStatChanged(); }
    }

    public string StatSummary { get; private set; } = string.Empty;

    private void OnStatChanged()
    {
        if (TotalFiles > 0)
            StatSummary = $"(已修{RetouchedFiles}/{TotalFiles})";
        else
            StatSummary = string.Empty;
    }

    public override string ToString() =>
        string.IsNullOrWhiteSpace(StatSummary) ? Title : $"{Title} {StatSummary}";
}

public sealed record CategoryChoice(MediaCategory Category, string Display)
{
    public override string ToString() => Display;
}

public sealed record TransferModeChoice(TransferMode Mode, string Display)
{
    public override string ToString() => Display;
}

public enum ActiveTaskKind
{
    None,
    Analysis,
    Import,
    Preview
}

public sealed record CameraBrandGuess(string DisplayName, string Brand, string Reason, string? Icon, string BadgeText);

public sealed record DeviceGroupViewModel(string Name, string Icon, string Subtitle, IReadOnlyList<ConnectedDeviceViewModel> Devices);

public sealed record ConnectedDeviceViewModel(string Name, string Kind, string Detail, bool IsConnected, string Icon, string Brand, string BadgeText, string Path)
{
    public string StateText => IsConnected ? "已连接" : "未连接";
}

public sealed record DeviceContentItemViewModel(string Name, string Kind, string FullPath, string Detail, string Icon);

public sealed record CategorySummaryViewModel(string Name, string FullPath, int FileCount, string SizeText)
{
    public string Subtitle => $"{FileCount} 个文件 · {SizeText}";
}

public sealed record ImportSectionKey(bool NeedsAttention, MediaCategory Category);

public sealed record ImportFileScanProgress(string CurrentFolder, int MatchedFiles);

public sealed class ImportCategorySectionViewModel : ObservableObject
{
    private const int PageSize = 120;
    private int _displayLimit;

    public ImportCategorySectionViewModel(string name, string icon, string subtitle, string accentBrush, IReadOnlyList<ImportPreviewItemViewModel> items)
    {
        Name = name;
        Icon = icon;
        Subtitle = subtitle;
        AccentBrush = accentBrush;
        Items = items;
        _displayLimit = Math.Min(PageSize, Items.Count);
        ShowMoreCommand = new RelayCommand(ShowMore, () => HasHiddenItems);
    }

    public string Name { get; }

    public string Icon { get; }

    public string Subtitle { get; }

    public string AccentBrush { get; }

    public IReadOnlyList<ImportPreviewItemViewModel> Items { get; }

    public IReadOnlyList<ImportPreviewItemViewModel> VisibleItems => Items.Take(_displayLimit).ToArray();

    public int HiddenCount => Math.Max(0, Items.Count - _displayLimit);

    public bool HasHiddenItems => HiddenCount > 0;

    public string RenderHint => HasHiddenItems
        ? $"为防止卡顿，当前先显示 {_displayLimit:N0}/{Items.Count:N0} 张；导入仍会处理本分区全部勾选文件。"
        : $"已显示本分区全部 {Items.Count:N0} 张。";

    public IRelayCommand ShowMoreCommand { get; }

    private void ShowMore()
    {
        _displayLimit = Math.Min(Items.Count, _displayLimit + PageSize);
        OnPropertyChanged(nameof(VisibleItems));
        OnPropertyChanged(nameof(HiddenCount));
        OnPropertyChanged(nameof(HasHiddenItems));
        OnPropertyChanged(nameof(RenderHint));
        ShowMoreCommand.NotifyCanExecuteChanged();
    }
}

public sealed record ImportRunResult(int Success, int Skipped, int Failed, IReadOnlyList<string> Lines);

public sealed record PreviewDateSectionInfo(string Key, string Title);

public sealed record PreviewSortChoice(int Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record InferenceDeviceChoice(string Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record FaceEngineChoice(FaceRecognitionEngineKind Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record FaceProfileChoice(FaceRecognitionProfile Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record BrowseEntryChoice(BrowseEntryMode Value, string Label, string Description)
{
    public override string ToString() => Label;
}

public sealed record CalendarDayViewModel(
    DateOnly? Date,
    string DayText,
    bool IsAvailable,
    bool IsCurrentMonth,
    bool IsSelected);

public sealed class PreviewDateSectionViewModel : ObservableObject
{
    private bool _isExpanded;
    private readonly Action<PreviewDateSectionViewModel, bool>? _expandedChanged;

    public PreviewDateSectionViewModel(
        string key,
        string title,
        IReadOnlyList<PreviewFileViewModel> items,
        bool isExpanded,
        Action<PreviewDateSectionViewModel, bool>? expandedChanged = null,
        bool showHeader = true)
    {
        Key = key;
        Title = title;
        Items = items;
        _isExpanded = isExpanded;
        _expandedChanged = expandedChanged;
        ShowHeader = showHeader;
        ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    public string Key { get; }
    public string Title { get; }
    public IReadOnlyList<PreviewFileViewModel> Items { get; }
    public int Count => Items.Count;
    public bool ShowHeader { get; }

    public string ToggleLabel => IsExpanded ? "收起" : "展开";

    public string ToggleGlyph => IsExpanded ? "⌄" : "›";

    public IRelayCommand ToggleCommand { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(ToggleLabel));
                OnPropertyChanged(nameof(ToggleGlyph));
                _expandedChanged?.Invoke(this, value);
            }
        }
    }
}

public sealed partial class PreviewFileViewModel : ObservableObject
{
    public string Name { get; init; }
    public string Category { get; init; }
    public string FullPath { get; init; }
    public string SizeText { get; init; }
    public string Extension { get; init; }
    [ObservableProperty] private ImageSource? _thumbnail;

    [ObservableProperty] private bool _isSelected;

    [ObservableProperty] private string _smartCategory = "待分类";

    [ObservableProperty] private string _manualTagsDisplay = string.Empty;

    public bool HasManualTags => !string.IsNullOrWhiteSpace(ManualTagsDisplay);

    partial void OnManualTagsDisplayChanged(string value) => OnPropertyChanged(nameof(HasManualTags));

    public PreviewFileViewModel(string name, string category, string fullPath, string sizeText, string extension, ImageSource? thumbnail)
    {
        Name = name;
        Category = category;
        FullPath = fullPath;
        SizeText = sizeText;
        Extension = extension;
        Thumbnail = thumbnail;
    }

    public string Caption => IsRetouched
        ? $"{Category} · 显示修后成品"
        : $"{Category} · {SizeText}";

    public bool HasThumbnail => Thumbnail is not null;

    partial void OnThumbnailChanged(ImageSource? value) => OnPropertyChanged(nameof(HasThumbnail));

    [ObservableProperty] private bool _isRetouched;

    [ObservableProperty] private string? _retouchedPath;

    public string PreviewPath => IsRetouched && !string.IsNullOrWhiteSpace(RetouchedPath)
        ? RetouchedPath
        : FullPath;

    partial void OnIsRetouchedChanged(bool value)
    {
        OnPropertyChanged(nameof(PreviewPath));
        OnPropertyChanged(nameof(Caption));
    }

    partial void OnRetouchedPathChanged(string? value) => OnPropertyChanged(nameof(PreviewPath));

    public int Rating
    {
        get => FileMetaStore.TryGet(FullPath).Rating;
        set => FileMetaStore.Update(FullPath, m => m.Rating = Math.Clamp(value, 0, 5));
    }

    public string Tags
    {
        get => FileMetaStore.TryGet(FullPath).Tags;
        set => FileMetaStore.Update(FullPath, m => m.Tags = value ?? "");
    }
}

public sealed class FileMeta
{
    public int Rating { get; set; }
    public string Tags { get; set; } = "";
}

public static class FileMetaStore
{
    private static readonly ConcurrentDictionary<string, FileMeta> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    public static FileMeta TryGet(string filePath)
    {
        if (Cache.TryGetValue(filePath, out var cached)) return cached;

        var metaPath = GetMetaPath(filePath);
        if (metaPath is null) return new();

        try
        {
            if (File.Exists(metaPath))
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, FileMeta>>(File.ReadAllText(metaPath));
                if (dict is not null && dict.TryGetValue(filePath, out var meta))
                {
                    Cache[filePath] = meta;
                    return meta;
                }
            }
        }
        catch { }

        var empty = new FileMeta();
        Cache[filePath] = empty;
        return empty;
    }

    public static void Update(string filePath, Action<FileMeta> mutate)
    {
        var current = TryGet(filePath);
        mutate(current);
        Cache[filePath] = current;

        var metaPath = GetMetaPath(filePath);
        if (metaPath is null) return;

        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);
                var dict = File.Exists(metaPath)
                    ? JsonSerializer.Deserialize<Dictionary<string, FileMeta>>(File.ReadAllText(metaPath)) ?? new()
                    : new();
                dict[filePath] = current;
                File.WriteAllText(metaPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = false }));
            }
            catch { }
        }
    }

    private static string? GetMetaPath(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        return dir is not null ? Path.Combine(dir, ".hanabe", "meta.json") : null;
    }
}

public sealed class ImportPreviewItemViewModel : ObservableObject
{
    private bool _isSelected = true;
    private CategoryChoice _selectedCategory;
    private ImageSource? _thumbnail;

    public ImportPreviewItemViewModel(MediaGroup group, MediaCategory category, IReadOnlyList<CategoryChoice> categoryChoices, bool requiresConfirmation, LibraryDate? targetDate, ImageSource? thumbnail, string personLabel, int queueIndex)
    {
        Group = group;
        CategoryChoices = categoryChoices;
        RequiresConfirmation = requiresConfirmation;
        TargetDate = targetDate;
        _thumbnail = thumbnail;
        PersonLabel = personLabel;
        QueueIndex = queueIndex;
        _selectedCategory = categoryChoices.First(choice => choice.Category == category);
    }

    public event Action? SelectionChanged;

    public MediaGroup Group { get; }

    public IReadOnlyList<CategoryChoice> CategoryChoices { get; }

    public bool RequiresConfirmation { get; }

    public bool NeedsAttention => SelectedCategory.Category == MediaCategory.Unconfirmed || TargetDate is null;

    public int QueueIndex { get; }

    public string QueueNumber => $"No.{QueueIndex:0000}";

    public LibraryDate? TargetDate { get; }

    public ImageSource? Thumbnail => _thumbnail;

    public string PersonLabel { get; }

    public bool HasPersonLabel => !string.IsNullOrWhiteSpace(PersonLabel);

    public bool HasThumbnail => Thumbnail is not null;

    public bool HasNoThumbnail => Thumbnail is null;

    public void SetThumbnail(ImageSource? thumbnail)
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
        return new MediaGroup(Group.GroupKey, SelectedCategory.Category, Group.Primary, Group.Sidecars);
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
