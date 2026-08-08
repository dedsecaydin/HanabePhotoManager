# Photo Treemap Performance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make treemap browsing responsive and complete for large libraries while restoring a viewport-sized root overview and genuine justified inner galleries.

**Architecture:** Keep `JustifiedGalleryLayout` deterministic in Core, let `PhotoTreemapControl` compute only intersecting visible leaf paths, and make `MainWindowViewModel` own a generation-safe, viewport-priority thumbnail pipeline. Dimension IO remains background work; publishing observable treemap state returns to the captured UI context.

**Tech Stack:** .NET 8, C# 12, WPF, CommunityToolkit.Mvvm, xUnit, FluentAssertions.

## Global Constraints

- Preserve MVVM boundaries, duplicate-import behavior, and retouched-output write protection.
- No synchronous filesystem, image decode, hashing, or scanning on the UI thread.
- Release build uses `/warnaserror`; all tests must pass.

---

### Task 1: Deterministic justified-gallery and root bounds

**Files:**
- Modify: `src/HanabePhotoManager.Core/Browsing/Treemap/JustifiedGalleryLayout.cs`
- Modify: `src/HanabePhotoManager.App/Browsing/Treemap/PhotoTreemapControl.cs`
- Create: `tests/HanabePhotoManager.Core.Tests/Browsing/Treemap/JustifiedGalleryLayoutTests.cs`

- [ ] Write layout tests for full non-final rows and bounded final-row heights.
- [ ] Make each completed justified row consume the available width without overflow; keep only an intentionally sparse final row natural.
- [ ] Reset and report content bounds per render; make root content equal to its viewport-sized Squarified bounds.
- [ ] Include only viewport-intersecting leaf rectangles in the visible-thumbnail request set.

### Task 2: UI-safe dimension publication and a self-healing viewport pipeline

**Files:**
- Modify: `src/HanabePhotoManager.App/Browsing/Treemap/ProgressiveTreemapViewModel.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/Browsing/Treemap/ProgressiveTreemapViewModelTests.cs`

- [ ] Write a test that dimension submission updates aspects and layout revision.
- [ ] Marshal background dimension batches onto the view-model synchronization context before publishing.
- [ ] Replace cancel-and-restart viewport loading with one active request generation plus a pending visible-path set; completed requests drain pending work and stale completions cannot clear newer state.
- [ ] Decode only bounded thumbnail sizes and retain current exception/timeout fallbacks.

### Task 3: Viewport-sized overview and regression verification

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/Browsing/Treemap/PhotoTreemapControlTests.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/Browsing/Treemap/BrowseTreemapIntegrationTests.cs`

- [ ] Add tests for the visible-area policy and root overview behavior contracts.
- [ ] Size root treemap to the ScrollViewer viewport and skip fit-to-content when at root; preserve subtree scrolling and zoom.
- [ ] Run focused tests, complete solution build and test, then document outcomes.
