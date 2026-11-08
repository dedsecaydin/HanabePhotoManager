namespace HanabePhotoManager.App.Platform;

public interface ISecretProtector
{
    string Protect(string? value, string? purpose = null);
    string Unprotect(string? value, string? purpose = null);
}

internal sealed class UnavailableSecretProtector : ISecretProtector
{
    public string Protect(string? value, string? purpose = null) => string.IsNullOrEmpty(value) ? string.Empty : throw new PlatformNotSupportedException("此平台尚未配置安全密钥存储。");
    public string Unprotect(string? value, string? purpose = null) => string.IsNullOrEmpty(value) ? string.Empty : throw new PlatformNotSupportedException("此平台尚未配置安全密钥存储。");
}
