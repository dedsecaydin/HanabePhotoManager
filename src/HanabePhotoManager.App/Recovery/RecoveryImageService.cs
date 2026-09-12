using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HanabePhotoManager.Core.Recovery;

namespace HanabePhotoManager.App.Recovery;

public sealed class RecoveryImageService
{
    private readonly string? _devicePath;
    private readonly Func<Stream>? _deviceReader;
    private readonly Action<string>? _validateOutput;
    public RecoveryImageService() { }
    internal RecoveryImageService(string devicePath, Func<Stream> reader, Action<string>? validateOutput = null)
    { _devicePath = devicePath; _deviceReader = reader; _validateOutput = validateOutput; }
    private Stream OpenSource(string path) => path == _devicePath && _deviceReader is not null
        ? _deviceReader() : new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, SearchBlockSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
    private const long MaximumBoxSize = 16L * 1024 * 1024 * 1024;
    private const int SearchBlockSize = 4 * 1024 * 1024;

    public async Task<RecoveryScanResult> ScanAsync(string imagePath, IProgress<double>? progress, CancellationToken cancellationToken,
        IProgress<RecoveryCandidate>? candidateProgress = null, DateTime? writtenFrom = null, DateTime? writtenTo = null, bool includeUnknownTime = true)
    {
        if (imagePath != _devicePath && !RecoverySafetyPolicy.IsSupportedImage(imagePath)) throw new NotSupportedException("仅支持 .img 和 .raw 镜像。");
        await using var stream = OpenSource(imagePath);
        if (stream.Length < 512) throw new InvalidDataException("镜像不足 512 字节，请选择完整的存储卡镜像。");
        var boot = new byte[512];
        await stream.ReadExactlyAsync(boot, cancellationToken);
        var exfatOffset = FindExFatOffset(stream, boot, cancellationToken);
        var isExFat = exfatOffset >= 0;
        var sectorSize = isExFat ? 1 << bootAt(stream, exfatOffset + 108) : 0;
        var clusterSize = isExFat ? sectorSize * (1 << bootAt(stream, exfatOffset + 109)) : 0;

        if (writtenFrom.HasValue != writtenTo.HasValue || writtenFrom > writtenTo) throw new ArgumentException("请填写有效的起止写入时间。");
        IReadOnlyList<ExFatTimeIndex.Entry> allEntries = [];
        if (isExFat)
        {
            try { allEntries = ExFatTimeIndex.Read(stream, exfatOffset, cancellationToken); }
            catch (InvalidDataException) when (writtenFrom is null) { /* Full carving still works without a readable directory. */ }
        }
        IReadOnlyList<ExFatTimeIndex.Entry>? index = null;
        if (writtenFrom is not null)
        {
            if (!isExFat) throw new NotSupportedException("按写入时间快速扫描需要可读取的 exFAT 目录。请选择完整扫描查找目录已丢失的视频。");
            index = allEntries
                .Where(e => e.LastWrite is { } time ? time >= writtenFrom && time <= writtenTo : includeUnknownTime).ToArray();
        }
        var starts = index is null ? await FindFtypSignaturesAsync(stream, progress, cancellationToken) : index.Select(e => e.Offset).Distinct().ToList();
        var entriesByOffset = allEntries.GroupBy(e => e.Offset).ToDictionary(g => g.Key, g => g.ToArray());
        var candidates = new List<RecoveryCandidate>();
        foreach (var start in starts.OrderBy(x => x))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (candidates.Any(c => (c.IsJpeg || c.IsRawPhoto && c.CanRecoverDirectly) && start > c.StartOffset && start < c.EndOffset)) continue;
            entriesByOffset.TryGetValue(start, out var matchingEntries);
            var entry = matchingEntries?.Length == 1 ? matchingEntries[0] : null;
            var candidate = ReadMediaCandidate(stream, start, cancellationToken, entry?.Contiguous == true ? entry.Length : null);
            if (candidate is not null && entry is not null)
            {
                var safe = entry.Contiguous && candidate.Length == entry.Length;
                candidate = candidate with { LastWriteTime = entry.LastWrite, IsFragmented = !safe,
                    Status = safe ? candidate.Status : RecoveryCandidateStatus.Experimental,
                    StructureEvidence = candidate.StructureEvidence + (candidate.IsRawPhoto && !safe ? "\n目录长度或连续性未确认，不能导出。" : "") };
            }
            if (candidate is not null && candidates.All(item => Math.Abs(item.StartOffset - candidate.StartOffset) > 16))
            {
                candidates.Add(candidate);
                candidateProgress?.Report(candidate);
            }
            if (index is not null) progress?.Report((starts.IndexOf(start) + 1d) * 100 / Math.Max(1, starts.Count));
        }
        progress?.Report(100);
        return new(imagePath, stream.Length, isExFat, sectorSize, clusterSize, candidates, DateTimeOffset.UtcNow);

