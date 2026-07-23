using System.Security.Cryptography;
using HanabePhotoManager.Core.Imports;

namespace HanabePhotoManager.Infrastructure.Files;

public sealed class Sha256FileHasher : IFileHasher
{
    public async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 128,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}
