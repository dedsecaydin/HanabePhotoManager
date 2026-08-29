# Import Date Folder Preflight Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pause after source analysis and let the user batch-confirm the exact target folder for every import date before any media is copied.

**Architecture:** Add an App-layer preflight model/service that scans same-date folders and resolves one explicit target directory per `LibraryDate`. The import ViewModel owns a preflight phase and freezes a date-to-directory map; Core planning accepts an explicit date directory, and resume entries persist that directory. The existing date-folder service remains the single naming/sanitizing authority.

**Tech Stack:** .NET 8, C# 12, WPF, CommunityToolkit.Mvvm, xUnit, FluentAssertions.

## Global Constraints

- Work only under `D:\HanabePhoto` and its D-drive worktree; never OneDrive.
- No media copy or move may begin before every selected date has a valid explicit decision.
- Dates and detected folders are read-only; only remarks and conflict strategies are editable.
- Existing same-date folders require one explicit strategy: create separate, rename selected existing folder, or use selected existing folder unchanged.
- Reuse `LibraryDateFolderService` for parsing, sanitizing, scanning, and renaming.
- Preserve the independent post-import date-folder management page.
- Preserve all unrelated user files and repository changes.

---

### Task 1: Explicit Date Directory Planning Contract

**Files:**
- Modify: `src/HanabePhotoManager.Core/Imports/ImportPlanBuilder.cs`
- Test: `tests/HanabePhotoManager.Core.Tests/Imports/ImportPlanBuilderTests.cs`

**Interfaces:**
- Produces: `ImportPlanBuilder.BuildAsync(..., string? namingTemplate = null, string? explicitDateDirectory = null)`.
- Rule: when supplied, the normalized fully qualified directory replaces `Path.Combine(root, date.RelativePath)` for sequence probing and destination construction.

- [ ] Add a failing test that passes `D:\Library\08月\08.19_客户` and expects every planned file below that directory.
- [ ] Run the focused Core test and confirm it fails because the overload/parameter does not exist.
- [ ] Add and validate `explicitDateDirectory`; use it consistently for sequence discovery and destinations while preserving the current default.
- [ ] Run all `ImportPlanBuilderTests` and confirm they pass.
- [ ] Commit with `feat: support explicit import date directories`.

### Task 2: Preflight Decision Model and Resolver

**Files:**
- Create: `src/HanabePhotoManager.App/Imports/ImportDateFolderDecision.cs`
- Create: `src/HanabePhotoManager.App/Imports/ImportDateFolderPreflightService.cs`
- Test: `tests/HanabePhotoManager.App.Tests/ImportDateFolderPreflightServiceTests.cs`

**Interfaces:**
- Produces enum `ImportDateFolderStrategy { Unselected, CreateSeparate, RenameExisting, UseExisting }`.
- Produces immutable candidate `(Name, FullPath, Remark)` and mutable decision properties for date, count, candidates, selected candidate, remark, strategy, preview path, validation message, and validity.
- Produces service methods to scan candidates, normalize/preview a decision, validate all decisions against current disk state, and apply required renames before import.

- [ ] Write failing tests for no-existing-folder empty/nonempty remarks and sanitized preview.
- [ ] Run focused tests and verify expected type-not-found failures.
- [ ] Implement the minimal decision and scan/preview logic using `LibraryDateFolderService`.
- [ ] Write failing tests for all three existing-folder strategies, multiple candidates, missing selection, duplicate target, missing source, and rename failure.
- [ ] Run focused tests and confirm the behavioral failures.
- [ ] Implement validation and rename application; never create media/category directories during preflight.
- [ ] Run the focused suite and commit with `feat: add import date folder preflight decisions`.

### Task 3: Import State Machine, Frozen Mapping, and Resume

**Files:**
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/HanabePhotoManager.App/Services/ImportResumeStore.cs`
- Test: `tests/HanabePhotoManager.App.Tests/ImportDateFolderWorkflowTests.cs`
- Test: `tests/HanabePhotoManager.App.Tests/ImportResumeStoreTests.cs`

**Interfaces:**
- Produces `ObservableCollection<ImportDateFolderDecision> ImportDateFolderDecisions` and `IsImportDateFolderPreflightVisible`.
- Produces commands `ConfirmImportDateFoldersCommand` and `BackFromImportDateFoldersCommand`.
- Extends `ImportResumeEntry` with `TargetDateDirectory`.
- Consumes Task 1 explicit directory and Task 2 decisions.

- [ ] Add failing state tests: analysis prepares decisions and does not call/run import; unresolved media still blocks preflight; Back performs no filesystem/media action.
- [ ] Verify the tests fail against the current auto-import flow.
- [ ] Change `AnalyzeAndImportAsync` to analyze then enter preflight; split confirmation from `ImportSelectedAsync` and invalidate decisions whenever sources, selection, category, date, library root, or a fresh analysis changes.
- [ ] Add failing tests that confirmation freezes one directory per date and passes it into duplicate scan, plan construction, and per-date execution.
- [ ] Implement mapping use in `RunImportAsync`/`RunImportDateAsync`; use explicit directory for duplicate size maps and plan construction.
- [ ] Add failing resume tests for JSON round-trip and missing restored target directory.
- [ ] Persist `TargetDateDirectory`; resume only against the stored directory and stop with a re-confirmation message if it is missing.
- [ ] Run affected App tests and commit with `feat: gate imports on date folder preflight`.

### Task 4: Batch Confirmation UI

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Test: `tests/HanabePhotoManager.App.Tests/ImportWorkflowUiTests.cs`
- Test: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Interfaces:**
- Binds Task 3 decisions and commands.
- Uses the three full Chinese strategy labels and exposes automation names for row controls and page actions.

- [ ] Add failing XAML contract tests for the preflight heading, scrollable decision list, read-only date/count, candidate selector, remark editor, three strategies, final path preview, validation text, Back, and Confirm buttons.
- [ ] Run the focused UI tests and confirm missing-contract failures.
- [ ] Add the preflight surface inside the center import workspace; hide the normal queue list while preflight is active and keep bottom actions visible outside the scrolling list.
- [ ] Bind row changes to immediate preview/validation refresh and command invalidation.
- [ ] Run UI/resource tests and commit with `feat: add batch date folder confirmation UI`.

### Task 5: Regression, Documentation, Publish, and Runtime Smoke

**Files:**
- Modify: `docs/current-status.md`
- Modify: `docs/agent-change-log.md`
- Modify: `AGENT_HANDOFF.md`

**Interfaces:**
- Verifies Tasks 1-4 as one workflow.

- [ ] Run focused Core/App preflight, resume, import workflow, theme, and resource tests.
- [ ] Run `dotnet build HanabePhotoManager.sln -c Release /warnaserror` and require 0 warnings/0 errors.
- [ ] Run `dotnet test HanabePhotoManager.sln -c Release --no-build` and require 0 failures.
- [ ] Use a disposable D-drive library with three dates to verify no-existing, one-existing, and multiple-existing rows plus all three strategies; do not use the real photo library.
- [ ] Check keyboard traversal and Light/Dark readability; record any unexecuted interaction honestly.
- [ ] Update status, handoff, and append-only agent change log with exact evidence.
- [ ] Stop only the running process whose executable is `D:\hanabe-publish-v2\HanabePhotoManager.App.exe`, publish the verified self-contained win-x64 app over `D:\hanabe-publish-v2`, and launch that exact executable.
- [ ] Commit documentation with `docs: record import preflight verification`.

