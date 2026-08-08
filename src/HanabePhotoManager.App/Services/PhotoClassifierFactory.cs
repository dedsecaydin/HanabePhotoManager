using System.IO;

namespace HanabePhotoManager.App.Services;

public static class PhotoClassifierFactory
{
    public const string RulesMode = "轻量规则识别";
    public const string OnnxMode = "本地 ONNX 多标签识别";

    public const string MobileClipMode = "本地 MobileCLIP 高精度语义识别（推荐）";
    public const string SigLip2Mode = "本地 SigLIP2 高精度语义识别";
    public static IReadOnlyList<string> SemanticModes { get; } = [MobileClipMode, SigLip2Mode];

    public static IPhotoClassifier Create(string? mode)
    {
        var rules = new RuleBasedPhotoClassifier();
        if (string.Equals(mode, MobileClipMode, StringComparison.Ordinal))
        {
            var mobileClip = Path.Combine(AppContext.BaseDirectory, "Models", "MobileCLIP");
            return new MobileClipPhotoClassifier(
                Path.Combine(mobileClip, "mobileclip_s2_visual.onnx"),
                Path.Combine(mobileClip, "label_embeddings.json"), rules);
        }
        if (string.Equals(mode, SigLip2Mode, StringComparison.Ordinal))
        {
            var sigLip2 = Path.Combine(AppContext.BaseDirectory, "Models", "SigLIP2");
            return new SigLip2PhotoClassifier(
                Path.Combine(sigLip2, "siglip2_visual.onnx"),
                Path.Combine(sigLip2, "label_embeddings.json"), rules);
        }
        if (!string.Equals(mode, OnnxMode, StringComparison.Ordinal)) return rules;

        var directory = Path.Combine(AppContext.BaseDirectory, "Models", "Classification");
        return new OnnxPhotoClassifier(
            Path.Combine(directory, "mobilenetv2-7.onnx"),
            Path.Combine(directory, "imagenet_classes.txt"),
            rules,
            OnnxPhotoClassifier.OfficialModelSha256);
    }
}
