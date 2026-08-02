# Authenticated Remote Photo Sharing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an authenticated remote-sharing service that streams explicitly authorized photos from the always-on Hanabe computer or an on-demand Hibana computer through Cloudflare Tunnel, with reusable owner-created accounts, bounded concurrent sessions, and automatic Wake-on-LAN.

**Architecture:** Add one `HanabePhotoManager.ShareHost` executable that runs as either Gateway or Agent. Core owns transport-neutral sharing and wake policies, Infrastructure owns SQLite, password hashing, path containment, DPAPI secrets, and Wake-on-LAN, the Gateway exposes separate public and loopback-admin listeners, and the WPF App controls it through the local admin listener. The Hibana Agent is LAN-only and accepts mutually authenticated Gateway requests.

**Tech Stack:** .NET 8.0.422, C# 12, ASP.NET Core minimal APIs, Windows Services, SQLite, PBKDF2-HMAC-SHA256, DPAPI, HTTP cookies, mutual TLS, ImageSharp, WPF, CommunityToolkit.Mvvm, xUnit, FluentAssertions, Cloudflare Tunnel.

## Global Constraints

- Keep dependency direction `App/ShareHost -> Core + Infrastructure`, `Infrastructure -> Core`; Core references no WPF, ASP.NET, SQLite, Windows Service, HTTP, or Cloudflare type.
- The Gateway public listener binds only to `127.0.0.1:18443`; the admin listener binds only to `127.0.0.1:18444`; the Agent listener defaults to LAN HTTPS port `18445`.
- Cloudflare publishes only `http://127.0.0.1:18443`; it never publishes the admin or Agent listener.
- Do not add F7000C port forwarding, UPnP, WAN administration, router plug-ins, or DDNS dependencies.
- Do not upload or intentionally cache protected photo bytes in cloud storage, Cloudflare Cache, or a VPS.
- Default account device limit is `1`; session heartbeat is `60` seconds; lease expiry is `5` minutes without renewal.
- Wake policy sends `3` packets `1` second apart, waits `120` seconds, and applies a `10` minute device cooldown.
- Public APIs accept stable random media IDs only; no public request accepts an absolute, relative, UNC, or device path.
- Passwords, cookies, authorization headers, Tunnel tokens, private keys, full local paths, and DPAPI plaintext never enter logs or source control.
- Reuse existing design-system resources; do not add page-local colors, control templates, or shared style duplicates.
- Preserve all unrelated dirty-worktree changes and stage only files named by the active task.

---

## File Structure

### New production areas

- `src/HanabePhotoManager.Core/Sharing/`: immutable models, store/capability contracts, authorization, session, and wake policies.
- `src/HanabePhotoManager.Infrastructure/Sharing/`: SQLite schema/stores, PBKDF2 password hashing, DPAPI secret storage, canonical media resolution, WOL sender.
- `src/HanabePhotoManager.ShareHost/`: dual-role Windows service, public/admin/agent endpoints, orchestration, visitor static client.
- `src/HanabePhotoManager.App/Sharing/`: local admin client, focused management ViewModel, WPF page.
- `tests/HanabePhotoManager.ShareHost.Tests/`: in-memory/TestServer host tests, including endpoint and orchestration integration.

### Persistent data

- `%LOCALAPPDATA%/HanabePhotoManager/Sharing/sharing.db`: accounts, shares, grants, devices, roots, media allowlist, sessions, and schema version.
- `%LOCALAPPDATA%/HanabePhotoManager/Sharing/admin-secret.bin`: DPAPI-protected local admin secret.
- `%PROGRAMDATA%/HanabePhotoManager/ShareHost/appsettings.json`: role, listener, database path, device identity, certificate thumbprints; never contains plaintext passwords or private keys.

---

### Task 1: Add the dual-role ShareHost projects and composition boundary

**Files:**
- Create: `src/HanabePhotoManager.ShareHost/HanabePhotoManager.ShareHost.csproj`
- Create: `src/HanabePhotoManager.ShareHost/Program.cs`
- Create: `src/HanabePhotoManager.ShareHost/Hosting/ShareHostRole.cs`
- Create: `src/HanabePhotoManager.ShareHost/Hosting/ShareHostOptions.cs`
- Create: `tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj`
- Create: `tests/HanabePhotoManager.ShareHost.Tests/Hosting/ShareHostCompositionTests.cs`
- Modify: `HanabePhotoManager.sln`

**Interfaces:**
- Produces: `ShareHostRole { Gateway, Agent }`, `ShareHostOptions`, and `ShareHostApplication.Build(string[] args)` used by all later host tasks.

- [ ] **Step 1: Write the failing composition test**

```csharp
[Theory]
[InlineData("Gateway", ShareHostRole.Gateway)]
[InlineData("Agent", ShareHostRole.Agent)]
public void Build_ReadsExplicitRole(string value, ShareHostRole expected)
{
    using var app = ShareHostApplication.Build(["--ShareHost:Role", value, "--environment", "Testing"]);
    app.Services.GetRequiredService<IOptions<ShareHostOptions>>().Value.Role.Should().Be(expected);
}
```

- [ ] **Step 2: Run the test and verify the project/type failure**

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter FullyQualifiedName~ShareHostCompositionTests`

Expected: FAIL because the new project and `ShareHostApplication` do not exist.

- [ ] **Step 3: Create the projects and minimal host composition**

Use `Microsoft.NET.Sdk.Web`, target `net8.0-windows`, reference Core and Infrastructure, add `Microsoft.Extensions.Hosting.WindowsServices` `8.0.1`, and expose:

```csharp
public enum ShareHostRole { Gateway, Agent }

public sealed class ShareHostOptions
{
    public const string SectionName = "ShareHost";
    public ShareHostRole Role { get; set; }
    public string PublicAddress { get; set; } = "127.0.0.1";
    public string AdminAddress { get; set; } = "127.0.0.1";
    public string AgentAddress { get; set; } = "0.0.0.0";
    public int PublicPort { get; set; } = 18443;
    public int AdminPort { get; set; } = 18444;
    public int AgentPort { get; set; } = 18445;
}

