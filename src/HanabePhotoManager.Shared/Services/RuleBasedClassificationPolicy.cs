using System.IO;
using HanabePhotoManager.App.Models;
using MetadataExtractor;

namespace HanabePhotoManager.App.Services;

/// <summary>Shared classification thresholds and metadata hints, independent of image decoder.</summary>
internal static class RuleBasedClassificationPolicy
{
    internal static PhotoClassificationResult Classify(double luminance, double greenRatio, double edgeRatio, bool hasFace, string path)
    {
        var labels = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var reasons = new List<string>();
        if (hasFace) { labels["人像"] = .95; reasons.Add("检测到人脸"); }
        if (luminance < 58)
        {
            labels["夜景"] = Math.Clamp(.72 + (58 - luminance) / 140, .72, .92);
            reasons.Add("整体亮度较低");
        }
        if (greenRatio >= .30)
        {
            labels["自然风景"] = Math.Clamp(.62 + greenRatio * .30, .62, .88);
            labels["植物"] = Math.Clamp(.58 + greenRatio * .30, .58, .84);
            reasons.Add("画面中绿色植被占比较高");
        }
        if (edgeRatio >= .12 && greenRatio < .22)
        {
            labels["建筑"] = Math.Clamp(.56 + edgeRatio, .56, .78);
            labels["城市风光"] = Math.Clamp(.52 + edgeRatio, .52, .74);
            reasons.Add("检测到较密集的直线与结构边缘");
        }
        if (!string.IsNullOrEmpty(path)) ApplyExifHints(path, labels, reasons);
        if (labels.Count == 0) { labels["待分类"] = 1; reasons.Add("没有达到可靠判断阈值"); }
        var ranked = labels.Select(pair => new PhotoLabelScore(pair.Key, Math.Round(pair.Value, 3)))
            .OrderByDescending(label => label.Score).ThenBy(label => label.Label, StringComparer.CurrentCulture).ToArray();
        return new PhotoClassificationResult(ranked, "rules", "rules-1.0", string.Join("；", reasons));
    }

    private static void ApplyExifHints(string path, IDictionary<string, double> labels, ICollection<string> reasons)
    {
        try
        {
            var text = string.Join(' ', ImageMetadataReader.ReadMetadata(path).SelectMany(directory => directory.Tags).Select(tag => $"{tag.Name} {tag.Description}"));
            var matched = false;
            matched |= AddHint(text, ["food", "dish", "meal", "美食"], "美食", labels);
            matched |= AddHint(text, ["animal", "pet", "dog", "cat", "动物"], "动物", labels);
            matched |= AddHint(text, ["building", "architecture", "建筑"], "建筑", labels);
            if (matched) reasons.Add("EXIF/IPTC 描述提供了类别线索");
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException) { }
    }

    private static bool AddHint(string text, IEnumerable<string> terms, string label, IDictionary<string, double> labels)
    {
        if (!terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase))) return false;
        labels.TryGetValue(label, out var current);
        labels[label] = Math.Max(current, .78);
        return true;
    }
}
