using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HanabePhotoManager.Core.Recovery;

namespace HanabePhotoManager.App.Recovery;

public sealed class RecoveryImageService
{
    private const long MaximumBoxSize = 16L * 1024 * 1024 * 1024;
    private const int SearchBlockSize = 4 * 1024 * 1024;

    public async Task<RecoveryScanResult> ScanAsync(string imagePath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!RecoverySafetyPolicy.IsSupportedImage(imagePath)) throw new NotSupportedException("仅支持 .img 和 .raw 镜像。");
        await using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read, SearchBlockSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var boot = new byte[512];
        await stream.ReadExactlyAsync(boot, cancellationToken);
        var exfatOffset = FindExFatOffset(stream, boot, cancellationToken);
        var isExFat = exfatOffset >= 0;
        var sectorSize = isExFat ? 1 << bootAt(stream, exfatOffset + 108) : 0;
        var clusterSize = isExFat ? sectorSize * (1 << bootAt(stream, exfatOffset + 109)) : 0;

        var starts = await FindFtypSignaturesAsync(stream, progress, cancellationToken);
        var candidates = new List<RecoveryCandidate>();
        foreach (var start in starts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = ReadCandidate(stream, start);
            if (candidate is not null && candidates.All(item => Math.Abs(item.StartOffset - candidate.StartOffset) > 16)) candidates.Add(candidate);
        }
        progress?.Report(100);
        return new(imagePath, stream.Length, isExFat, sectorSize, clusterSize, candidates, DateTimeOffset.UtcNow);

