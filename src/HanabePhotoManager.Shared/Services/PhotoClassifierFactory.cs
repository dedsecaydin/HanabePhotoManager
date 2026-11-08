namespace HanabePhotoManager.App.Services;

public static class PhotoClassifierFactory
{
    public const string RulesMode = "轻量规则识别";
    public const string OnnxMode = "本地 ONNX 多标签识别";
    public const string MobileClipMode = "本地 MobileCLIP 高精度语义识别（推荐）";

    public static Func<string?, IPhotoClassifier> PlatformFactory { get; set; } =
        _ => throw new PlatformNotSupportedException("此平台尚未配置照片分类器。");

    public static IPhotoClassifier Create(string? mode) => PlatformFactory(mode);
}
