# Build and Testing Standard

> **Purpose:** Define mandatory builds, automated tests, smoke tests, and publish decisions.  
> **Scope:** `HanabePhotoManager.sln`, four test projects, Windows WPF smoke testing, portable desktop checks, macOS ARM64 bundle smoke testing, and verification artifacts.
> **Audience:** Contributors and reviewers evaluating completion.  
> **References:** [architecture.md](architecture.md), [design-system.md](design-system.md), [Publish-Clean.ps1](../tools/Publish-Clean.ps1)

## Table of Contents

- [Baseline Commands](#baseline-commands)
- [Verification Matrix](#verification-matrix)
- [When to Build](#when-to-build)
- [When to Publish](#when-to-publish)
- [Smoke Test](#smoke-test)
- [Evidence Rules](#evidence-rules)

## Baseline Commands

Run from the repository root:

```powershell
dotnet restore HanabePhotoManager.sln
dotnet build HanabePhotoManager.sln -c Release /warnaserror
dotnet test HanabePhotoManager.sln -c Release --no-build
```

If a running WPF process locks normal output, use one isolated root for both build and test:

```powershell
dotnet build HanabePhotoManager.sln -c Release /warnaserror --artifacts-path .artifacts/agent-verification
dotnet test HanabePhotoManager.sln -c Release --no-build --artifacts-path .artifacts/agent-verification
```

Do not point `--no-build` tests at a different output from the preceding build.

## Verification Matrix

| Change | Minimum automated verification | Additional verification |
|---|---|---|
| Core policy/model/contract | Core tests + Release solution build | Downstream tests when public contracts change |
| Infrastructure filesystem/database/cloud | Infrastructure tests + Release solution build | Full tests for persistence/shared contracts |
| ViewModel or App service | App tests + Release solution build | Affected workflow smoke test |
| Desktop.Core policy/contract/ViewModel | Desktop.Core tests + Release solution build | Portable startup/composition tests when contracts change |
| Avalonia Desktop/composition/packaging | Desktop.Core tests + Release solution build | `osx-arm64` cross-publish and macOS bundle-host smoke |
| XAML page/window | App/resource tests + Release solution build | Light/Dark and keyboard smoke test |
| Theme/dictionary/style/template | Full App tests + Release solution build | Runtime theme switching and affected screens |
| Map/WebView2 bridge/assets | App tests + Release solution build | Interactive Windows map check |
| Model asset/inference | Relevant App tests + Release solution build | Known fixture and publish-size/notices review |
| Project/dependency/cross-project contract | Full solution build and tests | Restore and publish-impact review |
| Documentation only | Link and authority review | Build/test if commands, paths, or executable assumptions change |
| Release candidate | Full solution build and tests | Publish and full regression |

Focused project tests or `--filter FullyQualifiedName~TypeName` may accelerate iteration but never replace final verification.

## When to Build

Build after changing C#, XAML, project files, embedded assets, dictionaries, bindings, or dependencies. Build before `--no-build` tests. Always run a Release solution build before completing executable changes.

## When to Publish

Publish only for release candidates or changes affecting deployment contents/startup: publish properties, runtime/native dependencies, WebView2/map/model assets, icon, or publish tooling. Avalonia Desktop packaging changes require an `osx-arm64` cross-publish; this verifies payload creation on Windows but is not macOS runtime validation. Normal feature iteration and documentation-only changes do not publish. The Windows formal procedure is owned by `release.md`; macOS artifact and launch checks are owned by [macos-testing.md](macos-testing.md).

## Smoke Test

For user-facing changes, run the affected workflow plus:

- Start and close without missing resources, unhandled exceptions, or a hung process.
- Navigate to and away from the affected screen without stale state.
- Check debugger output for new binding errors.
- Switch Light → Dark → Light and verify the affected view continues to resolve resources.
- Reach interactive controls by keyboard and verify dialog Enter/Escape behavior where applicable.
- Exercise happy path, empty input, recoverable failure, and cancellation/back/close.
- Confirm progress and command enabled state settle correctly.
- Restart when persisted/resumable state changed.
- Use disposable sample files; never a user's real photo library.

Visual acceptance criteria belong only to [design-system.md](design-system.md).

### Portable and macOS phase 1 smoke scope

The macOS workflow runs only the cross-platform Core and Desktop.Core test projects, publishes Desktop for `osx-arm64`, creates `Hanabe Photo Manager.app`, and executes `Contents/MacOS/HanabePhotoManager.Desktop --smoke-test` from inside that bundle. The smoke path validates startup composition and XAML loading without opening a window.

Infrastructure and App tests remain part of the mandatory full Windows solution gate in phase 1. A green macOS job does not establish that Windows-specific Infrastructure implementations are portable, and a Windows cross-publish does not establish that the bundle launched on macOS.

## Evidence Rules

A failing required build or test blocks completion. Report exact commands, configuration, pass/fail/skip totals, environment failures, and omitted manual checks. Real cloud credentials/network accounts are opt-in; default tests use fixtures, fakes, or the simulator.
