namespace HanabePhotoManager.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
using HanabePhotoManager.App.Services;

public partial class App : System.Windows.Application
{
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
