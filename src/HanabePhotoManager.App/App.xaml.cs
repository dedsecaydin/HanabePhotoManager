namespace HanabePhotoManager.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
using HanabePhotoManager.App.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

public partial class App : System.Windows.Application
{
    static App()
    {
        EventManager.RegisterClassHandler(
            typeof(ComboBox),
            UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(ComboBox_PreviewMouseLeftButtonDown),
            true);
    }

    private static void ComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ComboBox { IsEnabled: true } comboBox ||
            comboBox.IsDropDownOpen ||
            ItemsControl.ContainerFromElement(comboBox, e.OriginalSource as DependencyObject) is ComboBoxItem)
        {
            return;
        }

        comboBox.Focus();
        comboBox.IsDropDownOpen = true;
        e.Handled = true;
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        Services.ThemeManager.LoadAndApply();
        base.OnStartup(e);
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        // Dispose SigLIP2 session if initialized to ensure native resources are released.
        try
        {
            SigLip2OnnxSessionManager.DisposeSession();
        }
        catch
        {
            // Swallow exceptions during shutdown to avoid preventing app exit; log if a logging system is available.
        }

        base.OnExit(e);
    }
}
