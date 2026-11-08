using System.Globalization;
using System.Text.RegularExpressions;

namespace HanabePhotoManager.App.Imports;

internal static partial class HanabeOriginCommentCodec
{
    private const string Prefix = "Hanabe:v1;";

    internal static string Merge(string? existingComment, FileOriginMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var retainedLines = (existingComment ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Where(line => !line.StartsWith(Prefix, StringComparison.Ordinal))
            .ToList();
        while (retainedLines.Count > 0 && string.IsNullOrWhiteSpace(retainedLines[^1]))
        {
            retainedLines.RemoveAt(retainedLines.Count - 1);
        }

        retainedLines.Add($"{Prefix}OriginalName={Uri.EscapeDataString(metadata.OriginalName)};OriginalLength={metadata.OriginalLength.ToString(CultureInfo.InvariantCulture)};OriginalSha256={metadata.OriginalSha256.ToUpperInvariant()}");
        return string.Join("\n", retainedLines);
    }

    internal static bool TryParse(string? comment, out FileOriginMetadata metadata)
    {
        metadata = null!;
        var line = (comment ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .FirstOrDefault(value => value.StartsWith(Prefix, StringComparison.Ordinal));
        if (line is null)
        {
            return false;
        }

        var fields = line[Prefix.Length..]
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);
        if (!fields.TryGetValue("OriginalName", out var encodedName)
            || !fields.TryGetValue("OriginalLength", out var lengthText)
            || !long.TryParse(lengthText, NumberStyles.None, CultureInfo.InvariantCulture, out var length)
            || length < 0
            || !fields.TryGetValue("OriginalSha256", out var hash)
            || !Sha256Regex().IsMatch(hash))
        {
            return false;
        }

        try
        {
            metadata = new FileOriginMetadata(Uri.UnescapeDataString(encodedName), length, hash.ToUpperInvariant());
            return !string.IsNullOrWhiteSpace(metadata.OriginalName);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    [GeneratedRegex("^[0-9A-Fa-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();
}
