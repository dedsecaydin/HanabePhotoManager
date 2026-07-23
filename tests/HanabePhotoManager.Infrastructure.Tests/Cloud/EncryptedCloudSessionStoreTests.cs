using FluentAssertions;
using HanabePhotoManager.Core.Cloud;
using HanabePhotoManager.Infrastructure.Cloud;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;

namespace HanabePhotoManager.Infrastructure.Tests.Cloud;

public sealed class EncryptedCloudSessionStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hanabe-session-" + Guid.NewGuid().ToString("N"));
    private string PathName => Path.Combine(_root, "sessions.bin");

    [Fact]
    public async Task SaveLoadAndDelete_RoundTripsCurrentUserEncryptedSession()
    {
        var store = new EncryptedCloudSessionStore(PathName);
        var token = new CloudAuthToken(CloudProviderKind.Baidu, "access", "refresh", DateTimeOffset.UtcNow.AddHours(1),
            new Dictionary<string, string> { ["client_id"] = "app" });
        await store.SaveAsync(token);
        File.Exists(PathName).Should().BeTrue();
        var loaded = await store.LoadAsync(CloudProviderKind.Baidu);
        loaded.Should().BeEquivalentTo(token);
        await store.DeleteAsync(CloudProviderKind.Baidu);
        (await store.LoadAsync(CloudProviderKind.Baidu)).Should().BeNull();
    }

    [Fact]
    public async Task CorruptFile_FailsClearly()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(PathName, [1, 2, 3]);
        var act = () => new EncryptedCloudSessionStore(PathName).LoadAsync(CloudProviderKind.Baidu);
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task MultipleProviders_DoNotOverwriteEachOther()
    {
        var store = new EncryptedCloudSessionStore(PathName);
        await store.SaveAsync(new CloudAuthToken(CloudProviderKind.Baidu, "ba", "br", DateTimeOffset.UtcNow.AddHours(1)));
        await store.SaveAsync(new CloudAuthToken(CloudProviderKind.Quark, "qa", "qr", DateTimeOffset.UtcNow.AddHours(1)));
        (await store.LoadAsync(CloudProviderKind.Baidu))!.AccessToken.Should().Be("ba");
        (await store.LoadAsync(CloudProviderKind.Quark))!.AccessToken.Should().Be("qa");
    }

    [Fact]
    public async Task ConcurrentInstances_PreserveBothProviderUpdates()
    {
        var first = new EncryptedCloudSessionStore(PathName);
        var second = new EncryptedCloudSessionStore(PathName);
        await Task.WhenAll(
            first.SaveAsync(new CloudAuthToken(CloudProviderKind.Baidu, "ba", "br", DateTimeOffset.UtcNow.AddHours(1))),
            second.SaveAsync(new CloudAuthToken(CloudProviderKind.Quark, "qa", "qr", DateTimeOffset.UtcNow.AddHours(1))));
        (await first.LoadAsync(CloudProviderKind.Baidu)).Should().NotBeNull();
        (await first.LoadAsync(CloudProviderKind.Quark)).Should().NotBeNull();
    }

    [Fact]
    public async Task CorruptFile_IsQuarantined()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(PathName, [1, 2, 3]);
        var act = () => new EncryptedCloudSessionStore(PathName).LoadAsync(CloudProviderKind.Baidu);
        await act.Should().ThrowAsync<InvalidDataException>();
        Directory.GetFiles(_root, "sessions.bin.corrupt-*").Should().ContainSingle();
        File.Exists(PathName).Should().BeFalse();
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task WindowsAcl_ContainsOnlyCurrentUserAndSystem()
    {
        if (!OperatingSystem.IsWindows()) return;
        var store = new EncryptedCloudSessionStore(PathName);
        await store.SaveAsync(new CloudAuthToken(CloudProviderKind.Baidu, "a", "r", DateTimeOffset.UtcNow.AddHours(1)));
        var security = new FileInfo(PathName).GetAccessControl();
        var sids = security.GetAccessRules(true, false, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>().Select(rule => ((SecurityIdentifier)rule.IdentityReference).Value).ToArray();
        sids.Should().Contain(WindowsIdentity.GetCurrent().User!.Value);
        sids.Should().Contain(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value);
        sids.Should().NotContain(new SecurityIdentifier(WellKnownSidType.WorldSid, null).Value);
        sids.Should().NotContain(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null).Value);
    }

    public void Dispose() { try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { } }
}
