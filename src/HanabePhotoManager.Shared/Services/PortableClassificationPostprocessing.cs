using HanabePhotoManager.App.Models;

namespace HanabePhotoManager.App.Services;

internal static class PortableClassificationPostprocessing
{
    public static IReadOnlyList<PhotoLabelScore> MapImageNetLabels(
        IEnumerable<(string Label, double Score)> predictions)
    {
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (label, score) in predictions)
        {
            AddMapped(label, score, scores, "动物",
                "dog", "cat", "terrier", "retriever", "spaniel", "hound", "shepherd", "bird", "eagle", "owl", "fish", "shark", "whale", "monkey", "panda", "bear", "horse", "cow", "ox", "sheep", "goat", "rabbit", "hamster", "turtle", "lizard", "snake", "insect", "butterfly");
            AddMapped(label, score, scores, "美食",
                "food", "dish", "pizza", "burger", "cheeseburger", "hotdog", "bread", "cake", "ice cream", "fruit", "orange", "lemon", "apple", "banana", "strawberry", "pineapple", "wine", "espresso", "soup", "salad");
            AddMapped(label, score, scores, "交通",
                "car", "cab", "taxi", "truck", "bus", "train", "airliner", "aircraft", "bicycle", "motor scooter", "motorcycle", "boat", "ship", "van", "tractor");
            AddMapped(label, score, scores, "建筑",
                "church", "castle", "palace", "monastery", "mosque", "lighthouse", "library", "cinema", "barn", "boathouse", "yurt", "dome");
            AddMapped(label, score, scores, "自然风景",
                "cliff", "valley", "volcano", "lakeside", "seashore", "coral reef", "geyser", "promontory", "sandbar");
            AddMapped(label, score, scores, "植物",
                "flower", "daisy", "rose", "sunflower", "orchid", "corn", "acorn", "mushroom", "buckeye", "rapeseed");
            AddMapped(label, score, scores, "室内",
                "sofa", "chair", "table", "bed", "wardrobe", "bookcase", "refrigerator", "television", "desktop computer", "lamp");
            AddMapped(label, score, scores, "人像",
                "bride", "groom", "ballplayer", "scuba diver");
        }

        return scores.Select(pair => new PhotoLabelScore(pair.Key, Math.Round(Math.Clamp(pair.Value, 0, 1), 3)))
            .OrderByDescending(label => label.Score)
            .ThenBy(label => label.Label, StringComparer.CurrentCulture)
            .ToArray();
    }

    private static void AddMapped(
        string sourceLabel,
        double score,
        IDictionary<string, double> destination,
        string category,
        params string[] terms)
    {
        if (!terms.Any(term => sourceLabel.Contains(term, StringComparison.OrdinalIgnoreCase))) return;
        destination.TryGetValue(category, out var current);
        destination[category] = Math.Min(1, current + score);
    }

    internal static double[] Softmax(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return [];
        var maximum = values.Max();
        var exponentials = values.Select(value => Math.Exp(value - maximum)).ToArray();
        var sum = exponentials.Sum();
        return sum <= double.Epsilon ? new double[values.Count] : exponentials.Select(value => value / sum).ToArray();
    }

    public static IReadOnlyList<PhotoLabelScore> RankLabels(IReadOnlyList<float> image, IReadOnlyDictionary<string, float[]> labels)
    {
        var imageNorm = Math.Sqrt(image.Sum(value => value * value));
        if (imageNorm <= double.Epsilon) return [];
        var ranked = labels.Select(pair =>
        {
            var count = Math.Min(image.Count, pair.Value.Length);
            var dot = Enumerable.Range(0, count).Sum(i => image[i] * pair.Value[i]);
            var norm = Math.Sqrt(pair.Value.Take(count).Sum(value => value * value));
            return (pair.Key, Score: norm <= double.Epsilon ? -1d : dot / (imageNorm * norm));
        }).OrderByDescending(item => item.Score).ToArray();
        if (ranked.Length == 0) return [];
        var cutoff = ranked[0].Score - Math.Clamp(MobileClipRuntimeOptions.SimilarityWindow, .02, .30);
        return ranked.Where(item => item.Score >= cutoff).Take(Math.Clamp(MobileClipRuntimeOptions.MaximumLabels, 1, 5))
            .Select(item => new PhotoLabelScore(item.Key, Math.Round(Math.Clamp((item.Score + 1) / 2, 0, 1), 3))).ToArray();
    }

}
