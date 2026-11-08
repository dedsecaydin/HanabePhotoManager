namespace HanabePhotoManager.App.Compression;

/// <summary>按固定大小、比例或总预算分配压缩目标。</summary>
public enum CompressionTargetMode
{
    PerImage,
    WholeBatch
}

/// <summary>规划压缩时需要的源文件大小和像素数量。</summary>
public sealed record CompressionSource(string Path, long Length, long PixelCount);

/// <summary>源图片及其分配到的目标字节数。</summary>
public sealed record CompressionWorkItem(CompressionSource Source, long TargetBytes);

/// <summary>在整批图片之间分配目标体积并保持预算约束。</summary>
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