public static class ShareHostApplication
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Host.UseWindowsService(options => options.ServiceName = "Hanabe Photo Share Host");
        builder.Services.AddOptions<ShareHostOptions>()
            .Bind(builder.Configuration.GetSection(ShareHostOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        var app = builder.Build();
        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
        return app;
    }
}
```

Add both projects to the solution under the matching `src` and `tests` solution folders.

- [ ] **Step 4: Run the focused test and Release build**

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter FullyQualifiedName~ShareHostCompositionTests`

Expected: PASS.

Run: `dotnet build HanabePhotoManager.sln -c Release /warnaserror`

Expected: build succeeds with zero warnings.

- [ ] **Step 5: Commit the project boundary**

```powershell
git add HanabePhotoManager.sln src/HanabePhotoManager.ShareHost tests/HanabePhotoManager.ShareHost.Tests
git commit -m "feat: add remote sharing host boundary"
```

### Task 2: Implement sharing domain models and deterministic policies

**Files:**
- Create: `src/HanabePhotoManager.Core/Sharing/ShareModels.cs`
- Create: `src/HanabePhotoManager.Core/Sharing/IShareStores.cs`
- Create: `src/HanabePhotoManager.Core/Sharing/ShareAccessPolicy.cs`
- Create: `src/HanabePhotoManager.Core/Sharing/ShareSessionPolicy.cs`
- Create: `src/HanabePhotoManager.Core/Sharing/ShareWakePolicy.cs`
- Create: `tests/HanabePhotoManager.Core.Tests/Sharing/ShareAccessPolicyTests.cs`
- Create: `tests/HanabePhotoManager.Core.Tests/Sharing/ShareSessionPolicyTests.cs`
- Create: `tests/HanabePhotoManager.Core.Tests/Sharing/ShareWakePolicyTests.cs`

**Interfaces:**
- Produces: `VisitorAccount`, `PhotoShare`, `ShareGrant`, `ShareMediaItem`, `ShareDevice`, `ShareRoot`, `ShareSession`, `ShareAccessPolicy.Evaluate`, `ShareSessionPolicy.Evaluate`, and `ShareWakePolicy.Evaluate`.
- Produces: `IShareCatalogStore`, `IShareSessionStore`, `IShareDeviceStore`, `IPasswordHasher`, `IShareMediaResolver`, and `IWakePacketSender`.

- [ ] **Step 1: Write failing policy tests for every approved boundary**

```csharp
[Fact]
public void Evaluate_DeniesRevokedGrantBeforeDisclosingMedia() =>
    ShareAccessPolicy.Evaluate(Context(grantEnabled: false), Now)
        .Should().Be(ShareAccessDecision.GrantDenied);

[Fact]
public void Evaluate_DeniesWhenShareCapacityIsFull() =>
    ShareSessionPolicy.Evaluate(new(2, 2, 0, 1, Now, Now.AddMinutes(5)))
        .Should().Be(ShareSessionDecision.ShareCapacityFull);

[Fact]
public void Evaluate_OfflineAgentAndUnavailableRootRequestsWake() =>
    ShareWakePolicy.Evaluate(new(false, false, null, Now), Now)
        .Should().Be(ShareWakeDecision.SendWakePackets);

[Fact]
public void Evaluate_OnlineAgentAndUnavailableRootReportsStorageFailure() =>
    ShareWakePolicy.Evaluate(new(true, false, null, Now), Now)
        .Should().Be(ShareWakeDecision.RootUnavailable);
```

- [ ] **Step 2: Run Core sharing tests and verify missing-type failures**

Run: `dotnet test tests/HanabePhotoManager.Core.Tests/HanabePhotoManager.Core.Tests.csproj -c Release --filter FullyQualifiedName~Sharing`

Expected: FAIL because `HanabePhotoManager.Core.Sharing` types do not exist.

- [ ] **Step 3: Implement validated immutable records, contracts, and pure policies**

Use `Guid` IDs, `DateTimeOffset` timestamps, positive limit validation, and these decisions:

```csharp
public enum ShareAccessDecision { Allowed, AccountDisabled, AccountExpired, ShareDisabled, ShareExpired, GrantDenied, DownloadDenied, MediaDenied }
public enum ShareSessionDecision { Allowed, ShareCapacityFull, AccountDeviceCapacityFull }
public enum ShareWakeDecision { None, SendWakePackets, WaitForAgent, Cooldown, RootUnavailable, TimedOut }

public static class SharePolicyDefaults
{
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan WakeTimeout = TimeSpan.FromSeconds(120);
    public static readonly TimeSpan WakeCooldown = TimeSpan.FromMinutes(10);
    public const int WakePacketCount = 3;
    public static readonly TimeSpan WakePacketInterval = TimeSpan.FromSeconds(1);
}
```

Keep store methods cancellation-aware and use explicit results for expected capacity/auth failures.

- [ ] **Step 4: Run the Core test project**

Run: `dotnet test tests/HanabePhotoManager.Core.Tests/HanabePhotoManager.Core.Tests.csproj -c Release`

Expected: all Core tests pass.

- [ ] **Step 5: Commit the domain contract**

```powershell
git add src/HanabePhotoManager.Core/Sharing tests/HanabePhotoManager.Core.Tests/Sharing
git commit -m "feat: add remote sharing domain policies"
```

### Task 3: Add secure SQLite persistence and password hashing

