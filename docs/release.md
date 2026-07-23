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

Inspect the App project, assembly metadata, release notes, and publish script for all version-bearing values. The current script output name includes `v1.0`; treat it as a release parameter that must match the approved version, not an automatically current value. Confirm persisted settings, metadata, queues, indexes, and sessions remain compatible or have a tested migration strategy.

## Build and Test Gate

```powershell
dotnet restore HanabePhotoManager.sln
dotnet build HanabePhotoManager.sln -c Release /warnaserror --artifacts-path .artifacts/release-verification
dotnet test HanabePhotoManager.sln -c Release --no-build --artifacts-path .artifacts/release-verification
```

All required tests must pass before publishing.

## Publish

```powershell
powershell -ExecutionPolicy Bypass -File tools/Publish-Clean.ps1
```

This is the formal release path. It publishes the App for `win-x64`, self-contained with ReadyToRun, preserves required WebView2 runtime DLLs, removes only accidental WebView2 user-data inside the verified project output, and produces a ZIP under `artifacts/`. If requirements change, update the script and this document together.

## Artifact Inspection

- Directory and ZIP names match the approved version.
- The extracted ZIP starts without a separately installed .NET runtime.
- Required DLLs, native runtimes, themes, icons, map/model assets, licenses, and notices exist.
- No source, tests, `.git`, `.artifacts`, logs, browser data, sessions, credentials, settings, or personal media are included.
- Validate a freshly extracted copy, not only the source publish directory.

## Regression and Record

Run the global smoke checks in [testing.md](testing.md), then exercise with disposable media: startup/restart, theme switch and primary navigation, import discovery/cancel/completion, library browse and metadata, viewer, available people/analysis/map paths, compression, watermark, and cloud simulator. Confirm persisted settings survive restart and the publish directory receives no unexpected runtime data.

Record version, date, source revision when available, SDK, publish command, test totals, smoke result, filename, checksum, known limitations, and storage-format changes. Retain the prior accepted artifact until the new one passes. Rollback must never delete user libraries or application data.
