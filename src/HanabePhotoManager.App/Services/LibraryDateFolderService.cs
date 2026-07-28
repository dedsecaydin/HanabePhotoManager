using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace HanabePhotoManager.App.Services;

public sealed record LibraryDateFolderName(
    int Month,
    int Day,
    string Suffix,
    string NormalizedName);

public static class LibraryDateFolderService
{
    private static readonly Regex SeparatedDatePrefix = new(
        @"^\s*(?<month>\d{1,2})\s*[.\-．。]\s*(?<day>\d{1,2})(?<suffix>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ChineseDatePrefix = new(
        @"^\s*(?<month>\d{1,2})\s*月\s*(?<day>\d{1,2})(?:日(?<suffixWithDay>.*)|(?<suffix>.*))$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CompactDatePrefix = new(
        @"^\s*(?<digits>\d{3,4})(?<suffix>(?:[_\-\s].*)?)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParseName(
        string? folderName,
        int expectedMonth,
        out LibraryDateFolderName parsed)
    {
        parsed = new LibraryDateFolderName(0, 0, string.Empty, string.Empty);
        if (string.IsNullOrWhiteSpace(folderName) || expectedMonth is < 1 or > 12)
        {
            return false;
        }

        var match = SeparatedDatePrefix.Match(folderName);
        var suffix = string.Empty;
        int month;
        int day;

        if (match.Success)
        {
            if (!TryParseNumber(match.Groups["month"].Value, out month) ||
                !TryParseNumber(match.Groups["day"].Value, out day))
            {
                return false;
            }

            suffix = match.Groups["suffix"].Value;
        }
        else
        {
            match = ChineseDatePrefix.Match(folderName);
            if (match.Success)
            {
                if (!TryParseNumber(match.Groups["month"].Value, out month) ||
                    !TryParseNumber(match.Groups["day"].Value, out day))
                {
                    return false;
                }

                suffix = match.Groups["suffixWithDay"].Success
                    ? match.Groups["suffixWithDay"].Value
                    : match.Groups["suffix"].Value;
            }
            else
            {
                match = CompactDatePrefix.Match(folderName);
                if (!match.Success ||
                    !TryParseCompactDate(match.Groups["digits"].Value, out month, out day))
                {
                    return false;
                }

                suffix = match.Groups["suffix"].Value;
            }
        }

        if (month != expectedMonth ||
            day < 1 ||
            day > DateTime.DaysInMonth(2000, month))
        {
            return false;
        }

        parsed = new LibraryDateFolderName(
            month,
            day,
            suffix,
            $"{month:00}.{day:00}{suffix}");
        return true;
    }

    public static string NormalizeDirectoryName(
        string directoryPath,
        LibraryDateFolderName parsed)
    {
        var fullPath = Path.GetFullPath(directoryPath);
        var parent = Path.GetDirectoryName(fullPath);
        var currentName = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(parent) ||
            string.Equals(currentName, parsed.NormalizedName, StringComparison.Ordinal))
        {
            return fullPath;
        }

        var target = Path.Combine(parent, parsed.NormalizedName);
        if (Directory.Exists(target) || File.Exists(target))
        {
            var suffix = 2;
            do
            {
                target = Path.Combine(parent, $"{parsed.NormalizedName}_{suffix++}");
            }
            while (Directory.Exists(target) || File.Exists(target));
        }

        try
        {
            Directory.Move(fullPath, target);
            return target;
        }
        catch (IOException)
        {
            return fullPath;
        }
        catch (UnauthorizedAccessException)
        {
            return fullPath;
        }
    }

    private static bool TryParseCompactDate(string digits, out int month, out int day)
    {
        month = 0;
        day = 0;
        var monthLength = digits.Length - 2;
        return TryParseNumber(digits[..monthLength], out month) &&
               TryParseNumber(digits[monthLength..], out day);
    }

    private static bool TryParseNumber(string value, out int number) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number);
}