**Files:**
- Create: `src/HanabePhotoManager.Infrastructure/Sharing/ShareDatabase.cs`
- Create: `src/HanabePhotoManager.Infrastructure/Sharing/SqliteShareCatalogStore.cs`
- Create: `src/HanabePhotoManager.Infrastructure/Sharing/SqliteShareSessionStore.cs`
- Create: `src/HanabePhotoManager.Infrastructure/Sharing/SqliteShareDeviceStore.cs`
- Create: `src/HanabePhotoManager.Infrastructure/Sharing/Pbkdf2PasswordHasher.cs`
- Create: `tests/HanabePhotoManager.Infrastructure.Tests/Sharing/Pbkdf2PasswordHasherTests.cs`
- Create: `tests/HanabePhotoManager.Infrastructure.Tests/Sharing/SqliteShareStoreTests.cs`

**Interfaces:**
- Consumes: Core store and password contracts from Task 2.
- Produces: SQLite implementations with transactional `TryAcquireLeaseAsync` and versioned password envelopes.

- [ ] **Step 1: Write failing round-trip, race, revocation, and hash tests**

```csharp
[Fact]
public void Verify_WrongPasswordFailsWithoutExposingHashDetails()
{
    var hasher = new Pbkdf2PasswordHasher(iterations: 600_000);
    var encoded = hasher.Hash("correct horse battery staple");
    hasher.Verify("wrong", encoded).Should().BeFalse();
}

[Fact]
public async Task TryAcquireLease_ConcurrentRequestsNeverExceedShareLimit()
{
    var store = SessionStore();
    var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(index =>
        store.TryAcquireLeaseAsync(AccountId, ShareId, $"device-{index}", Now, 10, 2,
            SharePolicyDefaults.LeaseDuration)));
    results.Count(result => result.IsAcquired).Should().Be(2);
}
```

- [ ] **Step 2: Run Infrastructure sharing tests and verify failure**

Run: `dotnet test tests/HanabePhotoManager.Infrastructure.Tests/HanabePhotoManager.Infrastructure.Tests.csproj -c Release --filter FullyQualifiedName~Sharing`

Expected: FAIL because the stores and hasher do not exist.

- [ ] **Step 3: Implement schema version 1 and atomic stores**

Create tables `visitor_accounts`, `photo_shares`, `share_grants`, `share_devices`, `share_roots`, `share_media`, and `share_sessions`, with foreign keys enabled, WAL mode, UTC timestamps, unique login names, and indexes on active session/account/share columns. `TryAcquireLeaseAsync` must use one immediate transaction: delete expired leases, count share/account leases, insert only when both limits allow, then commit.

Encode passwords as `v1.pbkdf2-sha256.<iterations>.<base64-salt>.<base64-hash>`, using a random 32-byte salt, a 32-byte derived key, `Rfc2898DeriveBytes.Pbkdf2`, and `CryptographicOperations.FixedTimeEquals`.

- [ ] **Step 4: Run focused and full Infrastructure tests**

Run: `dotnet test tests/HanabePhotoManager.Infrastructure.Tests/HanabePhotoManager.Infrastructure.Tests.csproj -c Release --filter FullyQualifiedName~Sharing`

Expected: PASS, including exactly two acquired leases in the race test.

Run: `dotnet test tests/HanabePhotoManager.Infrastructure.Tests/HanabePhotoManager.Infrastructure.Tests.csproj -c Release`

Expected: all Infrastructure tests pass.

- [ ] **Step 5: Commit persistence**

```powershell
git add src/HanabePhotoManager.Infrastructure/Sharing tests/HanabePhotoManager.Infrastructure.Tests/Sharing
git commit -m "feat: persist sharing accounts and sessions"
```

### Task 4: Protect the local admin channel and expose account/share administration

**Files:**
- Create: `src/HanabePhotoManager.Infrastructure/Sharing/DpapiShareSecretStore.cs`
- Create: `src/HanabePhotoManager.ShareHost/Endpoints/AdminEndpointMappings.cs`
- Create: `src/HanabePhotoManager.ShareHost/Security/AdminSecretAuthenticationHandler.cs`
- Create: `src/HanabePhotoManager.Core/Sharing/ShareAdminModels.cs`
- Create: `tests/HanabePhotoManager.Infrastructure.Tests/Sharing/DpapiShareSecretStoreTests.cs`
- Create: `tests/HanabePhotoManager.ShareHost.Tests/Endpoints/AdminEndpointTests.cs`

**Interfaces:**
- Produces: `ShareAdminSnapshot`, `CreatedVisitorAccount`, account/share/device create and update request records, and loopback endpoints under `/admin/v1` protected by `X-Hanabe-Admin-Key`.

- [ ] **Step 1: Write failing endpoint tests**

```csharp
[Fact]
public async Task CreateAccount_WithoutAdminSecretReturnsUnauthorized() =>
    (await _client.PostAsJsonAsync("/admin/v1/accounts", new { loginName = "family" }))
        .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

[Fact]
public async Task CreateAccount_ReturnsPasswordOnceAndPersistsOnlyHash()
{
    using var request = AdminPost("/admin/v1/accounts", new { loginName = "family", deviceLimit = 1 });
    var response = await _client.SendAsync(request);
    var created = await response.Content.ReadFromJsonAsync<CreatedVisitorAccount>();
    created!.InitialPassword.Should().NotBeNullOrWhiteSpace();
    (await _catalog.FindAccountByLoginAsync("family")).PasswordHash
        .Should().NotContain(created.InitialPassword);
}
```

- [ ] **Step 2: Run the endpoint tests and verify missing endpoint failures**

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter FullyQualifiedName~AdminEndpointTests`

Expected: FAIL because `/admin/v1` is not mapped.

- [ ] **Step 3: Implement two Kestrel listeners and admin endpoints**

Configure Gateway Kestrel endpoints by local port and reject admin requests whose `LocalPort != 18444`. Protect the admin port with a 32-byte DPAPI-backed secret and fixed-time comparison. Add account create/reset/enable, share create/update/disable, grant replace, device/root upsert, selected-media registration, active-session list/evict, and wake-command endpoints. `POST /admin/v1/shares/{shareId}/media` accepts full paths only on this authenticated loopback listener, resolves them against registered roots, stores random media IDs plus root-relative paths, and rejects every path outside a registered root. Return initial passwords only from create/reset responses.

- [ ] **Step 4: Run host and Infrastructure security tests**

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter "FullyQualifiedName~AdminEndpointTests|FullyQualifiedName~ShareHostCompositionTests"`

