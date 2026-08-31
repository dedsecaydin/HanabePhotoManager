using CommunityToolkit.Mvvm.ComponentModel;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.App.Imports;

public enum ImportDateFolderStrategy
{
    Unselected,
    CreateSeparate,
    RenameExisting,
    UseExisting,
}

public sealed record ImportDateFolderCandidate(string Name, string FullPath, string Remark);

public sealed record ImportDateFolderStrategyOption(ImportDateFolderStrategy Value, string DisplayName);

public sealed class ImportDateFolderDecision : ObservableObject
{
    private readonly Action<ImportDateFolderDecision> _refresh;
    private string _remark = string.Empty;
    private ImportDateFolderStrategy _strategy;
    private ImportDateFolderCandidate? _selectedExistingFolder;

    internal ImportDateFolderDecision(
        LibraryDate date,
        int mediaCount,
        IReadOnlyList<ImportDateFolderCandidate> existingFolders,
        Action<ImportDateFolderDecision> refresh)
    {
        Date = date;
        MediaCount = mediaCount;
        ExistingFolders = existingFolders;
        _refresh = refresh;
        _strategy = existingFolders.Count == 0
            ? ImportDateFolderStrategy.CreateSeparate
            : ImportDateFolderStrategy.UseExisting;
        _selectedExistingFolder = existingFolders.FirstOrDefault();
    }

    public static IReadOnlyList<ImportDateFolderStrategyOption> StrategyOptions { get; } =
    [
        new(ImportDateFolderStrategy.CreateSeparate, "1. 同日期新建不同备注文件夹"),
        new(ImportDateFolderStrategy.RenameExisting, "2. 修改原文件夹备注并导入"),
        new(ImportDateFolderStrategy.UseExisting, "3. 使用原文件夹名，不更改"),
    ];

    public IReadOnlyList<ImportDateFolderStrategyOption> AvailableStrategies => StrategyOptions;

    public LibraryDate Date { get; }
    public int MediaCount { get; }
    public string DateText => $"{Date.Month:00}.{Date.Day:00}";
    public IReadOnlyList<ImportDateFolderCandidate> ExistingFolders { get; }
    public bool HasExistingFolders => ExistingFolders.Count > 0;

    public string Remark
    {
        get => _remark;
        set
        {
            if (SetProperty(ref _remark, value ?? string.Empty)) _refresh(this);
        }
    }

    public ImportDateFolderStrategy Strategy
    {
        get => _strategy;
        set
        {
            if (SetProperty(ref _strategy, value))
            {
                if (value == ImportDateFolderStrategy.RenameExisting &&
                    _selectedExistingFolder is not null && string.IsNullOrWhiteSpace(_remark))
                {
                    _remark = _selectedExistingFolder.Remark;
                    OnPropertyChanged(nameof(Remark));
                }
                _refresh(this);
            }
        }
    }

    public ImportDateFolderCandidate? SelectedExistingFolder
    {
        get => _selectedExistingFolder;
        set
        {
            if (SetProperty(ref _selectedExistingFolder, value))
            {
                if (_strategy == ImportDateFolderStrategy.RenameExisting &&
                    value is not null && string.IsNullOrWhiteSpace(_remark))
                {
                    _remark = value.Remark;
                    OnPropertyChanged(nameof(Remark));
                }
                _refresh(this);
            }
        }
    }

    public string FinalDirectoryName { get; internal set; } = string.Empty;
    public string FinalDirectoryPath { get; internal set; } = string.Empty;
    public string ValidationMessage { get; internal set; } = string.Empty;
    public bool IsValid { get; internal set; }

    internal void PublishResolution()
    {
        OnPropertyChanged(nameof(FinalDirectoryName));
        OnPropertyChanged(nameof(FinalDirectoryPath));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(IsValid));
    }
}
