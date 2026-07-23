# Visual QA Follow-up Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correct only the Settings, Cloud, Watermark, disabled-state, high-DPI text, and PhotoViewer confirmation defects confirmed by the 2026-07-23 Visual QA.

**Architecture:** Preserve existing view models and workflows. Apply explicit display metadata in Settings, a small UI state machine around the existing Cloud WebView lifecycle, shared semantic style adjustments for text/buttons, and reuse the existing delete confirmation dialog in PhotoViewer.

**Tech Stack:** .NET 8, C# 12, WPF/XAML, WebView2, xUnit, FluentAssertions.

## Global Constraints

- Do not add product features or change existing business workflows.
- Do not perform unrelated refactoring.
- Use existing design tokens and semantic brushes; do not add page-level hard-coded colors.
- Verify Release Build, full tests, win-x64 publish, and published-app Visual QA.
- Save screenshots under `D:\App\artifacts\visual-qa-followup-20260723`.

---

### Task 1: Settings display labels

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

- [ ] Add a failing XAML assertion that choice-object ComboBoxes declare `DisplayMemberPath="Label"`.
- [ ] Run the focused test and confirm it fails on the current object text.
- [ ] Add only the missing display-member declarations without changing selection bindings.
- [ ] Run the focused test and confirm it passes.

### Task 2: Cloud lifecycle feedback

**Files:**
- Modify: `src/HanabePhotoManager.App/Cloud/CloudPage.xaml`
- Modify: `src/HanabePhotoManager.App/Cloud/CloudPage.xaml.cs`
- Test: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

- [ ] Add failing assertions for Loading, Error, Retry, and nonblank state coverage.
- [ ] Run the focused test and confirm it fails.
- [ ] Add a status overlay driven by WebView initialization/navigation lifecycle, with a bounded timeout and retry calling the existing initialization/navigation path.
- [ ] Keep Content visible only after successful navigation; expose a clear Empty state for successful blank documents.
- [ ] Run the focused test and confirm it passes.

### Task 3: Shared button and text layout

**Files:**
- Modify: `src/HanabePhotoManager.App/Themes/Controls/Buttons.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Controls/Layout.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Controls/Status.xaml`
- Modify: `src/HanabePhotoManager.App/Watermark/WatermarkPage.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

- [ ] Add failing assertions for content alignment, disabled semantic foreground/background, and wrapping/line stacking.
- [ ] Run the focused test and confirm it fails.
- [ ] Apply shared MinHeight, Padding, vertical alignment, wrapping, and semantic disabled-state setters.
- [ ] Add only Watermark-specific wrapping/grid constraints that cannot be shared.
- [ ] Run the focused test and confirm it passes.

### Task 4: PhotoViewer delete confirmation

**Files:**
- Modify: `src/HanabePhotoManager.App/PhotoViewerWindow.xaml.cs`
- Test: `tests/HanabePhotoManager.App.Tests/PreviewPerformanceTests.cs`

- [ ] Add a failing assertion that both Delete key and command route through the existing `DeleteConfirmationWindow.Confirm` path.
- [ ] Run the focused test and confirm it fails.
- [ ] Intercept the button/keyboard action in the window, request confirmation, then invoke the unchanged view-model deletion command only when confirmed.
- [ ] Run the focused test and confirm it passes.

### Task 5: Release verification and Visual QA

**Files:**
- Output: `artifacts/HanabePhotoManager-v1.0-full-optimized`
- Output: `artifacts/visual-qa-followup-20260723`

- [ ] Run `dotnet build HanabePhotoManager.sln -c Release /warnaserror --artifacts-path .artifacts/release-verification-followup` and require 0 warnings/errors.
- [ ] Run `dotnet test HanabePhotoManager.sln -c Release --no-build --artifacts-path .artifacts/release-verification-followup` and require 0 failures.
- [ ] Run `powershell -ExecutionPolicy Bypass -File tools/Publish-Clean.ps1` and require a win-x64 self-contained artifact.
- [ ] Launch the published executable and capture main, Settings, Cloud, Compression, Watermark, PhotoViewer, Light, Dark, and restored-window screenshots.
- [ ] Record 125%, 150%, and 200% DPI as manual checks if stable automated switching is unavailable.
