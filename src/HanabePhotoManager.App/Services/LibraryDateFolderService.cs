using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace HanabePhotoManager.App.Services;

/// <summary>从照片库目录名解析出的月份、日期和展示标题。</summary>
public sealed record LibraryDateFolderName(
    int Month,
    int Day,
    string Suffix,
    string NormalizedName);

/// <summary>日期目录备注重命名的明确结果状态。</summary>
public enum DateFolderRenameStatus
{
    Success,
    NoChange,
    SourceMissing,
    TargetExists,
    Failed,
}

/// <summary>日期目录备注重命名的结果。</summary>
public sealed record DateFolderRenameResult(
    DateFolderRenameStatus Status,
    string SourcePath,
    string EffectivePath,
    string? ErrorMessage = null);

/// <summary>用于日期目录批量页展示的只读目录条目。</summary>
public sealed record DateFolderEntry(int Month, int Day, string Remark, string FullPath);

/// <summary>集中解析和格式化照片库的“月/日”目录约定。</summary>
public static class LibraryDateFolderService
{
    private static readonly Regex MonthDirectoryName = new(
        @"^\s*(?<month>\d{1,2})\s*月\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

    /// <summary>扫描照片库根目录下直接的月/日目录，不修改任何目录名。</summary>
    public static IReadOnlyList<DateFolderEntry> Scan(string libraryRoot)
    {
        if (string.IsNullOrWhiteSpace(libraryRoot) || !Directory.Exists(libraryRoot))
        {
            return Array.Empty<DateFolderEntry>();
        }

        var entries = new List<DateFolderEntry>();
        try
        {
            foreach (var monthDirectory in Directory.GetDirectories(libraryRoot))
            {
                if (!TryParseMonthDirectoryName(Path.GetFileName(monthDirectory), out var month))
                {
                    continue;
                }

                foreach (var dateDirectory in Directory.GetDirectories(monthDirectory))
                {
                    if (!TryParseName(Path.GetFileName(dateDirectory), month, out var parsed))
                    {
                        continue;
                    }

                    entries.Add(new DateFolderEntry(
                        parsed.Month,
                        parsed.Day,
                        NormalizeRemark(parsed.Suffix),
                        dateDirectory));
                }
            }
        }
        catch (IOException)
        {
            return entries.OrderBy(entry => entry.Month).ThenBy(entry => entry.Day).ToArray();
        }
        catch (UnauthorizedAccessException)
        {
            return entries.OrderBy(entry => entry.Month).ThenBy(entry => entry.Day).ToArray();
        }

        return entries.OrderBy(entry => entry.Month).ThenBy(entry => entry.Day).ToArray();
    }

    /// <summary>重命名日期目录的备注部分，并将文件系统结果显式返回给调用方。</summary>
    public static DateFolderRenameResult RenameRemark(string sourcePath, string remark)
    {
        var sourceFullPath = Path.GetFullPath(sourcePath);
        if (!Directory.Exists(sourceFullPath))
        {
            return new DateFolderRenameResult(
                DateFolderRenameStatus.SourceMissing,
                sourceFullPath,
                sourceFullPath);
        }

        if (!TryParseDateFolderName(Path.GetFileName(sourceFullPath), out var parsed))
        {
            return new DateFolderRenameResult(
                DateFolderRenameStatus.Failed,
                sourceFullPath,
                sourceFullPath,
                "The source directory name is not a valid date folder name.");
        }

        var parent = Path.GetDirectoryName(sourceFullPath);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return new DateFolderRenameResult(
                DateFolderRenameStatus.Failed,
                sourceFullPath,
                sourceFullPath,
                "The source directory has no parent directory.");
        }

        var normalizedRemark = NormalizeRemark(remark);
        var targetName = $"{parsed.Month:00}.{parsed.Day:00}";
        if (!string.IsNullOrEmpty(normalizedRemark))
        {
            targetName = $"{targetName}_{normalizedRemark}";
        }

        var targetPath = Path.Combine(parent, targetName);
        if (string.Equals(sourceFullPath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            return new DateFolderRenameResult(
                DateFolderRenameStatus.NoChange,
                sourceFullPath,
                sourceFullPath);
        }

        if (Directory.Exists(targetPath) || File.Exists(targetPath))
        {
            return new DateFolderRenameResult(
                DateFolderRenameStatus.TargetExists,
                sourceFullPath,
                targetPath);
        }

        try
        {
            Directory.Move(sourceFullPath, targetPath);
            return new DateFolderRenameResult(
                DateFolderRenameStatus.Success,
                sourceFullPath,
                targetPath);
        }
        catch (IOException exception)
        {
            return new DateFolderRenameResult(
                DateFolderRenameStatus.Failed,
                sourceFullPath,
                sourceFullPath,
                exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return new DateFolderRenameResult(
                DateFolderRenameStatus.Failed,
                sourceFullPath,
                sourceFullPath,
                exception.Message);
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

    private static bool TryParseMonthDirectoryName(string? folderName, out int month)
    {
        month = 0;
        var match = MonthDirectoryName.Match(folderName ?? string.Empty);
        return match.Success &&
               TryParseNumber(match.Groups["month"].Value, out month) &&
               month is >= 1 and <= 12;
    }

    private static bool TryParseDateFolderName(string? folderName, out LibraryDateFolderName parsed)
    {
        for (var month = 1; month <= 12; month++)
        {
            if (TryParseName(folderName, month, out parsed))
            {
                return true;
            }
        }

        parsed = new LibraryDateFolderName(0, 0, string.Empty, string.Empty);
        return false;
    }

    private static string NormalizeRemark(string? remark)
    {
        if (string.IsNullOrWhiteSpace(remark))
        {
            return string.Empty;
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(remark.Where(character => !invalidCharacters.Contains(character)).ToArray());
        return sanitized.Trim(' ', '\t', '_', '-');
    }

    private static bool TryParseNumber(string value, out int number) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number);
}
