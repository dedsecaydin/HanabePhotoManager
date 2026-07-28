# Watermark Folder Batch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an isolated folder-batch mode to the existing watermark page while preserving the current image-list workflow.

**Architecture:** A focused folder-batch service discovers supported images and maps each source to an output directory that preserves its relative folder structure. `WatermarkViewModel` owns the new mode state and reuses `WatermarkExportService` for the actual image processing; the existing `Items` and `ExportAsync` path remain unchanged.

**Tech Stack:** .NET 8, C# 12, WPF, CommunityToolkit.Mvvm, xUnit, ImageSharp

## Global Constraints

- Do not rewrite or alter the behavior of the existing single/multiple-image watermark workflow.
- Folder scanning and processing must be asynchronous and cancelable.
- Hidden files/directories, unsupported files, and the output tree must be excluded.
- Existing files are never overwritten; name collisions append a numeric suffix.
- Do not commit or push.

---

### Task 1: Folder discovery and output mapping

**Files:**
- Create: `src/HanabePhotoManager.App/Watermark/WatermarkFolderBatchService.cs`
- Create: `tests/HanabePhotoManager.App.Tests/WatermarkFolderBatchServiceTests.cs`

**Interfaces:**
- Produces: `WatermarkFolderBatchService.ScanAsync(...)` returning source path, source root, and relative directory records.
- Produces: `WatermarkFolderBatchService.ProcessAsync(...)` that calls `WatermarkExportService` with a per-item output directory.

- [ ] Write tests for recursive/non-recursive discovery, hidden/output exclusion, preserved relative paths, unique names, progress, and cancellation.
- [ ] Run the focused tests and confirm they fail because the service does not exist.
- [ ] Implement the minimal service and scan/result models.
- [ ] Run the focused tests until they pass.

### Task 2: ViewModel integration

**Files:**
- Modify: `src/HanabePhotoManager.App/Watermark/WatermarkViewModel.cs`
- Create: `tests/HanabePhotoManager.App.Tests/WatermarkFolderBatchViewModelTests.cs`

**Interfaces:**
- Consumes: `WatermarkFolderBatchService`.
- Produces: mode selection, source-folder collection, scan counts, success/failure counts, folder output root, scan command, process command, and shared cancel command.

- [ ] Write tests that folder-mode state is independent from `Items` and that scan/process command eligibility tracks the new state.
- [ ] Run the focused tests and confirm expected failures.
- [ ] Add folder-mode observable state and async commands while reusing existing watermark settings.
- [ ] Run the focused tests until they pass.

### Task 3: WPF folder-mode controls

**Files:**
- Modify: `src/HanabePhotoManager.App/Watermark/WatermarkPage.xaml`

**Interfaces:**
- Consumes: the folder-mode ViewModel properties and commands from Task 2.

- [ ] Add a mode selector and a folder-mode panel containing source list, add/remove controls, output root picker, recursion switch, scan totals, progress, cancel, and start controls.
- [ ] Keep the current image-list panel and its footer bindings intact.
- [ ] Run App tests and the Release solution build to validate bindings/resources/XAML compilation.

### Task 4: Verification

**Files:**
- No production changes expected.

- [ ] Run focused watermark tests.
- [ ] Run the full Release solution build with warnings as errors.
- [ ] Run the full Release test suite without rebuilding.
- [ ] Inspect `git diff --check`, `git status --short`, and the final diff; confirm no commit or push occurred.
