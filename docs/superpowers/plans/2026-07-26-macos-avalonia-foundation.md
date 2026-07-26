# macOS Avalonia Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a tested Avalonia desktop shell and repeatable unsigned Apple Silicon `.app` build without breaking the existing WPF client.

**Architecture:** Keep Core and Infrastructure platform-neutral and preserve the existing WPF App. Add a new Avalonia Desktop project, a cross-platform Desktop.Core project for presentation/platform contracts, and a matching test project. macOS-only implementations stay behind narrow interfaces; GitHub Actions performs the authoritative `osx-arm64` publish and app-bundle assembly.

**Tech Stack:** .NET 8.0.422, C# 12, Avalonia 11, CommunityToolkit.Mvvm 8.4, Microsoft.Extensions.DependencyInjection 8, xUnit 2.9, FluentAssertions 6.12, GitHub Actions macOS runner.

## Global Constraints

- Preserve `src/HanabePhotoManager.App` as the Windows WPF client.
- Target Apple Silicon only with runtime identifier `osx-arm64`.
- Set the minimum supported operating system to macOS 11 Big Sur.
- Publish self-contained, unsigned artifacts; do not add signing, notarization, or App Store steps.
- Keep Core free of UI and operating-system APIs.
- Platform failures must be explicit; file deletion must never silently become permanent deletion.
- Do not modify or commit the unrelated untracked `tools/SigLIP2Export/` directory.
- Use test-first changes and commit each task independently.

## Roadmap Boundary

This is phase 1 of four independently reviewed plans:

1. Foundation: Avalonia shell, platform contracts, safe macOS adapters, CI `.app` artifact.
2. Core experience: library browsing, importing, viewer, search, ratings, tags, metadata, settings.
3. Advanced experience: faces, classification, map, compression, watermark, contests, cloud.
4. Release closure: native model packaging, DMG, full macOS regression, documentation and known limitations.

This plan ends with a launchable shell and platform contract foundation. It does not claim feature parity.

## File Map

- `src/HanabePhotoManager.Desktop.Core/`: shared shell state and operating-system contracts; no Avalonia or WPF references.
- `src/HanabePhotoManager.Desktop/`: Avalonia entrypoint, views, macOS adapters, dependency injection and packaging metadata.
- `tests/HanabePhotoManager.Desktop.Core.Tests/`: cross-platform unit and contract tests.
- `.github/workflows/macos-arm64.yml`: authoritative macOS restore, test, publish and `.app` artifact workflow.
- `tools/macos/create-app-bundle.sh`: deterministic unsigned `.app` bundle assembly.
- `docs/macos-testing.md`: first-launch and smoke-test instructions.

---

### Task 1: Add the cross-platform desktop contracts project

**Files:**
- Create: `src/HanabePhotoManager.Desktop.Core/HanabePhotoManager.Desktop.Core.csproj`
- Create: `src/HanabePhotoManager.Desktop.Core/Platform/ITrashService.cs`
- Create: `src/HanabePhotoManager.Desktop.Core/Platform/IAppPaths.cs`
- Create: `src/HanabePhotoManager.Desktop.Core/Platform/IExternalFileService.cs`
- Create: `tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj`
- Create: `tests/HanabePhotoManager.Desktop.Core.Tests/Platform/PlatformContractTests.cs`
- Modify: `HanabePhotoManager.sln`

**Interfaces:**
- Produces: `Task MoveToTrashAsync(string path, CancellationToken cancellationToken = default)`
- Produces: `string ApplicationDataDirectory`, `string CacheDirectory`
- Produces: `Task RevealInFileManagerAsync(string path, CancellationToken cancellationToken = default)`

- [ ] **Step 1: Create the test project and write contract-shape tests**

```csharp
using FluentAssertions;
using HanabePhotoManager.Desktop.Core.Platform;

namespace HanabePhotoManager.Desktop.Core.Tests.Platform;

public sealed class PlatformContractTests
{
    [Fact]
    public void TrashService_ExposesCancelableAsyncOperation()
    {
        var method = typeof(ITrashService).GetMethod(nameof(ITrashService.MoveToTrashAsync));
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be<Task>();
        method.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(typeof(string), typeof(CancellationToken));
    }

    [Fact]
    public void AppPaths_SeparatesDurableAndCachedData()
    {
        typeof(IAppPaths).GetProperty(nameof(IAppPaths.ApplicationDataDirectory)).Should().NotBeNull();
        typeof(IAppPaths).GetProperty(nameof(IAppPaths.CacheDirectory)).Should().NotBeNull();
    }
}
```

