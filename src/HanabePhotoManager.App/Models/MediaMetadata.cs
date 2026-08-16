namespace HanabePhotoManager.App.Models;

public sealed class MediaMetadataSnapshot
{
    public int Version { get; set; } = 1;

    public List<string> CustomTags { get; set; } = [];

    public List<string> MapSourcePaths { get; set; } = [];

    public List<MediaMetadataEntry> Entries { get; set; } = [];
}

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

public sealed record PhotoLabelScore(string Label, double Score);

public sealed record PhotoLocation(double Latitude, double Longitude, PhotoLocationSource Source, string? DisplayName = null);

public enum PhotoLocationSource
{
    Exif,
    Manual
}
