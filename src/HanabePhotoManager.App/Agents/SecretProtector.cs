using System.Runtime.InteropServices;
using System.Text;

namespace HanabePhotoManager.App.Agents;

/// <summary>
/// 用 Windows DPAPI（当前用户）加密 API Key 和接口令牌后再写进 settings.json：
/// 换一个 Windows 账户或把文件拷到别的电脑上都解不开。
/// </summary>
public static class SecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HanabePhotoManager.Agents.v1");

    public static string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return string.Empty;
        var data = Encoding.UTF8.GetBytes(plain);
        return Convert.ToBase64String(Transform(data, protect: true));
    }

    public static string Unprotect(string? protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64)) return string.Empty;
        try { return Encoding.UTF8.GetString(Transform(Convert.FromBase64String(protectedBase64), protect: false)); }
        catch (FormatException) { return string.Empty; }
        catch (InvalidOperationException) { return string.Empty; }
    }

    private static byte[] Transform(byte[] input, bool protect)
    {
        var inBlob = Allocate(input);
        var entropyBlob = Allocate(Entropy);
        var outBlob = new DataBlob();
        try
        {
            var ok = protect
                ? CryptProtectData(ref inBlob, null, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref outBlob)
                : CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref outBlob);
            if (!ok) throw new InvalidOperationException("DPAPI 加解密失败：" + Marshal.GetLastWin32Error());
            var result = new byte[outBlob.Size];
            Marshal.Copy(outBlob.Data, result, 0, outBlob.Size);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(inBlob.Data);
            Marshal.FreeHGlobal(entropyBlob.Data);
            if (outBlob.Data != IntPtr.Zero) LocalFree(outBlob.Data);
        }
    }

    private static DataBlob Allocate(byte[] bytes)
    {
        var pointer = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        return new DataBlob { Size = bytes.Length, Data = pointer };
    }

    private const int UiForbidden = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DataBlob dataIn, string? description, ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);
}
