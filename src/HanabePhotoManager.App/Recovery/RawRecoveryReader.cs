using System.Buffers.Binary;
using System.IO;
using System.Text;
using HanabePhotoManager.Core.Recovery;

namespace HanabePhotoManager.App.Recovery;

// Independent format-based reader. No PhotoRec or LibRaw source is embedded.
// RAW has vendor trailers: only a directory-bounded, contiguous file is exportable.
internal sealed class RawRecoveryReader(FileStream stream, long start, long? directoryLength, CancellationToken token)
{
    private readonly long _limit = Math.Min(directoryLength ?? 512L * 1024 * 1024, stream.Length - start);
    private long _end;
    private bool _little;
    private string _format = string.Empty;

    internal static bool IsSignature(ReadOnlySpan<byte> header) => header.Length >= 12 &&
        (header[..4].SequenceEqual("II*\0"u8) || header[..4].SequenceEqual("MM\0*"u8) ||
         header[4..8].SequenceEqual("ftyp"u8) && header[8..12].SequenceEqual("crx "u8));

    internal RecoveryCandidate? Read()
    {
        try
        {
            var header = Bytes(0, 16);
            if (!IsSignature(header)) return null;
            bool valid;
            if (header.AsSpan(8, 4).SequenceEqual("crx "u8)) { _format = "CR3"; valid = ReadCr3(); }
            else { _little = header[0] == 'I'; valid = ReadTiff(header); }
            if (_format.Length == 0) return null;
            var bounded = directoryLength is > 0 && directoryLength <= stream.Length - start;
            var direct = valid && bounded;
            var reason = !valid ? "结构或数据引用不完整，不能自动导出。" : !bounded ?
                "已识别 RAW 结构，但缺少目录文件长度；不能确认专有尾部数据，不能自动导出。" :
                "目录文件边界与结构检查通过；保留原始 RAW 数据，未验证像素解码。请用相机 RAW 软件检查画面。";
            return new(Guid.NewGuid().ToString("N"), $"candidate_{start:X}.{_format.ToLowerInvariant()}", start,
                start + (bounded ? directoryLength!.Value : Math.Max(16, _end)), false, false, false, false,
                false, direct ? RecoveryConfidence.High : RecoveryConfidence.Low,
                direct ? RecoveryCandidateStatus.Direct : RecoveryCandidateStatus.Experimental)
            { RawFormat = _format, HasCompletePhotoStructure = valid, DirectoryLength = bounded ? directoryLength : null,
                StructureEvidence = $"{_format} · {( _format == "CR3" ? "容器、RAW 轨道与采样范围" : "TIFF 目录、图像条带/瓦片范围")}\n{reason}" };
        }
        catch (InvalidDataException) { return null; }
        catch (EndOfStreamException) { return null; }
        catch (InvalidOperationException) { return null; }
        catch (OverflowException) { return null; }
    }

    private byte[] Bytes(long at, int length)
    {
        token.ThrowIfCancellationRequested();
        if (at < 0 || length < 0 || at > _limit - length) throw new InvalidDataException();
        _end = Math.Max(_end, at + length);
        var data = new byte[length]; stream.Position = start + at; stream.ReadExactly(data); return data;
    }
    private uint U32(ReadOnlySpan<byte> data) => _little ? BinaryPrimitives.ReadUInt32LittleEndian(data) : BinaryPrimitives.ReadUInt32BigEndian(data);
    private ushort U16(ReadOnlySpan<byte> data) => _little ? BinaryPrimitives.ReadUInt16LittleEndian(data) : BinaryPrimitives.ReadUInt16BigEndian(data);
    private void Range(long at, long length)
    {
        if (at < 0 || length <= 0 || at > _limit - length) throw new InvalidDataException();
        _end = Math.Max(_end, at + length);
    }

