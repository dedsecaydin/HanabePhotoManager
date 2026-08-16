using System;
using System.Text.RegularExpressions;

namespace HanabePhotoManager.Core.Imports;

/// <summary>
/// 导入文件命名模板：占位符 {seq} / {seq:N} / {orig} / {date}，支持保存复用。
/// 默认模板 "JK{seq}" 与历史行为一致（JK0001、JK0002 …）。
/// </summary>
public static class ImportNamingFormatter
{
    public const string DefaultTemplate = "JK{seq}";

    private static readonly Regex SequencePattern = new(
        @"\{seq(?::(\d+))?\}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string Format(string? template, int sequence, string originalStem, LibraryDate date)
    {
        var effective = string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template;

        var result = SequencePattern.Replace(effective, match =>
        {
            var width = match.Groups[1].Success && int.TryParse(match.Groups[1].Value, out var parsed) && parsed > 0
                ? parsed
                : 4;
            return sequence.ToString("D" + width);
        });

        result = result.Replace("{orig}", originalStem, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{date}", $"{date.Year:0000}{date.Month:00}{date.Day:00}", StringComparison.OrdinalIgnoreCase);
        return result;
    }

    public static bool UsesOriginalName(string? template)
    {
        var effective = string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template;
        return effective.Contains("{orig}", StringComparison.OrdinalIgnoreCase);
    }
}
