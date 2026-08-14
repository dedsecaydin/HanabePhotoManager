# Windows Installer and Desktop Shortcut Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce and locally install an upgradeable x64 Setup EXE whose stable desktop shortcut opens the currently installed Hanabe Photo Manager.

**Architecture:** A version-parameterized clean publish feeds an SDK-style WiX MSI and Burn bundle. Windows Installer owns files, shortcuts, repair, upgrade, and uninstall while application data remains outside the installation directory.

**Tech Stack:** PowerShell, .NET 8 publish, WiX Toolset SDK through NuGet/MSBuild, Windows Installer, xUnit/Pester-style script assertions where available.

## Global Constraints

- Primary artifact is `HanabePhotoManager-Setup-x64.exe`; no user-facing portable ZIP is required.
- Sources and artifacts stay under `D:\HanabePhoto`, never OneDrive.
- Installed binaries use `Program Files\Hanabe Photo Manager` and may request elevation.
- Upgrade repairs the desktop and Start menu shortcuts.
- Uninstall never removes user libraries, settings, credentials, indexes, or caches.
- Version is supplied once and propagated to all version-bearing outputs.
- Settings shows the same version in a selectable branch-style release tree with vertically scrollable notes.

---

### Task 1: Versioned clean publish contract

**Files:**
- Modify: `tools/Publish-Clean.ps1`
- Modify: `src/HanabePhotoManager.App/HanabePhotoManager.App.csproj`
- Create: `tests/Installer/PublishClean.Tests.ps1`
- Modify: `docs/release.md`

**Interfaces:**
- Produces: `Publish-Clean.ps1 -Version <semver> -OutputRoot <path>` and a clean `payload\win-x64` directory.

- [ ] Add source-level failing assertions for a mandatory normalized version parameter, safe D-drive/project-root output validation, and absence of a fixed `v1.0` artifact name.
- [ ] Run the installer test script and verify RED against the current fixed publish script.
- [ ] Implement version propagation and a manifest containing version, source revision, runtime, and checksum inputs.
- [ ] Re-run script assertions, then execute a clean Release publish.
- [ ] Commit Task 1 files only.

### Task 2: MSI payload, shortcuts, and upgrade identity

**Files:**
- Create: `installer/HanabePhotoManager.Installer/HanabePhotoManager.Installer.wixproj`
- Create: `installer/HanabePhotoManager.Installer/Package.wxs`
- Create: `installer/HanabePhotoManager.Installer/Package.zh-CN.wxl`
- Create: `tests/Installer/InstallerAuthoring.Tests.ps1`

**Interfaces:**
- Consumes: Task 1 publish directory and version.
- Produces: x64 MSI with stable upgrade identity, Program Files payload, desktop/Start menu shortcuts, and safe uninstall behavior.

- [ ] Add failing source assertions for stable upgrade identity, x64/per-machine scope, desktop and Start menu shortcuts, executable target, downgrade prevention, and no user-data removal.
- [ ] Run assertions and verify RED because installer authoring is absent.
- [ ] Add the SDK-style WiX project and package authoring with harvested clean payload.
- [ ] Restore/build the installer, inspect MSI metadata, and verify focused assertions GREEN.
- [ ] Commit Task 2 files only.

### Task 3: Setup EXE bundle and release orchestration

**Files:**
- Create: `installer/HanabePhotoManager.Setup/HanabePhotoManager.Setup.wixproj`
- Create: `installer/HanabePhotoManager.Setup/Bundle.wxs`
- Modify: `tools/Publish-Clean.ps1`
- Modify: `tests/Installer/InstallerAuthoring.Tests.ps1`

**Interfaces:**
- Consumes: Task 2 MSI.
- Produces: `artifacts/<version>/HanabePhotoManager-Setup-x64.exe` plus SHA-256 checksum and manifest.

- [ ] Add failing assertions for a Burn bundle, embedded MSI chain, stable bundle upgrade identity, Chinese display name, and deterministic output path.
- [ ] Run assertions and verify RED.
- [ ] Implement the bundle and invoke both WiX projects from the release script.
- [ ] Build Setup, calculate SHA-256, and verify assertions GREEN.
- [ ] Commit Task 3 files only.

### Task 4: Real local install and upgrade verification

**Files:**
- Create: `tools/Test-InstalledRelease.ps1`
- Modify: `docs/release.md`
- Modify: `tests/Installer/InstallerAuthoring.Tests.ps1`

**Interfaces:**
- Consumes: Setup EXE.
- Produces: evidence that the desktop shortcut resolves to the installed current executable and launches it.

- [ ] Add failing dry-run tests for exact target-path validation, process timeout, exit-code handling, and non-destructive uninstall boundaries.
- [ ] Implement install/upgrade/shortcut/launch verification with explicit absolute paths and recoverable logs under `D:\HanabePhoto\.artifacts`.
- [ ] Build a current alpha Setup, install it locally, resolve the desktop shortcut through Windows Shell, and launch the installed app.
- [ ] Verify application startup setting remains visible; record installed path and shortcut target.
- [ ] Run Release build/full tests, update release documentation, and commit verified changes.

### Task 5: Version tree and scrolling release notes

**Files:**
- Create: `src/HanabePhotoManager.App/ReleaseNotes/ReleaseNotesViewModel.cs`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Create: `tests/HanabePhotoManager.App.Tests/ReleaseNotes/ReleaseNotesViewModelTests.cs`

**Interfaces:**
- Consumes: the canonical application informational version.
- Produces: `ReleaseNotes.Versions`, `SelectedVersion`, `CurrentVersionLabel`, and selected scrollable notes.

- [ ] Add failing tests for current/newer/history labels, version selection, branch presentation bindings, and the bounded scrolling details pane.
- [ ] Run focused tests and verify RED because the release-notes module is absent.
- [ ] Implement the focused view model and Settings presentation without adding state to `MainWindowViewModel` beyond composition.
- [ ] Verify focused and full App tests GREEN and confirm the displayed current version matches the Setup build input.
- [ ] Commit the module, integration, and updated release documentation.
