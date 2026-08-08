using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;

namespace HanabePhotoManager.App.Duplicates;

public partial class ImportDuplicateDecisionWindow : Window
{
    private readonly string _existingPath;

    public ImportDuplicateDecision Decision { get; private set; } = ImportDuplicateDecision.Skip;

    public ImportDuplicateDecisionWindow(string incomingPath, string existingPath, bool existingIsReadOnlyRetouched)
    {
        _existingPath = existingPath;
        InitializeComponent();
        DescriptionText.Text = existingIsReadOnlyRetouched
            ? "匹配文件位于修后目录，已按只读保留。请选择是否跳过或仍要导入。"
            : "请选择如何处理这次导入；不会自动删除或覆盖图库文件。";
        IncomingPathText.Text = incomingPath;
        ExistingPathText.Text = existingPath + (existingIsReadOnlyRetouched ? "（修后：只读保留）" : string.Empty);
        IncomingImage.Source = LoadThumbnail(incomingPath);
        ExistingImage.Source = LoadThumbnail(existingPath);
    }

    private static BitmapImage? LoadThumbnail(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.DecodePixelWidth = 480;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (NotSupportedException) { return null; }
        catch (System.IO.IOException) { return null; }
        catch (ArgumentException) { return null; }
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        Decision = ImportDuplicateDecision.Skip;
        DialogResult = true;
    }

    private void ImportAnyway_Click(object sender, RoutedEventArgs e)
    {
        Decision = ImportDuplicateDecision.ImportAnyway;
        DialogResult = true;
    }

    private void Locate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_existingPath}\"") { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception) { }
    }
}
