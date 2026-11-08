namespace HanabePhotoManager.App.Platform;

internal sealed class WpfSecretProtector : ISecretProtector
{
    public string Protect(string? value, string? purpose = null) => Agents.SecretProtector.Protect(value);
    public string Unprotect(string? value, string? purpose = null) => Agents.SecretProtector.Unprotect(value);
}