Expected: PASS; requests on the public listener cannot reach `/admin/v1`.

- [ ] **Step 5: Commit the admin boundary**

```powershell
git add src/HanabePhotoManager.Core/Sharing/ShareAdminModels.cs src/HanabePhotoManager.Infrastructure/Sharing/DpapiShareSecretStore.cs src/HanabePhotoManager.ShareHost tests/HanabePhotoManager.Infrastructure.Tests/Sharing tests/HanabePhotoManager.ShareHost.Tests/Endpoints
git commit -m "feat: add protected sharing administration API"
```

### Task 5: Implement visitor login, reusable grants, cookies, and lease capacity

**Files:**
- Create: `src/HanabePhotoManager.ShareHost/Endpoints/VisitorAuthEndpointMappings.cs`
- Create: `src/HanabePhotoManager.ShareHost/Services/VisitorAuthenticationService.cs`
- Create: `src/HanabePhotoManager.ShareHost/Security/VisitorCookieEvents.cs`
- Create: `src/HanabePhotoManager.ShareHost/Security/LoginRateLimiter.cs`
- Create: `tests/HanabePhotoManager.ShareHost.Tests/Endpoints/VisitorAuthenticationTests.cs`
- Create: `tests/HanabePhotoManager.ShareHost.Tests/Endpoints/VisitorCapacityTests.cs`
- Create: `tests/HanabePhotoManager.ShareHost.Tests/Security/SecurityLogRedactionTests.cs`

**Interfaces:**
- Produces: `LoginRequest`, `ShareSummary`, `POST /api/v1/login`, `POST /api/v1/logout`, `POST /api/v1/sessions/heartbeat`, and `GET /api/v1/shares`.

- [ ] **Step 1: Write failing authentication and capacity tests**

```csharp
[Fact]
public async Task Login_ValidReusableAccountReturnsOnlyGrantedShares()
{
    await SeedAccountWithGrants(ShareA, ShareB);
    await LoginAsync("family", Password);
    (await _client.GetFromJsonAsync<ShareSummary[]>("/api/v1/shares"))!
        .Select(item => item.Id).Should().BeEquivalentTo(ShareA, ShareB);
}

[Fact]
public async Task OpenShare_WhenCapacityIsFullReturnsConflictWithoutCreatingLease()
{
    await FillShareCapacity(ShareA);
    (await _client.PostAsync($"/api/v1/shares/{ShareA}/open", null))
        .StatusCode.Should().Be(HttpStatusCode.Conflict);
    (await _sessions.ListActiveAsync(ShareA)).Should().HaveCount(ShareLimit);
}

[Fact]
public async Task FailedLogin_DoesNotLogPasswordCookieOrAuthorizationMaterial()
{
    await PostLoginAsync("family", "secret-never-log");
    _logs.Joined.Should().NotContainAny("secret-never-log", "Cookie", "Authorization");
}
```

- [ ] **Step 2: Run tests and verify 404/unauthorized failures**

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter "FullyQualifiedName~VisitorAuthenticationTests|FullyQualifiedName~VisitorCapacityTests"`

Expected: FAIL because visitor authentication endpoints are absent.

- [ ] **Step 3: Implement cookie authentication and session leases**

Use a non-identifying generic login error, `Secure`, `HttpOnly`, `SameSite=Strict` cookies, credential revision validation on every cookie refresh, account/source-key rate limiting, and atomic share-open lease acquisition. On the loopback-only public listener, derive the visitor source key from Cloudflare's validated `CF-Connecting-IP` value and fall back to the socket address. Reject state-changing authenticated requests whose `Origin` is not the configured public origin and do not enable CORS. Heartbeat renews only the caller's lease. Logout, owner eviction, disabled account/share, revoked grant, and credential revision mismatch invalidate the lease and cookie. Apply structured logging filters so credentials and full paths are never serialized.

- [ ] **Step 4: Run visitor authentication tests**

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter "FullyQualifiedName~VisitorAuthenticationTests|FullyQualifiedName~VisitorCapacityTests"`

Expected: PASS, including concurrent open requests never exceeding configured limits and captured logs containing no submitted credential or authorization material.

- [ ] **Step 5: Commit visitor authentication**

```powershell
git add src/HanabePhotoManager.ShareHost/Endpoints/VisitorAuthEndpointMappings.cs src/HanabePhotoManager.ShareHost/Services/VisitorAuthenticationService.cs src/HanabePhotoManager.ShareHost/Security tests/HanabePhotoManager.ShareHost.Tests/Endpoints
git commit -m "feat: authenticate remote photo visitors"
```

### Task 6: Enforce media allowlists and stream Hanabe-local photos

**Files:**
- Create: `src/HanabePhotoManager.Infrastructure/Sharing/ShareMediaResolver.cs`
- Create: `src/HanabePhotoManager.ShareHost/Services/ShareMediaService.cs`
- Create: `src/HanabePhotoManager.ShareHost/Endpoints/VisitorMediaEndpointMappings.cs`
- Create: `tests/HanabePhotoManager.Infrastructure.Tests/Sharing/ShareMediaResolverTests.cs`
- Create: `tests/HanabePhotoManager.ShareHost.Tests/Endpoints/VisitorMediaTests.cs`
- Modify: `src/HanabePhotoManager.ShareHost/HanabePhotoManager.ShareHost.csproj`

**Interfaces:**
- Produces: `RegisterSelectedMediaRequest`, `ShareMediaResolutionStatus`, `ShareMediaResolutionResult`, `POST /admin/v1/shares/{shareId}/media`, `GET /api/v1/shares/{shareId}/media`, `/media/{mediaId}/preview`, and `/media/{mediaId}/content`.

- [ ] **Step 1: Write failing containment and HTTP tests**

