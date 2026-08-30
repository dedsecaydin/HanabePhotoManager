using System.Buffers.Binary;

namespace HanabePhotoManager.App.Services;

internal static class PcmWavVolumeScaler
{
    internal static byte[] Scale(ReadOnlySpan<byte> wav, double volume)
    {
        var result = wav.ToArray();
        var factor = Math.Clamp(volume, 0, 1);
        var dataOffset = FindDataOffset(result);
        if (dataOffset < 0) return result;
        for (var offset = dataOffset; offset + 1 < result.Length; offset += 2)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(result.AsSpan(offset, 2));
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(offset, 2), (short)Math.Clamp(Math.Round(sample * factor), short.MinValue, short.MaxValue));
        }
        return result;
    }

    private static int FindDataOffset(ReadOnlySpan<byte> wav)
    {
        for (var offset = 12; offset + 8 <= wav.Length;)
        {
            var size = BinaryPrimitives.ReadInt32LittleEndian(wav.Slice(offset + 4, 4));
            if (wav.Slice(offset, 4).SequenceEqual("data"u8)) return offset + 8;
            if (size < 0) return -1;
            offset += 8 + size + (size & 1);
        }
        return -1;
    }
}
