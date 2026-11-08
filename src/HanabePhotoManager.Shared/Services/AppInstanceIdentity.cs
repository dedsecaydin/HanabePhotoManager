using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HanabePhotoManager.App.Services;

internal static class AppInstanceIdentity
{
    internal static string MutexName(string? isolatedDataRoot)
    {
        const string normal = "HanabePhotoManager.SingleInstance";
        if (string.IsNullOrWhiteSpace(isolatedDataRoot)) return normal;
        var normalized = Path.GetFullPath(isolatedDataRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
        var defaultRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HanabePhotoManager").ToUpperInvariant();
        if (normalized == defaultRoot) return normal;
        return normal + "." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}