```csharp
[Theory]
[InlineData("..\\outside.jpg")]
[InlineData("sub\\..\\..\\outside.jpg")]
public async Task ResolveAsync_PathEscapeIsRejected(string storedRelativePath) =>
    (await _resolver.ResolveAsync(RootId, storedRelativePath)).Status
        .Should().Be(ShareMediaResolutionStatus.OutsideRoot);

[Fact]
public async Task Content_UnlistedMediaIdReturnsNotFoundWithoutPathDisclosure()
{
    var response = await _client.GetAsync($"/api/v1/shares/{ShareA}/media/{Guid.NewGuid()}/content");
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    (await response.Content.ReadAsStringAsync()).Should().NotContain(TemporaryRoot);
}
```

- [ ] **Step 2: Run resolver and media tests and verify failure**

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter FullyQualifiedName~VisitorMediaTests`

Expected: FAIL because media endpoints do not exist.

- [ ] **Step 3: Implement random media registration, canonical resolution, previews, and ranges**

Generate a random `Guid` media ID when the owner registers a selected path. Store only its root ID and root-relative path. On every read, combine with the registered canonical root, normalize, require containment using an ending directory separator, reject reparse points along the path, and open read-only with `FileShare.ReadWrite | FileShare.Delete`.

Add ImageSharp `3.1.11` to ShareHost. Produce bounded JPEG previews for supported raster formats; return a typed `preview_unavailable` response for unsupported formats. Use ASP.NET `Results.File` with range processing for original content, cancellation propagation, `Cache-Control: private, no-store`, `X-Content-Type-Options: nosniff`, and attachment disposition only when download permission is enabled.

- [ ] **Step 4: Run Infrastructure and host media tests**

Run: `dotnet test tests/HanabePhotoManager.Infrastructure.Tests/HanabePhotoManager.Infrastructure.Tests.csproj -c Release --filter FullyQualifiedName~ShareMediaResolverTests`

Expected: PASS for traversal, sibling-prefix, reparse-point, missing-file, and valid-file cases.

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter FullyQualifiedName~VisitorMediaTests`

Expected: PASS for allowlist, authorization, no-store, range, cancellation, and download-policy cases.

- [ ] **Step 5: Commit local media sharing**

```powershell
git add src/HanabePhotoManager.Infrastructure/Sharing/ShareMediaResolver.cs src/HanabePhotoManager.ShareHost tests/HanabePhotoManager.Infrastructure.Tests/Sharing/ShareMediaResolverTests.cs tests/HanabePhotoManager.ShareHost.Tests/Endpoints/VisitorMediaTests.cs
git commit -m "feat: stream allowlisted local photos"
```

### Task 7: Add device health and Wake-on-LAN transmission

**Files:**
- Create: `src/HanabePhotoManager.Infrastructure/Sharing/WakeOnLanSender.cs`
- Create: `src/HanabePhotoManager.ShareHost/Services/DeviceWakeCoordinator.cs`
- Create: `tests/HanabePhotoManager.Infrastructure.Tests/Sharing/WakeOnLanSenderTests.cs`
- Create: `tests/HanabePhotoManager.ShareHost.Tests/Services/DeviceWakeCoordinatorTests.cs`

**Interfaces:**
- Produces: `DeviceWakeCoordinator.EnsureRootAvailableAsync(Guid rootId, CancellationToken)` returning `RootAvailabilityResult` with `Available`, `Waking`, `TimedOut`, or `RootUnavailable`.

- [ ] **Step 1: Write failing packet and state-machine tests**

```csharp
[Fact]
public void BuildMagicPacket_RepeatsMacSixteenTimesAfterHeader()
{
    var packet = WakeOnLanSender.BuildMagicPacket(PhysicalAddress.Parse("001122334455"));
    packet.Take(6).Should().OnlyContain(value => value == 0xff);
    packet.Length.Should().Be(102);
}

[Fact]
public async Task EnsureRootAvailable_OfflineAgentSendsThreePacketsAndWaitsForHeartbeat()
{
    _agent.IsOnline = false;
    var pending = _coordinator.EnsureRootAvailableAsync(RootId, CancellationToken.None);
    await _clock.AdvanceAsync(TimeSpan.FromSeconds(3));
    _wake.Sent.Should().HaveCount(3);
    _agent.SetOnline(rootAvailable: true);
    (await pending).Status.Should().Be(RootAvailabilityStatus.Available);
}
```

- [ ] **Step 2: Run WOL tests and verify failure**

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter FullyQualifiedName~DeviceWakeCoordinatorTests`

Expected: FAIL because the coordinator is absent.

- [ ] **Step 3: Implement UDP broadcast and deterministic coordination**

Send to the registered IPv4 broadcast address and port `9`; do not forward through the WAN. Inject `TimeProvider` and `IWakePacketSender`. Coalesce concurrent wake requests per device, send exactly three packets one second apart, wait at most 120 seconds for authenticated Agent health, persist last attempt/success, and enforce ten-minute cooldown. If Agent is online and root is unavailable, return `RootUnavailable` without sending a packet.

- [ ] **Step 4: Run WOL tests**

Run: `dotnet test tests/HanabePhotoManager.Infrastructure.Tests/HanabePhotoManager.Infrastructure.Tests.csproj -c Release --filter FullyQualifiedName~WakeOnLanSenderTests`

Expected: PASS.

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter FullyQualifiedName~DeviceWakeCoordinatorTests`

Expected: PASS without real network or wall-clock delays.

- [ ] **Step 5: Commit WOL support**

```powershell
git add src/HanabePhotoManager.Infrastructure/Sharing/WakeOnLanSender.cs src/HanabePhotoManager.ShareHost/Services/DeviceWakeCoordinator.cs tests/HanabePhotoManager.Infrastructure.Tests/Sharing/WakeOnLanSenderTests.cs tests/HanabePhotoManager.ShareHost.Tests/Services/DeviceWakeCoordinatorTests.cs
git commit -m "feat: wake offline photo devices"
```

