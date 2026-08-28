# Import Naming and Date Folder Batch Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add remembered import naming presets and a safe page for batch-editing date-folder remarks, while reporting real filesystem outcomes and keeping save text readable.

**Architecture:** Keep `ImportNamingFormatter` and `ImportNamingTemplate` as the naming source of truth, adding a small preset mapping at the App presentation layer. Extend `LibraryDateFolderService` into a deterministic scan/rename boundary, then place batch state and commands in focused ViewModels consumed by a new WPF page hosted by the existing shell.

**Tech Stack:** .NET 8, C# 12, WPF, CommunityToolkit.Mvvm, xUnit, FluentAssertions

## Global Constraints

- Work only under `D:\HanabePhoto`; never write project artifacts to OneDrive.
- Dates are read-only; the feature edits remarks only and never moves a folder across dates or months.
- Existing custom import templates remain valid and are represented as `Custom`, not overwritten on load.
- Never overwrite or merge an existing target directory.
- Preserve import copy/move, verification, resume, duplicate handling, category grouping, and sequence behavior.
- Reuse design tokens and shared controls; do not introduce page-level color constants or another button template.
- Follow red-green-refactor for every behavior change and append executable changes to `docs/agent-change-log.md`.

---

## File Structure

- Modify `src/HanabePhotoManager.Core/Imports/ImportNamingFormatter.cs`: expose canonical preset templates without changing formatting semantics.
- Create `src/HanabePhotoManager.App/Imports/ImportNamingPreset.cs`: preset value/display/template mapping and custom-template detection.
- Modify `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`: expose preset choices, persist selection through the existing template setting, navigate to folder management, and remove sequential remark prompts.
- Modify `src/HanabePhotoManager.App/MainWindow.xaml`: add import-page preset selector and host/navigation wiring.
- Modify `src/HanabePhotoManager.App/Services/LibraryDateFolderService.cs`: scan date folders and return explicit rename outcomes.
- Create `src/HanabePhotoManager.App/DateFolders/DateFolderItemViewModel.cs`: one editable folder row and its status.
- Create `src/HanabePhotoManager.App/DateFolders/DateFolderManagementViewModel.cs`: refresh and batch-save orchestration.
- Create `src/HanabePhotoManager.App/DateFolders/DateFolderManagementPage.xaml` and `.xaml.cs`: token-based batch editor UI.
- Modify `src/HanabePhotoManager.App/Themes/Controls/Buttons.xaml` only if a failing theme contract test proves the shared primary content foreground is not inherited in a required state.
- Add focused tests under `tests/HanabePhotoManager.Core.Tests/Imports` and `tests/HanabePhotoManager.App.Tests`.
- Modify `docs/agent-change-log.md` after executable verification.

---

### Task 1: Import Naming Presets

**Files:**
- Modify: `src/HanabePhotoManager.Core/Imports/ImportNamingFormatter.cs`
- Create: `src/HanabePhotoManager.App/Imports/ImportNamingPreset.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Test: `tests/HanabePhotoManager.Core.Tests/Imports/ImportNamingFormatterTests.cs`
- Create: `tests/HanabePhotoManager.App.Tests/ImportNamingPresetTests.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/AppSettingsStoreTests.cs`

**Interfaces:**
- Produces: `ImportNamingPresetKind`, `ImportNamingPreset`, `ImportNamingPreset.Resolve(string)`, `SelectedImportNamingPreset`, `ImportNamingPresets`.
- Consumes: existing `ImportNamingTemplate`, `AppSettings.ImportNamingTemplate`, and `ImportPlanBuilder.BuildAsync(..., namingTemplate)`.

- [ ] **Step 1: Write failing formatter and preset tests**

```csharp
[Fact]
public void Format_SequenceAndOriginalPreset_UsesFullWidthParentheses()
{
    ImportNamingFormatter.Format(
        ImportNamingFormatter.SequenceAndOriginalTemplate,
        1,
        "DSC_1234",
        new LibraryDate(2026, 8, 28))
        .Should().Be("JK0001（DSC_1234）");
}

