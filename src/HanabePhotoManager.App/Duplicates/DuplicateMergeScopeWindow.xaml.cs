using System.Windows;

namespace HanabePhotoManager.App.Duplicates;

public partial class DuplicateMergeScopeWindow : Window
{
    public DuplicateMergeScope Scope { get; private set; } = DuplicateMergeScope.ExactAndVisual;

    public DuplicateMergeScopeWindow() => InitializeComponent();

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        Scope = ExactOnlyOption.IsChecked == true
            ? DuplicateMergeScope.ExactSha256Only
            : VisualOnlyOption.IsChecked == true
                ? DuplicateMergeScope.VisualSimilarOnly
                : DuplicateMergeScope.ExactAndVisual;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
