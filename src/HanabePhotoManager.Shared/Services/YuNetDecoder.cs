using System.Numerics;
using Microsoft.ML.OnnxRuntime;

namespace HanabePhotoManager.App.Services;

/// <summary>YuNet 输出解码后的候选人脸。</summary>
internal sealed record YuNetDetection(float X, float Y, float Width, float Height, float Score, Vector2[] Landmarks);

/// <summary>把 YuNet 多尺度张量输出解码为边界框和五点关键点。</summary>
internal static class YuNetDecoder
{
    public static IReadOnlyList<YuNetDetection> Decode(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs,
        int inputEdge, float scoreThreshold, float nmsThreshold)
    {
        var map = outputs.ToDictionary(output => output.Name, StringComparer.OrdinalIgnoreCase);
        var candidates = new List<YuNetDetection>();
        foreach (var stride in new[] { 8, 16, 32 })
        {
            if (!map.TryGetValue($"cls_{stride}", out var cls)
                || !map.TryGetValue($"obj_{stride}", out var obj)
                || !map.TryGetValue($"bbox_{stride}", out var bbox)
                || !map.TryGetValue($"kps_{stride}", out var kps)) continue;
            var classifications = cls.AsEnumerable<float>().ToArray();
            var objectness = obj.AsEnumerable<float>().ToArray();
            var boxes = bbox.AsEnumerable<float>().ToArray();
            var keypoints = kps.AsEnumerable<float>().ToArray();
            var columns = inputEdge / stride;
            for (var index = 0; index < classifications.Length; index++)
            {
                var score = MathF.Sqrt(Math.Clamp(classifications[index], 0, 1)
                                       * Math.Clamp(objectness[index], 0, 1));
                if (score < scoreThreshold) continue;
                var row = index / columns;
                var column = index % columns;
                var boxOffset = index * 4;
                var centerX = (column + boxes[boxOffset]) * stride;
                var centerY = (row + boxes[boxOffset + 1]) * stride;
                var width = MathF.Exp(boxes[boxOffset + 2]) * stride;
                var height = MathF.Exp(boxes[boxOffset + 3]) * stride;
                var landmarks = new Vector2[5];
                for (var point = 0; point < 5; point++)
                    landmarks[point] = new(
                        (column + keypoints[index * 10 + point * 2]) * stride,
                        (row + keypoints[index * 10 + point * 2 + 1]) * stride);
                candidates.Add(new(centerX - width / 2, centerY - height / 2, width, height, score, landmarks));
            }
        }
        return NonMaximumSuppression(candidates, nmsThreshold);
    }

    internal static IReadOnlyList<YuNetDetection> NonMaximumSuppression(
        IEnumerable<YuNetDetection> candidates, float threshold)
    {
        var ordered = candidates.OrderByDescending(item => item.Score).ToList();
        var kept = new List<YuNetDetection>();
        foreach (var candidate in ordered)
        {
            if (kept.All(existing => IntersectionOverUnion(candidate, existing) <= threshold))
                kept.Add(candidate);
        }
        return kept;
    }

    private static float IntersectionOverUnion(YuNetDetection first, YuNetDetection second)
    {
        var x1 = Math.Max(first.X, second.X);
        var y1 = Math.Max(first.Y, second.Y);
        var x2 = Math.Min(first.X + first.Width, second.X + second.Width);
        var y2 = Math.Min(first.Y + first.Height, second.Y + second.Height);
        var intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var union = first.Width * first.Height + second.Width * second.Height - intersection;
        return union <= 0 ? 0 : intersection / union;
    }
}
