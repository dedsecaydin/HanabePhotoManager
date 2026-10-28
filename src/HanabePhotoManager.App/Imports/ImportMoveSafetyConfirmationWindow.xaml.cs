using System.Windows;

namespace HanabePhotoManager.App.Imports;

public partial class ImportMoveSafetyConfirmationWindow : Window
{
    public ImportMoveSafetyConfirmationWindow() => InitializeComponent();

    public ImportMoveSafetyConfirmationWindow(string renameDetails) : this()
    {
        Heading.Text = "确认修改已有文件夹？";
        Description.Text = "这会改变已有文件夹的名称和路径。请检查以下改名清单。";
        ConfirmButton.Content = "确认改名并导入";
        var details = new System.Windows.Controls.TextBlock
        {
            Text = renameDetails,
            TextWrapping = TextWrapping.Wrap
        };
        details.SetResourceReference(StyleProperty, "Dialog.Body");
        DetailsSurface.Child = new System.Windows.Controls.ScrollViewer
        {
            Content = details,
            MaxHeight = 300,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => BackButton.Focus();
    private void Back_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    private void Confirm_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
}