### Task 8: Implement the mutually authenticated Hibana Agent

**Files:**
- Create: `src/HanabePhotoManager.ShareHost/Endpoints/AgentEndpointMappings.cs`
- Create: `src/HanabePhotoManager.ShareHost/Services/ShareAgentClient.cs`
- Create: `src/HanabePhotoManager.ShareHost/Security/AgentCertificateValidator.cs`
- Create: `src/HanabePhotoManager.ShareHost/Services/RemoteMediaSource.cs`
- Create: `tests/HanabePhotoManager.ShareHost.Tests/Agent/AgentMutualTlsTests.cs`
- Create: `tests/HanabePhotoManager.ShareHost.Tests/Agent/RemoteMediaStreamingTests.cs`

**Interfaces:**
- Produces: LAN-only `GET /agent/v1/health`, `PUT /agent/v1/catalog`, `POST /agent/v1/roots/{rootId}/probe`, and `GET /agent/v1/media/{mediaId}`; produces `IShareAgentClient` consumed by Task 9.

- [ ] **Step 1: Write failing certificate and streaming tests**

```csharp
[Fact]
public async Task Agent_RejectsClientWithoutRegisteredCertificate() =>
    (await _untrustedClient.GetAsync("/agent/v1/health"))
        .StatusCode.Should().Be(HttpStatusCode.Forbidden);

[Fact]
public async Task Agent_StreamUsesLocalAllowlistAndNeverAcceptsAPath()
{
    await _trustedClient.PutAsJsonAsync("/agent/v1/catalog", CatalogWith(MediaId, RootId, "2026\\08\\photo.jpg"));
    var response = await _trustedClient.GetAsync($"/agent/v1/media/{MediaId}");
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    (await response.Content.ReadAsByteArrayAsync()).Should().Equal(PhotoBytes);
    _routes.Should().NotContain(route => route.Contains("{path", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 2: Run Agent tests and verify failure**

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter FullyQualifiedName~Agent`

Expected: FAIL because Agent endpoints and certificate validation are absent.

- [ ] **Step 3: Implement Agent-role HTTPS and Gateway client certificates**

When role is Agent, bind only the configured LAN HTTPS endpoint, require a client certificate, and compare its SHA-256 fingerprint against the registered Gateway certificate. The Gateway client presents its protected certificate and pins the registered Agent certificate fingerprint. The authenticated catalog-sync endpoint replaces mappings transactionally for that Agent and stores root/media IDs plus root-relative paths locally. Media read requests contain only root/media IDs. Agent resolution repeats the same canonical-root containment checks before opening a file.

