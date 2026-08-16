namespace HanabePhotoManager.InstallerShell;

public sealed class InstallerFlowState
{
    public InstallerStep Step { get; private set; } = InstallerStep.Welcome;
    public bool HasReadLicense { get; private set; }
    public bool HasAcceptedLicense { get; private set; }
    public bool CanGoBack => Step is InstallerStep.License;
    public bool CanContinue => Step == InstallerStep.Welcome || (Step == InstallerStep.License && HasReadLicense && HasAcceptedLicense);

    public void MarkLicenseRead() => HasReadLicense = true;

    public void SetLicenseAccepted(bool accepted) => HasAcceptedLicense = HasReadLicense && accepted;

    public void Continue()
    {
        if (!CanContinue)
        {
            return;
        }

        Step = Step == InstallerStep.Welcome ? InstallerStep.License : InstallerStep.Installing;
    }

    public void Back()
    {
        if (CanGoBack)
        {
            Step = InstallerStep.Welcome;
        }
    }

    public void Complete(bool succeeded) => Step = succeeded ? InstallerStep.Complete : InstallerStep.Failed;
}
