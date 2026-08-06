using System.IO;
﻿using System.Text.Json;
using System.Text.Json.Serialization;
using HanabePhotoManager.App.Navigation;

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

        await using var stream = File.OpenRead(_settingsPath);
        return await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options, cancellationToken).ConfigureAwait(false)
               ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
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

    public NavigationDisplayMode NavigationDisplayMode { get; set; } = NavigationDisplayMode.Text;

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
    public int DefaultPreviewSort { get; set; }

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
}
