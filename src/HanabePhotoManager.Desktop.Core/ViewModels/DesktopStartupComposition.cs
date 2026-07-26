namespace HanabePhotoManager.Desktop.Core.ViewModels;

public static class DesktopStartupComposition
{
    public static void ValidateShell()
    {
        var shell = new DesktopShellViewModel();

        if (string.IsNullOrWhiteSpace(shell.Title) || string.IsNullOrWhiteSpace(shell.Status))
        {
            throw new InvalidOperationException("The desktop shell display state must be available at startup.");
        }
    }
}
