namespace HanabePhotoManager.App.Imports;

internal sealed record FileOriginMetadataWriteResult(bool Success, string? Error)
{
    internal static FileOriginMetadataWriteResult Completed { get; } = new(true, null);
    internal static FileOriginMetadataWriteResult Failure(string error) => new(false, error);
}
