using System.Windows;
using System.Windows.Input;

namespace HanabePhotoManager.App.Browsing;

/// <summary>「按场次拆分到文件夹」确认窗口：时间线展示每一场的时间、数量和去向，新文件夹备注可改。</summary>
public partial class DaySplitConfirmWindow : Window
{
    private readonly DaySplitPreview _preview;

    public DaySplitConfirmWindow(DaySplitPreview preview)
    {
        InitializeComponent();
        _preview = preview;
        DataContext = preview;
    }

    public static bool Confirm(Window? owner, DaySplitPreview preview)
    {
        var dialog = new DaySplitConfirmWindow(preview);
        if (owner is not null && owner.IsVisible) dialog.Owner = owner;
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return dialog.ShowDialog() == true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (_preview.Validate() is { } error)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
