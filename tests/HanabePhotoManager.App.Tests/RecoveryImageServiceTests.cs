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
        Directory.GetFiles(output, "*.raw-candidate", SearchOption.AllDirectories).Should().ContainSingle();
        Directory.GetFiles(output, "*.json", SearchOption.AllDirectories).Should().ContainSingle();
        Directory.GetFiles(output, "*.md", SearchOption.AllDirectories).Should().ContainSingle();
    }

    [Fact]
    public async Task Export_CancellationLeavesNoPartialOutput()
    {
        var image = Path.Combine(_root, "cancel.img");
        await File.WriteAllBytesAsync(image, BuildImage());
        var service = new RecoveryImageService();
        var scan = await service.ScanAsync(image, null, default);
        using var cancellation = new CancellationTokenSource();
        var output = Path.Combine(_root, "output");
        var action = () => service.ExportDirectAsync(scan, scan.Candidates.Single(), output,
            new CancelOnProgress(cancellation), cancellation.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
        Directory.GetFileSystemEntries(output).Should().BeEmpty();
        (await File.ReadAllBytesAsync(image)).Should().Equal(BuildImage());
    }

    private sealed class CancelOnProgress(CancellationTokenSource cancellation) : IProgress<double>
    {
        public void Report(double value) => cancellation.Cancel();
    }

    [Fact]
    public async Task Export_RejectsChangedImage()
    {
        var image = Path.Combine(_root, "changed.img");
        await File.WriteAllBytesAsync(image, BuildImage());
        var service = new RecoveryImageService();
        var scan = await service.ScanAsync(image, null, default);
        await File.WriteAllBytesAsync(image, new byte[512]);
        var action = () => service.ExportDirectAsync(scan, scan.Candidates.Single(), Path.Combine(_root, "output"), null, default);
        await action.Should().ThrowAsync<InvalidDataException>();
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

    [Fact]
    public async Task WriteTimeRange_UsesDirectoryLastWriteAndSkipsOutOfRange()
    {
        var path = Path.Combine(_root, "dated.img");
        var bytes = new byte[4096];
        "EXFAT   "u8.CopyTo(bytes.AsSpan(3)); bytes[108] = 9;
        void U32(int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
        U32(80, 1); U32(88, 2); U32(92, 4); U32(96, 2); U32(512 + 8, 0xffffffff);
        var file = BuildImage()[512..]; file.CopyTo(bytes, 1536);
        bytes[1024] = 0x85; bytes[1025] = 2;
        uint timestamp = (46u << 25) | (9u << 21) | (7u << 16) | (14u << 11) | (30u << 5);
        U32(1036, timestamp);
        bytes[1056] = 0xc0; bytes[1057] = 3; bytes[1059] = 5; U32(1076, 3);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(1080), (ulong)file.Length);
        bytes[1088] = 0xc1; System.Text.Encoding.Unicode.GetBytes("a.mp4").CopyTo(bytes, 1090);
        await File.WriteAllBytesAsync(path, bytes);
        var service = new RecoveryImageService();
        var inside = await service.ScanAsync(path, null, default, writtenFrom: new(2026, 9, 7, 14, 0, 0), writtenTo: new(2026, 9, 7, 15, 0, 0));
        inside.Candidates.Should().ContainSingle();
        inside.Candidates[0].LastWriteTime.Should().Be(new DateTime(2026, 9, 7, 14, 30, 0));
        inside.Candidates[0].CanRecoverDirectly.Should().BeTrue();
        var outside = await service.ScanAsync(path, null, default, writtenFrom: new(2026, 9, 8), writtenTo: new(2026, 9, 9));
        outside.Candidates.Should().BeEmpty();
        U32(1036, 0); await File.WriteAllBytesAsync(path, bytes);
        var unknown = await service.ScanAsync(path, null, default, writtenFrom: new(2026, 9, 7), writtenTo: new(2026, 9, 8), includeUnknownTime: false);
        unknown.Candidates.Should().BeEmpty();
        var included = await service.ScanAsync(path, null, default, writtenFrom: new(2026, 9, 7), writtenTo: new(2026, 9, 8));
        included.Candidates.Should().ContainSingle();
        included.Candidates[0].LastWriteTime.Should().BeNull();
    }

    private static void WriteBox(Stream stream, string type, byte[] payload)
    {
        Span<byte> header = stackalloc byte[8]; BinaryPrimitives.WriteUInt32BigEndian(header, (uint)(payload.Length + 8));
        System.Text.Encoding.ASCII.GetBytes(type, header[4..]); stream.Write(header); stream.Write(payload);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
