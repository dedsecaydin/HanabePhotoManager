using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace HanabePhotoManager.App.Services;

internal static class ShellThumbnailProvider
{
    public static BitmapSource? TryGetThumbnail(string path, int size = 512)
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

            factory.GetImage(
                new ThumbnailSize { Width = size, Height = size },
                ShellItemImageFactoryFlags.BiggerSizeOk | ShellItemImageFactoryFlags.ThumbnailOnly,
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
        BiggerSizeOk = 0x00000001,
        ThumbnailOnly = 0x00000008
    }
}
