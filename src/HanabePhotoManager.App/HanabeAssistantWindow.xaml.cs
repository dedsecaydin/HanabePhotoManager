using System.Windows;
using System.Windows.Input;
using HanabePhotoManager.App.ViewModels;

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

        if (e.OriginalSource is DependencyObject source && FindParent<System.Windows.Controls.Primitives.ButtonBase>(source) is not null)
        {
            return;
        }

        try { DragMove(); }
        catch (InvalidOperationException) { }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void OpenHanabe_Click(object sender, RoutedEventArgs e) => RestoreMainWindow();

    private async void Resume_Click(object sender, RoutedEventArgs e)
    {
        RestoreMainWindow();
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.ResumePendingImportAsync();
        }
    }

    private static void RestoreMainWindow()
    {
        if (System.Windows.Application.Current.MainWindow is not { } main) return;
        main.Show();
        main.WindowState = WindowState.Normal;
        main.Activate();
    }

    private static T? FindParent<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match) return match;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }
        return null;
    }
}