    private bool ReadTiff(byte[] header)
    {
        var queue = new Queue<uint>(); queue.Enqueue(U32(header.AsSpan(4)));
        if (header.AsSpan(8, 4).SequenceEqual("CR\x02\0"u8)) { _format = "CR2"; queue.Enqueue(U32(header.AsSpan(12))); }
        var visited = new HashSet<uint>();
        bool image = false, raw = _format == "CR2";
        string make = string.Empty;
        while (queue.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var at = queue.Dequeue();
            if (at == 0) continue;
            if (!visited.Add(at) || visited.Count > 256) throw new InvalidDataException();
            int count = U16(Bytes(at, 2));
            if (count > 4096) throw new InvalidDataException();
            var entries = Bytes(at + 2L, count * 12 + 4);
            var values = new Dictionary<ushort, uint[]>();
            for (int i = 0; i < count; i++)
            {
                var entry = entries.AsSpan(i * 12, 12);
                var tag = U16(entry); var type = U16(entry[2..]); var n = U32(entry[4..]);
                int unit = type switch { 1 or 2 or 6 or 7 => 1, 3 or 8 => 2, 4 or 9 or 11 or 13 => 4, 5 or 10 or 12 => 8, _ => 0 };
                if (unit == 0) throw new InvalidDataException();
                long size = (long)n * unit;
                long offset = size > 4 ? U32(entry[8..]) : at + 2L + i * 12 + 8;
                if (size > 0) Range(offset, size);
                if (tag == 271 && size is > 0 and <= 256) make = Encoding.ASCII.GetString(Bytes(offset, (int)size)).TrimEnd('\0').Trim();
                if (tag == 50706) { _format = "DNG"; raw = true; }
                if (tag is 33421 or 33422) raw = true;
                if (tag is not (259 or 262 or 273 or 279 or 324 or 325 or 330 or 34665 or 34853 or 40965 or 513 or 514)) continue;
                if (type is not (3 or 4 or 13) || n > 65536) throw new InvalidDataException();
                var data = Bytes(offset, (int)size);
                var list = new uint[n];
                for (int j = 0; j < list.Length; j++) list[j] = unit == 2 ? U16(data.AsSpan(j * unit)) : U32(data.AsSpan(j * unit));
                if (!values.TryAdd(tag, list)) throw new InvalidDataException();
                if (tag is 330 or 34665 or 34853 or 40965) foreach (var child in list) queue.Enqueue(child);
                if (tag == 262 && list.Contains(32803u) || tag == 259 && list.Contains(32767u)) raw = true;
            }
            foreach (var (offsetTag, sizeTag) in new[] { (273, 279), (324, 325), (513, 514) })
            {
                bool a = values.TryGetValue((ushort)offsetTag, out var offsets), b = values.TryGetValue((ushort)sizeTag, out var sizes);
                if (!a && !b) continue;
                if (!a || !b || offsets!.Length == 0 || offsets.Length != sizes!.Length) throw new InvalidDataException();
                for (int j = 0; j < offsets.Length; j++) Range(offsets[j], sizes[j]);
                if (offsetTag != 513) image = true; // A JPEG preview alone is not a RAW image.
            }
            var next = U32(entries.AsSpan(count * 12));
            if (next != 0 && !visited.Contains(next)) queue.Enqueue(next);
            else if (next != 0) throw new InvalidDataException();
        }
        if (_format.Length == 0 && raw && make.Equals("SONY", StringComparison.OrdinalIgnoreCase)) _format = "ARW";
        if (_format.Length == 0 && raw && make.StartsWith("NIKON", StringComparison.OrdinalIgnoreCase)) _format = "NEF";
        return image && raw;
    }

    private sealed record Box(string Type, long Payload, long End);
    private List<Box> Boxes(long from, long to)
    {
        var result = new List<Box>();
        while (from < to)
        {
            if (result.Count >= 4096 || to - from < 8) throw new InvalidDataException();
            var h = Bytes(from, 8); long size = BinaryPrimitives.ReadUInt32BigEndian(h); int header = 8;
            if (size == 1) { size = checked((long)BinaryPrimitives.ReadUInt64BigEndian(Bytes(from + 8, 8))); header = 16; }
            if (size < header || size > to - from) throw new InvalidDataException();
            result.Add(new(Encoding.ASCII.GetString(h, 4, 4), from + header, from + size)); from += size;
        }
        return result;
    }

