using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace HanabePhotoManager.App.Recovery;

// Directory timestamps are card-local last-write times, not MP4 creation times.
internal static class ExFatTimeIndex
{
    internal sealed record Entry(long Offset, long Length, string Name, DateTime? LastWrite, bool Contiguous);

    internal static IReadOnlyList<Entry> Read(Stream image, long volume, CancellationToken token)
    {
        byte[] ReadAt(long offset, int count)
        {
            token.ThrowIfCancellationRequested();
            if (offset < 0 || offset > image.Length - count) throw new InvalidDataException("exFAT 目录超出镜像范围。");
            var bytes = new byte[count]; image.Position = offset; image.ReadExactly(bytes); return bytes;
        }
        var boot = ReadAt(volume, 512);
        if (boot[108] is < 9 or > 12 || boot[109] > 25 - boot[108]) throw new InvalidDataException("exFAT 簇大小无效。");
        long sector = 1L << boot[108], cluster = sector << boot[109];
        var fatOffset = volume + U32(boot, 80) * sector;
        var heap = volume + U32(boot, 88) * sector;
        var count = U32(boot, 92);
        long Address(uint c) => c >= 2 && c - 2 < count ? checked(heap + (c - 2) * cluster) : throw new InvalidDataException("exFAT 簇编号无效。");
        var queue = new Queue<(uint Cluster, long Length, bool Contiguous)>();
        queue.Enqueue((U32(boot, 96), 0, false));
        var visited = new HashSet<uint>();
        var result = new List<Entry>();
        long directoryBytes = 0;
        while (queue.Count > 0)
        {
            var directory = queue.Dequeue();
            var current = directory.Cluster;
            using var data = new MemoryStream();
            var remaining = directory.Length;
            while (current >= 2 && current < 0xfffffff8)
            {
                token.ThrowIfCancellationRequested();
                if (!visited.Add(current)) throw new InvalidDataException("exFAT 目录链循环或重复。");
                var read = checked((int)(directory.Contiguous ? Math.Min(cluster, remaining) : cluster));
                if (read <= 0) break;
                directoryBytes += read;
                if (directoryBytes > 128 * 1024 * 1024) throw new InvalidDataException("目录索引超过安全读取上限，请改用完整扫描。");
                data.Write(ReadAt(Address(current), read));
                remaining -= read;
                if (directory.Contiguous)
                {
                    if (remaining <= 0) break;
                    current++;
                }
                else current = U32(ReadAt(fatOffset + current * 4L, 4), 0);
            }
            var entries = data.ToArray();
            for (int i = 0; i + 32 <= entries.Length; i += 32)
            {
                token.ThrowIfCancellationRequested();
                if (entries[i] == 0) break;
                // Include deleted file sets when their stream/name records remain.
                if ((entries[i] & 0x7f) != 5) continue;
                int secondary = entries[i + 1];
                if (secondary < 2 || i + (secondary + 1L) * 32 > entries.Length) continue;
                var stream = i + 32;
                if ((entries[stream] & 0x7f) != 0x40) continue;
                var first = U32(entries, stream + 20);
                var lengthUnsigned = BinaryPrimitives.ReadUInt64LittleEndian(entries.AsSpan(stream + 24));
                if (lengthUnsigned > long.MaxValue || first < 2 || first - 2 >= count) continue;
                var length = (long)lengthUnsigned;
                var contiguous = (entries[stream + 1] & 2) != 0;
                var name = new StringBuilder();
                for (int n = 2; n <= secondary; n++)
                {
                    int at = i + n * 32;
                    if ((entries[at] & 0x7f) == 0x41) name.Append(Encoding.Unicode.GetString(entries, at + 2, 30));
                }
                var text = name.ToString();
                if (text.Length < entries[stream + 3]) continue;
                text = text[..entries[stream + 3]];
                bool isDirectory = (entries[i + 4] & 0x10) != 0;
                if (isDirectory && entries[i] == 0x85) queue.Enqueue((first, length, contiguous));
                else if (!isDirectory && (text.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || text.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || text.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || new[] { ".arw", ".cr2", ".cr3", ".nef", ".dng" }.Contains(Path.GetExtension(text), StringComparer.OrdinalIgnoreCase)))
                    result.Add(new(Address(first), length, text, DecodeTimestamp(U32(entries, i + 12), entries[i + 21]), contiguous));
                i += secondary * 32;
            }
        }
        return result;
    }

    internal static DateTime? DecodeTimestamp(uint stamp, byte increment)
    {
        if (stamp == 0 || increment > 199) return null;
        try { return new DateTime(1980 + (int)(stamp >> 25), (int)(stamp >> 21 & 15), (int)(stamp >> 16 & 31), (int)(stamp >> 11 & 31), (int)(stamp >> 5 & 63), (int)(stamp & 31) * 2, DateTimeKind.Unspecified).AddMilliseconds(increment * 10); }
        catch (ArgumentOutOfRangeException) { return null; }
    }
    private static uint U32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
}
