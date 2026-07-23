namespace HanabePhotoManager.App.Compression;

public enum CompressionTargetMode
{
    PerImage,
    WholeBatch
}

public sealed record CompressionSource(string Path, long Length, long PixelCount);

public sealed record CompressionWorkItem(CompressionSource Source, long TargetBytes);

public sealed class ImageCompressionPlanner
{
    public IReadOnlyList<CompressionWorkItem> CreatePlan(
        IReadOnlyList<CompressionSource> files,
        CompressionTargetMode mode,
        long targetBytes)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (targetBytes <= 0) throw new ArgumentOutOfRangeException(nameof(targetBytes));
        if (files.Count == 0) return [];

        if (mode == CompressionTargetMode.PerImage)
        {
            return files.Select(file => new CompressionWorkItem(file, targetBytes)).ToArray();
        }

        var totalWeight = files.Sum(file => Math.Max(1L, file.Length));
        var allocations = files.Select((file, index) =>
        {
            var exact = (decimal)targetBytes * Math.Max(1L, file.Length) / totalWeight;
            var floor = (long)decimal.Floor(exact);
            return new Allocation(index, file, floor, exact - floor);
        }).ToArray();

        var remainder = targetBytes - allocations.Sum(item => item.Bytes);
        foreach (var allocation in allocations
                     .OrderByDescending(item => item.Fraction)
                     .ThenBy(item => item.Index)
                     .Take((int)Math.Min(remainder, allocations.Length)))
        {
            allocation.Bytes++;
        }

        return allocations.OrderBy(item => item.Index)
            .Select(item => new CompressionWorkItem(item.Source, item.Bytes))
            .ToArray();
    }

    private sealed class Allocation(int index, CompressionSource source, long bytes, decimal fraction)
    {
        public int Index { get; } = index;
        public CompressionSource Source { get; } = source;
        public long Bytes { get; set; } = bytes;
        public decimal Fraction { get; } = fraction;
    }
}
