using System.Buffers.Binary;
using System.IO;
using FluentAssertions;
using HanabePhotoManager.App.Recovery;
using HanabePhotoManager.Core.Recovery;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class RecoveryImageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HanabeRecoveryTests", Guid.NewGuid().ToString("N"));
    public RecoveryImageServiceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task ScanAsync_FindsBoundedCompleteMp4AndAllowsSafeCopyOnly()
    {
        var image = Path.Combine(_root, "sony.img");
        await File.WriteAllBytesAsync(image, BuildImage());
        var service = new RecoveryImageService();

        var scan = await service.ScanAsync(image, null, CancellationToken.None);

        scan.IsExFat.Should().BeTrue();
        scan.Candidates.Should().ContainSingle();
        var candidate = scan.Candidates.Single();
        candidate.CanRecoverDirectly.Should().BeTrue();
        var output = Path.Combine(_root, "output");
        var recovered = await service.ExportDirectAsync(scan, candidate, output, null, CancellationToken.None);
        File.Exists(recovered).Should().BeTrue();
        Directory.GetFiles(output, "*.raw-candidate").Should().ContainSingle();
        Directory.GetFiles(output, "*.json").Should().ContainSingle();
        Directory.GetFiles(output, "*.md").Should().ContainSingle();
    }

    private static byte[] BuildImage()
    {
        using var stream = new MemoryStream();
        var boot = new byte[512]; "EXFAT   "u8.CopyTo(boot.AsSpan(3)); boot[108] = 9; boot[109] = 3; stream.Write(boot);
        WriteBox(stream, "ftyp", "isom0000"u8.ToArray());
        WriteBox(stream, "mdat", new byte[64]);
        using var moov = new MemoryStream(); WriteBox(moov, "stsz", new byte[12]); WriteBox(moov, "stco", new byte[12]);
        WriteBox(stream, "moov", moov.ToArray());
        return stream.ToArray();
    }

    private static void WriteBox(Stream stream, string type, byte[] payload)
    {
        Span<byte> header = stackalloc byte[8]; BinaryPrimitives.WriteUInt32BigEndian(header, (uint)(payload.Length + 8));
        System.Text.Encoding.ASCII.GetBytes(type, header[4..]); stream.Write(header); stream.Write(payload);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
