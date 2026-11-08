using System.IO;
using System.Runtime.InteropServices;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.Mac.Services;

internal sealed class MacTrashService(Action<string>? trash = null) : IRecycleBinFileService
{
    private static readonly Lazy<IntPtr> Foundation = new(() => NativeLibrary.Load("/System/Library/Frameworks/Foundation.framework/Foundation"));
    private readonly Action<string> _trash = trash ?? TrashWithFileManager;

    public void MoveToRecycleBin(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) throw new FileNotFoundException("要移入废纸篓的项目不存在。", fullPath);
        _trash(fullPath);
    }

    private static void TrashWithFileManager(string path)
    {
        _ = Foundation.Value;
        var pool = Send(Send(GetClass("NSAutoreleasePool"), Selector("alloc")), Selector("init"));
        var utf8 = Marshal.StringToCoTaskMemUTF8(path);
        try
        {
            var name = SendOne(GetClass("NSString"), Selector("stringWithUTF8String:"), utf8);
            var url = SendOne(GetClass("NSURL"), Selector("fileURLWithPath:"), name);
            var manager = Send(GetClass("NSFileManager"), Selector("defaultManager"));
            if (name == IntPtr.Zero || url == IntPtr.Zero || manager == IntPtr.Zero ||
                Trash(manager, Selector("trashItemAtURL:resultingItemURL:error:"), url, out _, out _) == 0)
                throw new IOException("无法确认项目已移入 macOS 废纸篓，请检查文件状态。");
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8);
            if (pool != IntPtr.Zero) Send(pool, Selector("drain"));
        }
    }

    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    [DllImport(ObjC, EntryPoint = "objc_getClass")] private static extern IntPtr GetClass(string name);
    [DllImport(ObjC, EntryPoint = "sel_registerName")] private static extern IntPtr Selector(string name);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr selector);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr SendOne(IntPtr receiver, IntPtr selector, IntPtr arg);
    [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern byte Trash(IntPtr receiver, IntPtr selector, IntPtr url, out IntPtr resultingUrl, out IntPtr error);
}