[Theory]
[InlineData("JK{seq}", ImportNamingPresetKind.Sequence)]
[InlineData("{orig}", ImportNamingPresetKind.Original)]
[InlineData("JK{seq}（{orig}）", ImportNamingPresetKind.SequenceAndOriginal)]
[InlineData("{date}_{orig}", ImportNamingPresetKind.Custom)]
public void Resolve_MapsKnownTemplatesAndPreservesCustom(string template, ImportNamingPresetKind expected)
{
    ImportNamingPreset.Resolve(template).Kind.Should().Be(expected);
}
```

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
dotnet test tests/HanabePhotoManager.Core.Tests/HanabePhotoManager.Core.Tests.csproj -c Release --filter FullyQualifiedName~ImportNamingFormatterTests
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter FullyQualifiedName~ImportNamingPresetTests
```

Expected: FAIL because `SequenceAndOriginalTemplate`, `ImportNamingPresetKind`, and `ImportNamingPreset` do not exist.

- [ ] **Step 3: Add the canonical constants and preset mapping**

```csharp
public const string SequenceTemplate = "JK{seq}";
public const string OriginalTemplate = "{orig}";
public const string SequenceAndOriginalTemplate = "JK{seq}（{orig}）";

public enum ImportNamingPresetKind { Sequence, Original, SequenceAndOriginal, Custom }

public sealed record ImportNamingPreset(ImportNamingPresetKind Kind, string DisplayName, string Template)
{
    public static ImportNamingPreset Resolve(string? template) =>
        All.FirstOrDefault(item => string.Equals(item.Template, template, StringComparison.OrdinalIgnoreCase))
        ?? new(ImportNamingPresetKind.Custom, "自定义", template ?? ImportNamingFormatter.DefaultTemplate);
}
```

Expose the three selectable presets through `ImportNamingPresets`. Set `SelectedImportNamingPreset` by assigning its `Template` to the existing `ImportNamingTemplate`; on initialization resolve the saved template without replacing custom values.

- [ ] **Step 4: Add import-page selector and persistence test**

```xml
<ComboBox ItemsSource="{Binding ImportNamingPresets}"
          DisplayMemberPath="DisplayName"
          SelectedItem="{Binding SelectedImportNamingPreset}"
          AutomationProperties.Name="导入文件命名方式" />
```

Add an App settings round-trip test that saves `ImportNamingFormatter.SequenceAndOriginalTemplate`, reloads it, and asserts exact equality.

- [ ] **Step 5: Run focused tests and verify GREEN**

```powershell
dotnet test tests/HanabePhotoManager.Core.Tests/HanabePhotoManager.Core.Tests.csproj -c Release --filter FullyQualifiedName~ImportNamingFormatterTests
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter "FullyQualifiedName~ImportNamingPresetTests|FullyQualifiedName~AppSettingsStoreTests"
```

Expected: all selected tests pass.

- [ ] **Step 6: Commit the naming preset slice**

```powershell
git add src/HanabePhotoManager.Core/Imports/ImportNamingFormatter.cs src/HanabePhotoManager.App/Imports/ImportNamingPreset.cs src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs src/HanabePhotoManager.App/MainWindow.xaml tests/HanabePhotoManager.Core.Tests/Imports/ImportNamingFormatterTests.cs tests/HanabePhotoManager.App.Tests/ImportNamingPresetTests.cs tests/HanabePhotoManager.App.Tests/AppSettingsStoreTests.cs
git commit -m "feat: add remembered import naming presets"
```

---

### Task 2: Reliable Date Folder Scan and Rename Outcomes

**Files:**
- Modify: `src/HanabePhotoManager.App/Services/LibraryDateFolderService.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/LibraryDateFolderServiceTests.cs`

