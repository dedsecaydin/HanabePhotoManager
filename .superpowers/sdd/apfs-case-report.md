# APFS Case-Sensitive Path Identity Fix

## Scope

`MediaGroupBuilder` now uses the `LocalPathSyntax` path-identity comparison
policy consistently. Windows drive and UNC identities compare without case;
POSIX identities compare with ordinal (case-sensitive) semantics.

## Behavior covered

- `/photos/A.JPG` and `/photos/a.JPG` are distinct inputs rather than duplicates.
- Those distinct POSIX paths remain independently consumable media groups.
- Case-distinct POSIX Sony videos each receive only their matching sidecar.
- Windows drive and UNC casing-only variants remain duplicate paths.

## TDD evidence

Before the production change, the new POSIX tests failed with
`Duplicate FullPath '/photos/a.JPG' is not allowed.` (3 failures, 27 passes in
the focused test class). After the change, the focused class passed 30/30.

## Verification

- `dotnet test tests/HanabePhotoManager.Core.Tests/HanabePhotoManager.Core.Tests.csproj -c Release --no-restore` — 357 passed.
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror --no-restore` — 0 warnings, 0 errors (after `dotnet restore HanabePhotoManager.sln` prepared the newly created worktree).
- `dotnet test HanabePhotoManager.sln -c Release --no-build --no-restore` — 726 passed (Core 357, Infrastructure 136, App 207, Desktop.Core 26).
- `dotnet publish src/HanabePhotoManager.Desktop/HanabePhotoManager.Desktop.csproj -c Release -r osx-arm64 --self-contained true -o .artifacts/macos-cross-publish` — passed.

## Notes

The first solution build was intentionally stopped at missing `project.assets.json`
files because the fresh worktree had not been restored. A separate solution
restore resolved that environment prerequisite; the succeeding Release build,
tests, and cross-publish are the validation evidence.
