# Minimized Hanabe Assistant Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a draggable, topmost Hanabe progress window while the main window is minimized and hide it when the main window returns.

**Architecture:** A focused `HanabeAssistantWindow` binds to the existing `MainWindowViewModel`; `MainWindow` owns its lifecycle and synchronizes it from `StateChanged` and the existing visibility setting. No task state is duplicated.

**Tech Stack:** .NET 8, C# 12, WPF, xUnit, FluentAssertions.

## Global Constraints

- Work only under `D:\HanabePhoto`; do not use OneDrive.
- Reuse existing theme tokens and `Assets/Hanabe/hanabe-assistant.png`.
- The floating window must not appear when `ShowHanabeAssistant` is false.
- Do not change progress-producing business logic or bindings.

---

### Task 1: Window visibility policy

**Files:**
- Create: `src/HanabePhotoManager.App/Services/HanabeAssistantWindowPolicy.cs`
- Create: `tests/HanabePhotoManager.App.Tests/HanabeAssistantWindowPolicyTests.cs`

**Interfaces:**
- Produces: `internal static bool ShouldShow(WindowState state, bool enabled)`.

- [ ] Write tests asserting only `(WindowState.Minimized, true)` returns true.
- [ ] Run `dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj --filter FullyQualifiedName~HanabeAssistantWindowPolicyTests --no-restore`; expect failure because the policy is missing.
- [ ] Implement the one-expression policy using `state == WindowState.Minimized && enabled`.
- [ ] Re-run the filtered test; expect all policy cases to pass.
- [ ] Commit the policy and its tests with `test: define minimized assistant visibility`.

### Task 2: Floating WPF window

**Files:**
- Create: `src/HanabePhotoManager.App/HanabeAssistantWindow.xaml`
- Create: `src/HanabePhotoManager.App/HanabeAssistantWindow.xaml.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Interfaces:**
- Consumes: `MainWindowViewModel` as `DataContext`.
- Produces: a topmost, non-taskbar, transparent WPF window with `ProgressLabel`, `ProgressValue`, and `EstimatedTimeRemaining` bindings.

- [ ] Add a failing XAML contract test requiring `Topmost="True"`, `ShowInTaskbar="False"`, the three existing progress bindings, the Hanabe asset, and a title-surface mouse-down handler.
- [ ] Run the focused contract test; expect failure because the XAML file does not exist.
- [ ] Create the XAML using existing `Brush.*`, `Radius.*`, typography and button styles; do not hardcode colors.
- [ ] Implement left-button `DragMove()` with safe exception handling and a close control that calls `Hide()`.
- [ ] Re-run the focused test and build the App project; expect success with zero warnings.
- [ ] Commit with `feat: add minimized Hanabe assistant window`.

### Task 3: Main-window lifecycle integration

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Interfaces:**
- Consumes: `HanabeAssistantWindowPolicy.ShouldShow`, `_viewModel.ShowHanabeAssistant`.
- Produces: `UpdateHanabeAssistantWindow()` and deterministic cleanup on closing.

- [ ] Add failing source-contract assertions for a `StateChanged` synchronization handler, the policy call, shared `DataContext`, and close cleanup.
- [ ] Run the focused test and observe the expected missing lifecycle markers.
- [ ] Lazily create one assistant window, set its owner/data context, show it only when policy returns true, and hide otherwise.
- [ ] React to `ShowHanabeAssistant` property changes and close the child during main-window shutdown.
- [ ] Ensure the in-window card is hidden while minimized and remains unchanged during Normal/Maximized states.
- [ ] Run focused tests, Release `/warnaserror` build and full tests.
- [ ] Commit with `feat: show Hanabe assistant when minimized`.

### Task 4: Runtime QA and documentation

**Files:**
- Modify: `docs/current-status.md`
- Modify: `docs/agent-change-log.md`

- [ ] Publish self-contained output to a new D-drive artifact directory.
- [ ] Launch the fresh artifact, minimize the main window, confirm the floating window appears, drag it, restore the main window, and confirm it disappears.
- [ ] Turn off the assistant setting and confirm minimization does not show it.
- [ ] Record automated versus manual evidence accurately in both documents.
- [ ] Commit with `docs: record minimized assistant verification`.

