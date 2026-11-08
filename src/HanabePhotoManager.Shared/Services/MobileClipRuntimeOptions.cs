namespace HanabePhotoManager.App.Services;

/// <summary>控制 MobileCLIP 本地运行时的模型路径和性能参数。</summary>
public static class MobileClipRuntimeOptions
{
    public static int MaximumLabels { get; set; } = 3;
    public static double SimilarityWindow { get; set; } = .10;
    public static string DevicePreference { get; set; } = "自动（NVIDIA 优先）";
}

