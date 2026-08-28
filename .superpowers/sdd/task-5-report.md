# Task 5 — Save Button Readability and Final Verification

## Status

自动化验证完成，交互验收待完成。`Buttons.xaml` 已检查但未改动：所需共享模板契约在加强测试前已经存在。

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

## Not Executed / Unverified

- No interactive UI automation session was available. The following remain unverified in this task: switching each selectable theme and visually checking Save All in Normal/Hover/Pressed/Focus/Disabled; keyboard traversal through the selector, row editors, Refresh, and Save All; actual UI import-preset switching and restart restoration; UI confirmation that import completion never opens remark dialogs.
- The corresponding behavior has automated regression coverage in the solution tests, but that does not replace the omitted interactive checks.

## Commit

Initial verification: `b47552d test: verify import naming and folder batch workflow`.

Review remediation: `0796f97 test: strengthen primary button theme contracts`.

## Concerns

The unverified items above are interactive-runtime acceptance checks, not automated test failures. No production button-template change was warranted by current source or the new contract test.
