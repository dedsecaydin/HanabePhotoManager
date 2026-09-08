using System.IO;
using HanabePhotoManager.Core.Recovery;

namespace HanabePhotoManager.App.Recovery;

internal static class JpegRecoveryReader
{
    // Read marker lengths rather than mistaking an EXIF thumbnail's EOI for the main image end.
    internal static RecoveryCandidate? Read(FileStream stream, long start, CancellationToken token)
    {
        stream.Position = start;
        long limit = Math.Min(stream.Length, start + 128L * 1024 * 1024);
        int Byte() => stream.Position < limit ? stream.ReadByte() : -1;
        if (Byte() != 0xff || Byte() != 0xd8) return null;
        bool frame = false, scan = false, entropy = false;
        while (stream.Position < limit)
        {
            token.ThrowIfCancellationRequested();
            int prefix = Byte();
            if (prefix != 0xff) { if (entropy && prefix >= 0) continue; return null; }
            int marker;
            do { marker = Byte(); } while (marker == 0xff);
            if (marker < 0) return null;
            if (entropy && (marker == 0 || marker is >= 0xd0 and <= 0xd7)) continue;
            if (marker == 0xd9)
                return frame && scan ? new RecoveryCandidate(Guid.NewGuid().ToString("N"), $"photo_{start:X}.jpg", start, stream.Position,
                    false, false, false, false, false, RecoveryConfidence.High, RecoveryCandidateStatus.Direct)
                    { IsJpeg = true, HasCompletePhotoStructure = true } : null;
            if (marker is 0 or 0xd8 || marker is >= 0xd0 and <= 0xd7) return null;
            int hi = Byte(), lo = Byte();
            if (hi < 0 || lo < 0) return null;
            int length = hi * 256 + lo;
            if (length < 2 || stream.Position > limit - (length - 2)) return null;
            if (marker is 0xc0 or 0xc1 or 0xc2)
            {
                if (length < 8) return null;
                var header = new byte[length - 2]; stream.ReadExactly(header);
                if ((header[1] | header[2]) == 0 || (header[3] | header[4]) == 0 || header[5] == 0 || length != 8 + 3 * header[5]) return null;
                frame = true;
            }
            else stream.Position += length - 2;
            if (marker == 0xda) { if (!frame || length < 6) return null; scan = true; entropy = true; }
            else entropy = false;
        }
        return null;
    }
}
