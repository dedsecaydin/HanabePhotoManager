# Remote Photo Sharing Design

**Date:** 2026-08-02
**Status:** Approved design
**Scope:** Authenticated, owner-managed remote access to explicitly shared photos stored on the Hanabe and Hibana Windows computers

## 1. Summary

Hanabe Photo Manager will add authenticated remote photo sharing without uploading the photo library to cloud storage. A permanently online Hanabe computer will run a local Share Gateway and connect to Cloudflare through an outbound Tunnel. Visitors will sign in with owner-created reusable accounts and may access only the shares explicitly granted to those accounts.

The Hanabe computer will also act as the Wake-on-LAN coordinator for Hibana. When a requested root library belongs to an offline Hibana device, the gateway will wake it, wait for its Share Agent to become ready, verify that the root is usable, and then proxy only the authorized photo data. The ZXHN F7000C remains an ordinary LAN gateway; the design requires no inbound port mapping, UPnP, router plug-in, or router firmware modification.

## 2. Goals

- Let the owner create remote shares from selected photos.
- Keep original photos on the computers that own their root libraries.
- Require a visitor account before any share content is disclosed.
- Let the owner reuse one visitor account across multiple shares.
- Let the owner grant and revoke account-to-share access.
- Let the owner set share expiration, download permission, total concurrent sessions, and per-account device limits.
- Automatically wake an offline device when its bound root library is requested.
- Expose the service through a Cloudflare Tunnel without opening the home router to inbound traffic.
- Preserve the existing Core, Infrastructure, and App dependency direction.

## 3. Non-goals

- Uploading or synchronizing the photo library to Cloudflare, a VPS, or an object-storage provider.
- Anonymous possession-of-link access.
- Remote desktop access to either Windows computer.
- General-purpose filesystem browsing or arbitrary path downloads.
- Installing the WPF application on Linux or on a Raspberry Pi.
- Depending on ZXHN F7000C custom firmware, remote administration, UPnP, or WAN-side Wake-on-LAN.
- Guaranteeing availability while the permanently online Hanabe gateway itself is powered off or disconnected.

## 4. Deployment Topology

```text
Visitor browser
    -> HTTPS share.hanabe.cn
    -> Cloudflare edge
    -> outbound Cloudflare Tunnel
    -> Hanabe Share Gateway on 127.0.0.1
         -> local authorization/session database
         -> Hanabe local root libraries
         -> LAN Wake-on-LAN for Hibana
         -> mutually authenticated LAN connection to Hibana Share Agent
              -> Hibana local root libraries
```

The Cloudflare DNS zone currently exists, but no Tunnel is configured for this feature. Implementation will create a dedicated Tunnel route from `share.hanabe.cn` to a loopback-only Share Gateway endpoint. Existing DNS records and the existing VPN subdomain are outside this feature and must remain unchanged.

The WPF process does not need to stay open. The Share Gateway and Hibana Share Agent run as Windows background services and start automatically with Windows.

## 5. Architectural Ownership

### Core

Core owns provider-neutral and transport-neutral policies and models:

- visitor account identity and lifecycle;
- share identity, expiration, and download policy;
- account-to-share grants;
- concurrent-session and lease policy;
- root-library-to-device binding;
- device availability and wake state;
- Wake-on-LAN retry, timeout, and cooldown policy;
- stable media identifiers used by an allowlist.

Core must not reference WPF, ASP.NET, Cloudflare types, Windows services, HTTP cookies, SQLite, or Wake-on-LAN socket implementations.

### Infrastructure

Infrastructure owns external-system implementations:

- durable account, share, grant, device, and session stores;
- password hashing and verification;
- atomic session-capacity acquisition and lease release;
- stable media identifier to canonical path resolution;
- Wake-on-LAN packet transmission;
- mutually authenticated gateway-to-agent transport;
- Windows service hosting support and protected service credentials.

Persistent formats and schema migrations require Infrastructure tests. Secrets must use Windows-protected storage where applicable and must never enter source control or logs.

### App

App owns presentation and desktop orchestration:

- share creation and editing;
- visitor account creation, reset, disable, and deletion;
- grant assignment;
- active-session display and forced eviction;
- device/root binding and Wake-on-LAN testing;
- health and error presentation.

The workflow receives focused ViewModels and services. It must not expand `MainWindowViewModel` with sharing business policy.

### Share Hosts

The Share Gateway hosts the public web workflow behind the Tunnel. The Hibana Share Agent hosts no public endpoint. Share-host projects may compose Core contracts and Infrastructure adapters, but public HTTP details stay out of Core.

## 6. Domain Model

### Visitor account

A visitor account contains a stable ID, owner-chosen or generated login name, password-hash metadata, enabled state, optional expiration, per-account device-session limit, creation time, and credential revision. The default device-session limit is one. Password reset increments the credential revision and invalidates prior sessions.

