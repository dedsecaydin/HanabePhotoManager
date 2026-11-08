using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using HanabePhotoManager.App.Platform;

namespace HanabePhotoManager.Mac.Services;

internal interface IMacKeychainBackend
{
    void Set(string account, string value);
    string? Get(string account);
}

/// <summary>Settings contain only fixed-purpose references; secret bytes stay in Keychain.</summary>
internal sealed class MacKeychainSecretProtector(IMacKeychainBackend backend) : ISecretProtector
{
    private const string Prefix = "keychain:v1:";

    public string Protect(string? value, string? purpose = null)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var account = ValidatePurpose(purpose);
        backend.Set(account, value);
        return Prefix + account;
    }

    public string Unprotect(string? value, string? purpose = null)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var account = ValidatePurpose(purpose);
        return value == Prefix + account ? backend.Get(account) ?? string.Empty : string.Empty;
    }

    private static string ValidatePurpose(string? purpose) => purpose is "api-key" or "server-token"
        ? purpose : throw new ArgumentException("不支持的密钥用途。", nameof(purpose));
}

/// <summary>Uses the system Security framework directly, without exposing passwords in process arguments.</summary>
internal sealed class NativeMacKeychainBackend : IMacKeychainBackend
{
    private const string Security = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const int ItemNotFound = -25300;
    private static readonly byte[] Service = Encoding.UTF8.GetBytes("com.hanabe.photomanager");

    public void Set(string account, string value)
    {
        var name = Encoding.UTF8.GetBytes(account);
        var password = Encoding.UTF8.GetBytes(value);
        IntPtr item = IntPtr.Zero, data = IntPtr.Zero;
        try
        {
            var status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)Service.Length, Service, (uint)name.Length, name, out _, out data, out item);
            if (data != IntPtr.Zero) { SecKeychainItemFreeContent(IntPtr.Zero, data); data = IntPtr.Zero; }
            if (status == ItemNotFound)
                status = SecKeychainAddGenericPassword(IntPtr.Zero, (uint)Service.Length, Service, (uint)name.Length, name, (uint)password.Length, password, out item);
            else if (status == 0)
                status = SecKeychainItemModifyAttributesAndData(item, IntPtr.Zero, (uint)password.Length, password);
            if (status != 0) throw new InvalidOperationException($"Keychain 写入失败（{status}）。");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
            if (data != IntPtr.Zero) SecKeychainItemFreeContent(IntPtr.Zero, data);
            if (item != IntPtr.Zero) CFRelease(item);
        }
    }

    public string? Get(string account)
    {
        var name = Encoding.UTF8.GetBytes(account);
        IntPtr item = IntPtr.Zero, data = IntPtr.Zero;
        try
        {
            var status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)Service.Length, Service, (uint)name.Length, name, out var length, out data, out item);
            if (status == ItemNotFound) return null;
            if (status != 0) throw new InvalidOperationException($"Keychain 读取失败（{status}）。");
            var bytes = new byte[checked((int)length)];
            try { Marshal.Copy(data, bytes, 0, bytes.Length); return Encoding.UTF8.GetString(bytes); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        finally
        {
            if (data != IntPtr.Zero) SecKeychainItemFreeContent(IntPtr.Zero, data);
            if (item != IntPtr.Zero) CFRelease(item);
        }
    }

    [DllImport(Security)] private static extern int SecKeychainFindGenericPassword(IntPtr keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account, out uint passwordLength, out IntPtr passwordData, out IntPtr item);
    [DllImport(Security)] private static extern int SecKeychainAddGenericPassword(IntPtr keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account, uint passwordLength, byte[] password, out IntPtr item);
    [DllImport(Security)] private static extern int SecKeychainItemModifyAttributesAndData(IntPtr item, IntPtr attributes, uint length, byte[] data);
    [DllImport(Security)] private static extern int SecKeychainItemFreeContent(IntPtr attributes, IntPtr data);
    [DllImport(CoreFoundation)] private static extern void CFRelease(IntPtr value);
}