Create the test project with `net8.0`, xUnit 2.9.3, `Microsoft.NET.Test.Sdk` 17.8.0, FluentAssertions 6.12.2, and a project reference to Desktop.Core.

- [ ] **Step 2: Run the tests and verify the missing project/contracts fail**

Run:

```powershell
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release
```

Expected: FAIL because `HanabePhotoManager.Desktop.Core` and its platform interfaces do not exist.

- [ ] **Step 3: Add Desktop.Core and the three contracts**

```csharp
namespace HanabePhotoManager.Desktop.Core.Platform;

public interface ITrashService
{
    Task MoveToTrashAsync(string path, CancellationToken cancellationToken = default);
}

public interface IAppPaths
{
    string ApplicationDataDirectory { get; }
    string CacheDirectory { get; }
}

public interface IExternalFileService
{
    Task RevealInFileManagerAsync(string path, CancellationToken cancellationToken = default);
}
```

The project targets `net8.0`, references Core only, and uses CommunityToolkit.Mvvm 8.4.0. Desktop.Core does not consume Infrastructure; a later Desktop composition project may reference Infrastructure when it composes implementations. Add both new projects to the solution under the existing `src` and `tests` solution folders.

- [ ] **Step 4: Run contract and existing portable tests**

Run:

```powershell
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release
dotnet test tests/HanabePhotoManager.Core.Tests/HanabePhotoManager.Core.Tests.csproj -c Release
dotnet test tests/HanabePhotoManager.Infrastructure.Tests/HanabePhotoManager.Infrastructure.Tests.csproj -c Release
```

Expected: all tests PASS; existing totals remain at least 351 Core and 136 Infrastructure tests.

- [ ] **Step 5: Commit the contracts**

```powershell
git add HanabePhotoManager.sln src/HanabePhotoManager.Desktop.Core tests/HanabePhotoManager.Desktop.Core.Tests
git commit -m "feat: add cross-platform desktop contracts"
```

### Task 2: Add tested macOS application paths

**Files:**
- Create: `src/HanabePhotoManager.Desktop/Platform/MacOsAppPaths.cs`
- Create: `tests/HanabePhotoManager.Desktop.Core.Tests/Platform/MacOsAppPathsPolicyTests.cs`
- Create: `src/HanabePhotoManager.Desktop.Core/Platform/MacOsAppPathsPolicy.cs`

**Interfaces:**
- Consumes: `IAppPaths`
- Produces: `MacOsAppPathsPolicy.Resolve(string homeDirectory)` returning `(ApplicationDataDirectory, CacheDirectory)`

- [ ] **Step 1: Write path policy tests**

```csharp
[Fact]
public void Resolve_UsesAppleApplicationSupportAndCaches()
{
    var result = MacOsAppPathsPolicy.Resolve("/Users/hanabe");

    result.ApplicationDataDirectory.Should()
        .Be("/Users/hanabe/Library/Application Support/Hanabe Photo Manager");
    result.CacheDirectory.Should()
        .Be("/Users/hanabe/Library/Caches/Hanabe Photo Manager");
}

[Theory]
[InlineData("")]
[InlineData(" ")]
public void Resolve_RejectsMissingHome(string home)
{
    var action = () => MacOsAppPathsPolicy.Resolve(home);
    action.Should().Throw<ArgumentException>();
}
```

- [ ] **Step 2: Run the focused test and verify failure**

Run:

```powershell
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release --filter MacOsAppPathsPolicyTests
```

Expected: FAIL because `MacOsAppPathsPolicy` does not exist.

- [ ] **Step 3: Implement the pure path policy and adapter**

```csharp
namespace HanabePhotoManager.Desktop.Core.Platform;

public static class MacOsAppPathsPolicy
{
    public static (string ApplicationDataDirectory, string CacheDirectory) Resolve(string homeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);
        var library = Path.Combine(Path.GetFullPath(homeDirectory), "Library");
        return (
            Path.Combine(library, "Application Support", "Hanabe Photo Manager"),
            Path.Combine(library, "Caches", "Hanabe Photo Manager"));
    }
}
```

