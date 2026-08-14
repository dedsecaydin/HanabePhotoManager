using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using HanabePhotoManager.App.ViewModels;

namespace HanabePhotoManager.App.People;

/// <summary>
/// Modal picker that lets the user choose the target person for a merge. The
/// current person is excluded from the candidate list by the caller.
/// </summary>
public partial class MergePersonDialog : Window
{
    public ObservableCollection<PersonAlbumItemViewModel> Targets { get; } = [];
    public PersonAlbumItemViewModel? SelectedTarget { get; private set; }

    public MergePersonDialog(IEnumerable<PersonAlbumItemViewModel> candidates)
    {
        InitializeComponent();
        foreach (var candidate in candidates) Targets.Add(candidate);
        TargetList.ItemsSource = Targets;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (TargetList.SelectedItem is PersonAlbumItemViewModel item)
        {
            SelectedTarget = item;
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TargetList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (TargetList.SelectedItem is PersonAlbumItemViewModel item)
        {
            SelectedTarget = item;
            DialogResult = true;
        }
    }
}
