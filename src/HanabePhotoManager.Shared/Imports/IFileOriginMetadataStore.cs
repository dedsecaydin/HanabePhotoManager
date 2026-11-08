namespace HanabePhotoManager.App.Imports;

internal interface IFileOriginMetadataStore
{
    Task<FileOriginMetadata?> ReadAsync(string path, CancellationToken cancellationToken);
    Task<FileOriginMetadataWriteResult> WriteAndVerifyAsync(string path, FileOriginMetadata metadata, CancellationToken cancellationToken);
}
