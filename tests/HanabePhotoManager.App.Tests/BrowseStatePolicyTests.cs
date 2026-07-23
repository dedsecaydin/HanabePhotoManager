using FluentAssertions;
using HanabePhotoManager.App.Services;
using Xunit;

namespace HanabePhotoManager.App.Tests;

public sealed class BrowseStatePolicyTests
{
    private readonly BrowseStatePolicy _policy = new();
    private readonly BrowseSnapshot _persisted = new("2026-07-16", "JPG生图", "flower", 2, 180, "a.jpg");
    private readonly BrowseSnapshot _session = new("2026-07-20", "修后", "city", 4, 220, "b.jpg");

    [Fact]
    public void ResolveOnEntry_CrossLaunch_UsesPersistedState() =>
        _policy.ResolveOnEntry(BrowseEntryMode.CrossLaunchRestore, _persisted, _session).Should().Be(_persisted);

    [Fact]
    public void ResolveOnEntry_SessionOnly_UsesSessionState() =>
        _policy.ResolveOnEntry(BrowseEntryMode.SessionRestore, _persisted, _session).Should().Be(_session);

    [Fact]
    public void ResolveOnEntry_AlwaysAllDates_ClearsOnlyTheDate() =>
        _policy.ResolveOnEntry(BrowseEntryMode.AlwaysAllDates, _persisted, _session).DateKey.Should().BeNull();

    [Fact]
    public void ResolveOnEntry_SessionOnlyWithoutSession_StartsAtAllDates() =>
        _policy.ResolveOnEntry(BrowseEntryMode.SessionRestore, _persisted, null).DateKey.Should().BeNull();

    [Fact]
    public void ResolveOnEntry_WithoutSnapshot_UsesGlobalBrowseDefaults()
    {
        var defaults = new BrowseDefaults("4★", 7, 196);

        var resolved = _policy.ResolveOnEntry(BrowseEntryMode.SessionRestore, null, null, defaults);

        resolved.SortIndex.Should().Be(7);
        resolved.ThumbnailSize.Should().Be(196);
    }
}
