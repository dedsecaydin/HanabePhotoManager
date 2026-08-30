using System.Windows;
using System.Windows.Input;

namespace HanabePhotoManager.App;

public partial class HanabeAssistantWindow : Window
{
    public HanabeAssistantWindow()
    {
        InitializeComponent();
        var area = SystemParameters.WorkArea;
        Left = Math.Max(area.Left, area.Right - Width - 24);
        Top = Math.Max(area.Top, area.Bottom - Height - 24);
    }

    private void Surface_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        try { DragMove(); }
        catch (InvalidOperationException) { }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e) => Hide();
}