`MacOsAppPaths` reads `Environment.SpecialFolder.UserProfile`, delegates to the policy, creates both directories in its constructor, and exposes them through `IAppPaths`.

- [ ] **Step 4: Run focused and full Desktop.Core tests**

Run:

```powershell
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/HanabePhotoManager.Desktop.Core/Platform/MacOsAppPathsPolicy.cs src/HanabePhotoManager.Desktop/Platform/MacOsAppPaths.cs tests/HanabePhotoManager.Desktop.Core.Tests/Platform/MacOsAppPathsPolicyTests.cs
git commit -m "feat: define macOS application paths"
```

### Task 3: Add the Avalonia application shell

**Files:**
- Create: `src/HanabePhotoManager.Desktop/HanabePhotoManager.Desktop.csproj`
- Create: `src/HanabePhotoManager.Desktop/Program.cs`
- Create: `src/HanabePhotoManager.Desktop/App.axaml`
- Create: `src/HanabePhotoManager.Desktop/App.axaml.cs`
- Create: `src/HanabePhotoManager.Desktop/Views/MainWindow.axaml`
- Create: `src/HanabePhotoManager.Desktop/Views/MainWindow.axaml.cs`
- Create: `src/HanabePhotoManager.Desktop.Core/ViewModels/DesktopShellViewModel.cs`
- Create: `tests/HanabePhotoManager.Desktop.Core.Tests/ViewModels/DesktopShellViewModelTests.cs`
- Modify: `HanabePhotoManager.sln`

**Interfaces:**
- Produces: `DesktopShellViewModel.Title`
- Produces: `DesktopShellViewModel.Status`

- [ ] **Step 1: Write the shell ViewModel test**

```csharp
public sealed class DesktopShellViewModelTests
{
    [Fact]
    public void Constructor_ExposesProductAndMigrationStatus()
    {
        var subject = new DesktopShellViewModel();

        subject.Title.Should().Be("Hanabe Photo Manager");
        subject.Status.Should().Be("macOS migration foundation");
    }
}
```

- [ ] **Step 2: Run the test and verify failure**

Run:

```powershell
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release --filter DesktopShellViewModelTests
```

Expected: FAIL because `DesktopShellViewModel` does not exist.

- [ ] **Step 3: Implement the ViewModel**

```csharp
namespace HanabePhotoManager.Desktop.Core.ViewModels;

public sealed class DesktopShellViewModel
{
    public string Title => "Hanabe Photo Manager";
    public string Status => "macOS migration foundation";
}
```

- [ ] **Step 4: Create the Avalonia app**

