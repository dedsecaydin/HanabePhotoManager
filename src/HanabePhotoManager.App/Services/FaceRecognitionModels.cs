using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace HanabePhotoManager.App.Services;

public enum FaceRecognitionEngineKind
{
    YuNetSFace,
    ArcFaceR100
}

public enum FaceRecognitionProfile
{
    Speed,
    Balanced,
    HighAccuracy
}

public static class FaceRecognitionDefaults
{
    public const double YuNetSFaceThreshold = 0.62;
    public const double YuNetSFaceCurrentThreshold = 0.40;
    public const double ArcFaceR100Threshold = 0.45;
}

public sealed record FaceEngineAvailability(bool IsAvailable, string Reason)
{
    public static FaceEngineAvailability Available { get; } = new(true, string.Empty);
}

public sealed class FaceRecognitionOptions
{
    public FaceRecognitionEngineKind Engine { get; set; } = FaceRecognitionEngineKind.YuNetSFace;
    public FaceRecognitionProfile Profile { get; set; } = FaceRecognitionProfile.Balanced;
    public string? DetectorModelPath { get; set; }
    public string? RecognizerModelPath { get; set; }
    public bool ModelLicenseConfirmed { get; set; }
    public string? ModelLicenseDescription { get; set; }
    public double MatchThreshold { get; set; } = FaceRecognitionDefaults.YuNetSFaceThreshold;
    public int MaxConcurrency { get; set; }
    public int BatchSize { get; set; }

    public FaceEngineAvailability EvaluateAvailability()
    {
        if (Engine == FaceRecognitionEngineKind.YuNetSFace)
            return FaceEngineAvailability.Available;
        if (string.IsNullOrWhiteSpace(DetectorModelPath) || !File.Exists(DetectorModelPath))
            return new(false, "ArcFace 检测器模型缺失。");
        if (string.IsNullOrWhiteSpace(RecognizerModelPath) || !File.Exists(RecognizerModelPath))
            return new(false, "ArcFace R100 识别模型缺失。");
        if (!ModelLicenseConfirmed || string.IsNullOrWhiteSpace(ModelLicenseDescription))
            return new(false, "ArcFace 模型许可未明确确认。");
        return FaceEngineAvailability.Available;
    }
}

public sealed record FaceModelIdentity(
    string StorageKey,
    FaceRecognitionEngineKind Engine,
    string ModelVersion,
    double MatchThreshold,
    int EmbeddingVersion)
{
    public static FaceModelIdentity YuNetSFaceLegacy { get; } =
        new("yunet-sface:v1", FaceRecognitionEngineKind.YuNetSFace, "opencv-yunet-2023mar+sface-2021dec", FaceRecognitionDefaults.YuNetSFaceThreshold, 1);

    public static FaceModelIdentity YuNetSFaceCurrent { get; } =
        new("yunet-sface:v4-raw-rgb-t0.40-q0.75", FaceRecognitionEngineKind.YuNetSFace,
            "opencv-yunet-2023mar+sface-2021dec/raw-rgb", FaceRecognitionDefaults.YuNetSFaceCurrentThreshold, 2);

    public static FaceModelIdentity CreateArcFace(string detectorPath, string recognizerPath, double threshold)
    {
        var fingerprint = HashFiles(detectorPath, recognizerPath);
        return new($"arcface-r100:{fingerprint}:t{threshold:0.####}",
            FaceRecognitionEngineKind.ArcFaceR100, fingerprint, threshold, 2);
    }

    private static string HashFiles(params string[] paths)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in paths)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetFullPath(path)));
            using var stream = File.OpenRead(path);
            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer)) > 0)
                hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()[..16];
    }
}

public sealed class FaceModelMismatchException(string stored, string requested)
    : InvalidOperationException($"人物库模型不匹配：已存储 {stored}，当前请求 {requested}。禁止混用向量。");
