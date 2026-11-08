using System.IO;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string _newImportCategoryFolderName = string.Empty;
    private string _importCategoryFolderError = string.Empty;
    private List<ImportRoutingRule> _importRoutingRules = [];
    private string _newRoutingMatchKind = nameof(ImportRuleMatchKind.Extension);
    private string _newRoutingPattern = string.Empty;
    private string _newRoutingTargetFolder = "RAW生图";
    private string _importRoutingRuleError = string.Empty;

    public IReadOnlyList<string> ImportCategoryFolders => _importCategoryFolders;
    public IReadOnlyList<ImportRoutingRule> ImportRoutingRules => _importRoutingRules;
    public IReadOnlyList<ImportFolderOverview> ImportFolderOverviews => _importCategoryFolders
        .Select(folder => new ImportFolderOverview(
            folder,
            folder switch
            {
                "RAW生图" => "RAW 格式",
                "JPG生图" => "JPG / JPEG",
                "视频" => "常见视频",
                "action视频" => "DJI Action",
                _ => "手动选择"
            },
            _importRoutingRules.Select((rule, index) => new ImportRuleOverview(rule, index + 1))
                .Where(item => item.Rule.FolderName.Equals(folder, StringComparison.OrdinalIgnoreCase))
                .ToArray()))
        .ToArray();

    public string ImportRoutingSummary => $"{_importCategoryFolders.Count} 个分类文件夹 · {_importRoutingRules.Count} 条自定义规则";

    public string NewRoutingMatchKind
    {
        get => _newRoutingMatchKind;
        set => SetProperty(ref _newRoutingMatchKind, value);
    }

    public string NewRoutingPattern
    {
        get => _newRoutingPattern;
        set => SetProperty(ref _newRoutingPattern, value);
    }

    public string NewRoutingTargetFolder
    {
        get => _newRoutingTargetFolder;
        set => SetProperty(ref _newRoutingTargetFolder, value);
    }

    public string ImportRoutingRuleError
    {
        get => _importRoutingRuleError;
        private set => SetProperty(ref _importRoutingRuleError, value);
    }

    public bool AddImportRoutingRule()
    {
        var pattern = NewRoutingPattern.Trim();
        if (!Enum.TryParse<ImportRuleMatchKind>(NewRoutingMatchKind, out var kind) ||
            !Enum.IsDefined(kind) || !_importCategoryFolders.Contains(NewRoutingTargetFolder, StringComparer.OrdinalIgnoreCase) ||
            pattern.Length is < 1 or > 80 || pattern.IndexOfAny(['/', '\\', '*', '?']) >= 0 ||
            (kind == ImportRuleMatchKind.Extension &&
             (pattern.TrimStart('.').Length is < 1 or > 16 ||
              pattern.TrimStart('.').Any(character => !char.IsAsciiLetterOrDigit(character)))))
        {
            ImportRoutingRuleError = "请填写有效条件并选择目标分类；扩展名只填字母和数字，例如 CR3。";
            return false;
        }

        _importRoutingRules = [.. _importRoutingRules, new ImportRoutingRule(kind, pattern, NewRoutingTargetFolder)];
        NotifyImportRoutingOverview();
        NewRoutingPattern = string.Empty;
        ImportRoutingRuleError = string.Empty;
        _ = SaveSettingsAsync();
        return true;
    }

    public void RemoveImportRoutingRule(int index)
    {
        if (index < 0 || index >= _importRoutingRules.Count) return;
        _importRoutingRules = _importRoutingRules.Where((_, current) => current != index).ToList();
        NotifyImportRoutingOverview();
        _ = SaveSettingsAsync();
    }

    public void MoveImportRoutingRule(int index, int delta)
    {
        var destination = index + delta;
        if (index < 0 || index >= _importRoutingRules.Count || destination < 0 || destination >= _importRoutingRules.Count) return;
        (_importRoutingRules[index], _importRoutingRules[destination]) = (_importRoutingRules[destination], _importRoutingRules[index]);
        _importRoutingRules = [.. _importRoutingRules];
        NotifyImportRoutingOverview();
        _ = SaveSettingsAsync();
    }

    public string NewImportCategoryFolderName
    {
        get => _newImportCategoryFolderName;
        set => SetProperty(ref _newImportCategoryFolderName, value ?? string.Empty);
    }

    public string ImportCategoryFolderError
    {
        get => _importCategoryFolderError;
        private set => SetProperty(ref _importCategoryFolderError, value);
    }

    public bool AddImportCategoryFolder()
    {
        var folder = NewImportCategoryFolderName.Trim();
        if (!IsValidImportFolderName(folder))
        {
            ImportCategoryFolderError = "请输入有效的文件夹名称（最多 64 个字符，不含路径符号）。";
            return false;
        }
        if (_importCategoryFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            ImportCategoryFolderError = "这个分类已经存在。";
            return false;
        }
        ApplyImportFolders([.. _importCategoryFolders, folder]);
        NewImportCategoryFolderName = string.Empty;
        ImportCategoryFolderError = string.Empty;
        return true;
    }

    public void RemoveImportCategoryFolder(string folder)
    {
        if (_importRoutingRules.Any(rule => rule.FolderName.Equals(folder, StringComparison.OrdinalIgnoreCase)))
        {
            ImportCategoryFolderError = "这个分类仍被自动分流规则使用，请先移除对应规则。";
            return;
        }
        if (_importCategoryFolders.Count <= 1)
        {
            ImportCategoryFolderError = "至少保留一个分类文件夹。";
            return;
        }
        ApplyImportFolders(_importCategoryFolders.Where(name => !name.Equals(folder, StringComparison.OrdinalIgnoreCase)).ToList());
        ImportCategoryFolderError = string.Empty;
    }

    /// <summary>每个日期目录下的分类文件夹；更改后重新分析来源即可在队列中选用。</summary>
    public string ImportCategoryFoldersText
    {
        get => string.Join("，", _importCategoryFolders);
        set
        {
            var folders = NormalizeImportFolderNames((value ?? string.Empty)
                .Split([',', '，', ';', '；', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
            if (folders.Count == 0 || folders.SequenceEqual(_importCategoryFolders, StringComparer.OrdinalIgnoreCase)) return;
            ApplyImportFolders(folders);
        }
    }

    private void ApplyImportFolders(List<string> folders)
    {
        _importCategoryFolders = folders;
        _categoryChoices = BuildCategoryChoices(folders);
        OnPropertyChanged(nameof(ImportCategoryFolders));
        OnPropertyChanged(nameof(ImportCategoryFoldersText));
        OnPropertyChanged(nameof(CategoryChoices));
        OnPropertyChanged(nameof(ImportFolderOverviews));
        OnPropertyChanged(nameof(ImportRoutingSummary));
        if (!folders.Contains(NewRoutingTargetFolder, StringComparer.OrdinalIgnoreCase))
            NewRoutingTargetFolder = folders[0];
        _ = SaveSettingsAsync();
    }

    private static bool IsValidImportFolderName(string folder)
    {
        if (folder.Length is not (> 0 and <= 64) || folder is "." or ".." ||
            folder.EndsWith('.') || folder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        var deviceName = folder.Split('.')[0];
        return !new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }
            .Contains(deviceName, StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> NormalizeImportFolderNames(IEnumerable<string> names)
    {
        var result = new List<string>();
        foreach (var name in names)
        {
            var folder = name.Trim();
            if (!IsValidImportFolderName(folder) ||
                result.Contains(folder, StringComparer.OrdinalIgnoreCase)) continue;
            result.Add(folder);
        }
        return result;
    }

    private static IReadOnlyList<CategoryChoice> BuildCategoryChoices(IEnumerable<string> folders)
    {
        var choices = new List<CategoryChoice> { new(MediaCategory.Unconfirmed, "待确认") };
        foreach (var folder in folders)
        {
            var category = folder switch
            {
                "RAW生图" => MediaCategory.Raw,
                "JPG生图" => MediaCategory.Jpeg,
                "修后" => MediaCategory.Edited,
                "视频" => MediaCategory.Video,
                "action视频" => MediaCategory.ActionVideo,
                "素材" => MediaCategory.Material,
                _ => MediaCategory.Material
            };
            choices.Add(new CategoryChoice(category, folder, folder));
        }
        return choices;
    }

    private void NotifyImportRoutingOverview()
    {
        OnPropertyChanged(nameof(ImportRoutingRules));
        OnPropertyChanged(nameof(ImportFolderOverviews));
        OnPropertyChanged(nameof(ImportRoutingSummary));
    }
}

public sealed record ImportFolderOverview(string Name, string BuiltInHint, IReadOnlyList<ImportRuleOverview> Rules)
{
    public bool HasNoRules => Rules.Count == 0;
}

public sealed record ImportRuleOverview(ImportRoutingRule Rule, int Priority)
{
    public string PriorityLabel => $"#{Priority:00}";
    public string ConditionLabel => $"{Rule.KindLabel} · {Rule.Pattern}";
}