The generated initial password is shown once. Plaintext passwords are never persisted. Login responses do not reveal whether an account exists.

### Share

A share contains a stable ID, display title, enabled state, optional expiration, download permission, total concurrent-session limit, and an allowlist of stable media IDs. It does not contain visitor-supplied paths.

### Access grant

An access grant connects one reusable visitor account to one share. Removing the grant immediately blocks new requests and invalidates that account's sessions for the share.

### Session

A session is a server-side lease associated with an account, a share, a browser/device identifier, credential revision, creation time, last activity, and expiration. Limits represent active browser/device sessions, not verified real-world people.

Capacity is enforced at two levels:

- total active sessions for the share;
- active device sessions for the account.

Capacity acquisition must be atomic. The initial defaults use a 60-second page heartbeat and release a lease after five minutes without renewal. Logout, owner eviction, revocation, expiration, or missed heartbeats release it. These timing values are named configuration owned by the session policy.

### Device and root binding

Each shareable root library is bound to a device record containing device identity, agent identity, MAC address, expected LAN address, broadcast address, health state, and wake configuration. A media item resolves through its registered root binding; the public API never accepts a raw root or filesystem path.

## 7. Authentication and Authorization

Visitors open `https://share.hanabe.cn`, sign in, and receive a short-lived secure session cookie. Cookies use `Secure`, `HttpOnly`, and an appropriate `SameSite` policy. Authentication, authorization, share state, grant state, and capacity are checked before metadata, thumbnails, originals, or downloads are returned.

Authorization is deny-by-default:

1. the account is enabled and unexpired;
2. the password is valid;
3. the share is enabled and unexpired;
4. an active account-to-share grant exists;
5. session capacity is available;
6. the requested media ID belongs to the share allowlist;
7. the requested operation is permitted by the share policy.

Login attempts are rate-limited by account key and source address. Repeated failures cause a temporary lockout. Owner reset or disable operations invalidate existing sessions.

## 8. Visitor Data Flow

### Hanabe-local photo

1. The visitor signs in and selects an authorized share.
2. The gateway atomically acquires a session lease.
3. The requested media ID is resolved through the share allowlist and registered root binding.
4. The gateway opens the canonical Hanabe-local file read-only.
5. The gateway returns metadata, thumbnail, preview, or permitted original data.

### Hibana photo while Hibana is online

1. The gateway validates the visitor, share, grant, capacity, media ID, and operation.
2. The gateway connects to the known Hibana Agent over a mutually authenticated LAN channel.
3. Hibana independently verifies the signed gateway request and its local media mapping.
4. Hibana streams the authorized content to the gateway, which streams it to the visitor.

### Hibana photo while Hibana is offline

1. The gateway determines that the bound root is unreachable and the bound Agent is offline.
2. The gateway enters the wake state and sends Wake-on-LAN packets according to policy.
3. The visitor sees a device-waking status page and polls a share-scoped status endpoint.
4. The gateway waits for the authenticated Agent heartbeat.
5. After the Agent is online, the gateway verifies that the bound root is usable.
6. If usable, the browser resumes the original request; otherwise the wake attempt ends with a generic unavailable result.

## 9. Wake-on-LAN Policy

Wake-on-LAN is automatic only when both conditions are true:

- the registered root is not reachable; and
- the owning Agent is offline.

The initial policy sends three magic packets one second apart, waits up to 120 seconds for an authenticated heartbeat, and applies a ten-minute cooldown before another automatic wake attempt for the same device. These values are named policy configuration rather than scattered constants.

If the Agent is online but the root remains unusable, the system classifies the problem as storage, permission, or configuration failure and does not repeat Wake-on-LAN. The owner sees the diagnostic category; the visitor receives only a generic unavailable message.

The management UI includes test Wake-on-LAN, wake now, last attempt, last success, current state, and actionable diagnostics. Wake-on-LAN cannot repair an unavailable local disk on the permanently online Hanabe computer.

## 10. File and Transport Security

- The Share Gateway public origin listens only on a loopback address and is reached through the outbound Tunnel.
- No F7000C port forwarding, WAN administration, DDNS dependency, or UPnP mapping is required.
- The Hibana Agent accepts only mutually authenticated requests from the registered Hanabe gateway.
- Canonical path resolution occurs only after resolving a server-owned stable media ID through a registered root.
- Resolved paths must remain inside the bound canonical root after normalization and link/reparse-point checks.
- Directory listing, arbitrary path input, and general filesystem APIs are not exposed.
- Responses use private/no-store cache directives as appropriate; Cloudflare must not publicly cache protected photos.
- Logs exclude plaintext passwords, cookies, bearer material, protected service credentials, and complete local paths.
- Photo streams are not persisted by the Tunnel or by an application-level VPS cache.
- Range requests and cancellation are supported so interrupted downloads stop local work promptly.

