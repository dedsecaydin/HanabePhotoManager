namespace HanabePhotoManager.Core.Imports;

public interface IFileHasher
{
    Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken);
}

public interface IDestinationProbe
{
    Task<ConflictKind> CheckAsync(SourceMediaFile source, string destination, CancellationToken cancellationToken);
}
