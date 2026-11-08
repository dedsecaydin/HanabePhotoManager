using System.IO;
using OpenCvSharp;
using CvSize = OpenCvSharp.Size;

namespace HanabePhotoManager.App.Services;

/// <summary>在模型不可用时按文件名和基础图像特征提供确定性的分类回退。</summary>
public sealed class RuleBasedPhotoClassifier : IPhotoClassifier
{
    private const int AnalysisEdge = 320;
    private readonly Func<Mat, bool>? _faceDetector;

    public RuleBasedPhotoClassifier(Func<Mat, bool>? faceDetector = null)
    {
        _faceDetector = faceDetector;
    }

    public string EngineId => "rules";

    public string Version => "rules-1.0";

    public Task<PhotoClassificationResult> ClassifyAsync(string path, CancellationToken cancellationToken) =>
        Task.Run(() => ClassifyCore(path, cancellationToken), cancellationToken);

    private PhotoClassificationResult ClassifyCore(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path)) throw new FileNotFoundException("找不到要分析的照片。", path);

        using var original = Cv2.ImRead(path, ImreadModes.Color);
        if (original.Empty()) throw new InvalidDataException("无法解码照片。请确认它是受支持的图像格式。");
        cancellationToken.ThrowIfCancellationRequested();

        var scale = Math.Min(1d, AnalysisEdge / (double)Math.Max(original.Width, original.Height));
        using var image = new Mat();
        if (scale < 1)
            Cv2.Resize(original, image, new CvSize(), scale, scale, InterpolationFlags.Area);
        else
            original.CopyTo(image);

        var hasFace = _faceDetector?.Invoke(image) ?? DetectFace(image);

        using var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        var luminance = Cv2.Mean(gray).Val0;

        using var hsv = new Mat();
        Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);
        using var greenMask = new Mat();
        Cv2.InRange(hsv, new Scalar(32, 45, 28), new Scalar(92, 255, 255), greenMask);
        var greenRatio = Cv2.CountNonZero(greenMask) / (double)(greenMask.Rows * greenMask.Cols);

        using var edges = new Mat();
        Cv2.Canny(gray, edges, 70, 150);
        var edgeRatio = Cv2.CountNonZero(edges) / (double)(edges.Rows * edges.Cols);
        cancellationToken.ThrowIfCancellationRequested();
        return RuleBasedClassificationPolicy.Classify(luminance, greenRatio, edgeRatio, hasFace, path);
    }

    private static bool DetectFace(Mat image)
    {
        var modelPath = ResolveModelPath("haarcascade_frontalface_alt2.xml");
        if (modelPath is null) return false;
        try
        {
            using var detector = new CascadeClassifier(modelPath);
            using var gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.EqualizeHist(gray, gray);
            return detector.DetectMultiScale(gray, 1.12, 4, HaarDetectionTypes.ScaleImage, new CvSize(28, 28)).Length > 0;
        }
        catch (OpenCVException)
        {
            return false;
        }
    }

    private static string? ResolveModelPath(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Models", "Face", fileName),
            Path.Combine(AppContext.BaseDirectory, fileName)
        };
        return candidates.FirstOrDefault(File.Exists);
    }

}