**Interfaces:**
- Produces: `DateFolderEntry`, `DateFolderRenameStatus`, `DateFolderRenameResult`, `Scan(string)`, `RenameRemark(string, string)`.
- Consumes: existing `TryParseName` parsing rules and Windows `Directory.Move`.

- [ ] **Step 1: Write failing outcome tests using test-owned directories**

```csharp
[Fact]
public void RenameRemark_ReturnsSourceMissingInsteadOfSuccess()
{
    var result = LibraryDateFolderService.RenameRemark(Path.Combine(_root, "08月", "08.28"), "婚礼");
    result.Status.Should().Be(DateFolderRenameStatus.SourceMissing);
}

[Fact]
public void RenameRemark_DoesNotOverwriteExistingTarget()
{
    var month = Directory.CreateDirectory(Path.Combine(_root, "08月")).FullName;
    var source = Directory.CreateDirectory(Path.Combine(month, "08.28")).FullName;
    Directory.CreateDirectory(Path.Combine(month, "08.28_婚礼"));
    LibraryDateFolderService.RenameRemark(source, "婚礼").Status
        .Should().Be(DateFolderRenameStatus.TargetExists);
    Directory.Exists(source).Should().BeTrue();
}

[Fact]
public void RenameRemark_CanClearAnExistingRemark()
{
    var source = Directory.CreateDirectory(Path.Combine(_root, "08月", "08.28_婚礼")).FullName;
    var result = LibraryDateFolderService.RenameRemark(source, "");
    result.Status.Should().Be(DateFolderRenameStatus.Success);
    Path.GetFileName(result.EffectivePath).Should().Be("08.28");
}
```

- [ ] **Step 2: Run service tests and verify RED**

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter FullyQualifiedName~LibraryDateFolderServiceTests
```

Expected: FAIL because the outcome types and methods do not exist.

- [ ] **Step 3: Implement scan and explicit outcomes**

```csharp
public enum DateFolderRenameStatus { Success, NoChange, SourceMissing, TargetExists, Failed }

public sealed record DateFolderRenameResult(
    DateFolderRenameStatus Status,
    string SourcePath,
    string EffectivePath,
    string? ErrorMessage = null);

public sealed record DateFolderEntry(int Month, int Day, string Remark, string FullPath);
```

`Scan` must enumerate only direct month/date directories under the supplied library root, parse with `TryParseName`, normalize the editable remark by trimming the suffix separators, and return chronological order without renaming anything. `RenameRemark` must derive the date prefix from the source name, sanitize the remark, check source and target, call `Directory.Move` once, and map `IOException`/`UnauthorizedAccessException` to `Failed` with the exception message.

- [ ] **Step 4: Run service tests and verify GREEN**

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter FullyQualifiedName~LibraryDateFolderServiceTests
```

Expected: all `LibraryDateFolderServiceTests` pass, including existing normalization tests.

- [ ] **Step 5: Commit the filesystem boundary**

```powershell
git add src/HanabePhotoManager.App/Services/LibraryDateFolderService.cs tests/HanabePhotoManager.App.Tests/LibraryDateFolderServiceTests.cs
git commit -m "fix: report date folder rename outcomes"
```

---

### Task 3: Batch Date Folder Management ViewModel

**Files:**
- Create: `src/HanabePhotoManager.App/DateFolders/DateFolderItemViewModel.cs`
- Create: `src/HanabePhotoManager.App/DateFolders/DateFolderManagementViewModel.cs`
- Create: `tests/HanabePhotoManager.App.Tests/DateFolderManagementViewModelTests.cs`

**Interfaces:**
- Consumes: `LibraryDateFolderService.Scan` and `LibraryDateFolderService.RenameRemark` from Task 2.
- Produces: `Items`, `RefreshCommand`, `SaveAllCommand`, `Summary`, `IsBusy`, and per-row `EditedRemark`, `IsDirty`, `StatusText`.

- [ ] **Step 1: Write failing batch behavior tests**

