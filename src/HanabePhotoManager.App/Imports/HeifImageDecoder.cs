using System.IO;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace HanabePhotoManager.App.Imports;

/// <summary>解码 HEIF 主图；只读取原件，库随应用部署，不依赖系统 HEVC 扩展。</summary>
internal static class HeifImageDecoder
{
    private static readonly Lazy<IntPtr> Library = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "tools", "libheif", "heif.dll");
        var handle = LoadLibraryEx(path, IntPtr.Zero, 0x00000100 | 0x00001000);
        return handle != IntPtr.Zero ? handle : throw new IOException($"HEIF 解码库无法加载（{Marshal.GetLastWin32Error()}）：{path}");
    });

    internal static Image<Rgb24> Decode(string path, CancellationToken token)
    {
        var context = Function<Allocate>("heif_context_alloc")();
        if (context == IntPtr.Zero) throw new IOException("无法创建 HEIF 解码器。");
        IntPtr handle = IntPtr.Zero, image = IntPtr.Zero;
        try
        {
            token.ThrowIfCancellationRequested();
            Check(Function<ReadFile>("heif_context_read_from_file")(context, path, IntPtr.Zero));
            Check(Function<GetHandle>("heif_context_get_primary_image_handle")(context, out handle));
            Check(Function<DecodeImage>("heif_decode_image")(handle, out image, 1, 10, IntPtr.Zero));
            token.ThrowIfCancellationRequested();
            var width = Function<GetDimension>("heif_image_get_width")(image, 10);
            var height = Function<GetDimension>("heif_image_get_height")(image, 10);
            if (width <= 0 || height <= 0 || (long)width * height > 150_000_000)
                throw new IOException($"HEIF 图像尺寸无效或过大：{width} × {height}。");
            var plane = Function<GetPlane>("heif_image_get_plane_readonly")(image, 10, out var stride);
            var rowBytes = checked(width * 3);
            if (plane == IntPtr.Zero || stride < rowBytes) throw new IOException("HEIF 像素缓冲无效。");
            var pixels = new byte[checked(rowBytes * height)];
            for (var row = 0; row < height; row++)
            {
                token.ThrowIfCancellationRequested();
                Marshal.Copy(IntPtr.Add(plane, checked(row * stride)), pixels, row * rowBytes, rowBytes);
            }
            var result = SixLabors.ImageSharp.Image.LoadPixelData<Rgb24>(pixels, width, height);
            try
            {
                var size = Function<ProfileSize>("heif_image_get_raw_color_profile_size")(image).ToUInt64();
                if (size is > 0 and <= 16_777_216)
                {
                    var profile = new byte[(int)size];
                    Check(Function<ReadProfile>("heif_image_get_raw_color_profile")(image, profile));
                    result.Metadata.IccProfile = new IccProfile(profile);
                }
                return result;
            }
            catch { result.Dispose(); throw; }
        }
        finally
        {
            if (image != IntPtr.Zero) Function<Release>("heif_image_release")(image);
            if (handle != IntPtr.Zero) Function<Release>("heif_image_handle_release")(handle);
            Function<Release>("heif_context_free")(context);
        }
    }

    private static T Function<T>(string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(Library.Value, name));
    private static void Check(Error error)
    {
        if (error.Code != 0) throw new IOException("HEIF 解码失败：" + Marshal.PtrToStringUTF8(error.Message));
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Error { public readonly int Code; public readonly int Subcode; public readonly IntPtr Message; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "LoadLibraryExW")]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Allocate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Release(IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate Error ReadFile(IntPtr context, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, IntPtr options);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate Error GetHandle(IntPtr context, out IntPtr handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate Error DecodeImage(IntPtr handle, out IntPtr image, int colorspace, int chroma, IntPtr options);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetDimension(IntPtr image, int channel);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GetPlane(IntPtr image, int channel, out int stride);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate UIntPtr ProfileSize(IntPtr image);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate Error ReadProfile(IntPtr image, [Out] byte[] data);
}