Use Avalonia 11 package versions resolved consistently across `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, and `Avalonia.Fonts.Inter`. Reference Desktop.Core. Set `OutputType` to `WinExe`, `TargetFramework` to `net8.0`, `RuntimeIdentifiers` to `osx-arm64`, `SelfContained` to `true`, `PublishSingleFile` to `false`, and `ApplicationId` to `com.hanabe.photomanager`.

`MainWindow.axaml` binds its title and two text elements to `Title` and `Status`, uses `FluentTheme`, and contains no Windows-specific namespace. `App.OnFrameworkInitializationCompleted` creates the main window with `DesktopShellViewModel` as its data context.

`Program` must accept `--smoke-test`, validate the startup composition prerequisites, and exit with code 0 before creating a window. This is the runtime contract consumed by Task 7 macOS CI.

- [ ] **Step 5: Build the Avalonia project and run tests**

Run:

```powershell
dotnet build src/HanabePhotoManager.Desktop/HanabePhotoManager.Desktop.csproj -c Release
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release
```

Expected: build and tests PASS with zero warnings.

- [ ] **Step 6: Commit**

```powershell
git add HanabePhotoManager.sln src/HanabePhotoManager.Desktop src/HanabePhotoManager.Desktop.Core/ViewModels tests/HanabePhotoManager.Desktop.Core.Tests/ViewModels
git commit -m "feat: add Avalonia desktop shell"
```

### Task 4: Implement fail-closed Trash and Finder adapters

**Files:**
- Create: `src/HanabePhotoManager.Desktop.Core/Platform/ProcessCommand.cs`
- Create: `src/HanabePhotoManager.Desktop.Core/Platform/IProcessRunner.cs`
- Create: `src/HanabePhotoManager.Desktop/Platform/ProcessRunner.cs`
- Create: `src/HanabePhotoManager.Desktop/Platform/MacOsTrashService.cs`
- Create: `src/HanabePhotoManager.Desktop/Platform/MacOsExternalFileService.cs`
- Create: `tests/HanabePhotoManager.Desktop.Core.Tests/Platform/MacOsCommandPolicyTests.cs`
- Create: `src/HanabePhotoManager.Desktop.Core/Platform/MacOsCommandPolicy.cs`

**Interfaces:**
- Produces: `ProcessCommand(string FileName, IReadOnlyList<string> Arguments)`
- Produces: `Task<int> RunAsync(ProcessCommand command, CancellationToken cancellationToken)`
- Consumes: `ITrashService`, `IExternalFileService`

- [ ] **Step 1: Write command construction tests**

```csharp
[Fact]
public void Trash_UsesFinderDeleteWithoutShellInterpolation()
{
    var command = MacOsCommandPolicy.MoveToTrash("/Users/me/Pictures/a 'quoted'.jpg");

    command.FileName.Should().Be("/usr/bin/osascript");
    command.Arguments.Should().Equal(
        "-e",
        "on run argv",
        "-e",
        "tell application \"Finder\" to delete POSIX file (item 1 of argv)",
        "-e",
        "end run",
        "--",
        "/Users/me/Pictures/a 'quoted'.jpg");
}

