using HanabePhotoManager.App.Models;

namespace HanabePhotoManager.App.Services;

public interface IPhotoClassifier
{
    string EngineId { get; }

    string Version { get; }

    Task<PhotoClassificationResult> ClassifyAsync(string path, CancellationToken cancellationToken);
}

public sealed record PhotoClassificationResult(
    IReadOnlyList<PhotoLabelScore> Labels,
    string EngineId,
    string EngineVersion,
    string Explanation);
