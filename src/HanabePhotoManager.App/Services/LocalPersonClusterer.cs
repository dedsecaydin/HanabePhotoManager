using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.App.Services;

public sealed class LocalPersonClusterer
{
    private static readonly TimeSpan PerImageTimeout = TimeSpan.FromSeconds(2);

    private static readonly HashSet<string> JpegExtensions = new([".jpg", ".jpeg"], StringComparer.OrdinalIgnoreCase);

    public Task<PersonClusteringResult> ClusterAsync(
        IReadOnlyList<MediaGroup> groups,
        IProgress<PersonClusteringProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return Task.Run(() => Cluster(groups, progress, cancellationToken), cancellationToken);
    }

    private static PersonClusteringResult Cluster(
        IReadOnlyList<MediaGroup> groups,
        IProgress<PersonClusteringProgress>? progress,
        CancellationToken cancellationToken)
    {
        var candidates = new List<PersonImageFeature>();
        var jpegGroups = groups.Where(IsJpegGroup).ToArray();
        var total = Math.Max(jpegGroups.Length, 1);

        for (var index = 0; index < jpegGroups.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var group = jpegGroups[index];
            var featurePath = group.Primary.FullPath;
            if (TryExtractFeatureWithTimeout(featurePath, cancellationToken, out var vector))
            {
                candidates.Add(new PersonImageFeature(group.GroupKey, group.Primary.FullPath, vector));
            }

            progress?.Report(new PersonClusteringProgress(index + 1, total, candidates.Count));
        }

        if (candidates.Count == 0)
        {
            return new PersonClusteringResult(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), 0, 0);
        }