    private bool ReadCr3()
    {
        // Without a directory boundary the container is evidence only, never an MP4.
        if (directoryLength is null) { _end = 16; return false; }
        var boxes = Boxes(0, _limit);
        var moov = boxes.SingleOrDefault(b => b.Type == "moov");
        var data = boxes.Where(b => b.Type == "mdat").ToArray();
        if (moov is null || data.Length == 0) return false;
        bool rawTrack = false, canonPhoto = false;
        foreach (var child in Boxes(moov.Payload, moov.End))
        {
            if (child.Type == "uuid" && child.End - child.Payload >= 16 &&
                Convert.ToHexString(Bytes(child.Payload, 16)) == "85C0B687820F11E08111F4CE462B6A48")
                foreach (var box in Boxes(child.Payload + 16, child.End))
                    if (box.Type == "CNCV" && box.End - box.Payload >= 8)
                        canonPhoto |= Bytes(box.Payload, 8).AsSpan().SequenceEqual("CanonCR3"u8);
            if (child.Type != "trak") continue;
            var mdia = Boxes(child.Payload, child.End).SingleOrDefault(b => b.Type == "mdia");
            if (mdia is null) return false;
            var minf = Boxes(mdia.Payload, mdia.End).SingleOrDefault(b => b.Type == "minf");
            if (minf is null) return false;
            var stbl = Boxes(minf.Payload, minf.End).SingleOrDefault(b => b.Type == "stbl");
            if (stbl is null || !CheckSamples(stbl, data, out var isRaw)) return false;
            rawTrack |= isRaw;
        }
        return canonPhoto && rawTrack;
    }

    private bool CheckSamples(Box stbl, Box[] media, out bool isRaw)
    {
        isRaw = false;
        var tables = Boxes(stbl.Payload, stbl.End);
        var sz = tables.SingleOrDefault(b => b.Type == "stsz");
        var co = tables.SingleOrDefault(b => b.Type is "co64" or "stco");
        var sc = tables.SingleOrDefault(b => b.Type == "stsc");
        var sd = tables.SingleOrDefault(b => b.Type == "stsd");
        if (sz is null || co is null || sc is null || sd is null) return false;
        uint Field(Box b, int at)
        {
            if (b.End - b.Payload < at + 4L) throw new InvalidDataException();
            return BinaryPrimitives.ReadUInt32BigEndian(Bytes(b.Payload + at, 4));
        }
        var descriptions = Boxes(sd.Payload + 8, sd.End);
        if (Field(sd, 4) != descriptions.Count) return false;
        foreach (var d in descriptions.Where(b => b.Type == "CRAW" && b.End - b.Payload >= 78))
            isRaw |= Boxes(d.Payload + 78, d.End).Any(b => b.Type == "CMP1");
        uint commonSize = Field(sz, 4), samples = Field(sz, 8), chunks = Field(co, 4), mappings = Field(sc, 4);
        if (samples is 0 or > 1000000 || chunks is 0 or > 1000000 || mappings is 0 or > 65536) return false;
        if (sz.End - sz.Payload != 12L + (commonSize == 0 ? samples * 4L : 0) ||
            co.End - co.Payload != 8L + chunks * (co.Type == "co64" ? 8L : 4L) || sc.End - sc.Payload != 8L + mappings * 12L) return false;
        var map = new List<(uint First, uint Count)>();
        for (int i = 0; i < mappings; i++)
        {
            uint first = Field(sc, 8 + i * 12), count = Field(sc, 12 + i * 12), desc = Field(sc, 16 + i * 12);
            if (first == 0 || first > chunks || count == 0 || count > samples || desc == 0 || desc > descriptions.Count ||
                i == 0 && first != 1 || i > 0 && first <= map[^1].First) return false;
            map.Add((first, count));
        }
        uint sample = 0; int mapping = 0;
        for (int chunk = 0; chunk < chunks; chunk++)
        {
            token.ThrowIfCancellationRequested();
            while (mapping + 1 < map.Count && map[mapping + 1].First <= chunk + 1) mapping++;
            long offset = co.Type == "co64" ? checked((long)BinaryPrimitives.ReadUInt64BigEndian(Bytes(co.Payload + 8 + chunk * 8L, 8))) : Field(co, 8 + chunk * 4);
            long size = 0;
            for (int n = 0; n < map[mapping].Count; n++)
            {
                if (sample >= samples) return false;
                uint value = commonSize != 0 ? commonSize : Field(sz, 12 + checked((int)sample) * 4);
                if (value == 0) return false;
                size += value; sample++;
            }
            if (!media.Any(b => offset >= b.Payload && offset <= b.End - size)) return false;
        }
        return sample == samples;
    }
}
