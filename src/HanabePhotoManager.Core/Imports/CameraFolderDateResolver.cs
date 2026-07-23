using System.Globalization;
using System.Text.RegularExpressions;

namespace HanabePhotoManager.Core.Imports;

public sealed record DateResolution(
    LibraryDate? Date,
    IReadOnlyList<string> Warnings,
    bool RequiresConfirmation);

public sealed class CameraFolderDateResolver
{
    public DateResolution Resolve(string folderName, IReadOnlyCollection<int> metadataYears)
    {
        var digitSequence = Regex.Matches(folderName ?? string.Empty, "[0-9]{4,}")
            .LastOrDefault();

        if (digitSequence is null)
        {
            return ConfirmationRequired("文件夹名称中没有至少四位的数字日期。");
        }

        var monthDay = digitSequence.Value[^4..];
        var month = int.Parse(monthDay[..2], CultureInfo.InvariantCulture);
        var day = int.Parse(monthDay[2..], CultureInfo.InvariantCulture);

        if (month is < 1 or > 12 || day is < 1 or > 31)
        {
            return ConfirmationRequired($"文件夹日期 {monthDay} 无效。");
        }

        var yearCounts = metadataYears
            .Where(year => year is >= 1900 and <= 9999)
            .GroupBy(year => year)
            .Select(group => new { Year = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Year)
            .ToArray();

        if (yearCounts.Length == 0)
        {
            return ConfirmationRequired("照片元数据中没有有效年份，需人工确认。");
        }

        var mostFrequent = yearCounts[0];
        var tiedYears = yearCounts
            .Where(item => item.Count == mostFrequent.Count)
            .ToArray();

        if (tiedYears.Length > 1)
        {
            var details = string.Join("、", tiedYears.Select(item => $"{item.Year}（{item.Count} 张）"));
            return ConfirmationRequired($"照片元数据年份并列：{details}，需人工确认。");
        }

        LibraryDate date;
        try
        {
            date = new LibraryDate(mostFrequent.Year, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
            return ConfirmationRequired($"文件夹日期 {monthDay} 在 {mostFrequent.Year} 年无效。");
        }

        var warnings = yearCounts
            .Skip(1)
            .Select(item => $"另有 {item.Count} 张照片的元数据年份为 {item.Year}。")
            .ToArray();

        return new DateResolution(date, Array.AsReadOnly(warnings), false);
    }

    private static DateResolution ConfirmationRequired(string warning)
    {
        return new DateResolution(null, Array.AsReadOnly([warning]), true);
    }
}
