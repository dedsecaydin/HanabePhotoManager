namespace HanabePhotoManager.Infrastructure.Search;

/// <summary>集中解析 Chinese-CLIP 模型文件路径并报告本地就绪状态。</summary>
public sealed class ModelCatalog
{
    /// <summary>使用显式模型目录，或回退到当前用户的本地应用数据目录。</summary>
    public ModelCatalog(string? modelRoot = null)
    {
        ModelRoot = string.IsNullOrWhiteSpace(modelRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HanabePhotoManager", "models", "ChineseCLIP")
            : Path.GetFullPath(modelRoot);
    }

    public string ModelRoot { get; }
    public string ImageEncoderPath => Path.Combine(ModelRoot, "image_encoder.onnx");
    public string TextEncoderPath => Path.Combine(ModelRoot, "text_encoder.onnx");
    public string VocabularyPath => Path.Combine(ModelRoot, "vocab.txt");
    public bool IsReady => File.Exists(ImageEncoderPath) && File.Exists(TextEncoderPath) && File.Exists(VocabularyPath);

    /// <summary>返回可直接展示给用户的模型状态说明。</summary>
    public string GetMissingModelMessage() => IsReady
        ? "语义搜索模型已就绪。"
        : "语义搜索模型未就绪。请按 docs/features/semantic-search.md 下载 Chinese-CLIP 模型到本机应用数据目录。";
}