        static byte bootAt(Stream source, long offset) { source.Position = offset; var value = source.ReadByte(); return value < 0 ? (byte)0 : (byte)value; }
    }

    private static long FindExFatOffset(Stream stream, byte[] first, CancellationToken token)
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

    private static async Task<List<long>> FindFtypSignaturesAsync(Stream stream, IProgress<double>? progress, CancellationToken token)
    {
        var starts = new List<long>();
        var buffer = new byte[SearchBlockSize + 16];
        long offset = 0;
        while (offset < stream.Length)
        {
            token.ThrowIfCancellationRequested();
            stream.Position = offset;
            var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(SearchBlockSize, stream.Length - offset)), token);
            for (var j = 0; j + 3 <= read; j++)
                if (buffer[j] == 0xff && buffer[j + 1] == 0xd8 && buffer[j + 2] == 0xff) starts.Add(offset + j);
            for (var j = 0; j + 4 <= read; j++)
                if (buffer.AsSpan(j, 4).SequenceEqual("II*\0"u8) || buffer.AsSpan(j, 4).SequenceEqual("MM\0*"u8)) starts.Add(offset + j);
            for (var i = 4; i + 4 <= read; i++)
                if (buffer[i] == (byte)'f' && buffer[i + 1] == (byte)'t' && buffer[i + 2] == (byte)'y' && buffer[i + 3] == (byte)'p') starts.Add(offset + i - 4);
            offset += Math.Max(1, read - 8);
            progress?.Report(Math.Min(90, offset * 90d / stream.Length));
        }
        return starts;
    }

    private static RecoveryCandidate? ReadMediaCandidate(Stream stream, long start, CancellationToken token, long? directoryLength = null)
    {
        if (start < 0 || start > stream.Length - 16) return null;
        stream.Position = start;
        Span<byte> header = stackalloc byte[16]; stream.ReadExactly(header);
        // CR3 must not fall through to the MP4 parser, including damaged CR3 files.
        if (RawRecoveryReader.IsSignature(header)) return new RawRecoveryReader(stream, start, directoryLength, token).Read();
        return JpegRecoveryReader.Read(stream, start, token) ?? ReadCandidate(stream, start);
    }

    private static RecoveryCandidate? ReadCandidate(Stream stream, long start)
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

    private static bool ContainsSampleTable(Stream stream, long offset, long length)
    {
        var readLength = (int)Math.Min(length, 32 * 1024 * 1024);
        var data = new byte[readLength]; stream.Position = offset;
        var read = stream.Read(data, 0, readLength);
        return Contains(data.AsSpan(0, read), "stsz"u8) && (Contains(data.AsSpan(0, read), "stco"u8) || Contains(data.AsSpan(0, read), "co64"u8));
        static bool Contains(ReadOnlySpan<byte> data, ReadOnlySpan<byte> value) => data.IndexOf(value) >= 0;
    }

    public async Task<string> ExportDirectAsync(RecoveryScanResult scan, RecoveryCandidate candidate, string outputDirectory, IProgress<double>? progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _validateOutput?.Invoke(outputDirectory);
        if (!RecoverySafetyPolicy.CanExport(candidate, scan.ImageLength) || !scan.Candidates.Contains(candidate)) throw new InvalidOperationException("该候选不满足安全直恢条件。实验候选不会自动导出。");
        if (Path.GetFileName(candidate.ExpectedFileName) != candidate.ExpectedFileName || Path.GetFileName(candidate.Id) != candidate.Id)
            throw new InvalidDataException("候选文件名无效。");
        await using var imageLock = OpenSource(scan.ImagePath);
        if (imageLock.Length != scan.ImageLength || ReadMediaCandidate(imageLock, candidate.StartOffset, token, candidate.DirectoryLength) is not { CanRecoverDirectly: true } current || current.EndOffset != candidate.EndOffset)
            throw new InvalidDataException("镜像或候选结构发生变化，请重新扫描。");
        token.ThrowIfCancellationRequested();
        var finalDirectory = Path.Combine(Path.GetFullPath(outputDirectory), "recovered_" + Guid.NewGuid().ToString("N"));
        var stagingDirectory = finalDirectory + ".partial";
        Directory.CreateDirectory(stagingDirectory);
        try
        {
        outputDirectory = stagingDirectory;
        var rawPath = Path.Combine(outputDirectory, candidate.Id + ".raw-candidate");
        var mp4Path = Path.Combine(outputDirectory, candidate.ExpectedFileName);
        await CopyRangeAsync(scan.ImagePath, rawPath, candidate.StartOffset, candidate.Length, progress, token);
        await CopyRangeAsync(rawPath, mp4Path, 0, candidate.Length, null, token);
        string sha;
        await using (var recoveredStream = File.OpenRead(mp4Path))
            sha = Convert.ToHexString(await SHA256.HashDataAsync(recoveredStream, token)).ToLowerInvariant();
        var report = new { observed = new { scan.ImagePath, scan.ImageLength, scan.IsExFat }, verified = new { candidate.IsJpeg, candidate.RawFormat, candidate.DirectoryLength, candidate.StructureEvidence, candidate.HasCompletePhotoStructure, candidate.HasFtyp, candidate.HasMdat, candidate.HasMoov, candidate.HasSampleTables, sha256 = sha }, inferred = new { candidate.ExpectedFileName, candidate.Confidence }, experimental = false };
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, candidate.Id + ".json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), token);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, candidate.Id + ".md"), $"# 相机媒体恢复报告\n\n- 镜像：`{scan.ImagePath}`\n- 输出：`{Path.Combine(finalDirectory, candidate.ExpectedFileName)}`\n- SHA-256：`{sha}`\n- 验证：{(candidate.IsRawPhoto ? candidate.StructureEvidence : candidate.IsJpeg ? "已识别 JPEG 帧、扫描数据和结束标记；未验证全部像素解码" : "已识别 ftyp / mdat / moov / 采样表标记；未验证完整播放")}\n- 原始候选：`{Path.Combine(finalDirectory, candidate.Id + ".raw-candidate")}`\n", token);
        token.ThrowIfCancellationRequested();
        Directory.Move(stagingDirectory, finalDirectory);
        progress?.Report(100);
        return Path.Combine(finalDirectory, candidate.ExpectedFileName);
        }
        catch
        {
            // Only this attempt's isolated output is removed; source and previous exports are untouched.
            if (Directory.Exists(stagingDirectory)) Directory.Delete(stagingDirectory, true);
            throw;
        }
    }

    private async Task CopyRangeAsync(string sourcePath, string destinationPath, long offset, long length, IProgress<double>? progress, CancellationToken token)
    {
        await using var source = OpenSource(sourcePath);
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
