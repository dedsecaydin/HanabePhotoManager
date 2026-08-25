using System.Windows;

namespace HanabePhotoManager.App;

/// <summary>
/// 日期目录备注输入窗口，返回用户输入的可选备注。
/// </summary>
public partial class RemarkPromptWindow : Window
{
    public RemarkPromptWindow(string dateText)
    {
        InitializeComponent();
        TitleText.Text = $"要给 {dateText} 加备注吗？";
        RemarkBox.Focus();
    }

    public string Remark => RemarkBox.Text.Trim();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
