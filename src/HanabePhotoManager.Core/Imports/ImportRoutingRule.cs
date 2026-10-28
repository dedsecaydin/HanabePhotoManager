using System.Text.Json.Serialization;

namespace HanabePhotoManager.Core.Imports;

public enum ImportRuleMatchKind
{
    Extension,
    NameStartsWith,
    NameContains,
    NameEndsWith
}

/// <summary>按主文件的扩展名或不含扩展名的文件名，将整组媒体送往指定分类文件夹。</summary>
public sealed record ImportRoutingRule(ImportRuleMatchKind Kind, string Pattern, string FolderName)
{
    [JsonIgnore]
    public string KindLabel => Kind switch
    {
        ImportRuleMatchKind.Extension => "扩展名",
        ImportRuleMatchKind.NameStartsWith => "文件名开头",
        ImportRuleMatchKind.NameContains => "文件名包含",
        ImportRuleMatchKind.NameEndsWith => "文件名结尾",
        _ => "未知条件"
    };

    public bool Matches(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(Pattern)) return false;
        var fileName = Path.GetFileName(path);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var pattern = Pattern.Trim();
        return Kind switch
        {
            ImportRuleMatchKind.Extension => Path.GetExtension(fileName)
                .Equals($".{pattern.TrimStart('.')}", StringComparison.OrdinalIgnoreCase),
            ImportRuleMatchKind.NameStartsWith => stem.StartsWith(pattern, StringComparison.OrdinalIgnoreCase),
            ImportRuleMatchKind.NameContains => stem.Contains(pattern, StringComparison.OrdinalIgnoreCase),
            ImportRuleMatchKind.NameEndsWith => stem.EndsWith(pattern, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    public static string? FirstMatchingFolder(IEnumerable<ImportRoutingRule> rules, string primaryPath) =>
        rules.FirstOrDefault(rule => rule.Matches(primaryPath))?.FolderName;
}
