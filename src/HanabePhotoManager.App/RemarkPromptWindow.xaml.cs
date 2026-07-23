using System.Windows;

namespace HanabePhotoManager.App;

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