[Fact]
public void Reveal_UsesOpenRevealWithSeparateArgument()
{
    var command = MacOsCommandPolicy.Reveal("/Users/me/Pictures/a b.jpg");
    command.FileName.Should().Be("/usr/bin/open");
    command.Arguments.Should().Equal("-R", "/Users/me/Pictures/a b.jpg");
}
```

- [ ] **Step 2: Verify tests fail**

Run:

```powershell
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release --filter MacOsCommandPolicyTests
```

Expected: FAIL because the policy and command record do not exist.

- [ ] **Step 3: Implement command policy**

Implement the exact argument arrays asserted above. Reject blank paths with `ArgumentException`, normalize with `Path.GetFullPath`, and never build a single shell command string.

- [ ] **Step 4: Implement adapters**

`ProcessRunner` uses `ProcessStartInfo.ArgumentList`, sets `UseShellExecute = false`, redirects standard error, waits with cancellation, and throws `InvalidOperationException` including stderr on non-zero exit.

`MacOsTrashService` checks `File.Exists` or `Directory.Exists` before invoking the runner. Missing paths throw `FileNotFoundException`; failures propagate and never call `File.Delete` or `Directory.Delete`.

`MacOsExternalFileService` validates that the path exists and runs the reveal command.

- [ ] **Step 5: Run tests and static safety scan**

Run:

```powershell
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release
rg -n "File\\.Delete|Directory\\.Delete" src/HanabePhotoManager.Desktop/Platform/MacOsTrashService.cs
```

Expected: tests PASS; the safety scan returns no matches.

- [ ] **Step 6: Commit**

```powershell
git add src/HanabePhotoManager.Desktop.Core/Platform src/HanabePhotoManager.Desktop/Platform tests/HanabePhotoManager.Desktop.Core.Tests/Platform
git commit -m "feat: add safe macOS trash and Finder adapters"
```

### Task 5: Compose platform services through dependency injection

**Files:**
- Create: `src/HanabePhotoManager.Desktop/Composition/DesktopServices.cs`
- Create: `tests/HanabePhotoManager.Desktop.Core.Tests/Composition/ServiceContractTests.cs`
- Modify: `src/HanabePhotoManager.Desktop/App.axaml.cs`
- Modify: `tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj`

**Interfaces:**
- Produces: `IServiceCollection AddHanabeDesktop(this IServiceCollection services)`
- Consumes: all platform contracts from Tasks 1, 2 and 4.

- [ ] **Step 1: Write service registration test**

```csharp
[Fact]
public void AddHanabeDesktop_RegistersOneImplementationPerPlatformContract()
{
    var provider = new ServiceCollection()
        .AddHanabeDesktop()
        .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    provider.GetRequiredService<IAppPaths>().Should().BeOfType<MacOsAppPaths>();
    provider.GetRequiredService<ITrashService>().Should().BeOfType<MacOsTrashService>();
    provider.GetRequiredService<IExternalFileService>().Should().BeOfType<MacOsExternalFileService>();
    provider.GetRequiredService<DesktopShellViewModel>().Should().NotBeNull();
}
```

Add a project reference from the test project to Desktop and add Microsoft.Extensions.DependencyInjection 8.0.1.

- [ ] **Step 2: Run test and verify failure**

Run:

```powershell
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release --filter ServiceContractTests
```

Expected: FAIL because `AddHanabeDesktop` does not exist.

- [ ] **Step 3: Implement registrations**

Register `IProcessRunner`, `IAppPaths`, `ITrashService`, `IExternalFileService`, and `DesktopShellViewModel` as singletons. `App` creates one provider, resolves the shell ViewModel, and disposes the provider on desktop exit.

- [ ] **Step 4: Run tests and build**

Run:

```powershell
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release
dotnet build src/HanabePhotoManager.Desktop/HanabePhotoManager.Desktop.csproj -c Release
```

Expected: PASS with zero warnings.

- [ ] **Step 5: Commit**

```powershell
git add src/HanabePhotoManager.Desktop/Composition src/HanabePhotoManager.Desktop/App.axaml.cs tests/HanabePhotoManager.Desktop.Core.Tests
git commit -m "feat: compose macOS desktop services"
```

### Task 6: Assemble an unsigned macOS app bundle

**Files:**
- Create: `src/HanabePhotoManager.Desktop/Info.plist`
- Create: `tools/macos/create-app-bundle.sh`
- Create: `tests/HanabePhotoManager.Desktop.Core.Tests/Packaging/BundleMetadataTests.cs`

**Interfaces:**
- Produces: bundle id `com.hanabe.photomanager`
- Produces: minimum system `11.0`
- Produces: executable name `HanabePhotoManager.Desktop`

- [ ] **Step 1: Write metadata tests**

Read `Info.plist` using `XDocument` and assert:

```csharp
values["CFBundleIdentifier"].Should().Be("com.hanabe.photomanager");
values["CFBundleExecutable"].Should().Be("HanabePhotoManager.Desktop");
values["LSMinimumSystemVersion"].Should().Be("11.0");
values["NSHighResolutionCapable"].Should().Be("true");
```

- [ ] **Step 2: Verify the metadata test fails**

Run:

```powershell
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release --filter BundleMetadataTests
```

Expected: FAIL because `Info.plist` is missing.

- [ ] **Step 3: Add Info.plist and bundle script**

The script accepts exactly two positional arguments: publish directory and output directory. It creates:

```text
Hanabe Photo Manager.app/
  Contents/
    Info.plist
    MacOS/
      HanabePhotoManager.Desktop
      all published managed and native files
    Resources/
```

It starts with `set -euo pipefail`, validates both input paths, copies the checked-in plist, copies publish output into `Contents/MacOS`, and runs `chmod +x` only on the app host. It must not sign, notarize, or modify Gatekeeper globally.

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/HanabePhotoManager.Desktop/Info.plist tools/macos/create-app-bundle.sh tests/HanabePhotoManager.Desktop.Core.Tests/Packaging
git commit -m "build: add unsigned macOS app bundle"
```

### Task 7: Add the authoritative macOS ARM64 CI workflow

**Files:**
- Create: `.github/workflows/macos-arm64.yml`
- Create: `docs/macos-testing.md`

**Interfaces:**
- Consumes: `tools/macos/create-app-bundle.sh`
- Produces: GitHub artifact `hanabe-photo-manager-osx-arm64`

- [ ] **Step 1: Add workflow structure validation**

Before writing the workflow, add a repository test that loads `.github/workflows/macos-arm64.yml` as text and asserts it contains:

```text
runs-on: macos-14
dotnet test
-r osx-arm64
--self-contained true
create-app-bundle.sh
shasum -a 256
actions/upload-artifact@
```

Expected test name: `MacOsWorkflowTests.Workflow_BuildsAndUploadsArm64App`.

