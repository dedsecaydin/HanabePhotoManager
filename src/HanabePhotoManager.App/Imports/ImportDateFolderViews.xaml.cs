using System.Windows;
using System.Windows.Controls;
using HanabePhotoManager.App.ViewModels;

namespace HanabePhotoManager.App.Imports;

public partial class ImportDateFolderViews : System.Windows.Controls.UserControl
{
    public ImportDateFolderViews() => InitializeComponent();

    private void PreviewMedia_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ImportPreviewItemViewModel item }) return;
        var viewer = new PhotoViewerWindow([item.PrimaryPath], item.PrimaryPath) { Owner = Window.GetWindow(this) };
        viewer.ShowDialog();
    }
}
