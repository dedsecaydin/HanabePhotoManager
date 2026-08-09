# Import Features — Module Plan

## Scope and regression finding

The `SourceAutoImportDropTarget_Drop` path has accepted the complete WPF
`FileDrop` array since the initial UI commit (`5fbfdf1`), so drag-and-drop did
not lose multi-file support. The selectable-source path is the regression:
`BrowseSourceAsync` only invokes `FolderBrowserDialog`, which cannot return
individual files or Ctrl/Shift selections. History contains no import-source
`OpenFileDialog` path to preserve; the folder-only source-picker simplification
is the effective regression. The repair adds an explicit multi-file picker and
routes its complete `FileNames` collection through the existing multi-root
analysis pipeline.

## Import improvements module split

| Module | Responsibility | Files | Dependencies |
|---|---|---|---|
| Core progress contract | Deterministic x/N, percentage, terminal summary state; no WPF types | `src/HanabePhotoManager.Core/Imports/ImportProgress.cs`, `tests/HanabePhotoManager.Core.Tests/Imports/ImportProgressTests.cs` | Core only |
| Multi-source picker adapter | Present a Windows file picker with `Multiselect=true`; return selected paths without import policy | `src/HanabePhotoManager.App/Imports/ImportSourcePicker.cs`, `tests/HanabePhotoManager.App.Tests/Imports/ImportSourcePickerTests.cs` | App / WinForms |
| Batch duplicate decision | Express batch choices and resolve whether a transfer is allowed; preserve per-item choice when requested | `src/HanabePhotoManager.App/Duplicates/ImportDuplicateBatchDecisionPolicy.cs`, `src/HanabePhotoManager.App/Duplicates/ImportDuplicateBatchDecisionWindow.xaml`, `.xaml.cs`, matching App tests | App UI, existing single-item duplicate dialog |
| Import orchestration bridge | Build one duplicate preflight, coordinate existing plan/transfer services, update progress and summary; remain a focused partial ViewModel file | `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.Import.cs` | Core imports, Infrastructure file services, App picker/duplicate UI |
| Existing shell integration | Expose the picker command and render an import-only progress/cancel surface using existing tokens/styles | `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`, `src/HanabePhotoManager.App/MainWindow.xaml` | Existing commands and progress state |
| Library hash batch lookup | Reuse existing size map and SHA-256 implementation; add only a batch-oriented lookup if profiling/tests show repeated hashing | `src/HanabePhotoManager.Infrastructure/Files/LibraryContentScanner.cs`, Infrastructure tests | Core `IFileHasher` |

### Interface contracts

- `ImportProgress`: `Create(totalUnits)`, `Complete(units)` and `Cancel()` produce a bounded percentage plus completed/total counts.
- `IImportSourcePicker`: `PickFiles()` returns the exact `OpenFileDialog.FileNames` selection; `WinFormsImportSourcePicker` sets `Multiselect = true`.
- `ImportDuplicateBatchDecisionPolicy.ShouldPromptIndividually(decision)` and `ShouldTransfer(decision)` keep batch policy deterministic and testable.
- `ImportDuplicateBatchDecisionWindow`: accepts detected duplicate rows and returns `SkipAll`, `ImportAll`, or `DecideIndividually`.
- `MainWindowViewModel.Import.cs`: owns only import-specific command handlers/orchestration; `MainWindowViewModel.cs` retains composition and common application state.

## Implementation order

1. Core progress contract and unit tests.
2. File-picker adapter and command wiring; selected `FileNames` reuse `AnalyzeSourcePathsAsync`.
3. Batch duplicate policy/dialog and tests.
4. Move/implement import orchestration in `MainWindowViewModel.Import.cs`; preflight size then SHA-256 once per duplicate candidate, apply one batch decision, and update terminal summary.
5. Add import-page progress and cancellation presentation with existing `ProgressBar` and button resources.
6. Run focused tests, Release build and full test suite in `.artifacts/agent-verification`; update status/change log, commit and push.

## File-size guard

No new file may exceed 600 lines. The existing oversized `MainWindowViewModel.cs`
will receive only composition-level changes; all new import behavior belongs in
the focused import partial and feature files above.