Use a replaceable service interface or delegates so tests exercise real orchestration without touching a user's library.

```csharp
[Fact]
public async Task SaveAllAsync_ContinuesAfterOneRowFailsAndKeepsFailedEdit()
{
    var service = new RecordingDateFolderService(
        results: [DateFolderRenameStatus.Success, DateFolderRenameStatus.TargetExists]);
    var vm = CreateViewModel(service, TwoDirtyEntries());

    await vm.SaveAllAsync();

    vm.Summary.Should().Be("保存完成：成功 1，跳过 0，失败 1");
    vm.Items[0].IsDirty.Should().BeFalse();
    vm.Items[1].IsDirty.Should().BeTrue();
    vm.Items[1].StatusText.Should().Contain("目标文件夹已存在");
}
```

- [ ] **Step 2: Run ViewModel tests and verify RED**

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter FullyQualifiedName~DateFolderManagementViewModelTests
```

Expected: FAIL because the ViewModels and service abstraction do not exist.

- [ ] **Step 3: Implement row state and batch orchestration**

`DateFolderItemViewModel` derives from `ObservableObject`, stores original and edited remarks, and recomputes `IsDirty`. Successful save updates `FullPath` and original remark. Failed save retains `EditedRemark`.

`DateFolderManagementViewModel` owns an `ObservableCollection<DateFolderItemViewModel>`, refreshes from the current `LibraryRoot`, calls rename only for dirty rows, prevents re-entry via `IsBusy`, and produces exact success/skipped/failed counts. Empty library roots yield a clear summary and no filesystem calls.

- [ ] **Step 4: Run ViewModel tests and verify GREEN**

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter FullyQualifiedName~DateFolderManagementViewModelTests
```

Expected: all batch tests pass.

- [ ] **Step 5: Commit the batch ViewModel slice**

```powershell
git add src/HanabePhotoManager.App/DateFolders tests/HanabePhotoManager.App.Tests/DateFolderManagementViewModelTests.cs
git commit -m "feat: add batch date folder editing model"
```

---

### Task 4: Shell Page, Import Completion Entry, and Removal of Sequential Dialogs

**Files:**
- Create: `src/HanabePhotoManager.App/DateFolders/DateFolderManagementPage.xaml`
- Create: `src/HanabePhotoManager.App/DateFolders/DateFolderManagementPage.xaml.cs`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/NavigationOrderPolicyTests.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/NavigationMotionTests.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Interfaces:**
- Consumes: `DateFolderManagementViewModel` from Task 3 and current shell navigation conventions.
- Produces: `ShowDateFoldersCommand`, `IsDateFoldersPage`, shell page key `DateFolders`, and an import-result entry command.

- [ ] **Step 1: Write failing navigation and XAML contract tests**

```csharp
viewModel.ShowDateFoldersCommand.Execute(null);
viewModel.IsDateFoldersPage.Should().BeTrue();

mainXaml.Should().Contain("DateFolderManagementPageHost");
mainXaml.Should().Contain("AutomationProperties.Name=\"保存全部备注更改\"");
mainXaml.Should().Contain("Command=\"{Binding SaveAllCommand}\"");
```

Add a source contract assertion that `AskForDateRemarksAsync` and `new RemarkPromptWindow` are no longer present in the import completion path.

- [ ] **Step 2: Run focused UI tests and verify RED**

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter "FullyQualifiedName~NavigationOrderPolicyTests|FullyQualifiedName~NavigationMotionTests|FullyQualifiedName~ControlThemeTests"
```

Expected: FAIL because page key, host, command, and controls do not exist.

- [ ] **Step 3: Build the token-based page and shell wiring**

The page uses a header with Refresh and Save All, followed by a virtualized rows control. Each row binds read-only date/full folder name, editable remark, and status. Use existing styles such as `Button.Primary`, `Button.Secondary`, `Input.TextBox`, semantic brushes, spacing, radii, and typography tokens.

Add `DateFolders` to the existing navigation item factory and page visibility/title/subtitle mappings. When entering the page, call refresh. Replace post-import `AskForDateRemarksAsync` with a non-blocking result hint and `ShowDateFoldersCommand`; do not automatically navigate away from the import report.

- [ ] **Step 4: Run UI tests and Release build**

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter "FullyQualifiedName~NavigationOrderPolicyTests|FullyQualifiedName~NavigationMotionTests|FullyQualifiedName~ControlThemeTests|FullyQualifiedName~DateFolderManagementViewModelTests"
dotnet build HanabePhotoManager.sln -c Release /warnaserror
```

