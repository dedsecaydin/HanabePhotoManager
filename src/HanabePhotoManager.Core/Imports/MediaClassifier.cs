using System.Text.RegularExpressions;

namespace HanabePhotoManager.Core.Imports;

public sealed partial class MediaClassifier
{
    private static readonly HashSet<string> BuiltInExtensions = new(
        new[] { ".JPG", ".JPEG", ".MP4", ".MOV", ".MTS", ".M2TS", ".XML", ".LRF", ".AAC" },
        StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _rawExtensions;
    private readonly HashSet<string> _customVideoExtensions;

    public MediaClassifier(IEnumerable<string> rawExtensions, IEnumerable<string>? videoExtensions = null)
    {
        ArgumentNullException.ThrowIfNull(rawExtensions);

        _rawExtensions = new HashSet<string>(
            rawExtensions.Select(NormalizeExtension),
            StringComparer.OrdinalIgnoreCase);

        _customVideoExtensions = videoExtensions is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(
                videoExtensions.Select(NormalizeExtension),
                StringComparer.OrdinalIgnoreCase);
    }

    public ImportCandidate Classify(SourceMediaFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var fileName = Path.GetFileName(file.FullPath);
        if (DjiActionVideoPattern().IsMatch(fileName))
        {
            return Recognized(file, MediaCategory.ActionVideo, "DJI action-video filename");
        }

        if (SonyVideoPattern().IsMatch(fileName))
        {
            return Recognized(file, MediaCategory.Video, "Sony C-series video filename");
        }

        var extension = Path.GetExtension(fileName);
        if (_rawExtensions.Contains(extension))
        {
            return Recognized(file, MediaCategory.Raw, "Configured RAW extension");
        }

        if (extension.Equals(".JPG", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".JPEG", StringComparison.OrdinalIgnoreCase))
        {
            return Recognized(file, MediaCategory.Jpeg, "JPEG extension");
        }

        if (VideoContainerExtensions.Contains(extension) || _customVideoExtensions.Contains(extension))
        {
            return Recognized(file, MediaCategory.Video, "Video extension fallback");
        }

        if (extension.Equals(".XML", StringComparison.OrdinalIgnoreCase))
        {
            return Recognized(file, MediaCategory.Video, "Sony sidecar XML extension fallback");
        }

        if (extension.Equals(".LRF", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".AAC", StringComparison.OrdinalIgnoreCase))
        {
            return Recognized(file, MediaCategory.ActionVideo, "DJI sidecar extension fallback");
        }

        return new ImportCandidate(file, MediaCategory.Unconfirmed, "No recognized media rule", true);
    }

    private static ImportCandidate Recognized(
        SourceMediaFile file,
        MediaCategory category,
        string rule)
    {
        return new ImportCandidate(file, category, rule, false);
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException("RAW extension must be a non-empty extension token.", nameof(extension));
        }

        var trimmed = extension.Trim();
        var token = trimmed.StartsWith('.') ? trimmed[1..] : trimmed;
        var normalizedToken = token.ToUpperInvariant();
        if (token.Length == 0 ||
            token.Any(character => character > 0x7F) ||
            normalizedToken.Any(character =>
                (character < 'A' || character > 'Z') &&
                (character < '0' || character > '9')))
        {
            throw new ArgumentException(
                $"RAW extension '{extension}' must be one extension token containing only ASCII letters or digits.",
                nameof(extension));
        }

        var normalized = $".{normalizedToken}";
        if (BuiltInExtensions.Contains(normalized))
        {
            throw new ArgumentException(
                $"RAW extension '{extension}' conflicts with built-in media extension '{normalized}'.",
                nameof(extension));
        }

        return normalized;
    }

    private static readonly HashSet<string> VideoContainerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".MP4", ".MOV", ".MTS", ".M2TS", ".MXF", ".AVI", ".MKV", ".WMV", ".M4V",
        ".WEBM", ".TS", ".MPEG", ".MPG", ".3GP", ".FLV", ".OGV", ".M2T", ".MOD"
    };

    [GeneratedRegex(@"^DJI_[0-9]{14}_[0-9]{4}_D\.MP4$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DjiActionVideoPattern();

    [GeneratedRegex(@"^C[0-9]{4}\.MP4$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SonyVideoPattern();
}
