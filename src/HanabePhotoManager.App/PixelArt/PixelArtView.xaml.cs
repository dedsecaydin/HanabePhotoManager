using System.Windows;

namespace HanabePhotoManager.App.PixelArt;

public partial class PixelArtView : System.Windows.Controls.UserControl
{
    public PixelArtView() => InitializeComponent();

    private PixelArtViewModel? ViewModel => DataContext as PixelArtViewModel;

    private void ChooseImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "图片|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tif;*.tiff|所有文件|*.*"
        };
        if (dialog.ShowDialog() == true && ViewModel is { } viewModel)
            viewModel.SetSourceImage(dialog.FileName);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } viewModel || !viewModel.HasResult) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG 图片|*.png",
            FileName = "pixel-art.png"
        };
        if (dialog.ShowDialog() == true)
            viewModel.Export(dialog.FileName);
    }

    private void SizeOption_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.RadioButton radio || ViewModel is not { } viewModel)
            return;

        if (radio.Tag?.ToString() == "custom")
            viewModel.SelectCustom();
        else if (int.TryParse(radio.Tag?.ToString(), out var size))
            viewModel.SelectPreset(size);
    }
}
