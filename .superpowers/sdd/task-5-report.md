# Task 5 — Save Button Readability and Final Verification

## Status

自动化验证与可达的交互验收已完成。`Buttons.xaml` 已检查但未改动：所需共享模板契约在加强测试前已经存在。

## Diff

- `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`
  - Limits the primary-button assertions to `Button.PrimaryTextTemplate`, `Button.Primary`, and its `ControlTemplate`; verifies the normal foreground binding / `Brush.OnPrimary`, `PrimaryContent`, and the disabled-trigger `PrimaryContent → Brush.Text.Tertiary` relationship.
  - Proves the assertion rejects an isolated copy with the disabled `PrimaryContent` foreground override removed.
- `tests/HanabePhotoManager.App.Tests/DesignSystemResourceTests.cs`
  - Checks `Color.Primary`, `Color.OnPrimary`, `Color.Text.Tertiary`, and `Color.Surface.Disabled` in all eight current color dictionaries: Dynamic/Forest/Violet/Classic × Light/Dark.
  - Retains the Light/Dark shared semantic-brush contract for `Brush.Text.Tertiary`, `Brush.Primary`, and `Brush.OnPrimary`.
- `docs/agent-change-log.md`
  - Recorded implementation evidence, exact totals, smoke coverage, and omitted interactive checks.
- `src/HanabePhotoManager.App/Themes/Controls/Buttons.xaml`
  - No change; source already satisfied the required contract.

## TDD / Contract Evidence

The strengthened contract was applied before any production-template change. Its isolated-copy test removes the disabled `PrimaryContent` foreground override and proves that the contract fails; the unchanged real template then passes. No evidence justified changing `Buttons.xaml`.

## Automated Verification

| Command | Result |
|---|---|
| `dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter "FullyQualifiedName~ControlThemeTests|FullyQualifiedName~DesignSystemResourceTests"` | 51 passed, 0 failed, 0 skipped |
| `dotnet build HanabePhotoManager.sln -c Release /warnaserror` | Exit 0; 0 warnings, 0 errors |
| `dotnet test HanabePhotoManager.sln -c Release --no-build` | Core 160, Infrastructure 55, App 454, InstallerShell 12; 681 passed, 0 failed, 0 skipped |

## Executed Smoke Test

Using only disposable folders under `D:\HanabePhoto-Task5-Smoke-01a046df-02` and the Release binaries:

- Scanned three supported date folders.
- Renamed a remark and then cleared it back to `MM.DD`.
- Verified target-conflict and missing-source results without a false success.
- Verified the actual final filename from Release `ImportPlanBuilder.BuildRenamedFileName`: `JK0001（DSC_1234）.ARW`.
- Started the Release WPF application with isolated `APPDATA` and `LOCALAPPDATA`, then closed it normally (exit code 0).
- Deleted all three test-owned D: smoke directories after the checks.

## Interactive WPF Verification

The Release app was launched with isolated settings under the worktree and an empty test library. Windows UI Automation verified that the date-folder page exposes Refresh and Save All with automation names, and that Tab moves from Refresh to `保存全部备注更改`.

Save All normal-state text was visually checked and remained readable in all eight selectable themes: Dynamic/Forest/Violet/Classic × Light/Dark. Focused-state text was also checked in Light and Classic Dark. The app was closed normally after the checks.

## Not Executed / Unverified

- Hover and Pressed are transient states and Disabled requires an in-flight batch; those three states were not captured interactively. Their foreground relationships are covered by the strengthened shared-template contract tests across all eight theme color dictionaries.
- The empty isolated library had no editable date row, so row-editor Tab traversal was not exercised interactively. UI-level import-preset restart and a real completed import without remark dialogs were also not executed; their state and source contracts remain covered by automated tests.

## Commit

Initial verification: `b47552d test: verify import naming and folder batch workflow`.

Review remediation: `0796f97 test: strengthen primary button theme contracts`.

## Concerns

No production button-template change was warranted by current source, automated contract evidence, or the completed normal/focus visual checks.
