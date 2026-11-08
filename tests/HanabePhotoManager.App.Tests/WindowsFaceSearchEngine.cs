using System.IO;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using CvSize = OpenCvSharp.Size;

namespace HanabePhotoManager.App.Services;

internal sealed class WindowsFaceSearchEngine : IFaceSearchEmbeddingEngine
{
    private readonly CascadeClassifier _detector;
    private readonly Net _recognizer;

    private WindowsFaceSearchEngine(CascadeClassifier detector, Net recognizer)
    {
        _detector = detector;
        _recognizer = recognizer;
    }

    public static WindowsFaceSearchEngine Create()
    {
        var modelRoot = Path.Combine(AppContext.BaseDirectory, "Models", "Face");
        var detectorPath = Path.Combine(modelRoot, "haarcascade_frontalface_alt2.xml");
        var recognizerPath = Path.Combine(modelRoot, "face_recognition_sface_2021dec.onnx");
        if (!File.Exists(detectorPath) || !File.Exists(recognizerPath))
        {
            throw new FileNotFoundException("本地人脸识别模型缺失，请重新安装完整版本。");
        }

        var detector = new CascadeClassifier(detectorPath);
        var recognizer = CvDnn.ReadNetFromOnnx(recognizerPath)
                         ?? throw new InvalidOperationException("无法加载本地人脸特征模型。");
        return new WindowsFaceSearchEngine(detector, recognizer);
    }

    public string CacheIdentity => "legacy-haar-sface-v1";

    public IReadOnlyList<float[]> ExtractEmbeddings(string path, CancellationToken cancellationToken)
    {
        using var image = Cv2.ImRead(path, ImreadModes.Color);
        if (image.Empty()) return [];

        // Downscale very large originals for detection. SFace still receives
        // the aligned 112x112 crop, so this saves memory without changing its input.
        using var detectionImage = ResizeForDetection(image);
        using var gray = new Mat();
        Cv2.CvtColor(detectionImage, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.EqualizeHist(gray, gray);
        var faces = _detector.DetectMultiScale(
            gray, 1.1, 4, HaarDetectionTypes.ScaleImage,
            new CvSize(42, 42), null)
            .OrderByDescending(rect => rect.Width * rect.Height)
            .Take(8)
            .ToArray();
        if (faces.Length == 0) return [];

        var results = new List<float[]>(faces.Length);
        foreach (var face in faces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cropBounds = ExpandToSquare(face, detectionImage.Width, detectionImage.Height);
            using var crop = new Mat(detectionImage, cropBounds);
            using var blob = CvDnn.BlobFromImage(
                crop, 1d / 128d, new CvSize(112, 112),
                new Scalar(127.5, 127.5, 127.5), swapRB: true, crop: false);
            _recognizer.SetInput(blob);
            using var feature = _recognizer.Forward();
            var vector = new float[feature.Total()];
            feature.GetArray(out vector);
            Normalize(vector);
            if (vector.Length > 0) results.Add(vector);
        }

        return results;
    }

    private static Mat ResizeForDetection(Mat source)
    {
        const int maximumEdge = 1280;
        var largest = Math.Max(source.Width, source.Height);
        if (largest <= maximumEdge) return source.Clone();
        var scale = maximumEdge / (double)largest;
        var resized = new Mat();
        Cv2.Resize(source, resized, new CvSize(), scale, scale, InterpolationFlags.Area);
        return resized;
    }

    private static Rect ExpandToSquare(Rect face, int imageWidth, int imageHeight)
    {
        var side = (int)Math.Ceiling(Math.Max(face.Width, face.Height) * 1.34);
        var centerX = face.X + face.Width / 2;
        var centerY = face.Y + face.Height / 2;
        var x = Math.Clamp(centerX - side / 2, 0, Math.Max(0, imageWidth - 1));
        var y = Math.Clamp(centerY - side / 2, 0, Math.Max(0, imageHeight - 1));
        side = Math.Min(side, Math.Min(imageWidth - x, imageHeight - y));
        return new Rect(x, y, Math.Max(1, side), Math.Max(1, side));
    }

    private static void Normalize(float[] vector)
    {
        var length = Math.Sqrt(vector.Sum(value => value * value));
        if (length <= 0.000001) return;
        for (var index = 0; index < vector.Length; index++) vector[index] /= (float)length;
    }

    public void Dispose()
    {
        _detector.Dispose();
        _recognizer.Dispose();
    }
}

