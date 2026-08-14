using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace HanabePhotoManager.App.Services;

internal static class ShellThumbnailProvider
{
    /// <summary>
    /// Requests a thumbnail from the Windows Shell (Explorer's thumbnail engine).
    /// </summary>
    /// <param name="path">Full path of the media file.</param>
    /// <param name="size">Requested thumbnail edge length in pixels.</param>
    /// <param name="allowExtraction">
    /// When <c>true</c>, the Shell is allowed to extract the thumbnail from the
    /// file itself (seeking the first frame for videos) instead of only returning
    /// a thumbnail that already exists in the Shell's thumbnail cache. Required
    /// for video files, whose first frame is never in the cache until extracted.
    /// </param>
    public static BitmapSource? TryGetThumbnail(string path, int size = 512, bool allowExtraction = false)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        IShellItemImageFactory? factory = null;
        nint bitmapHandle = 0;

        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            var result = SHCreateItemFromParsingName(path, 0, ref iid, out factory);
            if (result != 0 || factory is null)
            {
                return null;
            }

            // ThumbnailOnly (0x08) is the fast path: it only returns a thumbnail
            // that already exists in the Shell cache. For videos the first frame
            // is never cached until extracted, so when extraction is allowed we
            // drop the flag and let the Shell generate the thumbnail from the
            // file itself (Explorer-style first-frame preview).
            var flags = ShellItemImageFactoryFlags.BiggerSizeOk |
                        (allowExtraction
                            ? ShellItemImageFactoryFlags.None
                            : ShellItemImageFactoryFlags.ThumbnailOnly);

            factory.GetImage(
                new ThumbnailSize { Width = size, Height = size },
                flags,
                out bitmapHandle);

            if (bitmapHandle == 0)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHBitmap(
                bitmapHandle,
                0,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (bitmapHandle != 0)
            {
                _ = DeleteObject(bitmapHandle);
            }

            if (factory is not null)
            {
                _ = Marshal.ReleaseComObject(factory);
            }
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string path,
        nint bindContext,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? shellItem);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint objectHandle);

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        void GetImage(
            ThumbnailSize size,
            ShellItemImageFactoryFlags flags,
            out nint bitmapHandle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ThumbnailSize
    {
        public int Width;

        public int Height;
    }

    [Flags]
    private enum ShellItemImageFactoryFlags
    {
        None = 0x00000000,
        BiggerSizeOk = 0x00000001,
        ThumbnailOnly = 0x00000008
    }
}
