using HanabePhotoManager.Mac.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class MacKeychainSecretProtectorTests
{
    [Fact]
    public void DifferentPurposesKeepSeparateSecretsAndPersistOnlyReferences()
    {
        var backend = new MemoryKeychain();
        var protector = new MacKeychainSecretProtector(backend);
        var api = protector.Protect("api-secret", "api-key");
        var server = protector.Protect("server-secret", "server-token");
        Assert.DoesNotContain("secret", api);
        Assert.DoesNotContain("secret", server);
        Assert.Equal("api-secret", protector.Unprotect(api, "api-key"));
        Assert.Equal("server-secret", protector.Unprotect(server, "server-token"));
        Assert.Equal(string.Empty, protector.Unprotect(api, "server-token"));
        protector.Protect("updated", "api-key");
        Assert.Equal("updated", protector.Unprotect(api, "api-key"));
        Assert.Equal(2, backend.Values.Count);
    }

    private sealed class MemoryKeychain : IMacKeychainBackend
    {
        internal Dictionary<string, string> Values { get; } = new();
        public void Set(string account, string value) => Values[account] = value;
        public string? Get(string account) => Values.GetValueOrDefault(account);
    }
}
