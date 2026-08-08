using System.Windows;
using HanabePhotoManager.App.ViewModels;
using WinForms = System.Windows.Forms;

namespace HanabePhotoManager.App.Compression;

public partial class CompressionPage : System.Windows.Controls.UserControl
{
    public CompressionPage() => InitializeComponent();
    private CompressionViewModel? ViewModel => DataContext as CompressionViewModel;

    private async void ChooseFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Filter = "图片|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tif;*.tiff;*.heic;*.arw;*.cr2;*.cr3;*.nef;*.dng;*.raf;*.orf;*.rw2|所有文件|*.*" };
        if (dialog.ShowDialog() == true && ViewModel is { } viewModel)
            await AddInputsAsync(viewModel, dialog.FileNames, recursive: false);
    }

    private async void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog { Description = "选择图片文件夹（自动扫描子文件夹）", UseDescriptionForTitle = true };
        if (dialog.ShowDialog() == WinForms.DialogResult.OK && ViewModel is { } viewModel)
            await AddInputsAsync(viewModel, [dialog.SelectedPath], recursive: true);
    }

    private void ChooseOutput_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog { Description = "选择压缩结果输出目录", UseDescriptionForTitle = true };
        if (dialog.ShowDialog() == WinForms.DialogResult.OK && ViewModel is { } viewModel) viewModel.OutputDirectory = dialog.SelectedPath;
    }

    private void OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] paths || ViewModel is not { } viewModel)
            return;
        if (viewModel.IsWeChatSendMode)
            viewModel.WeChatSender.AddInputs(paths, true);
        else
            await AddInputsAsync(viewModel, paths, recursive: true);
    }

    private static async Task AddInputsAsync(CompressionViewModel viewModel, IEnumerable<string> paths, bool recursive)
    {
        try
        {
            await viewModel.AddInputsAsync(paths, recursive);
        }
        catch (OperationCanceledException)
        {
            // The view model already reports cancellation to the user.
        }
    }
}
