namespace HanabePhotoManager.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Enter the reader before constructing WPF: StartupUri must never load a window in the helper.
        if (args.Length > 0 && args[0] == "--recovery-reader")
            return args.Length == 3 ? Recovery.RecoveryDeviceSession.RunReader(args[1], args[2]) : 2;
        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
