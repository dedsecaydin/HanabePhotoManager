using System.Windows;

namespace HanabePhotoManager.App.Duplicates;

public partial class DuplicateCleanupReportWindow : Window
{
    public DuplicateCleanupReportWindow(int selected, int deleted, int skipped, int failed, long freedBytes)
    {
        InitializeComponent();
        SummaryText.Text = $"本次选择清理 {selected} 个重复文件。图库序列已在成功删除后重新整理。";
        DeletedText.Text = deleted.ToString();
        SkippedText.Text = skipped.ToString();
        FailedText.Text = failed.ToString();
        FreedText.Text = $"释放空间：{FormatBytes(freedBytes)}";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return unit == 0 ? $"{bytes} B" : $"{value:0.##} {units[unit]}";
    }
}
