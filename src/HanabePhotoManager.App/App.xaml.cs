namespace HanabePhotoManager.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        Services.ThemeManager.LoadAndApply();
        base.OnStartup(e);
    }
}
