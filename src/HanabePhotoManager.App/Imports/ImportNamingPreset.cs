using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.App.Imports;

public enum ImportNamingPresetKind
{
    Sequence,
    Original,
    SequenceAndOriginal,
    Custom
}

public sealed record ImportNamingPreset(ImportNamingPresetKind Kind, string DisplayName, string Template)
{
    public static IReadOnlyList<ImportNamingPreset> All { get; } =
    [
        new(ImportNamingPresetKind.Sequence, "JK 序号", ImportNamingFormatter.SequenceTemplate),
        new(ImportNamingPresetKind.Original, "原文件名", ImportNamingFormatter.OriginalTemplate),
        new(ImportNamingPresetKind.SequenceAndOriginal, "JK 序号（原文件名）", ImportNamingFormatter.SequenceAndOriginalTemplate)
    ];

    public static ImportNamingPreset Resolve(string? template) =>
        All.FirstOrDefault(item => string.Equals(item.Template, template, StringComparison.OrdinalIgnoreCase))
        ?? new(ImportNamingPresetKind.Custom, "自定义", template ?? ImportNamingFormatter.DefaultTemplate);
}
