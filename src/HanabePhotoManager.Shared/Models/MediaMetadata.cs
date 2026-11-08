namespace HanabePhotoManager.App.Models;

/// <summary>媒体元数据文件的版本化根快照。</summary>
public sealed class MediaMetadataSnapshot
{
    public int Version { get; set; } = 1;

    public List<string> CustomTags { get; set; } = [];

    public List<string> MapSourcePaths { get; set; } = [];

    public List<MediaMetadataEntry> Entries { get; set; } = [];
}

/// <summary>以规范化媒体路径为键保存的分类、评分、位置和扫描状态。</summary>
public sealed class MediaMetadataEntry
{
    public string Path { get; set; } = string.Empty;

    public string Fingerprint { get; set; } = string.Empty;

    public List<PhotoLabelScore> AutomaticLabels { get; set; } = [];

    public string? ManualCategory { get; set; }

    public List<string> ManualTags { get; set; } = [];

    public string ClassifierVersion { get; set; } = string.Empty;

    public DateTimeOffset? AnalyzedAt { get; set; }

    public List<string> PeopleIds { get; set; } = [];

    public PhotoLocation? ExifLocation { get; set; }

    public bool MapExifScanned { get; set; }

    public long MapFileLength { get; set; }

    public long MapLastWriteTimeUtcTicks { get; set; }

    public PhotoLocation? ManualLocation { get; set; }

    public string EffectiveCategory => !string.IsNullOrWhiteSpace(ManualCategory)
        ? ManualCategory
        : AutomaticLabels.OrderByDescending(label => label.Score).FirstOrDefault()?.Label ?? "待分类";

    public PhotoLocation? EffectiveLocation => ManualLocation ?? ExifLocation;
}

/// <summary>分类标签及其置信度。</summary>
public sealed record PhotoLabelScore(string Label, double Score);

/// <summary>媒体位置、来源和可选展示名称。</summary>
public sealed record PhotoLocation(double Latitude, double Longitude, PhotoLocationSource Source, string? DisplayName = null);

/// <summary>位置来自 EXIF、用户输入或其他导入来源。</summary>
public enum PhotoLocationSource
{
    Exif,
    Manual
}
