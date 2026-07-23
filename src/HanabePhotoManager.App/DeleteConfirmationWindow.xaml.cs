using System.Windows;
using System.Windows.Input;

namespace HanabePhotoManager.App;

public partial class DeleteConfirmationWindow : Window
{
    public DeleteConfirmationWindow(string message, int selectedCount, int actualFileCount)
    {
        InitializeComponent();
        MessageText.Text = message;
        CountText.Text = actualFileCount > selectedCount
            ? $"已选 {selectedCount} 组 · 包含同名配对，共 {actualFileCount} 个 RAW/JPG 文件"
            : $"已选 {selectedCount} 项 · 共 {actualFileCount} 个文件";
    }

    public static bool Confirm(Window? owner, string message, int selectedCount, int actualFileCount)
    {
        var dialog = new DeleteConfirmationWindow(message, selectedCount, actualFileCount);
        if (owner is not null && owner.IsVisible)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
