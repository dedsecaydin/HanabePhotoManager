using System.IO;
using HanabePhotoManager.App.Models;
using MetadataExtractor;
using OpenCvSharp;
using CvSize = OpenCvSharp.Size;

namespace HanabePhotoManager.App.Services;

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

        var labels = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var reasons = new List<string>();

        var hasFace = _faceDetector?.Invoke(image) ?? DetectFace(image);
        if (hasFace)
        {
            labels["人像"] = 0.95;
            reasons.Add("检测到人脸");
        }

        using var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        var luminance = Cv2.Mean(gray).Val0;
        if (luminance < 58)
        {
            labels["夜景"] = Math.Clamp(0.72 + (58 - luminance) / 140, 0.72, 0.92);
            reasons.Add("整体亮度较低");
        }

        using var hsv = new Mat();
        Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);
        using var greenMask = new Mat();
        Cv2.InRange(hsv, new Scalar(32, 45, 28), new Scalar(92, 255, 255), greenMask);
        var greenRatio = Cv2.CountNonZero(greenMask) / (double)(greenMask.Rows * greenMask.Cols);
        if (greenRatio >= 0.30)
        {
            labels["自然风景"] = Math.Clamp(0.62 + greenRatio * 0.30, 0.62, 0.88);
            labels["植物"] = Math.Clamp(0.58 + greenRatio * 0.30, 0.58, 0.84);
            reasons.Add("画面中绿色植被占比较高");
        }

        using var edges = new Mat();
        Cv2.Canny(gray, edges, 70, 150);
        var edgeRatio = Cv2.CountNonZero(edges) / (double)(edges.Rows * edges.Cols);
        if (edgeRatio >= 0.12 && greenRatio < 0.22)
        {
            labels["建筑"] = Math.Clamp(0.56 + edgeRatio, 0.56, 0.78);
            labels["城市风光"] = Math.Clamp(0.52 + edgeRatio, 0.52, 0.74);
            reasons.Add("检测到较密集的直线与结构边缘");
        }

        ApplyExifHints(path, labels, reasons);
        cancellationToken.ThrowIfCancellationRequested();

        if (labels.Count == 0)
        {
            labels["待分类"] = 1;
            reasons.Add("没有达到可靠判断阈值");
        }

        var ranked = labels
            .Select(pair => new PhotoLabelScore(pair.Key, Math.Round(pair.Value, 3)))
            .OrderByDescending(label => label.Score)
            .ThenBy(label => label.Label, StringComparer.CurrentCulture)
            .ToArray();
        return new PhotoClassificationResult(ranked, EngineId, Version, string.Join("；", reasons));
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

    private static void ApplyExifHints(
        string path,
        IDictionary<string, double> labels,
        ICollection<string> reasons)
    {
        try
        {
            var text = string.Join(' ', ImageMetadataReader.ReadMetadata(path)
                .SelectMany(directory => directory.Tags)
                .Select(tag => $"{tag.Name} {tag.Description}"));
            var matched = false;
            matched |= AddHint(text, ["food", "dish", "meal", "美食"], "美食", labels);
            matched |= AddHint(text, ["animal", "pet", "dog", "cat", "动物"], "动物", labels);
            matched |= AddHint(text, ["building", "architecture", "建筑"], "建筑", labels);
            if (matched) reasons.Add("EXIF/IPTC 描述提供了类别线索");
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException)
        {
            // Metadata is optional; pixel analysis remains available.
        }
    }

    private static bool AddHint(string text, IEnumerable<string> terms, string label, IDictionary<string, double> labels)
    {
        if (!terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase))) return false;
        labels.TryGetValue(label, out var current);
        labels[label] = Math.Max(current, 0.78);
        return true;
    }
}
