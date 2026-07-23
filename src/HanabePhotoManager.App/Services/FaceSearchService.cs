using System.IO;
using System.Text.Json;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using CvSize = OpenCvSharp.Size;

namespace HanabePhotoManager.App.Services;

/// <summary>
/// Fully local face search powered by OpenCV YuNet + SFace. No image or face
/// feature leaves the computer. The library cache is keyed by path, size and
/// modification time so repeat searches do not decode the whole library again.
/// </summary>
public sealed class FaceSearchService
{
    private static readonly HashSet<string> SearchableExtensions = new(
        [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tif", ".tiff"],
        StringComparer.OrdinalIgnoreCase);

    private readonly string _cachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HanabePhotoManager", "FaceSearch", "face-features.json");

    public async Task<FaceReference> CreateReferenceAsync(string imagePath, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var engine = FaceEngine.Create();
            var embeddings = engine.ExtractEmbeddings(imagePath, cancellationToken);
            if (embeddings.Count == 0)
            {
                throw new InvalidOperationException("参考图中没有检测到清晰的正面人脸，请换一张脸更大、光线更好的照片。");
            }

            // The detector returns high confidence/larger faces first. For a
            // reference image the first face is the intended subject.
            return new FaceReference(imagePath, embeddings[0]);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<FaceSearchMatch>> SearchAsync(
        FaceReference reference,
        string libraryRoot,
        double minimumSimilarity,
        IProgress<FaceSearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (!Directory.Exists(libraryRoot))
        {
            throw new DirectoryNotFoundException("照片库路径不存在或当前无法访问。");
        }

        return await Task.Run(() => SearchCore(
            reference, libraryRoot, minimumSimilarity, progress, cancellationToken), cancellationToken);
    }

    private IReadOnlyList<FaceSearchMatch> SearchCore(
        FaceReference reference,
        string libraryRoot,
        double minimumSimilarity,
        IProgress<FaceSearchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var files = EnumerateImages(libraryRoot, cancellationToken).ToArray();
        var cache = LoadCache();
        var matches = new List<FaceSearchMatch>();
        var cacheChanged = false;

        using var engine = FaceEngine.Create();
        try
        {
            for (var index = 0; index < files.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = files[index];
                var fingerprint = CreateFingerprint(path);
                IReadOnlyList<float[]> embeddings;
                var fromCache = cache.TryGetValue(path, out var entry) && entry.Fingerprint == fingerprint;

                if (fromCache)
                {
                    embeddings = entry!.Embeddings;
                }
                else
                {
                    embeddings = engine.ExtractEmbeddings(path, cancellationToken);
                    cache[path] = new FaceCacheEntry(fingerprint, embeddings.Select(v => v.ToArray()).ToArray());
                    cacheChanged = true;
                }

                var best = embeddings.Count == 0
                    ? 0d
                    : embeddings.Max(candidate => CosineSimilarity(reference.Embedding, candidate));
                var didMatch = best >= minimumSimilarity;
                if (didMatch)
                {
                    matches.Add(new FaceSearchMatch(path, best, embeddings.Count));
                }

                // Cached scans can process thousands of files per second. Throttling
                // avoids flooding the WPF dispatcher and keeps stop/navigation responsive.
                if (!fromCache || didMatch || index % 12 == 0 || index == files.Length - 1)
                {
                    progress?.Report(new FaceSearchProgress(
                        index + 1, files.Length, matches.Count, fromCache, Path.GetFileName(path)));
                }
            }
        }
        finally
        {
            // A cancelled first scan still keeps everything completed so far.
            if (cacheChanged) SaveCache(cache, libraryRoot);
        }

        return matches
            .OrderByDescending(item => item.Similarity)
            .ThenByDescending(item => File.GetLastWriteTimeUtc(item.Path))
            .Take(300)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateImages(string root, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            IEnumerable<string> childDirectories = [];
            IEnumerable<string> childFiles = [];
            try
            {
                childDirectories = Directory.EnumerateDirectories(directory);
                childFiles = Directory.EnumerateFiles(directory);
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }

            foreach (var child in childDirectories)
            {
                if (!string.Equals(Path.GetFileName(child), ".hanabe", StringComparison.OrdinalIgnoreCase))
                {
                    pending.Push(child);
                }
            }

            foreach (var file in childFiles)
            {
                if (SearchableExtensions.Contains(Path.GetExtension(file)))
                {
                    yield return file;
                }
            }
        }
    }

    private Dictionary<string, FaceCacheEntry> LoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath)) return new(StringComparer.OrdinalIgnoreCase);
            return JsonSerializer.Deserialize<Dictionary<string, FaceCacheEntry>>(File.ReadAllText(_cachePath))
                   ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveCache(Dictionary<string, FaceCacheEntry> cache, string libraryRoot)
    {
        try
        {
            var fullRoot = Path.GetFullPath(libraryRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var compact = cache
                .Where(pair => pair.Key.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            var temporary = _cachePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(compact));
            File.Move(temporary, _cachePath, true);
        }
        catch
        {
            // A cache failure must never make face search fail.
        }
    }

    private static string CreateFingerprint(string path)
    {
        var info = new FileInfo(path);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }

    private static double CosineSimilarity(IReadOnlyList<float> first, IReadOnlyList<float> second)
    {
        if (first.Count == 0 || first.Count != second.Count) return 0;
        double dot = 0, firstLength = 0, secondLength = 0;
        for (var index = 0; index < first.Count; index++)
        {
            dot += first[index] * second[index];
            firstLength += first[index] * first[index];
            secondLength += second[index] * second[index];
        }

        var denominator = Math.Sqrt(firstLength) * Math.Sqrt(secondLength);
        return denominator <= 0.000001 ? 0 : Math.Clamp(dot / denominator, -1, 1);
    }

    private sealed class FaceEngine : IDisposable
    {
        private readonly CascadeClassifier _detector;
        private readonly Net _recognizer;

        private FaceEngine(CascadeClassifier detector, Net recognizer)
        {
            _detector = detector;
            _recognizer = recognizer;
        }

        public static FaceEngine Create()
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
            return new FaceEngine(detector, recognizer);
        }

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

    private sealed record FaceCacheEntry(string Fingerprint, float[][] Embeddings);
}

public sealed record FaceReference(string Path, float[] Embedding);

public sealed record FaceSearchMatch(string Path, double Similarity, int FacesInImage);

public sealed record FaceSearchProgress(int Processed, int Total, int Matches, bool FromCache, string CurrentFile);