- [ ] **Step 2: Run the focused test and verify failure**

Run:

```powershell
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release --filter MacOsWorkflowTests
```

Expected: FAIL because the workflow does not exist.

- [ ] **Step 3: Create the workflow**

Trigger on `workflow_dispatch` and pull requests changing `src/**`, `tests/**`, `tools/macos/**`, or the workflow itself. Use `actions/checkout`, `actions/setup-dotnet`, restore, run all three portable test projects, publish Desktop with:

```bash
dotnet publish src/HanabePhotoManager.Desktop/HanabePhotoManager.Desktop.csproj \
  -c Release -r osx-arm64 --self-contained true \
  -o artifacts/macos/publish
```

Run the bundle script, execute the app host with a `--smoke-test` argument that exits zero before creating a window, zip the `.app` with `ditto`, generate SHA-256 using `shasum -a 256`, and upload the zip plus checksum. Pin released major versions of GitHub actions; do not use floating branch names.

- [ ] **Step 4: Document first launch and smoke checks**

Document Finder “right-click → Open” as the primary path. If necessary, document only:

```bash
xattr -dr com.apple.quarantine "/Applications/Hanabe Photo Manager.app"
```

Include checks for startup, correct app data paths, Finder reveal, move-to-Trash behavior, and a clear statement that phase 1 is a shell rather than feature parity.

- [ ] **Step 5: Run workflow tests and inspect YAML**

Run:

```powershell
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release
git diff --check
```

Expected: PASS and no whitespace errors.

- [ ] **Step 6: Commit**

```powershell
git add .github/workflows/macos-arm64.yml docs/macos-testing.md tests/HanabePhotoManager.Desktop.Core.Tests
git commit -m "ci: build unsigned macOS arm64 app"
```

### Task 8: Verify phase 1 without regressing Windows

**Files:**
- Modify only if validation exposes a phase-1 defect.

**Interfaces:**
- Validates all outputs from Tasks 1 through 7.

- [ ] **Step 1: Restore and build the full Windows solution**

Run:

```powershell
dotnet restore HanabePhotoManager.sln
dotnet build HanabePhotoManager.sln -c Release --no-restore
```

Expected: PASS with zero warnings and zero errors.

- [ ] **Step 2: Run all automated tests**

Run:

```powershell
dotnet test HanabePhotoManager.sln -c Release --no-build
```

Expected: all test projects PASS.

- [ ] **Step 3: Cross-publish the managed macOS payload**

Run:

```powershell
dotnet publish src/HanabePhotoManager.Desktop/HanabePhotoManager.Desktop.csproj -c Release -r osx-arm64 --self-contained true -o .artifacts/macos-cross-publish
```

Expected: publish PASS and output contains `HanabePhotoManager.Desktop` plus Avalonia native assets. This is not treated as macOS runtime validation.

- [ ] **Step 4: Push the phase branch and run GitHub Actions**

Push the implementation branch, manually run `macos-arm64.yml`, and require a green job. Download the artifact and confirm the zip, checksum, Info.plist and executable exist.

- [ ] **Step 5: Run the real-device smoke matrix**

On an M1 or later Mac running macOS 11 or later:

1. Verify the checksum.
2. Extract the application.
3. Use Finder “right-click → Open”.
4. Confirm the shell window renders title and migration status.
5. Confirm app data and cache directories are created under `~/Library`.
6. Exercise Finder reveal with a filename containing spaces.
7. Move a disposable test file to Trash and restore it.
8. Confirm no permanent-delete path was used.

Expected: every item passes or is recorded with exact failure evidence.

- [ ] **Step 6: Commit any verification-only documentation updates**

```powershell
git add docs/macos-testing.md
git commit -m "docs: record macOS foundation verification"
```

Skip this commit when the document did not change.

## Phase 1 Exit Criteria

- Existing WPF solution builds and all existing tests pass.
- Desktop.Core contracts and tests pass on Windows and macOS runners.
- Avalonia shell builds for `osx-arm64`.
- GitHub Actions produces an unsigned `.app` zip and SHA-256 file.
- The app launches on a real M1 or later Mac.
- Trash failure is fail-closed and never permanently deletes the target.
- Remaining feature parity work is explicitly assigned to phases 2 through 4.