- [ ] **Step 4: Run Agent tests**

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter FullyQualifiedName~Agent`

Expected: PASS for trusted/untrusted certificates, missing media, range streaming, cancellation, and no path routes.

- [ ] **Step 5: Commit the Agent**

```powershell
git add src/HanabePhotoManager.ShareHost/Endpoints/AgentEndpointMappings.cs src/HanabePhotoManager.ShareHost/Services/ShareAgentClient.cs src/HanabePhotoManager.ShareHost/Security/AgentCertificateValidator.cs src/HanabePhotoManager.ShareHost/Services/RemoteMediaSource.cs tests/HanabePhotoManager.ShareHost.Tests/Agent
git commit -m "feat: stream photos from trusted share agents"
```

### Task 9: Connect visitor requests to wake and remote streaming states

**Files:**
- Modify: `src/HanabePhotoManager.ShareHost/Services/ShareMediaService.cs`
- Create: `src/HanabePhotoManager.ShareHost/Endpoints/DeviceStatusEndpointMappings.cs`
- Create: `tests/HanabePhotoManager.ShareHost.Tests/Endpoints/RemoteVisitorMediaTests.cs`

**Interfaces:**
- Consumes: `DeviceWakeCoordinator` and `IShareAgentClient` from Tasks 7-8.
- Produces: `DeviceWaitResponse`, `GET /api/v1/shares/{shareId}/devices/{deviceId}/status`, and `202 Accepted` wake responses with a poll URL.

- [ ] **Step 1: Write failing end-to-end orchestration tests**

```csharp
[Fact]
public async Task RemoteMedia_OfflineDeviceReturnsWakingThenStreamsAfterAgentAppears()
{
    _agent.IsOnline = false;
    var first = await GetRemoteMediaAsync();
    first.StatusCode.Should().Be(HttpStatusCode.Accepted);
    (await first.Content.ReadFromJsonAsync<DeviceWaitResponse>())!.State.Should().Be("waking");
    _agent.SetOnline(rootAvailable: true, media: PhotoBytes);
    (await GetRemoteMediaAsync()).StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
public async Task OnlineAgentWithMissingRootNeverSendsWakePacket()
{
    _agent.SetOnline(rootAvailable: false);
    (await GetRemoteMediaAsync()).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    _wake.Sent.Should().BeEmpty();
}
```

- [ ] **Step 2: Run orchestration tests and verify failure**

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter FullyQualifiedName~RemoteVisitorMediaTests`

Expected: FAIL because remote media is not routed through wake coordination.

- [ ] **Step 3: Implement the approved state flow**

Resolve the owning device before opening media. Return `202` with only public device state while WOL is active. After authenticated health succeeds, synchronize pending root/media mappings through `PUT /agent/v1/catalog`, probe the root, and then resume through the Agent. Return generic `503` after timeout or root failure, and retain detailed state only for the admin endpoint. Coalesce browser polling so it does not trigger additional wake bursts.

- [ ] **Step 4: Run remote visitor tests**

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter FullyQualifiedName~RemoteVisitorMediaTests`

Expected: PASS for online, waking, timeout, cooldown, missing-root, and cancellation cases.

- [ ] **Step 5: Commit remote orchestration**

```powershell
git add src/HanabePhotoManager.ShareHost/Services/ShareMediaService.cs src/HanabePhotoManager.ShareHost/Endpoints/DeviceStatusEndpointMappings.cs tests/HanabePhotoManager.ShareHost.Tests/Endpoints/RemoteVisitorMediaTests.cs
git commit -m "feat: resume photo access after device wake"
```

### Task 10: Build the visitor web experience

**Files:**
- Create: `src/HanabePhotoManager.ShareHost/wwwroot/index.html`
- Create: `src/HanabePhotoManager.ShareHost/wwwroot/app.js`
- Create: `src/HanabePhotoManager.ShareHost/wwwroot/styles.css`
- Create: `tests/HanabePhotoManager.ShareHost.Tests/Web/VisitorWebClientTests.cs`

**Interfaces:**
- Consumes: visitor APIs from Tasks 5-9.
- Produces: login, authorized share list, photo grid/viewer, optional download, capacity, waking, unavailable, and expired-session states.

- [ ] **Step 1: Write failing static-client contract tests**

```csharp
[Fact]
public void Client_ContainsEveryApprovedVisitorState()
{
    var script = File.ReadAllText(SourcePath("src", "HanabePhotoManager.ShareHost", "wwwroot", "app.js"));
    script.Should().ContainAll("login", "capacity-full", "waking", "unavailable", "session-expired");
    script.Should().NotContain("localPath");
}
```

- [ ] **Step 2: Run web-client tests and verify missing-file failure**

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter FullyQualifiedName~VisitorWebClientTests`

Expected: FAIL because `wwwroot` client files do not exist.

- [ ] **Step 3: Implement the accessible no-framework client**

Use semantic HTML, keyboard-operable dialogs, visible focus, responsive CSS, system fonts, neutral low-saturation surfaces, and no third-party CDN assets. Fetch only same-origin `/api/v1` endpoints with `credentials: "same-origin"`. Poll wake status with cancellation and bounded backoff. Start the 60-second lease heartbeat only for an open share and stop it on logout/unload. Render server-provided display names only through `textContent`.

- [ ] **Step 4: Run web and host tests**

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release`

Expected: all ShareHost tests pass.

- [ ] **Step 5: Commit the visitor client**

```powershell
git add src/HanabePhotoManager.ShareHost/wwwroot tests/HanabePhotoManager.ShareHost.Tests/Web
git commit -m "feat: add remote sharing visitor experience"
```

### Task 11: Add the WPF sharing management surface

**Files:**
- Create: `src/HanabePhotoManager.App/Sharing/ShareAdminClient.cs`
- Create: `src/HanabePhotoManager.App/Sharing/SharingViewModel.cs`
- Create: `src/HanabePhotoManager.App/Sharing/SharingPage.xaml`
- Create: `src/HanabePhotoManager.App/Sharing/SharingPage.xaml.cs`
- Create: `tests/HanabePhotoManager.App.Tests/Sharing/SharingViewModelTests.cs`
- Create: `tests/HanabePhotoManager.App.Tests/Sharing/SharingPageTests.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs` at `DefaultNavigationOrder`, command construction, page predicates, titles, and `CreateNavigationItem`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml` at namespace declarations and page-host grid
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml.cs` in `AnimateVisiblePage`
- Modify: `src/HanabePhotoManager.App/Themes/Tokens/Icons.xaml`
- Modify: `tests/HanabePhotoManager.App.Tests/NavigationOrderPolicyTests.cs`

**Interfaces:**
- Consumes: loopback `/admin/v1` API and Core admin records.
- Produces: `SharingViewModel`, navigation destination `Sharing`, and owner workflows approved in the design.

- [ ] **Step 1: Write failing ViewModel and structure tests**

```csharp
[Fact]
public async Task CreateAccount_ShowsInitialPasswordOnlyFromCreateResponse()
{
    var client = new FakeShareAdminClient { CreatedPassword = "one-time-password" };
    var viewModel = new SharingViewModel(client);
    await viewModel.CreateAccountCommand.ExecuteAsync(null);
    viewModel.RevealedInitialPassword.Should().Be("one-time-password");
    viewModel.Accounts.Should().ContainSingle(item => item.LoginName == client.CreatedLogin);
}

[Fact]
public void MainWindow_ContainsSharingPageUsingSharedResources()
{
    var xaml = File.ReadAllText(SourcePath("src", "HanabePhotoManager.App", "Sharing", "SharingPage.xaml"));
    xaml.Should().Contain("Layout.PageSurface");
    xaml.Should().Contain("Button.Primary");
    xaml.Should().NotContain("#[0-9A-Fa-f]{6}");
}
```

- [ ] **Step 2: Run App sharing tests and verify missing-type failures**

Run: `dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter FullyQualifiedName~Sharing`

Expected: FAIL because the Sharing feature does not exist.

- [ ] **Step 3: Implement focused administration UI and minimal shell wiring**

`ShareAdminClient` reads the DPAPI admin secret through the existing user-local app-data boundary and calls only `127.0.0.1:18444`. `SharingViewModel` accepts `Func<IReadOnlyList<string>> selectedPhotoPaths`, owns refresh, account create/reset/disable, share create/update, reusable grant assignment, selected-photo registration, device/root binding, active-session eviction, test wake, and wake-now commands. `MainWindowViewModel` supplies its existing selected-preview-path projection instead of absorbing sharing policy. `SharingPage` composes existing inputs, buttons, cards, lists, dialogs, status, typography, spacing, and responsive patterns; add only a semantic `Icon.Share` geometry to the existing icon dictionary.

Add `Sharing` after `Preview` in default navigation. Expose `ShowSharingCommand`, `IsSharingPage`, `Sharing` property, page title/subtitle, XAML host, and animation mapping. Do not move sharing policy into `MainWindowViewModel`.

- [ ] **Step 4: Run focused App tests and Release build**

Run: `dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter "FullyQualifiedName~Sharing|FullyQualifiedName~NavigationOrderPolicyTests|FullyQualifiedName~DesignSystemResourceTests"`

Expected: PASS.

Run: `dotnet build HanabePhotoManager.sln -c Release /warnaserror`

Expected: build succeeds with zero warnings.

- [ ] **Step 5: Commit the WPF management surface**

```powershell
git add src/HanabePhotoManager.App/Sharing src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs src/HanabePhotoManager.App/MainWindow.xaml src/HanabePhotoManager.App/MainWindow.xaml.cs src/HanabePhotoManager.App/Themes/Tokens/Icons.xaml tests/HanabePhotoManager.App.Tests/Sharing tests/HanabePhotoManager.App.Tests/NavigationOrderPolicyTests.cs
git commit -m "feat: manage remote photo sharing"
```

### Task 12: Package Windows services, configure Cloudflare safely, document, and verify

**Files:**
- Create: `tools/Install-ShareHost.ps1`
- Create: `tools/Uninstall-ShareHost.ps1`
- Create: `docs/sharing.md`
- Modify: `docs/architecture.md`
- Modify: `.ai/architecture-map.md`
- Modify: `docs/component-inventory.md`
- Modify: `docs/testing.md`
- Modify: `docs/release.md`
- Create: `tests/HanabePhotoManager.ShareHost.Tests/Hosting/DeploymentConfigurationTests.cs`

**Interfaces:**
- Produces: repeatable Gateway/Agent service installation and an operator-owned Cloudflare route; no credential-bearing file is committed.

- [ ] **Step 1: Write failing deployment safety tests**

```csharp
[Fact]
public void GatewayProductionConfig_BindsPublicAndAdminToLoopback()
{
    var options = LoadProductionGatewayOptions();
    IPAddress.Parse(options.PublicAddress).Should().Be(IPAddress.Loopback);
    IPAddress.Parse(options.AdminAddress).Should().Be(IPAddress.Loopback);
}

[Fact]
public void DeploymentInputsContainNoTunnelTokenOrPrivateKey()
{
    DeploymentInputText().Should().NotMatchRegex(
        "(?i)(\"TunnelToken\"\\s*:\\s*\"[^\"]+\"|-----BEGIN PRIVATE KEY-----)");
}
```

- [ ] **Step 2: Run deployment tests and verify missing configuration failure**

Run: `dotnet test tests/HanabePhotoManager.ShareHost.Tests/HanabePhotoManager.ShareHost.Tests.csproj -c Release --filter FullyQualifiedName~DeploymentConfigurationTests`

Expected: FAIL until production configuration and scripts exist.

- [ ] **Step 3: Implement service scripts and operator documentation**

`Install-ShareHost.ps1` accepts `-Role Gateway|Agent`, an explicit publish directory, and non-secret device/listener values; it validates resolved paths remain under the supplied publish directory before registering the Windows service. It never accepts or writes a Cloudflare token. `Uninstall-ShareHost.ps1` stops and removes only the exact Hanabe share service name after confirming it exists.

Document these Cloudflare dashboard actions without embedding credentials:

1. Create a named Tunnel for Hanabe Share Gateway.
2. Install `cloudflared` on the always-on Hanabe computer as its own Windows service using the dashboard-provided one-time command.
3. Add public hostname `share.hanabe.cn` with service `http://127.0.0.1:18443`.
4. Disable caching for `/api/*` and protected media responses; retain application `no-store` headers.
5. Do not map `18444` or `18445` and do not change the existing VPN subdomain.

Update architecture ownership/data flow, architecture map, component inventory, testing matrix, and release/publish contents for the two-role host.

- [ ] **Step 4: Run complete automated verification**

Run: `dotnet restore HanabePhotoManager.sln`

Expected: restore succeeds.

Run: `dotnet build HanabePhotoManager.sln -c Release /warnaserror`

Expected: build succeeds with zero warnings.

Run: `dotnet test HanabePhotoManager.sln -c Release --no-build`

Expected: every test project passes.

Run: `dotnet publish src/HanabePhotoManager.ShareHost/HanabePhotoManager.ShareHost.csproj -c Release -r win-x64 --self-contained false`

Expected: publish includes host executable, visitor assets, configuration template, SQLite native runtime, and no secret material.

- [ ] **Step 5: Perform manual Windows and Cloudflare acceptance**

On fresh publish output, install Gateway on Hanabe and Agent on Hibana. Verify service restart and machine reboot recovery; Light/Dark WPF states; account create/reset/disable; reusable grants; concurrent-browser limits and lease release; Hanabe-local range streaming; Hibana automatic WOL, waiting page, authenticated Agent connection, and resumed streaming; online-Agent/missing-root no-wake behavior; owner eviction; Tunnel restart; and that F7000C has no new port mapping or UPnP entry.

Record automated and manual evidence separately. Do not claim Cloudflare, WOL, or service recovery verified unless the real environment was reachable.

- [ ] **Step 6: Commit deployment and authoritative documentation**

```powershell
git add tools/Install-ShareHost.ps1 tools/Uninstall-ShareHost.ps1 docs/sharing.md docs/architecture.md .ai/architecture-map.md docs/component-inventory.md docs/testing.md docs/release.md tests/HanabePhotoManager.ShareHost.Tests/Hosting/DeploymentConfigurationTests.cs
git commit -m "docs: add remote sharing deployment workflow"
```

---

## Completion Gate

- All 12 task commits exist and contain only task-scoped files.
- Release build and every test project pass without warnings.
- Public/admin/Agent listener separation is covered by automated tests.
- Password, authorization, allowlist, lease race, path containment, WOL, mTLS, range, cancellation, and log-redaction tests pass.
- Real Gateway and Agent services recover after reboot.
- Real Cloudflare access works at `share.hanabe.cn` without router port mapping.
- Hibana wake resumes an authenticated visitor request, and storage failure does not create a wake loop.
- The owner can create reusable accounts, grant multiple shares, set both capacity limits, evict sessions, and revoke access immediately.
- Documentation distinguishes automated evidence from manual verification and does not contain secrets.
