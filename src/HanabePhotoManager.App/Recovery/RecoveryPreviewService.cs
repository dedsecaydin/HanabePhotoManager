using System.IO;
using System.Windows.Media.Imaging;
using HanabePhotoManager.Core.Recovery;

namespace HanabePhotoManager.App.Recovery;

internal sealed record RecoveryPreview(BitmapSource? Image, string Description);

/// <summary>Decodes a bounded, read-only view; no temporary copy of the card is created.</summary>
internal static class RecoveryPreviewService
{
    public static Task<RecoveryPreview> LoadAsync(string imagePath, RecoveryCandidate candidate, CancellationToken token) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        if (!candidate.CanRecoverDirectly) return new RecoveryPreview(null, "结构或文件边界未确认，无法安全预览。");
        if (!candidate.IsJpeg && !candidate.IsRawPhoto) return new RecoveryPreview(null, "视频结构已识别。导出后可用下方按钮播放，检查画面、声音与时长。");
        try
        {
            using var file = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var range = new CandidateStream(file, candidate.StartOffset, candidate.Length, token);
            var decoder = BitmapDecoder.Create(range, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            int width = frame.PixelWidth, height = frame.PixelHeight;
            if (width <= 0 || height <= 0 || (long)width * height > 80_000_000)
                return new RecoveryPreview(null, "图像尺寸超出预览上限，请导出后使用照片软件检查。");
            // CopyPixels forces actual decoding rather than accepting a header or cached embedded thumbnail.
            int stride = checked((width * frame.Format.BitsPerPixel + 7) / 8);
            if ((long)stride * height > 320_000_000) return new RecoveryPreview(null, "图像解码内存超出预览上限，请导出检查。");
            token.ThrowIfCancellationRequested();
            var pixels = new byte[checked(stride * height)];
            frame.CopyPixels(pixels, stride, 0);
            token.ThrowIfCancellationRequested();
            var decoded = BitmapSource.Create(width, height, 96, 96, frame.Format, frame.Palette, pixels, stride);
            BitmapSource preview = decoded;
            if (Math.Max(width, height) > 1024)
                preview = new TransformedBitmap(decoded, new System.Windows.Media.ScaleTransform(1024d / Math.Max(width, height), 1024d / Math.Max(width, height)));
            // Detach from decoder, stream and large full-resolution buffer.
            var thumbnail = new WriteableBitmap(preview);
            thumbnail.Freeze();
            return new RecoveryPreview(thumbnail, candidate.IsRawPhoto
                ? $"系统解码器提供的预览：{width:N0} × {height:N0}。可能来自内嵌图像，不代表 RAW 原始像素完整。"
                : $"像素解码通过：{width:N0} × {height:N0}。请查看画面；解码成功不能排除局部画面损坏。");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or NotSupportedException or System.Runtime.InteropServices.COMException or ArgumentException or InvalidOperationException or OverflowException)
        {
            token.ThrowIfCancellationRequested();
            return new RecoveryPreview(null, candidate.IsRawPhoto ? "系统没有可用的 RAW 预览，或内容无法解码。结构检查与像素检查分别进行，请导出后用相机软件验证。" : "结构已识别，但像素预览失败：文件可能损坏或当前解码器不支持。");
        }
    }, token);

    internal sealed class CandidateStream(Stream source, long start, long length, CancellationToken token) : Stream
    {
        private long _position;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => _position; set => Seek(value, SeekOrigin.Begin); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            token.ThrowIfCancellationRequested();
            if (start < 0 || length <= 0 || start > source.Length || length > source.Length - start) throw new InvalidDataException("候选范围超出镜像。");
            source.Position = start + _position;
            int read = source.Read(buffer, offset, (int)Math.Min(count, length - _position));
            _position += read;
            return read;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            long next = checked((origin == SeekOrigin.Begin ? 0 : origin == SeekOrigin.Current ? _position : length) + offset);
            if (next < 0 || next > length) throw new IOException("预览不能读取候选范围以外的数据。");
            return _position = next;
        }
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
