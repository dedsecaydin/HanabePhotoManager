using System.Windows;

namespace HanabePhotoManager.App.Albums;

public partial class CustomAlbumsPage : System.Windows.Controls.UserControl
{
    public CustomAlbumsPage() => InitializeComponent();

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "选择要加入自定义相册的文件夹"
        };
        if (dialog.ShowDialog() == true && DataContext is CustomAlbumsViewModel viewModel)
        {
            await viewModel.AddFolderAsync(dialog.FolderName);
        }
    }
}
