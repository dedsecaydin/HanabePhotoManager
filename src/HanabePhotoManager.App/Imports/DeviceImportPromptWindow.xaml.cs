using System.Windows;

namespace HanabePhotoManager.App.Imports;

public partial class DeviceImportPromptWindow : Window
{
    public DeviceImportPromptWindow(string sourceRoot, string? libraryRoot)
    {
        InitializeComponent();
        RootPathText.Text = sourceRoot;
        DestinationPathText.Text = string.IsNullOrWhiteSpace(libraryRoot)
            ? "尚未选择，进入导入页后选择目标照片库"
            : libraryRoot;
    }

    private void Import_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Later_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
