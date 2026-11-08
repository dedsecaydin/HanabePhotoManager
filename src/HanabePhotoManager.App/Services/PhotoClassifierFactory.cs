using System.IO;

namespace HanabePhotoManager.App.Services;

/// <summary>按设置、模型可用性和运行环境选择照片分类器实现。</summary>
internal static class WindowsPhotoClassifierFactory
{
    public static IPhotoClassifier Create(string? mode)
    {
        var rules = new RuleBasedPhotoClassifier();
        if (string.Equals(mode, PhotoClassifierFactory.MobileClipMode, StringComparison.Ordinal))
        {
            var mobileClip = Path.Combine(AppContext.BaseDirectory, "Models", "MobileCLIP");
            return new MobileClipPhotoClassifier(
                Path.Combine(mobileClip, "mobileclip_s2_visual.onnx"),
                Path.Combine(mobileClip, "label_embeddings.json"), rules);
        }
        if (!string.Equals(mode, PhotoClassifierFactory.OnnxMode, StringComparison.Ordinal)) return rules;

        var directory = Path.Combine(AppContext.BaseDirectory, "Models", "Classification");
        return new OnnxPhotoClassifier(
            Path.Combine(directory, "mobilenetv2-7.onnx"),
            Path.Combine(directory, "imagenet_classes.txt"),
            rules,
            OnnxPhotoClassifier.OfficialModelSha256);
    }
}