        static byte bootAt(FileStream source, long offset) { source.Position = offset; var value = source.ReadByte(); return value < 0 ? (byte)0 : (byte)value; }
    }

    private static long FindExFatOffset(FileStream stream, byte[] first, CancellationToken token)
    {
        if (first.AsSpan(3, 8).SequenceEqual("EXFAT   "u8)) return 0;
        if (first[510] != 0x55 || first[511] != 0xAA) return -1;
        for (var i = 0; i < 4; i++)
        {
            token.ThrowIfCancellationRequested();
            var entry = first.AsSpan(446 + i * 16, 16);
            var lba = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..12]);
            if (lba == 0) continue;
            var offset = (long)lba * 512;
            if (offset + 512 > stream.Length) continue;
            stream.Position = offset + 3;
            Span<byte> signature = new byte[8];
            if (stream.Read(signature) == 8 && signature.SequenceEqual("EXFAT   "u8)) return offset;
        }
        return -1;
    }

    private static async Task<List<long>> FindFtypSignaturesAsync(FileStream stream, IProgress<double>? progress, CancellationToken token)
    {
        var starts = new List<long>();
        var buffer = new byte[SearchBlockSize + 16];
        long offset = 0;
        while (offset < stream.Length)
        {
            token.ThrowIfCancellationRequested();
            stream.Position = offset;
            var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(SearchBlockSize, stream.Length - offset)), token);
            for (var i = 4; i + 4 <= read; i++)
                if (buffer[i] == (byte)'f' && buffer[i + 1] == (byte)'t' && buffer[i + 2] == (byte)'y' && buffer[i + 3] == (byte)'p') starts.Add(offset + i - 4);
            offset += Math.Max(1, read - 8);
            progress?.Report(Math.Min(90, offset * 90d / stream.Length));
        }
        return starts;
    }

    private static RecoveryCandidate? ReadCandidate(FileStream stream, long start)
    {
        var position = start;
        var hasFtyp = false; var hasMdat = false; var hasMoov = false; var hasTables = false;
        var boxes = 0;
        while (position + 8 <= stream.Length && boxes++ < 512)
        {
            Span<byte> header = new byte[16]; stream.Position = position;
            if (stream.Read(header[..8]) != 8) break;
            long size = BinaryPrimitives.ReadUInt32BigEndian(header[..4]);
            var type = Encoding.ASCII.GetString(header[4..8]);
            var headerSize = 8;
            if (size == 1) { if (stream.Read(header[8..16]) != 8) break; size = (long)BinaryPrimitives.ReadUInt64BigEndian(header[8..16]); headerSize = 16; }
            if (size == 0) size = stream.Length - position;
            if (size < headerSize || size > MaximumBoxSize || position > stream.Length - size) break;
            hasFtyp |= type == "ftyp"; hasMdat |= type == "mdat"; hasMoov |= type == "moov";
            if (type == "moov") hasTables = ContainsSampleTable(stream, position + headerSize, size - headerSize);
            position += size;
            if (hasFtyp && hasMdat && hasMoov) break;
        }
        if (!hasFtyp || !hasMdat) return null;
        var direct = hasMoov && hasTables;
        return new(Guid.NewGuid().ToString("N"), $"candidate_{start:X}.mp4", start, position, hasFtyp, hasMdat, hasMoov, hasTables,
            false, direct ? RecoveryConfidence.High : RecoveryConfidence.Medium,
            direct ? RecoveryCandidateStatus.Direct : RecoveryCandidateStatus.Experimental);
    }

    private static bool ContainsSampleTable(FileStream stream, long offset, long length)
    {
        var readLength = (int)Math.Min(length, 32 * 1024 * 1024);
        var data = new byte[readLength]; stream.Position = offset;
        var read = stream.Read(data, 0, readLength);
        return Contains(data.AsSpan(0, read), "stsz"u8) && (Contains(data.AsSpan(0, read), "stco"u8) || Contains(data.AsSpan(0, read), "co64"u8));
        static bool Contains(ReadOnlySpan<byte> data, ReadOnlySpan<byte> value) => data.IndexOf(value) >= 0;
    }

    public async Task<string> ExportDirectAsync(RecoveryScanResult scan, RecoveryCandidate candidate, string outputDirectory, IProgress<double>? progress, CancellationToken token)
    {
        if (!RecoverySafetyPolicy.CanExport(candidate, scan.ImageLength)) throw new InvalidOperationException("该候选不满足安全直恢条件。实验候选不会自动导出。");
        Directory.CreateDirectory(outputDirectory);
        var rawPath = Path.Combine(outputDirectory, candidate.Id + ".raw-candidate");
        var mp4Path = Path.Combine(outputDirectory, candidate.ExpectedFileName);
        await CopyRangeAsync(scan.ImagePath, rawPath, candidate.StartOffset, candidate.Length, progress, token);
        File.Copy(rawPath, mp4Path, overwrite: false);
        await using var recoveredStream = File.OpenRead(mp4Path);
        var sha = Convert.ToHexString(await SHA256.HashDataAsync(recoveredStream, token)).ToLowerInvariant();
        var report = new { observed = new { scan.ImagePath, scan.ImageLength, scan.IsExFat }, verified = new { candidate.HasFtyp, candidate.HasMdat, candidate.HasMoov, candidate.HasSampleTables, sha256 = sha }, inferred = new { candidate.ExpectedFileName, candidate.Confidence }, experimental = false };
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, candidate.Id + ".json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), token);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, candidate.Id + ".md"), $"# 相机视频安全恢复报告\n\n- 镜像：`{scan.ImagePath}`\n- 输出：`{mp4Path}`\n- SHA-256：`{sha}`\n- 验证：ftyp / mdat / moov / sample tables 均存在\n- 原始候选永久保留：`{rawPath}`\n", token);
        return mp4Path;
    }

    private static async Task CopyRangeAsync(string sourcePath, string destinationPath, long offset, long length, IProgress<double>? progress, CancellationToken token)
    {
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true);
        source.Position = offset; var buffer = new byte[1024 * 1024]; long copied = 0;
        while (copied < length)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, length - copied)), token);
            if (read == 0) throw new EndOfStreamException();
            await destination.WriteAsync(buffer.AsMemory(0, read), token); copied += read; progress?.Report(copied * 100d / length);
        }
        await destination.FlushAsync(token);
    }
}
