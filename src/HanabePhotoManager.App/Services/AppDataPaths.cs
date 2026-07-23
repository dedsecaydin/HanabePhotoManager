using System.IO;

namespace HanabePhotoManager.App.Services;

public static class AppDataPaths
{
    public static string Root
    {
        get
        {
            var isolated = Environment.GetEnvironmentVariable("HANABE_APP_DATA_DIR");
            return string.IsNullOrWhiteSpace(isolated)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HanabePhotoManager")
                : Path.GetFullPath(isolated);
        }
    }
}
