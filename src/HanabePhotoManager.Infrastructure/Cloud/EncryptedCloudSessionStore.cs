using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using HanabePhotoManager.Core.Cloud;

namespace HanabePhotoManager.Infrastructure.Cloud;

/// <summary>Stores cloud tokens encrypted with Windows DPAPI CurrentUser scope.</summary>
public sealed class EncryptedCloudSessionStore : ICloudSessionStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };
    private const int CurrentSchemaVersion = 1;
    private sealed record SessionEnvelope(int SchemaVersion, Dictionary<CloudProviderKind, CloudAuthToken> Sessions);

    public EncryptedCloudSessionStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Session path is required.", nameof(filePath));
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task SaveAsync(CloudAuthToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var processLock = await AcquireProcessLockAsync(cancellationToken).ConfigureAwait(false);
            var sessions = await ReadAsync(cancellationToken).ConfigureAwait(false);
            sessions[token.Provider] = token;
            await WriteAsync(sessions, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<CloudAuthToken?> LoadAsync(CloudProviderKind provider, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(provider)) throw new ArgumentOutOfRangeException(nameof(provider));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var processLock = await AcquireProcessLockAsync(cancellationToken).ConfigureAwait(false);
            var sessions = await ReadAsync(cancellationToken).ConfigureAwait(false);
            return sessions.TryGetValue(provider, out var token) ? token : null;
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteAsync(CloudProviderKind provider, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(provider)) throw new ArgumentOutOfRangeException(nameof(provider));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var processLock = await AcquireProcessLockAsync(cancellationToken).ConfigureAwait(false);
            if (!File.Exists(_filePath)) return;
            var sessions = await ReadAsync(cancellationToken).ConfigureAwait(false);
            if (!sessions.Remove(provider)) return;
            if (sessions.Count == 0) { File.Delete(_filePath); return; }
            await WriteAsync(sessions, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task<Dictionary<CloudProviderKind, CloudAuthToken>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath)) return new();
        byte[] encrypted;
        try { encrypted = await File.ReadAllBytesAsync(_filePath, cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new InvalidDataException("Cloud session cannot be read.", ex); }
        try
        {
            var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var envelope = JsonSerializer.Deserialize<SessionEnvelope>(plain, JsonOptions)
                ?? throw new InvalidDataException("Cloud session is empty.");
            if (envelope.SchemaVersion != CurrentSchemaVersion || envelope.Sessions is null || envelope.Sessions.Count == 0)
                throw new InvalidDataException("Cloud session schema is invalid.");
            foreach (var pair in envelope.Sessions)
            {
                if (!Enum.IsDefined(pair.Key) || pair.Value is null || pair.Value.Provider != pair.Key ||
                    string.IsNullOrWhiteSpace(pair.Value.AccessToken) || string.IsNullOrWhiteSpace(pair.Value.RefreshToken) ||
                    pair.Value.AppMetadata is null)
                    throw new InvalidDataException("Cloud session token fields are invalid.");
            }
            return envelope.Sessions;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            QuarantineCorruptFile();
            throw new InvalidDataException("Cloud session is corrupted or cannot be decrypted.", ex);
        }
    }

    private async Task WriteAsync(Dictionary<CloudProviderKind, CloudAuthToken> sessions, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        SecureDirectory(directory);
        if (sessions.Count == 0) throw new ArgumentException("At least one session is required.", nameof(sessions));
        var plain = JsonSerializer.SerializeToUtf8Bytes(new SessionEnvelope(CurrentSchemaVersion, sessions), JsonOptions);
        var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        var temp = _filePath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(temp, encrypted, cancellationToken).ConfigureAwait(false);
            File.Move(temp, _filePath, true);
            SecureFile(_filePath);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private async Task<FileStream> AcquireProcessLockAsync(CancellationToken cancellationToken)
    {
        var lockPath = _filePath + ".lock";
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1,
                    FileOptions.DeleteOnClose | FileOptions.Asynchronous);
            }
            catch (IOException)
            {
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void QuarantineCorruptFile()
    {
        if (!File.Exists(_filePath)) return;
        var quarantine = _filePath + ".corrupt-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N");
        try { File.Move(_filePath, quarantine, false); }
        catch (IOException)
        {
            try { File.Delete(_filePath); } catch { /* surface original corruption error */ }
        }
    }

    private static void SecureDirectory(string directory)
    {
        if (!OperatingSystem.IsWindows()) return;
        var info = new DirectoryInfo(directory);
        var security = info.GetAccessControl();
        security.SetSecurityDescriptorSddlForm($"D:(A;;FA;;;{WindowsIdentity.GetCurrent().User!.Value})(A;;FA;;;SY)", AccessControlSections.Access);
        info.SetAccessControl(security);
    }

    private static void SecureFile(string filePath)
    {
        if (!OperatingSystem.IsWindows()) return;
        var info = new FileInfo(filePath);
        var security = info.GetAccessControl();
        security.SetSecurityDescriptorSddlForm($"D:(A;;FA;;;{WindowsIdentity.GetCurrent().User!.Value})(A;;FA;;;SY)", AccessControlSections.Access);
        info.SetAccessControl(security);
    }
}
