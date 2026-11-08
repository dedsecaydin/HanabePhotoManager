using HanabePhotoManager.App.Models;

namespace HanabePhotoManager.App.Services;

/// <summary>定义本地照片内容分类器的统一异步入口。</summary>
public interface IPhotoClassifier
{
    string EngineId { get; }

    string Version { get; }

    Task<PhotoClassificationResult> ClassifyAsync(string path, CancellationToken cancellationToken);
}

/// <summary>分类标签、置信度和可选多标签分数。</summary>
public sealed record PhotoClassificationResult(
    IReadOnlyList<PhotoLabelScore> Labels,
    string EngineId,
    string EngineVersion,
    string Explanation);