Expected: selected tests pass; build exits 0 with 0 warnings and 0 errors.

- [ ] **Step 5: Commit the page and workflow**

```powershell
git add src/HanabePhotoManager.App/DateFolders src/HanabePhotoManager.App/MainWindow.xaml src/HanabePhotoManager.App/MainWindow.xaml.cs src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs tests/HanabePhotoManager.App.Tests/NavigationOrderPolicyTests.cs tests/HanabePhotoManager.App.Tests/NavigationMotionTests.cs tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs
git commit -m "feat: add date folder batch management page"
```

---

### Task 5: Save Button Readability and Final Verification

**Files:**
- Modify if required by failing evidence: `src/HanabePhotoManager.App/Themes/Controls/Buttons.xaml`
- Modify: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/DesignSystemResourceTests.cs`
- Modify: `docs/agent-change-log.md`

**Interfaces:**
- Consumes: shared `Button.Primary` and all supported theme brush contracts.
- Produces: a tested primary-button foreground contract for normal and disabled content.

- [ ] **Step 1: Add a failing shared-style contract test before changing the template**

```csharp
buttonsXaml.Should().Contain("Foreground=\"{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}\"");
buttonsXaml.Should().Contain("TargetName=\"PrimaryContent\"");
buttonsXaml.Should().Contain("Brush.OnPrimary");
```

If this test already passes, do not edit the shared template. Instead, keep the new page's button content as a plain string and validate the real themes manually; only change `Buttons.xaml` when a reproducible state demonstrates the contract is insufficient.

- [ ] **Step 2: Run App theme tests**

```powershell
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter "FullyQualifiedName~ControlThemeTests|FullyQualifiedName~DesignSystemResourceTests"
```

Expected: theme resource and button contract tests pass.

- [ ] **Step 3: Run complete automated verification**

```powershell
dotnet build HanabePhotoManager.sln -c Release /warnaserror
dotnet test HanabePhotoManager.sln -c Release --no-build
```

Expected: build exits 0 with 0 warnings/errors; all solution tests pass with 0 failures.

- [ ] **Step 4: Perform affected workflow smoke checks**

Use disposable folders under a test-owned directory on `D:`. Verify:

- import page switches among all three presets and restart restores the last choice;
- combined naming preview/output is `JK0001（DSC_1234）.ARW`;
- date folder page loads all supported date directories and edits several remarks in one save;
- clearing a remark returns the folder to `MM.DD`;
- target conflict and externally removed source show per-row failure without false success;
- import completion does not open a sequence of remark dialogs;
- Save All text is readable in Light, Dark, and every currently selectable theme for Normal, Hover, Pressed, Focus, and Disabled;
- keyboard navigation reaches the selector, row editors, Refresh, and Save All.

- [ ] **Step 5: Append the verified change log entry**

Record files changed, behavior delivered, exact build/test totals, and any manual checks that could not be reached in `docs/agent-change-log.md`.

- [ ] **Step 6: Commit verification documentation**

```powershell
git add tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs tests/HanabePhotoManager.App.Tests/DesignSystemResourceTests.cs src/HanabePhotoManager.App/Themes/Controls/Buttons.xaml docs/agent-change-log.md
git commit -m "test: verify import naming and folder batch workflow"
```

Do not stage `Buttons.xaml` if no evidence-based modification was required.
