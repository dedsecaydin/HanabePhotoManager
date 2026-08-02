# Progressive Photo Treemap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a progressively updating, zoomable photo treemap alongside the existing Browse grid with switchable size/count weighting.

**Architecture:** A pure Core layout policy calculates stable rectangles. A focused App view model consumes existing scan batches and publishes immutable snapshots. A custom WPF control renders those snapshots efficiently and reuses existing selection, thumbnail, theme, and viewer behavior.

**Tech Stack:** .NET 8, C# 12, WPF, CommunityToolkit.Mvvm, xUnit, FluentAssertions.

## Global Constraints

- Preserve the existing grid and all current Browse bindings and commands.
- No WPF/filesystem types in the Core layout policy.
- Coalesce progressive layout updates to a target interval of 150 ms.
- Persist grid/treemap and size/count choices with backward-compatible defaults.
- Use semantic Light/Dark resources; do not hardcode page-level colors or duplicate shared templates.

---

### Task 1: Deterministic treemap layout policy

**Files:**
- Create: `src/HanabePhotoManager.Core/Browsing/Treemap/TreemapModels.cs`
- Create: `src/HanabePhotoManager.Core/Browsing/Treemap/SquarifiedTreemapLayout.cs`
- Create: `tests/HanabePhotoManager.Core.Tests/Browsing/Treemap/SquarifiedTreemapLayoutTests.cs`

**Interfaces:**
- Produces: `TreemapWeightMode`, `TreemapNode`, `TreemapBounds`, `TreemapTile`, and `SquarifiedTreemapLayout.Calculate(IReadOnlyList<TreemapNode>, TreemapBounds)`.

- [ ] Write tests proving proportional area, stable ordering, in-bounds output, exclusion of non-positive weights, and empty-input behavior.
- [ ] Run `dotnet test tests/HanabePhotoManager.Core.Tests/HanabePhotoManager.Core.Tests.csproj -c Release --filter FullyQualifiedName~SquarifiedTreemapLayoutTests` and confirm RED because the types do not exist.
- [ ] Implement immutable validated models and the minimal squarified-row layout with path-based stable tie-breaking.
- [ ] Re-run the focused test and confirm all cases pass.
- [ ] Run the complete Core test project and commit only Task 1 files.

### Task 2: Progressive treemap state

**Files:**
- Create: `src/HanabePhotoManager.App/Browsing/Treemap/ProgressiveTreemapViewModel.cs`
- Create: `src/HanabePhotoManager.App/Browsing/Treemap/TreemapItemViewModel.cs`
- Create: `tests/HanabePhotoManager.App.Tests/Browsing/Treemap/ProgressiveTreemapViewModelTests.cs`

**Interfaces:**
- Consumes: Task 1 models and existing `LibraryDateSnapshotBatch`.
- Produces: `ApplyBatch`, `Complete`, `Reset`, `ZoomTo`, `NavigateToAncestor`, `WeightMode`, and immutable observable tile snapshots.

- [ ] Write failing tests for batch deduplication, size/count aggregation, mode switching, generation reset, zoom breadcrumb, and completed/partial state.
- [ ] Run the focused App tests and verify RED because the view model is absent.
- [ ] Implement the minimal focused view model; calculate snapshots off-dispatcher and reject stale generations.
- [ ] Verify focused tests GREEN, then run all App tests.
- [ ] Commit Task 2 files only.

### Task 3: Efficient themed WPF renderer

**Files:**
- Create: `src/HanabePhotoManager.App/Browsing/Treemap/PhotoTreemapControl.cs`
- Create: `src/HanabePhotoManager.App/Browsing/Treemap/PhotoTreemapAutomationPeer.cs`
- Modify: `src/HanabePhotoManager.App/HanabePhotoManager.App.csproj`
- Create: `tests/HanabePhotoManager.App.Tests/Browsing/Treemap/PhotoTreemapControlTests.cs`

**Interfaces:**
- Consumes: immutable tile snapshots from Task 2.
- Produces: bindable `ItemsSource`, `SelectedPath`, `OpenItemCommand`, and `ZoomCommand` dependency properties.

- [ ] Write failing tests for dependency-property ownership, hit testing, minimum thumbnail area, automation names, and no per-tile `FrameworkElement` materialization.
- [ ] Run the focused tests and confirm RED.
- [ ] Implement `FrameworkElement`-based drawing, hit testing, keyboard selection, and automation exposure using existing semantic brushes.
- [ ] Verify focused and full App tests GREEN.
- [ ] Commit Task 3 files only.

### Task 4: Browse integration and persistence

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/HanabePhotoManager.App/Services/AppSettingsStore.cs`
- Modify: `src/HanabePhotoManager.App/Services/LibraryDateSnapshotService.cs` only if completion metadata cannot be adapted without a contract change
- Modify: `docs/component-inventory.md`
- Modify: `tests/HanabePhotoManager.App.Tests/DateOpenPipelineTests.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/PreviewPerformanceTests.cs`
- Create: `tests/HanabePhotoManager.App.Tests/Browsing/Treemap/BrowseTreemapIntegrationTests.cs`

**Interfaces:**
- Consumes: Tasks 2-3.
- Produces: `BrowseDisplayMode`, `TreemapWeightMode`, and bindings that switch between the existing grid and `PhotoTreemapControl` without rescanning.

- [ ] Capture the existing Browse Binding/Command/event inventory in test assertions before modifying XAML.
- [ ] Add failing tests for mode controls, backward-compatible settings defaults, batch forwarding, selection synchronization, and preserved grid bindings.
- [ ] Run focused tests and verify RED for the missing integration.
- [ ] Add the two compact segmented selectors, bind the tree control, forward scan batches, and persist both choices.
- [ ] Verify focused App tests, full App tests, and Light/Dark resource parity.
- [ ] Commit Task 4 files and documentation.

### Task 5: Release and visual verification

**Files:**
- Modify only files required by defects proven during verification.
- Add screenshots under a new timestamped `.artifacts`/evidence directory; never overwrite prior evidence.

**Interfaces:**
- Consumes: complete feature.
- Produces: verified progressive behavior and regression evidence.

- [ ] Run Release solution build with warnings as errors.
- [ ] Run the full solution test suite without rebuilding.
- [ ] Publish and launch fresh output.
- [ ] Scan a disposable media tree and verify first-batch rendering, live resize, size/count switching, zoom/back, grid return, cancellation, empty/error states, Light/Dark, compact window, and 100%/150% DPI.
- [ ] Record exact automated counts and manual evidence, then commit only proven fixes/documentation.

