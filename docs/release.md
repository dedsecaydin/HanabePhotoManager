# Release Standard

> **Purpose:** Define formal version, publish, artifact, and regression procedure.  
> **Scope:** Windows x64 self-contained output produced by `tools/Publish-Clean.ps1`.  
> **Audience:** Release owners and AI agents preparing a formal deliverable.  
> **References:** [testing.md](testing.md), [Publish-Clean.ps1](../tools/Publish-Clean.ps1), [architecture.md](architecture.md)

## Table of Contents

- [Preconditions](#preconditions)
- [Version Check](#version-check)
- [Build and Test Gate](#build-and-test-gate)
- [Publish](#publish)
- [Artifact Inspection](#artifact-inspection)
- [Regression and Record](#regression-and-record)

## Preconditions

- Scope, target version, Windows architecture, and known limitations are agreed.
- No generated output, credentials, caches, user settings, sessions, or personal media are staged for distribution.
- Required dependency/model licenses and notices remain present.
- Cloud claims are limited to implemented, verified, officially authorized behavior.

## Version Check

Select one normalized semantic version such as `0.2.0-alpha.1`. The publish command passes this value to application assembly metadata and uses it as the release directory name. Confirm persisted settings, metadata, queues, indexes, and sessions remain compatible or have a tested migration strategy.

## Build and Test Gate

```powershell
dotnet restore HanabePhotoManager.sln
dotnet build HanabePhotoManager.sln -c Release /warnaserror --artifacts-path .artifacts/release-verification
dotnet test HanabePhotoManager.sln -c Release --no-build --artifacts-path .artifacts/release-verification
```

All required tests must pass before publishing.

## Publish

```powershell
powershell -ExecutionPolicy Bypass -File tools/Publish-Clean.ps1 -Version 0.2.0-alpha.1 -OutputRoot artifacts
```

This is the formal release path. It writes a clean, self-contained ReadyToRun payload to `artifacts/<version>/payload/win-x64`, builds the x64 MSI and Burn bundle, and emits the primary `artifacts/<version>/HanabePhotoManager-Setup-x64.exe` artifact with a SHA-256 file and `release-manifest.json`. It preserves required WebView2 runtime DLLs and removes only accidental WebView2 user-data inside the verified project output. Output is accepted only under the repository on the D drive. There is no user-facing portable ZIP for the installed release.

## Local Installation Verification

First review the exact paths and actions without changing the machine:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Test-InstalledRelease.ps1 -Version 0.2.0-alpha.1
```

After reviewing the dry run, open an elevated PowerShell session and execute the installation gate. Add `-PreviousSetupPath <older-setup>` to exercise a real upgrade:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Test-InstalledRelease.ps1 -Version 0.2.0-alpha.1 -Execute
```

The gate installs (or upgrades) the current bundle, requires the common desktop shortcut to resolve exactly to `Program Files\Hanabe Photo Manager\HanabePhotoManager.App.exe`, observes application startup, verifies uninstall preserves a sentinel under the application-data root, and reinstalls the current version so the machine is left ready to use. Logs and JSON evidence stay under `.artifacts/installed-release/<version>/`; the script never removes the photo library or the application-data root.

## Artifact Inspection

- The release directory and manifest match the approved version.
- The published payload starts without a separately installed .NET runtime.
- `HanabePhotoManager-Setup-x64.exe` and its `.sha256` file are present at the release root and the checksum recomputes successfully.
- The MSI database has the approved product version, stable upgrade code, per-machine scope, and desktop/Start menu shortcuts.
- Required DLLs, native runtimes, themes, icons, map/model assets, licenses, and notices exist.
- No source, tests, `.git`, `.artifacts`, logs, browser data, sessions, credentials, settings, or personal media are included.
- Validate a freshly extracted copy, not only the source publish directory.

## Regression and Record

Run the global smoke checks in [testing.md](testing.md), then exercise with disposable media: startup/restart, theme switch and primary navigation, import discovery/cancel/completion, library browse and metadata, viewer, available people/analysis/map paths, compression, watermark, and cloud simulator. Confirm persisted settings survive restart and the publish directory receives no unexpected runtime data.

Record version, date, source revision when available, SDK, publish command, test totals, smoke result, filename, checksum, known limitations, and storage-format changes. Retain the prior accepted artifact until the new one passes. Rollback must never delete user libraries or application data.
