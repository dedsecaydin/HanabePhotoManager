using System.IO;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using CvSize = OpenCvSharp.Size;

namespace HanabePhotoManager.App.Services;

public interface ILocalFaceEmbeddingService
{
    Task<IReadOnlyList<DetectedFace>> DetectAsync(string path, CancellationToken cancellationToken);
}

public sealed record DetectedFace(
    string SourcePath,
    float[] Embedding,
    int X,
    int Y,
    int Width,
    int Height);

public sealed class LocalFaceEmbeddingService : ILocalFaceEmbeddingService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyList<DetectedFace>> DetectAsync(string path, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => DetectCore(path, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private static IReadOnlyList<DetectedFace> DetectCore(string path, CancellationToken cancellationToken)
    {
        var modelRoot = Path.Combine(AppContext.BaseDirectory, "Models", "Face");
        var detectorPath = Path.Combine(modelRoot, "haarcascade_frontalface_alt2.xml");
        var recognizerPath = Path.Combine(modelRoot, "face_recognition_sface_2021dec.onnx");
        if (!File.Exists(detectorPath) || !File.Exists(recognizerPath)) return [];

        using var source = Cv2.ImRead(path, ImreadModes.Color);
        if (source.Empty()) return [];
        using var image = ResizeForDetection(source, out var scale);
        using var gray = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.EqualizeHist(gray, gray);
        using var detector = new CascadeClassifier(detectorPath);
        using var recognizer = CvDnn.ReadNetFromOnnx(recognizerPath);
        if (recognizer is null) return [];

        var rectangles = detector.DetectMultiScale(
                gray, 1.1, 4, HaarDetectionTypes.ScaleImage,
                new CvSize(42, 42), null)
            .OrderByDescending(rect => rect.Width * rect.Height)
            .Take(12)
            .ToArray();
        var faces = new List<DetectedFace>(rectangles.Length);
        foreach (var rectangle in rectangles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bounds = ExpandToSquare(rectangle, image.Width, image.Height);
            using var crop = new Mat(image, bounds);
            using var blob = CvDnn.BlobFromImage(
                crop, 1d / 128d, new CvSize(112, 112),
                new Scalar(127.5, 127.5, 127.5), swapRB: true, crop: false);
            recognizer.SetInput(blob);
            using var feature = recognizer.Forward();
            var vector = new float[feature.Total()];
            feature.GetArray(out vector);
            Normalize(vector);
            if (vector.Length == 0) continue;
            faces.Add(new DetectedFace(
                path, vector,
                (int)Math.Round(bounds.X / scale),
                (int)Math.Round(bounds.Y / scale),
                (int)Math.Round(bounds.Width / scale),
                (int)Math.Round(bounds.Height / scale)));
        }
        return faces;
    }

    private static Mat ResizeForDetection(Mat source, out double scale)
    {
        const int maximumEdge = 1280;
        var largest = Math.Max(source.Width, source.Height);
        scale = largest <= maximumEdge ? 1 : maximumEdge / (double)largest;
        if (scale >= 1) return source.Clone();
        var resized = new Mat();
        Cv2.Resize(source, resized, new CvSize(), scale, scale, InterpolationFlags.Area);
        return resized;
    }

    private static Rect ExpandToSquare(Rect face, int width, int height)
    {
        var side = (int)Math.Ceiling(Math.Max(face.Width, face.Height) * 1.34);
        var x = Math.Clamp(face.X + face.Width / 2 - side / 2, 0, Math.Max(0, width - 1));
        var y = Math.Clamp(face.Y + face.Height / 2 - side / 2, 0, Math.Max(0, height - 1));
        side = Math.Min(side, Math.Min(width - x, height - y));
        return new Rect(x, y, Math.Max(1, side), Math.Max(1, side));
    }

    private static void Normalize(float[] vector)
    {
        var length = Math.Sqrt(vector.Sum(value => value * value));
        if (length <= 0.000001) return;
        for (var index = 0; index < vector.Length; index++) vector[index] /= (float)length;
    }
}
