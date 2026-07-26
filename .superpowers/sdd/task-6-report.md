# Task 6 Report: Assemble an unsigned macOS app bundle

Status: **DONE**

## Delivery

- Added `src/HanabePhotoManager.Desktop/Info.plist` with the app bundle identifier, executable, macOS 11.0 minimum version, and high-resolution capability metadata.
- Added `tools/macos/create-app-bundle.sh`. It accepts exactly a publish directory and an output directory, resolves its repository and input targets independently of the caller's working directory, and creates `Hanabe Photo Manager.app/Contents` with `Info.plist`, `MacOS`, and `Resources`.
- The script validates the publish directory, app host, checked-in plist, and resolved output path. It rejects output nested in the publish directory, replaces only the explicit app-bundle target, copies all published files into `Contents/MacOS`, and runs `chmod +x` only for `HanabePhotoManager.Desktop`.
- The script does not sign, notarize, alter Gatekeeper, or modify WPF/other tooling.
- Added `BundleMetadataTests`, which locates the repository by walking up from `AppContext.BaseDirectory`, so the test does not depend on the process working directory.

## TDD evidence

Before `Info.plist` existed, added `BundleMetadataTests` and ran:

```powershell
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release --filter BundleMetadataTests
```

It failed as expected with `FileNotFoundException` for `src/HanabePhotoManager.Desktop/Info.plist`. After adding the plist, the focused metadata test passed: 1 passed, 0 failed, 0 skipped.

## Verification

```powershell
dotnet test tests/HanabePhotoManager.Desktop.Core.Tests/HanabePhotoManager.Desktop.Core.Tests.csproj -c Release --filter BundleMetadataTests
& 'C:\Program Files\Git\bin\bash.exe' -n tools/macos/create-app-bundle.sh
dotnet build HanabePhotoManager.sln -c Release /warnaserror
dotnet test HanabePhotoManager.sln -c Release --no-build
git diff --check
```

Results: focused metadata test 1/1 passed. Git Bash completed the shell syntax validation successfully. The Release solution build completed with 0 warnings and 0 errors. Full regression passed 718/718 (Core 351, Desktop.Core 24, Infrastructure 136, App 207). `git diff --check` found no whitespace errors for the tracked worktree changes.

## Self-review

- The script begins with `set -euo pipefail`, safely quotes path expansions, and only deletes the fully resolved named bundle target.
- The source plist is derived from the script location, not the caller's working directory; test repository discovery is likewise working-directory independent.
- `codesign`, notarization, `spctl`, `xattr`, `sudo`, and Gatekeeper changes are absent.
- This Windows host cannot execute a produced macOS application bundle. The metadata, script syntax, bundle layout construction logic, solution build, and automated regression suite were verified here; macOS runtime launch remains an environment-specific follow-up.