Cloudflare is the public ingress and TLS intermediary for the initial design. Application-level end-to-end encryption that prevents the ingress provider from processing plaintext is outside this scope and would require a separate design.

## 11. Failure Behavior

| Condition | Visitor behavior | Owner behavior |
|---|---|---|
| Invalid login | Generic authentication failure | Rate-limit/lockout event without plaintext credentials |
| Account, grant, or share expired/revoked | Access denied and session invalidated | Exact disabled or expired state |
| Share or account capacity full | Current-capacity message; no lease created | Active sessions and configured limits |
| Hibana waking | Automatic waiting page | Wake progress and last packet time |
| Wake timeout | Generic temporarily unavailable message | Timeout category and device health details |
| Agent online, root unavailable | Generic unavailable message | Storage, permission, or configuration category |
| Media moved or deleted | Item unavailable; no path disclosure | Missing media ID and root-relative diagnostic |
| Tunnel unavailable | Public service unavailable | Local desktop management remains usable |
| Visitor disconnects | Stream cancellation and eventual lease release | Session transitions to expired after lease timeout |

No failure path falls back to arbitrary filesystem access or silently widens a grant.

## 12. User Experience

The desktop experience adds focused sharing and visitor-account management surfaces using existing Hanabe design-system resources and shared controls. The workflow supports:

- selecting photos and creating a share;
- assigning one or more reusable accounts;
- setting expiration, download permission, share capacity, and account device capacity;
- copying the public share URL;
- viewing device/root health;
- testing and manually triggering Wake-on-LAN;
- viewing and evicting active sessions;
- revoking a grant or disabling a share immediately.

The visitor web experience includes login, share list, photo grid/viewer, optional download action, device-waking progress, capacity-full state, unavailable state, and session-expired state. It must not reveal machine names, local paths, internal addresses, or infrastructure diagnostics.

## 13. Testing Strategy

### Core tests

- account and share lifecycle;
- reusable grant authorization;
- share expiration and download permission;
- total share capacity and per-account device capacity;
- atomic lease semantics and expiration;
- Wake-on-LAN retry, timeout, and cooldown state transitions.

### Infrastructure tests

- password hashing and credential revision invalidation;
- account/share/grant/session persistence and migration;
- concurrent capacity acquisition under races;
- allowlist resolution and canonical-root containment;
- path traversal, alternate separators, links, and reparse points;
- Wake-on-LAN packet construction and target selection;
- gateway-to-agent mutual authentication;
- protected credentials and log redaction.

### App tests

- share creation and editing;
- account creation, reset, disable, and deletion;
- grant assignment and revocation;
- activity display and session eviction;
- root/device binding and Wake-on-LAN commands;
- Light/Dark resource and view-structure checks for new WPF surfaces.

### Integration and manual verification

- real `share.hanabe.cn` login through the Cloudflare Tunnel;
- Hanabe-local photo browsing and permitted download;
- Hibana online browsing;
- Hibana offline automatic wake, Agent startup, root verification, and resumed browsing;
- wake timeout and online-but-root-unavailable behavior;
- multiple concurrent browsers at and above configured limits;
- logout, abrupt disconnect, heartbeat expiry, and owner eviction;
- large photo, thumbnail, HTTP Range, cancellation, and interrupted-network behavior;
- Tunnel restart, Windows service restart, and machine reboot recovery.

Final executable verification follows `docs/testing.md`: Release solution build with warnings as errors, the applicable focused tests, full solution tests, Windows service smoke testing, and affected Light/Dark user-interface checks.

## 14. Acceptance Criteria

- A visitor cannot see share metadata or media before authentication.
- A reusable account can be granted access to multiple shares.
- Revoking a grant or disabling an account prevents subsequent requests and invalidates affected sessions.
- Concurrent active sessions never exceed the configured share or account limits under race conditions.
- A visitor can access only media IDs in the authorized share allowlist.
- No public request can select an arbitrary local path or escape a bound root.
- Hanabe-local photos are available while the Hanabe gateway and Tunnel are healthy.
- Requesting an offline Hibana root triggers bounded Wake-on-LAN and a waiting experience.
- A successful wake resumes access without the visitor manually reloading the workflow.
- An online Agent with an unusable root does not enter an automatic wake loop.
- Protected photos are not uploaded to cloud storage or intentionally persisted by the relay path.
- The home router exposes no new inbound Hanabe port.
- Existing Cloud, import, library, and photo-management workflows remain unaffected.

## 15. Implementation Sequencing Constraint

Implementation planning must separate the work into independently verifiable slices: domain and persistence, local gateway authentication, Hanabe-local sharing, device registration and Agent transport, Wake-on-LAN, Hibana streaming, WPF management surfaces, Cloudflare deployment, and end-to-end hardening. Cloudflare configuration must not precede a locally verified loopback-only gateway with working authentication and authorization.
