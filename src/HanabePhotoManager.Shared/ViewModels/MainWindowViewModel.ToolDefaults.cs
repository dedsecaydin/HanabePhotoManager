using System.Globalization;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.ViewModels;

public partial class MainWindowViewModel
{
    private double _defaultCompressionMegabytes = 2, _defaultWatermarkOpacity = .72;
    private bool _defaultCollageLimitSize, _defaultCollageBlur, _defaultWatermarkPreserveMetadata = true, _defaultWatermarkRecursive = true;
    public double DefaultCompressionMegabytes { get => _defaultCompressionMegabytes; set { if (SetProperty(ref _defaultCompressionMegabytes, double.IsFinite(value) ? Math.Clamp(value, .1, 100) : 2)) _ = SaveSettingsAsync(); } }
    public double DefaultWatermarkOpacity { get => _defaultWatermarkOpacity; set { if (SetProperty(ref _defaultWatermarkOpacity, double.IsFinite(value) ? Math.Clamp(value, .05, 1) : .72)) _ = SaveSettingsAsync(); } }
    public bool DefaultCollageLimitSize { get => _defaultCollageLimitSize; set { if (SetProperty(ref _defaultCollageLimitSize, value)) _ = SaveSettingsAsync(); } }
    public bool DefaultCollageBlur { get => _defaultCollageBlur; set { if (SetProperty(ref _defaultCollageBlur, value)) _ = SaveSettingsAsync(); } }
    public bool DefaultWatermarkPreserveMetadata { get => _defaultWatermarkPreserveMetadata; set { if (SetProperty(ref _defaultWatermarkPreserveMetadata, value)) _ = SaveSettingsAsync(); } }
    public bool DefaultWatermarkRecursive { get => _defaultWatermarkRecursive; set { if (SetProperty(ref _defaultWatermarkRecursive, value)) _ = SaveSettingsAsync(); } }

    private void LoadToolDefaults(AppSettings settings)
    {
        DefaultCompressionMegabytes = settings.DefaultCompressionMegabytes;
        DefaultCollageLimitSize = settings.DefaultCollageLimitSize;
        DefaultCollageBlur = settings.DefaultCollageBlur;
        DefaultWatermarkOpacity = settings.DefaultWatermarkOpacity;
        DefaultWatermarkPreserveMetadata = settings.DefaultWatermarkPreserveMetadata;
        DefaultWatermarkRecursive = settings.DefaultWatermarkRecursive;
        Compression.TargetValue = DefaultCompressionMegabytes.ToString(CultureInfo.CurrentCulture);
        Compression.TargetUnit = "MB";
        Compression.CollageLimitOutputSize = DefaultCollageLimitSize;
        Compression.CollageUseBlurredBackground = DefaultCollageBlur;
        Watermark.Opacity = DefaultWatermarkOpacity;
        Watermark.PreserveMetadata = DefaultWatermarkPreserveMetadata;
        Watermark.Recursive = DefaultWatermarkRecursive;
        Watermark.ScanSubfolders = DefaultWatermarkRecursive;
    }

    private void SaveToolDefaults(AppSettings settings)
    {
        settings.DefaultCompressionMegabytes = DefaultCompressionMegabytes;
        settings.DefaultCollageLimitSize = DefaultCollageLimitSize;
        settings.DefaultCollageBlur = DefaultCollageBlur;
        settings.DefaultWatermarkOpacity = DefaultWatermarkOpacity;
        settings.DefaultWatermarkPreserveMetadata = DefaultWatermarkPreserveMetadata;
        settings.DefaultWatermarkRecursive = DefaultWatermarkRecursive;
    }
}
