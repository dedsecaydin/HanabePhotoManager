# macOS ARM64 Testing

> **Purpose:** Verify the unsigned phase 1 macOS ARM64 application produced by GitHub Actions.
> **Scope:** Artifact integrity, first launch, application paths, Finder reveal, and move-to-Trash behavior.
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

## Manual smoke checks

Use disposable files and record the macOS version, Apple silicon model, workflow
run, source revision, and result of each check.

1. **Startup:** Launch the app, confirm the shell appears without a crash or
   missing resources, then quit and launch it again.
2. **Application data paths:** After launch, confirm app-created state stays under
   `~/Library/Application Support/Hanabe Photo Manager` and cache data stays under
   `~/Library/Caches/Hanabe Photo Manager`. No app state should appear inside the
   `.app` bundle or the source/download directory.
3. **Finder reveal:** Use the shell action that reveals a disposable existing file
   or directory. Finder must open and select the requested item.
4. **Move to Trash:** Use the shell action on a disposable file. Confirm Finder
   moves it to Trash rather than permanently deleting it, then restore or empty it
   manually as appropriate.

The workflow also runs `HanabePhotoManager.Desktop --smoke-test` against the
published ARM64 host. That automated check validates startup composition and XAML
loading without showing a window; it does not replace the manual checks above.

## Phase 1 limitation

Phase 1 is a native macOS shell foundation, not feature parity with the Windows
WPF application. Passing this checklist confirms the shell, packaging, and current
platform integrations only. It does not claim that the Windows photo-management
feature set is available on macOS.
