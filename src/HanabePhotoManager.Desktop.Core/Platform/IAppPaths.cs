namespace HanabePhotoManager.Desktop.Core.Platform;

public interface IAppPaths
{
    string ApplicationDataDirectory { get; }

    string CacheDirectory { get; }
}
