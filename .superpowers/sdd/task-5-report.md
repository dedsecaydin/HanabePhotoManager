# Task 5 — Save Button Readability and Final Verification

## Status

Completed with automated verification and a disposable D: non-interactive smoke test. `Buttons.xaml` was inspected but not changed: the required shared-template contract was already present before the test was added.

## Diff

- `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`
  - Strengthened the primary-button contract to require the string-content foreground binding, the `PrimaryContent` target, and `Brush.OnPrimary`.
- `tests/HanabePhotoManager.App.Tests/DesignSystemResourceTests.cs`
  - Added `Brush.Text.Tertiary`, `Brush.Primary`, and `Brush.OnPrimary` to the Light/Dark shared semantic-brush contract.
- `docs/agent-change-log.md`
  - Recorded implementation evidence, exact totals, smoke coverage, and omitted interactive checks.
- `src/HanabePhotoManager.App/Themes/Controls/Buttons.xaml`
  - No change; source already satisfied the required contract.

## TDD / Contract Evidence

The required test was added before any production-template change. It passed immediately because the current template already contained the three required strings. Per the task brief, no evidence justified changing `Buttons.xaml`; therefore a RED production-code cycle did not apply.

## Automated Verification

| Command | Result |
|---|---|
| `dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter "FullyQualifiedName~ControlThemeTests|FullyQualifiedName~DesignSystemResourceTests"` | 50 passed, 0 failed, 0 skipped |
| `dotnet build HanabePhotoManager.sln -c Release /warnaserror` | Exit 0; 0 warnings, 0 errors |
| `dotnet test HanabePhotoManager.sln -c Release --no-build` | Core 160, Infrastructure 55, App 453, InstallerShell 12; 680 passed, 0 failed, 0 skipped |

## Executed Smoke Test

Using only disposable folders under `D:\HanabePhoto-Task5-Smoke-01a046df-02` and the Release binaries:

- Scanned three supported date folders.
- Renamed a remark and then cleared it back to `MM.DD`.
- Verified target-conflict and missing-source results without a false success.
- Verified the combined naming output: `JK0001（DSC_1234）`.
- Started the Release WPF application with isolated `APPDATA` and `LOCALAPPDATA`, then closed it normally (exit code 0).
- Deleted both test-owned D: smoke directories after the check.

## Not Executed / Unverified

- No interactive UI automation session was available. The following remain unverified in this task: switching each selectable theme and visually checking Save All in Normal/Hover/Pressed/Focus/Disabled; keyboard traversal through the selector, row editors, Refresh, and Save All; actual UI import-preset switching and restart restoration; UI confirmation that import completion never opens remark dialogs.
- The corresponding behavior has automated regression coverage in the solution tests, but that does not replace the omitted interactive checks.

## Commit

`b47552d test: verify import naming and folder batch workflow`

## Concerns

The unverified items above are interactive-runtime acceptance checks, not automated test failures. No production button-template change was warranted by current source or the new contract test.
