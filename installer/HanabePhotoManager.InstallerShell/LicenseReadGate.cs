namespace HanabePhotoManager.InstallerShell;

public static class LicenseReadGate
{
    public static bool HasReachedEnd(double offset, double viewport, double extent)
    {
        if (double.IsNaN(offset) || double.IsNaN(viewport) || double.IsNaN(extent))
        {
            return false;
        }

        return extent <= viewport || offset + viewport >= extent - 1;
    }
}
