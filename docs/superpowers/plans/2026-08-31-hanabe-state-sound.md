# Hanabe State Sound Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add selectable camera, cute, and mixed Hanabe state sound packs with persisted volume, quiet mode, previews, anti-spam behavior, and optional main-window restore after actionable duplicate detection.

**Architecture:** Keep deterministic mapping in `HanabeSoundPolicy` and Windows playback in `HanabeSoundService`. Route the existing assistant state transition through the service and persist controls through the existing `AppSettingsStore`/`MainWindowViewModel` settings flow.

**Tech Stack:** .NET 8, C# 12, WPF, PCM WAV, CommunityToolkit.Mvvm, xUnit, FluentAssertions.

## Global Constraints

- Do not alter LibVLC video volume behavior.
- Idle and progress-percentage changes never make sound.
- Default style is Mixed, default volume is 35, sound is enabled, quiet mode is disabled.
- Playback failure must never interrupt import, scan, or verification.
- Use original locally generated WAV assets and package them with the application.
- Restore the main window only when duplicate detection succeeds with actionable duplicates and the setting is enabled.

---

### Task 1: Sound policy and assets

**Files:**
- Create: `src/HanabePhotoManager.App/Services/HanabeSoundPolicy.cs`
- Create: `src/HanabePhotoManager.App/Assets/Hanabe/Sounds/Camera/*.wav`
- Create: `src/HanabePhotoManager.App/Assets/Hanabe/Sounds/Cute/*.wav`
- Create: `src/HanabePhotoManager.App/Assets/Hanabe/Sounds/Mixed/*.wav`
- Test: `tests/HanabePhotoManager.App.Tests/HanabeSoundPolicyTests.cs`

**Interfaces:**
- Produces: `HanabeSoundStyle`, `HanabeSoundSettings`, and `HanabeSoundPolicy.Resolve(HanabeAssistantState, HanabeSoundSettings)`.

- [ ] Write failing tests for disabled, Idle, quiet mode, and all style/state mappings.
- [ ] Run `dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj --filter FullyQualifiedName~HanabeSoundPolicyTests` and verify failure.
- [ ] Implement the enum, immutable settings record, resource mapping, and volume normalization.
- [ ] Generate short 44.1 kHz PCM WAV assets with distinct envelopes for A, B, and Mixed.
- [ ] Re-run the focused tests and verify they pass.

### Task 2: Playback service and state integration

**Files:**
- Create: `src/HanabePhotoManager.App/Services/HanabeSoundService.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Test: `tests/HanabePhotoManager.App.Tests/HanabeSoundServiceTests.cs`

**Interfaces:**
- Consumes: `HanabeSoundPolicy.Resolve`.
- Produces: `IHanabeSoundService.PlayState(HanabeAssistantState, HanabeSoundSettings, bool force = false)` and `Preview(HanabeSoundStyle, double volume)`.

- [ ] Write failing tests with an injected playback delegate and clock for state deduplication, debounce, quiet mode, and forced preview.
- [ ] Run the focused test and verify failure.
- [ ] Implement asynchronous non-blocking playback with `MediaPlayer`, per-play volume, disposal, and exception isolation.
- [ ] Invoke playback only after `SetAssistantState` changes the state; do not invoke it for Idle.
- [ ] Re-run focused tests and verify they pass.

### Task 3: Persistent settings and settings-center controls

**Files:**
- Modify: `src/HanabePhotoManager.App/Services/AppSettingsStore.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/HanabePhotoManager.App/SettingsCenterPage.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/AppSettingsStoreTests.cs`
- Test: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Interfaces:**
- Produces: `HanabeSoundEnabled`, `HanabeSoundStyle`, `HanabeSoundVolume`, `HanabeSoundQuietMode`, style choices, and preview commands.

- [ ] Add failing tests for default settings, JSON round-trip, clamping, and XAML bindings.
- [ ] Run focused tests and verify failure.
- [ ] Add load/save properties and commands to the ViewModel using the existing settings lifecycle.
- [ ] Add one sound subsection beneath the Hanabe assistant row using existing ComboBox, CheckBox, Slider, and Button styles.
- [ ] Re-run focused tests and verify they pass.

### Task 4: Duplicate-detection window restore

**Files:**
- Create: `src/HanabePhotoManager.App/Services/DuplicateDetectionWindowPolicy.cs`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml.cs`
- Modify: `src/HanabePhotoManager.App/Services/AppSettingsStore.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/HanabePhotoManager.App/SettingsCenterPage.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/DuplicateDetectionWindowPolicyTests.cs`

**Interfaces:**
- Produces: `RestoreWindowAfterDuplicateDetection`, an actionable duplicate completion notification, and `DuplicateDetectionWindowPolicy.ShouldRestore(...)`.

- [ ] Add failing policy tests for enabled/disabled, minimized/visible, duplicate/no-duplicate, success/cancel/error.
- [ ] Run focused tests and verify failure.
- [ ] Expose a ViewModel completion event containing success and actionable duplicate count without moving window mechanics into the ViewModel.
- [ ] Handle the event in `MainWindow.xaml.cs`, restoring and activating only when the policy permits.
- [ ] Add the persisted setting and settings-center checkbox, then re-run focused tests.

### Task 5: Documentation and full verification

**Files:**
- Modify: `docs/current-status.md`
- Modify: `docs/agent-change-log.md`
- Modify: `AGENT_HANDOFF.md`

- [ ] Update current-state documents with behavior, defaults, assets, and known limitations.
- [ ] Run `dotnet build HanabePhotoManager.sln -c Release /warnaserror` and require zero warnings/errors.
- [ ] Run `dotnet test HanabePhotoManager.sln -c Release --no-build` and require all tests to pass.
- [ ] Run the formal publish path and verify all sound assets exist in published output.
- [ ] Smoke test app startup, settings switching, preview, scan/import completion, error, quiet mode, muted mode, and video playback independence.
