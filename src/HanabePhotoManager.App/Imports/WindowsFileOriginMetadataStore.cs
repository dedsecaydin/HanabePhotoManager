using System.Runtime.InteropServices;
using System.IO;

namespace HanabePhotoManager.App.Imports;

internal sealed class WindowsFileOriginMetadataStore : IFileOriginMetadataStore
{
    private static readonly PropertyKey CommentKey = new(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 6);
    private static readonly Guid PropertyStoreInterface = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

    public Task<FileOriginMetadata?> ReadAsync(string path, CancellationToken cancellationToken) =>
        RunStaAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return TryReadComment(path, out var comment, out _)
                && HanabeOriginCommentCodec.TryParse(comment, out var metadata)
                    ? metadata
                    : null;
        }, cancellationToken);

    public Task<FileOriginMetadataWriteResult> WriteAndVerifyAsync(string path, FileOriginMetadata metadata, CancellationToken cancellationToken) =>
        RunStaAsync(() => WriteAndVerify(path, metadata, cancellationToken), cancellationToken);

    private static Task<T> RunStaAsync<T>(Func<T> action, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.TrySetResult(action()); }
            catch (OperationCanceledException exception) { completion.TrySetCanceled(exception.CancellationToken); }
            catch (Exception exception) { completion.TrySetException(exception); }
        })
        {
            IsBackground = true,
            Name = "Hanabe Windows Property Store"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        return completion.Task;
    }

    private static FileOriginMetadataWriteResult WriteAndVerify(string path, FileOriginMetadata metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(path))
        {
            return FileOriginMetadataWriteResult.Failure("目标文件不存在");
        }

        if (!TryReadComment(path, out var existing, out var readError))
        {
            return FileOriginMetadataWriteResult.Failure(readError ?? "无法读取 Windows 备注");
        }

        IPropertyStore? store = null;
        var value = default(PropVariant);
        try
        {
            var iid = PropertyStoreInterface;
            var hr = SHGetPropertyStoreFromParsingName(path, IntPtr.Zero, GetPropertyStoreFlags.ReadWrite, ref iid, out store);
            if (hr < 0 || store is null)
            {
                return FileOriginMetadataWriteResult.Failure($"此格式不支持写入 Windows 备注（0x{hr:X8}）");
            }

            value = PropVariant.FromString(HanabeOriginCommentCodec.Merge(existing, metadata));
            var key = CommentKey;
            hr = store.SetValue(ref key, ref value);
            if (hr >= 0) hr = store.Commit();
            if (hr < 0)
            {
                return FileOriginMetadataWriteResult.Failure($"写入 Windows 备注失败（0x{hr:X8}）");
            }
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or IOException)
        {
            return FileOriginMetadataWriteResult.Failure(ex.Message);
        }
        finally
        {
            value.Dispose();
            if (store is not null) Marshal.FinalReleaseComObject(store);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return TryReadComment(path, out var verifiedComment, out var verifyError)
            && HanabeOriginCommentCodec.TryParse(verifiedComment, out var verified)
            && verified == metadata
                ? FileOriginMetadataWriteResult.Completed
                : FileOriginMetadataWriteResult.Failure(verifyError ?? "备注写入后回读校验失败");
    }

    private static bool TryReadComment(string path, out string? comment, out string? error)
    {
        comment = null;
        error = null;
        IPropertyStore? store = null;
        var value = default(PropVariant);
        try
        {
            var iid = PropertyStoreInterface;
            var hr = SHGetPropertyStoreFromParsingName(path, IntPtr.Zero, GetPropertyStoreFlags.Default, ref iid, out store);
            if (hr < 0 || store is null)
            {
                error = $"无法打开 Windows 属性（0x{hr:X8}）";
                return false;
            }

            var key = CommentKey;
            hr = store.GetValue(ref key, out value);
            if (hr < 0)
            {
                error = $"无法读取 Windows 备注（0x{hr:X8}）";
                return false;
            }

            comment = value.GetString();
            return true;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or IOException)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            value.Dispose();
            if (store is not null) Marshal.FinalReleaseComObject(store);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHGetPropertyStoreFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        IntPtr bindContext,
        GetPropertyStoreFlags flags,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore propertyStore);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    [Flags]
    private enum GetPropertyStoreFlags : uint { Default = 0, ReadWrite = 2 }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private readonly struct PropertyKey(Guid formatId, uint propertyId)
    {
        public readonly Guid FormatId = formatId;
        public readonly uint PropertyId = propertyId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant : IDisposable
    {
        [FieldOffset(0)] private ushort _variantType;
        [FieldOffset(8)] private IntPtr _pointer;

        internal static PropVariant FromString(string value) => new()
        {
            _variantType = 31,
            _pointer = Marshal.StringToCoTaskMemUni(value)
        };

        internal readonly string? GetString() => _variantType == 31 && _pointer != IntPtr.Zero
            ? Marshal.PtrToStringUni(_pointer)
            : null;

        public void Dispose()
        {
            if (_variantType != 0)
            {
                _ = PropVariantClear(ref this);
                _variantType = 0;
                _pointer = IntPtr.Zero;
            }
        }
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint propertyCount);
        [PreserveSig] int GetAt(uint propertyIndex, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }
}
