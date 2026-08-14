namespace HanabePhotoManager.Infrastructure.Search;

public sealed class ModelCatalog
{
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

    public string GetMissingModelMessage() => IsReady
        ? "语义搜索模型已就绪。"
        : "语义搜索模型未就绪。请按 docs/features/semantic-search.md 下载 Chinese-CLIP 模型到本机应用数据目录。";
}
