# 自定义文件夹（虚拟相册）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add persistent, read-only virtual references to user-selected photo folders with add, rename, remove, and image browsing workflows.

**Architecture:** Core owns the immutable album contract, Infrastructure owns durable JSON persistence, and App owns folder picker, image enumeration, thumbnails, ViewModel state, and WPF rendering. The existing MainWindow only receives a small navigation host and does not absorb the album workflow.

**Tech Stack:** .NET 8, C# 12, WPF, CommunityToolkit.Mvvm, xUnit, FluentAssertions.

## Global Constraints

- Removing an album removes only the app-local reference and never alters source files.
- Renaming changes only `DisplayName`.
- Persist only under LocalAppData; never OneDrive or the photo folder.
- Keep each new file focused and below 600 lines.

---

### Task 1: Core album contract

**Files:** Create `src/HanabePhotoManager.Core/Albums/CustomAlbum.cs`, `src/HanabePhotoManager.Core/Albums/ICustomAlbumStore.cs`; Test `tests/HanabePhotoManager.Core.Tests/Albums/CustomAlbumTests.cs`.

- [ ] Write a failing model-normalization test, run it, add the minimal record and contract, then rerun the focused test.

### Task 2: JSON persistence

**Files:** Create `src/HanabePhotoManager.Infrastructure/Albums/JsonCustomAlbumStore.cs`; Test `tests/HanabePhotoManager.Infrastructure.Tests/Albums/JsonCustomAlbumStoreTests.cs`.

- [ ] Write a failing round-trip and replacement test, run it, implement atomic JSON save/load, then rerun the focused test.

### Task 3: App scanner and ViewModel

**Files:** Create `src/HanabePhotoManager.App/Albums/CustomAlbumPhotoScanner.cs`, `CustomAlbumsViewModel.cs`, item view models; Test `tests/HanabePhotoManager.App.Tests/Albums/CustomAlbumPhotoScannerTests.cs`, `CustomAlbumsViewModelTests.cs`.

- [ ] Write failing scanner and rename/remove behavior tests, run them, implement the focused workflow, then rerun them.

### Task 4: WPF integration

**Files:** Create `src/HanabePhotoManager.App/Albums/CustomAlbumsPage.xaml` and code-behind; Modify `MainWindowViewModel.cs`, `MainWindow.xaml`.

- [ ] Add navigation/page hosting with existing theme tokens and explicit removal copy, build, and add structural App tests.

### Task 5: Documentation and release

**Files:** Modify `docs/current-status.md`, `docs/agent-change-log.md`.

- [ ] Run Release build (warnings as errors), full tests, requested self-contained publish to the supplied LocalAppData program directory, inspect output, commit and push.
