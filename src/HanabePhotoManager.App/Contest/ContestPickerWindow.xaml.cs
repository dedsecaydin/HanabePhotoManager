using System.Collections.ObjectModel;
using System.Windows;

namespace HanabePhotoManager.App.Contest;

public partial class ContestPickerWindow : System.Windows.Window
{
    public ObservableCollection<ContestItem> Contests { get; } = new();
    public ContestItem? SelectedContest { get; private set; }

    public ContestPickerWindow(System.Collections.Generic.IEnumerable<ContestItem> contests)
    {
        InitializeComponent();
        foreach (var c in contests) Contests.Add(c);
        ContestList.ItemsSource = Contests;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (ContestList.SelectedItem is ContestItem item)
        {
            SelectedContest = item;
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
