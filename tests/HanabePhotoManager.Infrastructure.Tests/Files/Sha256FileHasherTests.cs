using FluentAssertions;
using HanabePhotoManager.Infrastructure.Files;

namespace HanabePhotoManager.Infrastructure.Tests.Files;

public sealed class Sha256FileHasherTests
{
    [Fact]
    public async Task ComputeSha256Async_ReturnsUppercaseHexHash()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hanabe-{Guid.NewGuid():N}.txt");
        await File.WriteAllBytesAsync(path, [0x61, 0x62, 0x63]);

        try
        {
            var hash = await new Sha256FileHasher().ComputeSha256Async(path, CancellationToken.None);

            hash.Should().Be("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
