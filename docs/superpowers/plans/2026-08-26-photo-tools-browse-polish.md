# Photo Tools and Browse Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete watermark, collage, navigation, browse, and import-layout polish requested from the current published application.

**Architecture:** Keep deterministic geometry in calculators/services, WPF pointer mechanics in code-behind, observable state in existing view models, and visual states in existing XAML/resources. Each behavior receives a focused regression test before its minimal production change.

**Tech Stack:** .NET 8, C# 12, WPF, CommunityToolkit.Mvvm, ImageSharp, xUnit, FluentAssertions.

## Global Constraints

- Work only under `D:\HanabePhoto`; do not write to OneDrive.
- Preserve unrelated user files and dirty-worktree changes.
- Use existing design tokens and shared motion resources.
- Do not change unrelated business behavior, commands, or bindings.
- Append verified changes to `docs/agent-change-log.md`.

---

### Task 1: Watermark geometry and density

**Files:**
- Modify: `src/HanabePhotoManager.App/Watermark/WatermarkLayoutCalculator.cs`
- Modify: `src/HanabePhotoManager.App/Watermark/WatermarkViewModel.cs`
- Modify: `src/HanabePhotoManager.App/Watermark/WatermarkPage.xaml`
- Modify: `src/HanabePhotoManager.App/Watermark/WatermarkPage.xaml.cs`
- Test: `tests/HanabePhotoManager.App.Tests/WatermarkLayoutCalculatorTests.cs`
- Test: `tests/HanabePhotoManager.App.Tests/WatermarkPreviewStateTests.cs`

**Interfaces:**
- Consumes: normalized center, width ratio, preview/image dimensions.
- Produces: centered preview bounds and a shared automatic gap calculation used by preview and export.

- [ ] Write tests asserting maximum automatic density overlaps slightly and changing size leaves center values unchanged.
- [ ] Run the focused tests and confirm the new assertions fail.
- [ ] Add shared automatic gap calculation and center-positioned preview overlay using the actual uniform image viewport.
- [ ] Run the focused tests and confirm they pass.

### Task 2: Optional blurred collage background

**Files:**
- Modify: `src/HanabePhotoManager.App/Compression/ImageCollageService.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/CompressionViewModel.cs`
- Modify: `src/HanabePhotoManager.App/Compression/CompressionPage.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/ImageCollageServiceTests.cs`
- Test: `tests/HanabePhotoManager.App.Tests/CompressionViewModelTests.cs`

**Interfaces:**
- Consumes: `CollageOptions(..., bool UseBlurredBackground)`.
- Produces: unchanged white output when false; cover-cropped blurred slot background when true.

- [ ] Write image-pixel and view-model option tests and run them to observe failure.
- [ ] Implement the option, ImageSharp cover/crop/blur/darken layer, checkbox binding, and option forwarding.
- [ ] Run collage tests and confirm both legacy and new paths pass.

### Task 3: Navigation motion and primary selected state

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml.cs`
- Test: `tests/HanabePhotoManager.App.Tests/NavigationMotionTests.cs`

**Interfaces:**
- Consumes: `MainWindowViewModel.CurrentPage` and named page hosts.
- Produces: one interruptible transition for every destination and one-layer sidebar selection.

- [ ] Extend source-contract tests for all hosts, interruptible animation, and absence of nested selected icon outline.
- [ ] Run the focused test and confirm failure.
- [ ] Centralize host resolution, restart shared page motion on navigation, and simplify the navigation template selected trigger.
- [ ] Run navigation tests and confirm they pass.

### Task 4: Browse completeness and import spacing

**Files:**
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/PreviewPerformanceTests.cs`

**Interfaces:**
- Consumes: complete `_filteredCache` and date-section expansion state.
- Produces: all filtered date headers and all items from expanded groups in `PreviewWallItems`.

- [ ] Add regressions with multiple date folders and an expanded group larger than `VisiblePageSize`; assert complete section and wall counts.
- [ ] Run the focused tests and confirm the incomplete path fails.
- [ ] Remove any page-size truncation from grouping/expansion while keeping thumbnail loading viewport-bound; add tokenized margin above the import primary action.
- [ ] Run browse and XAML contract tests and confirm they pass.

### Task 5: Stable import tip transition

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml.cs`
- Test: `tests/HanabePhotoManager.App.Tests/ImportWorkflowUiTests.cs`

**Interfaces:**
- Consumes: the existing rotating import-tip text source.
- Produces: a fixed-size tip container and interruptible text-only fade/translate animation.

- [ ] Add a source-contract test asserting fixed tip dimensions and shared motion usage; run it to observe failure.
- [ ] Keep the container stable and animate only `ImportTipText` when the message advances.
- [ ] Run the focused import UI tests and confirm they pass.

### Task 6: Documentation and full verification

**Files:**
- Modify: `docs/agent-change-log.md`

**Interfaces:**
- Consumes: completed code and fresh command output.
- Produces: auditable change entry and verified build artifacts.

- [ ] Run focused app tests for watermark, collage, navigation, and browse.
- [ ] Run `dotnet build HanabePhotoManager.sln -c Release /warnaserror`.
- [ ] Run `dotnet test HanabePhotoManager.sln -c Release --no-build`.
- [ ] Run the publish command required by `docs/testing.md` and record actual results.
- [ ] Append exact verification evidence to `docs/agent-change-log.md` and review `git diff --check` plus scoped diff.
