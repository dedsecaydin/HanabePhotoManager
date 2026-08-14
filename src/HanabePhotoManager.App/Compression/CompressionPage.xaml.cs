using System.Windows;
using HanabePhotoManager.App.ViewModels;
using WinForms = System.Windows.Forms;

namespace HanabePhotoManager.App.Compression;

public partial class CompressionPage : System.Windows.Controls.UserControl
{
    private CompressionViewModel? _viewModel;

    public CompressionPage()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private CompressionViewModel? ViewModel => DataContext as CompressionViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null) _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel = e.NewValue as CompressionViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Deep-links (onboarding / ShowWatermarkCommand) set SelectedToolMode before
        // navigating here; surface the tool workspace instead of the card grid.
        if (e.PropertyName == nameof(CompressionViewModel.SelectedToolMode))
            ShowDetail();
    }

    private void ToolCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button) return;
        switch (button.Tag as string)
        {
            case "Compression": ShowTool(ImageToolMode.Compression); break;
            case "Collage": ShowTool(ImageToolMode.Collage); break;
            case "Watermark": ShowTool(ImageToolMode.Watermark); break;
            case "WeChat": ShowTool(ImageToolMode.WeChatSend); break;
            case "ContestOpen": Navigate("ContestOpen"); break;
            case "ContestJudged": Navigate("ContestJudged"); break;
        }
    }

    private void ShowTool(ImageToolMode mode)
    {
        if (ViewModel is { } viewModel) viewModel.SelectedToolMode = mode;
        ShowDetail();
    }

    private void ShowDetail()
    {
        if (ToolGridHost is null || ToolDetailHost is null) return;
        ToolGridHost.Visibility = Visibility.Collapsed;
        ToolDetailHost.Visibility = Visibility.Visible;
    }

    private void BackToGrid_Click(object sender, RoutedEventArgs e)
    {
        if (ToolGridHost is null || ToolDetailHost is null) return;
        ToolGridHost.Visibility = Visibility.Visible;
        ToolDetailHost.Visibility = Visibility.Collapsed;
    }

    private void Navigate(string page)
    {
        if (Window.GetWindow(this)?.DataContext is not MainWindowViewModel main) return;
        if (page == "ContestOpen") main.ShowContestOpenCommand.Execute(null);
        else main.ShowContestJudgedCommand.Execute(null);
    }

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
