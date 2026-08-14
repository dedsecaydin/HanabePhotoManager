# Integrated Semantic Search Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate local CLIP semantic search into the existing browse filters and photo wall, removing the standalone search page.

**Architecture:** Keep `SemanticSearchViewModel` as the service coordinator and publish ranked file paths to `MainWindowViewModel`. The normal browse filter pipeline intersects its candidates with all existing filters and preserves semantic rank unless the query is cleared.

**Tech Stack:** .NET 8, C# 12, WPF, CommunityToolkit.Mvvm, xUnit, FluentAssertions

## Global Constraints

- Reuse `ClipSemanticSearchService`, `SqliteSemanticIndexStore`, and `ModelCatalog`; do not rewrite inference.
- Preserve treemap throttling from `214bfd9` and import behavior from `b935f84`.
- Use only existing resources defined by `docs/design-system.md`.
- Final Release build must have 0 warnings and 0 errors; all tests must pass.
- Publish self-contained win-x64 output to `C:\Users\fulia\AppData\Local\Programs\HanabePhotoManager`.

---

### Task 1: Semantic coordinator behavior

**Files:**
- Modify: `src/HanabePhotoManager.App/Search/SemanticSearchViewModel.cs`
- Test: `tests/HanabePhotoManager.App.Tests/SemanticSearchViewModelTests.cs`

**Interfaces:**
- Consumes: `ISemanticSearchService.EnsureIndexAsync`, `SearchAsync`, and `GetIndexStatus`
- Produces: ranked result paths and a notification that browse filtering can observe

- [ ] Write tests proving the first non-empty query ensures the index before searching and publishes ranked paths.
- [ ] Run the focused tests and confirm failure because the current query path calls only `SearchAsync`.
- [ ] Add the minimal ensure-before-search and result notification behavior.
- [ ] Run the focused tests and confirm they pass.

### Task 2: Browse filter integration

**Files:**
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Test: `tests/HanabePhotoManager.App.Tests/PreviewPerformanceTests.cs`

**Interfaces:**
- Consumes: semantic query, ranked file paths, busy/progress/status/cancel state
- Produces: existing `_filteredCache`, `VisiblePreviewFiles`, and treemap data in semantic order

- [ ] Write tests proving semantic candidates are intersected with rating/category filters and retain CLIP rank.
- [ ] Run focused tests and confirm failure because semantic results do not affect `ApplyFilters`.
- [ ] Subscribe to semantic result changes, add candidate intersection/ranking, and refresh the existing browse wall.
- [ ] Run focused tests and confirm they pass.

### Task 3: Browse UI and navigation

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Test: `tests/HanabePhotoManager.App.Tests/DesignSystemResourceTests.cs`
- Test: `tests/HanabePhotoManager.App.Tests/NavigationOrderPolicyTests.cs`

**Interfaces:**
- Consumes: `SemanticSearch.QueryText`, `IsBusy`, `ProgressValue`, `StatusText`, and `CancelCommand`
- Produces: accessible inline browse search and no standalone navigation destination

- [ ] Write XAML/navigation contract tests for the inline controls, Design System styles, and removed standalone page.
- [ ] Run focused tests and confirm failure against the current shell.
- [ ] Add the inline browse controls and remove the standalone sidebar/page wiring.
- [ ] Run focused tests and confirm they pass.

### Task 4: Documentation, verification, release

**Files:**
- Modify: `docs/current-status.md`
- Modify: `docs/features/semantic-search.md`
- Modify: `docs/agent-change-log.md`

- [ ] Update current-state and usage documents with the integrated workflow.
- [ ] Run `dotnet build HanabePhotoManager.sln -c Release /warnaserror`.
- [ ] Run `dotnet test HanabePhotoManager.sln -c Release --no-build`.
- [ ] Publish self-contained win-x64 output to the requested installation directory.
- [ ] Launch the published executable, sample CPU, inspect the browse page, and execute a real description query.
- [ ] Review `git diff`, commit only task-owned files, and push the current branch.
