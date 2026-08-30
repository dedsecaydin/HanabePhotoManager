# Dual-Style Animated Hanabe Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add switchable Q-version and 8-bit animated Hanabe characters whose GIF changes with structured task state in the minimized assistant window.

**Architecture:** `MainWindowViewModel` exposes a small assistant-state enum and persisted visual style. A reusable `AnimatedGifImage` WPF control decodes embedded GIF frames, pauses when hidden, and falls back to the current PNG. Generated sprite sequences are assembled into twelve optimized GIF resources.

**Tech Stack:** .NET 8, C# 12, WPF `GifBitmapDecoder`, CommunityToolkit.Mvvm, xUnit, built-in image generation, ImageMagick.

## Global Constraints

- Work only under `D:\HanabePhoto`; never save project assets to OneDrive.
- Preserve the existing black-red twin-tail gothic camera-character identity.
- No third-party GIF runtime package.
- Hide and pause animation whenever the main window is not minimized.
- Missing or damaged animation resources must fall back to `Assets/Hanabe/hanabe-assistant.png`.

---

### Task 1: Assistant state and persisted visual style

**Files:**
- Create: `src/HanabePhotoManager.App/Services/HanabeAssistantState.cs`
- Create: `src/HanabePhotoManager.App/Services/HanabeAssistantVisualStyle.cs`
- Modify: `src/HanabePhotoManager.App/Services/AppSettingsStore.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Test: `tests/HanabePhotoManager.App.Tests/HanabeAssistantStateTests.cs`

**Interfaces:**
- Produces: `HanabeAssistantState { Idle, Scanning, Checking, Importing, Completed, Error }`, `HanabeAssistantVisualStyle { ChibiAnimated, PixelAnimated, Static, Off }`, bindable `AssistantState`, `AssistantVisualStyle`, and `AssistantAnimationSource`.

- [ ] Write failing tests for enum-to-resource paths, old `ShowHanabeAssistant` migration, style persistence, and state override priority.
- [ ] Run the focused tests; expect missing-type failures.
- [ ] Implement the enums, path resolver and settings properties with `ChibiAnimated` as default.
- [ ] Bind `Off` bidirectionally to the existing visibility behavior without removing the old setting key.
- [ ] Run focused tests; expect all cases to pass.
- [ ] Commit with `feat: model animated Hanabe state and style`.

### Task 2: GIF decoding and visibility-aware playback

**Files:**
- Create: `src/HanabePhotoManager.App/Controls/AnimatedGifImage.cs`
- Create: `tests/HanabePhotoManager.App.Tests/AnimatedGifImageTests.cs`

**Interfaces:**
- Produces: dependency properties `Source`, `FallbackSource`, `IsAnimationEnabled`, and `UseNearestNeighbor`; internal pure `GifFrameTiming.NormalizeDelay(int centiseconds)`.

- [ ] Write failing tests asserting zero/invalid GIF delays normalize to 100 ms and valid delays honor a 20 ms minimum.
- [ ] Run tests and observe missing control/timing failures.
- [ ] Implement `GifBitmapDecoder` frame loading, metadata delay parsing, one `DispatcherTimer`, source replacement, unload pause and fallback image loading.
- [ ] Apply `BitmapScalingMode.NearestNeighbor` only when `UseNearestNeighbor` is true.
- [ ] Run focused tests and an App build; expect zero warnings.
- [ ] Commit with `feat: add visibility-aware animated GIF control`.

### Task 3: Generate Q-version and pixel animation assets

**Files:**
- Create: `src/HanabePhotoManager.App/Assets/Hanabe/Animated/Chibi/*.gif`
- Create: `src/HanabePhotoManager.App/Assets/Hanabe/Animated/Pixel/*.gif`
- Modify: `src/HanabePhotoManager.App/HanabePhotoManager.App.csproj`
- Test: `tests/HanabePhotoManager.App.Tests/HanabeAnimationAssetTests.cs`

**Interfaces:**
- Produces twelve embedded GIFs named `idle`, `scanning`, `checking`, `importing`, `completed`, and `error` under both style directories.

- [ ] Add a failing asset-contract test requiring all twelve files, GIF signatures, at least two frames, square dimensions and explicit WPF Resource entries.
- [ ] Generate transparent Q-version sprite sheets using the existing character image as identity reference; generate separate transparent 8-bit sprite sheets with the same silhouette and palette.
- [ ] Split sheets into 8–16 consistent frames and assemble optimized looping GIFs; make completed/error finite visual beats that the ViewModel state timer controls.
- [ ] Inspect every GIF for identity, clipping, alpha edges and flashing; regenerate only defective states.
- [ ] Run asset tests; expect all twelve assets to pass.
- [ ] Commit with `feat: add dual-style Hanabe animation assets`.

### Task 4: Bind task lifecycle and settings UI

**Files:**
- Modify: `src/HanabePhotoManager.App/HanabeAssistantWindow.xaml`
- Modify: `src/HanabePhotoManager.App/HanabeAssistantWindow.xaml.cs`
- Modify: `src/HanabePhotoManager.App/SettingsCenterPage.xaml`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.Import.cs`
- Test: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Interfaces:**
- Consumes: `AssistantAnimationSource`, `AssistantVisualStyle`, `AssistantState`.
- Produces: minimized-window animated character and a persisted four-choice settings selector.

- [ ] Add failing XAML/source contract tests for `AnimatedGifImage`, fallback PNG, nearest-neighbor pixel binding and four settings choices.
- [ ] Add failing lifecycle tests for Analysis/Preview→Scanning, duplicate validation→Checking, Import→Importing, success→Completed, cancellation/failure→Error, and 3-second return to Idle.
- [ ] Replace the static assistant image with the animated control while preserving progress bindings and drag behavior.
- [ ] Wire structured task entry/exit points and a cancelable DispatcherTimer for terminal-state rollback.
- [ ] Add the settings selector using existing input and typography styles.
- [ ] Run focused UI and import tests; expect all to pass.
- [ ] Commit with `feat: drive Hanabe animations from task state`.

### Task 5: Release, runtime QA and documentation

**Files:**
- Modify: `docs/current-status.md`
- Modify: `docs/agent-change-log.md`

- [ ] Run `dotnet build HanabePhotoManager.sln -c Release --no-restore /warnaserror`; expect zero warnings and errors.
- [ ] Run `dotnet test HanabePhotoManager.sln -c Release --no-build --no-restore`; expect every suite to pass.
- [ ] Publish self-contained output to a new `D:\HanabePhoto\.artifacts\` directory.
- [ ] Verify both styles across six states, rapid state interruption, minimized show, restored hide, animation pause and missing-resource fallback.
- [ ] Cover the local installed DLL only after source/published hashes match, restart the app and repeat minimize/restore smoke testing.
- [ ] Record exact automated and runtime evidence, then commit with `docs: record animated Hanabe verification`.
