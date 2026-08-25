using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using HanabePhotoManager.App.ViewModels;

namespace HanabePhotoManager.App.People;

/// <summary>
/// 人物合并目标选择窗口；调用方需预先从候选集合排除当前人物。
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
