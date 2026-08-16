namespace HanabePhotoManager.InstallerShell;

public enum InstallerOutcome
{
    Success,
    Cancelled,
    RestartRequired,
    Failed
}

public static class InstallerExitCode
{
    public static InstallerOutcome Classify(int exitCode) => exitCode switch
    {
        0 => InstallerOutcome.Success,
        1602 => InstallerOutcome.Cancelled,
        1641 or 3010 => InstallerOutcome.RestartRequired,
        _ => InstallerOutcome.Failed
    };
}
