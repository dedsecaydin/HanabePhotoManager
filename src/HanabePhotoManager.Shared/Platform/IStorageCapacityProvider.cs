namespace HanabePhotoManager.App.Platform;

public interface IStorageCapacityProvider
{
    (ulong Free, ulong Total)? GetCapacity(string path);
}

internal sealed class UnavailableStorageCapacityProvider : IStorageCapacityProvider
{
    public (ulong Free, ulong Total)? GetCapacity(string path) => null;
}