        var clusters = new List<List<PersonImageFeature>>();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bestIndex = -1;
            var bestDistance = double.MaxValue;
            for (var clusterIndex = 0; clusterIndex < clusters.Count; clusterIndex++)
            {
                var distance = Distance(candidate.Vector, Centroid(clusters[clusterIndex]));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = clusterIndex;
                }
            }

            if (bestIndex >= 0 && bestDistance <= 0.34)
            {
                clusters[bestIndex].Add(candidate);
            }
            else
            {
                clusters.Add([candidate]);
            }
        }

        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var orderedClusters = clusters
            .OrderByDescending(cluster => cluster.Count)
            .ThenBy(cluster => cluster.Min(item => item.PrimaryPath), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (var index = 0; index < orderedClusters.Length; index++)
        {
            var label = $"人物{index + 1:00}";
            foreach (var item in orderedClusters[index])
            {
                labels[item.PrimaryPath] = label;
            }
        }

        PropagateLabelsByStem(groups, labels);
        return new PersonClusteringResult(labels, candidates.Count, orderedClusters.Length);
    }

    private static bool IsJpegGroup(MediaGroup group) =>
        group.Category == MediaCategory.Jpeg &&
        JpegExtensions.Contains(Path.GetExtension(group.Primary.FullPath)) &&
        File.Exists(group.Primary.FullPath);

    private static bool TryExtractFeature(string path, out double[] vector)
    {
        vector = [];
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.DecodePixelWidth = 96;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            BitmapSource source = bitmap;
            if (source.Format != PixelFormats.Bgra32)
            {
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                source.Freeze();
            }

            var width = source.PixelWidth;
            var height = source.PixelHeight;
            if (width <= 8 || height <= 8)
            {
                return false;
            }

            var stride = width * 4;
            var pixels = new byte[stride * height];
            source.CopyPixels(pixels, stride, 0);

            var features = new List<double>(48);
            AddRegionHistogram(pixels, width, height, stride, width / 4, 0, width / 2, height / 2, features, weight: 1.25); // 发型/脸部大概区域
            AddRegionHistogram(pixels, width, height, stride, width / 5, height / 3, width * 3 / 5, height / 2, features, weight: 1.6); // 服装区域权重大
            AddRegionHistogram(pixels, width, height, stride, 0, 0, width, height, features, weight: 0.55); // 整体色调

            vector = features.ToArray();
            Normalize(vector);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryExtractFeatureWithTimeout(string path, CancellationToken cancellationToken, out double[] vector)
    {
        vector = [];
        var extraction = Task.Run(() =>
        {
            var success = TryExtractFeature(path, out var extractedVector);
            return (Success: success, Vector: extractedVector);
        });

        try
        {
            var result = extraction
                .WaitAsync(PerImageTimeout, cancellationToken)
                .GetAwaiter()
                .GetResult();
            vector = result.Vector;
            return result.Success;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static void AddRegionHistogram(
        byte[] pixels,
        int imageWidth,
        int imageHeight,
        int stride,
        int startX,
        int startY,
        int width,
        int height,
        ICollection<double> output,
        double weight)
    {
        var bins = new double[12];
        var endX = Math.Clamp(startX + width, 0, imageWidth);
        var endY = Math.Clamp(startY + height, 0, imageHeight);
        startX = Math.Clamp(startX, 0, imageWidth - 1);
        startY = Math.Clamp(startY, 0, imageHeight - 1);

        var count = 0;
        for (var y = startY; y < endY; y += 2)
        {
            var row = y * stride;
            for (var x = startX; x < endX; x += 2)
            {
                var offset = row + x * 4;
                var b = pixels[offset] / 255d;
                var g = pixels[offset + 1] / 255d;
                var r = pixels[offset + 2] / 255d;
                var max = Math.Max(r, Math.Max(g, b));
                var min = Math.Min(r, Math.Min(g, b));
                var value = max;
                var saturation = max <= 0 ? 0 : (max - min) / max;
                var hue = Hue(r, g, b, max, min);
                var hueBin = Math.Clamp((int)(hue / 45d), 0, 7);
                bins[hueBin] += saturation * weight;
                bins[8 + Math.Clamp((int)(value * 4), 0, 3)] += (1.0 - saturation * 0.35) * weight;
                count++;
            }
        }

        if (count == 0)
        {
            count = 1;
        }

        foreach (var bin in bins)
        {
            output.Add(bin / count);
        }
    }

    private static double Hue(double r, double g, double b, double max, double min)
    {
        var delta = max - min;
        if (delta <= 0.0001)
        {
            return 0;
        }

        double hue;
        if (Math.Abs(max - r) < 0.0001)
        {
            hue = 60 * (((g - b) / delta) % 6);
        }
        else if (Math.Abs(max - g) < 0.0001)
        {
            hue = 60 * (((b - r) / delta) + 2);
        }
        else
        {
            hue = 60 * (((r - g) / delta) + 4);
        }

        return hue < 0 ? hue + 360 : hue;
    }

    private static double[] Centroid(IReadOnlyList<PersonImageFeature> cluster)
    {
        var length = cluster[0].Vector.Length;
        var centroid = new double[length];
        foreach (var item in cluster)
        {
            for (var index = 0; index < length; index++)
            {
                centroid[index] += item.Vector[index];
            }
        }

        for (var index = 0; index < length; index++)
        {
            centroid[index] /= cluster.Count;
        }

        Normalize(centroid);
        return centroid;
    }

    private static double Distance(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        var sum = 0d;
        for (var index = 0; index < a.Count; index++)
        {
            var diff = a[index] - b[index];
            sum += diff * diff;
        }

        return Math.Sqrt(sum);
    }

    private static void Normalize(double[] vector)
    {
        var magnitude = Math.Sqrt(vector.Sum(value => value * value));
        if (magnitude <= 0.000001)
        {
            return;
        }

        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] /= magnitude;
        }
    }

    private static void PropagateLabelsByStem(IReadOnlyList<MediaGroup> groups, IDictionary<string, string> labels)
    {
        var stemLabels = labels
            .GroupBy(pair => CreateStemKey(pair.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var stemKey = CreateStemKey(group.Primary.FullPath);
            if (!labels.ContainsKey(group.Primary.FullPath) && stemLabels.TryGetValue(stemKey, out var label))
            {
                labels[group.Primary.FullPath] = label;
            }
        }
    }

    private static string CreateStemKey(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
        return $"{directory}\0{Path.GetFileNameWithoutExtension(path)}";
    }

    private sealed record PersonImageFeature(string GroupKey, string PrimaryPath, double[] Vector);
}

public sealed record PersonClusteringProgress(int Processed, int Total, int Recognized);

public sealed record PersonClusteringResult(IReadOnlyDictionary<string, string> LabelsByPrimaryPath, int RecognizedImages, int ClusterCount);
