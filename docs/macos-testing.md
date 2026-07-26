# macOS ARM64 Testing

> **Purpose:** Verify the unsigned phase 1 macOS ARM64 application produced by GitHub Actions.
> **Scope:** Artifact integrity, first launch, startup smoke testing, and observable application paths.
> **Audience:** Contributors and reviewers testing `hanabe-photo-manager-osx-arm64`.
> **References:** [testing.md](testing.md), [macos-arm64.yml](../.github/workflows/macos-arm64.yml)

## Artifact and integrity

Run the `macOS ARM64` workflow manually, or download its
`hanabe-photo-manager-osx-arm64` artifact from a pull request. The artifact contains:

- `HanabePhotoManager-osx-arm64.zip`
- `HanabePhotoManager-osx-arm64.zip.sha256`

Verify the download before extracting it:

```bash
shasum -a 256 -c HanabePhotoManager-osx-arm64.zip.sha256
```

Extract the ZIP and drag `Hanabe Photo Manager.app` into `/Applications`.
The build is self-contained for Apple silicon and is not code-signed or notarized.

## Automated test scope

The macOS workflow runs the cross-platform Core and Desktop.Core test projects,
then publishes, bundles, and smoke-tests the Avalonia host. The complete Windows
solution build and test run remains a separate mandatory phase 1 regression gate.

Infrastructure is Windows-gated in phase 1 because its current implementations
include DPAPI, `kernel32` file-handle operations, and Win32-specific lock
contention handling. The Avalonia phase 1 shell does not reference Infrastructure.
Those implementations must be migrated and tested on macOS in later full-parity
phases; a green phase 1 macOS workflow does not claim they are portable.

## First launch

For the first launch, use Finder:

1. Open `/Applications`.
2. Right-click `Hanabe Photo Manager.app`.
3. Choose **Open**, then confirm **Open** in the macOS prompt.

This is the primary and preferred path for approving this specific unsigned app.
Do not disable Gatekeeper globally.

If macOS still blocks the app after the Finder flow, remove quarantine only from
this installed bundle:

```bash
xattr -dr com.apple.quarantine "/Applications/Hanabe Photo Manager.app"
```

Do not apply `xattr` to `/Applications`, the home directory, or any other broad path.

## Available phase 1 checks

Record the macOS version, Apple silicon model, workflow run, source revision,
and result of each available check.

1. **Startup:** Launch the app, confirm the shell appears without a crash or
   missing resources. Confirm it displays `Hanabe Photo Manager` and
   `macOS migration foundation`, then quit and launch it again.
2. **Application data paths:** If the test environment permits inspecting the
   user Library, confirm the launch creates or uses only
   `~/Library/Application Support/Hanabe Photo Manager` for durable data and
   `~/Library/Caches/Hanabe Photo Manager` for cache data. No app state should
   appear inside the `.app` bundle or the source/download directory.
3. **Automated startup smoke:** Confirm the workflow's `Smoke-test published
   host` step passes. It runs `HanabePhotoManager.Desktop --smoke-test` against
   the published ARM64 host and validates startup composition and XAML loading
   without showing a window.

## Adapter readiness and deferred interaction checks

Phase 1 includes registered macOS adapters for Finder reveal and move-to-Trash,
but the current shell has no controls or commands wired to those services.
Testers therefore cannot perform either interaction through the application UI,
and phase 1 acceptance must not report those end-to-end checks as passed.

Defer the following checks to phase 2, after the core UI exposes the corresponding
actions:

- Reveal a disposable existing file or directory and confirm Finder opens and
  selects the requested item.
- Move a disposable file to Trash and confirm Finder moves it rather than
  permanently deleting it.

## Phase 1 limitation

Phase 1 is a native macOS shell foundation, not feature parity with the Windows
WPF application. Passing the available checks confirms only artifact integrity,
shell startup, startup composition, and currently observable path behavior. It
does not validate unwired adapter interactions or claim that the Windows
photo-management feature set is available on macOS.
