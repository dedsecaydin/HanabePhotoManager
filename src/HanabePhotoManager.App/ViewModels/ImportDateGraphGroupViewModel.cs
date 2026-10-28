using CommunityToolkit.Mvvm.ComponentModel;
using HanabePhotoManager.App.Imports;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>A projection of the shared preflight decision, never a second import plan.</summary>
public sealed class ImportDateGraphGroupViewModel : ObservableObject
{
    private bool _isSelected;

    public ImportDateGraphGroupViewModel(ImportDateFolderDecision decision, IReadOnlyList<ImportPreviewItemViewModel> items)
    {
        Decision = decision;
        Items = items;
        Samples = items.Take(2).ToArray();
    }

    public ImportDateFolderDecision Decision { get; }
    public IReadOnlyList<ImportPreviewItemViewModel> Items { get; }
    public IReadOnlyList<ImportPreviewItemViewModel> Samples { get; }
    public bool HasOverflow => Items.Count > Samples.Count;
    public string OverflowText => $"+{Items.Count - Samples.Count}";
    public string FullDateText => $"{Decision.Date.Year}.{Decision.Date.Month:00}.{Decision.Date.Day:00}";
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
}

public sealed record ImportDateGraphMonthViewModel(string Title, IReadOnlyList<ImportDateGraphGroupViewModel> Groups);
