# Origin Comment Duplicate Check Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Store the incoming file's original name, length and pre-metadata SHA-256 in Windows “Comments”, then use that identity before the existing same-date SHA-256 duplicate check.

**Architecture:** A pure codec owns the versioned Hanabe comment line. An App service wraps Windows Property System access behind `IFileOriginMetadataStore`; import orchestration writes and verifies metadata only after a successful transfer. Duplicate preflight consults origin comments first and falls back to the existing size/hash scanner for legacy or unsupported files.

**Tech Stack:** .NET 8, C# 12, Windows Property System COM interop, WPF, xUnit, FluentAssertions.

## Global Constraints

- Work only under `D:\HanabePhoto`; do not use OneDrive.
- Never overwrite user comment text; replace only a line beginning with `Hanabe:v1;`.
- Compute and store SHA-256 before modifying the destination metadata.
- Unsupported/read-only formats must fail safely and retain the existing duplicate-check path.
- Do not inject bytes directly into RAW or media containers.

---

### Task 1: Versioned Hanabe comment codec

**Files:**
- Create: `src/HanabePhotoManager.App/Imports/FileOriginMetadata.cs`
- Create: `src/HanabePhotoManager.App/Imports/HanabeOriginCommentCodec.cs`
- Create: `tests/HanabePhotoManager.App.Tests/HanabeOriginCommentCodecTests.cs`

**Interfaces:**
- Produces: `FileOriginMetadata(string OriginalName, long OriginalLength, string OriginalSha256)`; `TryParse(string?, out FileOriginMetadata)`; `Merge(string? existingComment, FileOriginMetadata metadata)`.

- [ ] Write failing tests for round-trip, preservation of user text, replacement of an existing Hanabe line, escaped names, unknown fields, malformed hash and malformed length.
- [ ] Run the filtered tests; expect compilation failure because the codec is absent.
- [ ] Implement UTF-8 percent encoding for field values, require a 64-character hexadecimal hash, and compare the prefix ordinally.
- [ ] Re-run the filtered tests; expect all codec cases to pass.
- [ ] Commit with `feat: define Hanabe origin comment format`.

### Task 2: Windows Comments property store

**Files:**
- Create: `src/HanabePhotoManager.App/Imports/IFileOriginMetadataStore.cs`
- Create: `src/HanabePhotoManager.App/Imports/WindowsFileOriginMetadataStore.cs`
- Create: `src/HanabePhotoManager.App/Imports/FileOriginMetadataWriteResult.cs`
- Create: `tests/HanabePhotoManager.App.Tests/WindowsFileOriginMetadataStoreTests.cs`

**Interfaces:**
- Produces: `Task<FileOriginMetadata?> ReadAsync(string path, CancellationToken token)` and `Task<FileOriginMetadataWriteResult> WriteAndVerifyAsync(string path, FileOriginMetadata metadata, CancellationToken token)`.

- [ ] Add a failing Windows-only integration test using a D-drive temporary JPEG copy; assert existing user text survives and `System.Comment` reads back the Hanabe identity.
- [ ] Add a failing test that a read-only or unsupported target returns a failure result without changing the primary file contents beyond a successful property-handler commit.
- [ ] Implement minimal COM declarations for `SHGetPropertyStoreFromParsingName`, `IPropertyStore`, `PROPERTYKEY System.Comment`, `PROPVARIANT`, `GetValue`, `SetValue`, and `Commit`; always release COM objects and clear variants.
- [ ] After commit, reopen the property store and require an exact parsed metadata match before returning success.
- [ ] Re-run the focused Windows tests; classify unsupported handlers as skipped/safe failure rather than corrupting the sample.
- [ ] Commit with `feat: write Hanabe identity to Windows comments`.

### Task 3: Origin-aware same-date candidate matching

**Files:**
- Create: `src/HanabePhotoManager.App/Imports/OriginMetadataDuplicateMatcher.cs`
- Create: `tests/HanabePhotoManager.App.Tests/OriginMetadataDuplicateMatcherTests.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`

**Interfaces:**
- Consumes: metadata store and `IFileHasher`.
- Produces: a match only when original name (case-insensitive), original length and stored original SHA-256 all match the incoming source.

- [ ] Write failing tests for identical identity, same DSC with different hash, same hash with different original name, malformed/missing metadata and cancellation.
- [ ] Run the focused tests and observe missing matcher failure.
- [ ] Implement directory candidate enumeration, metadata reads and a single incoming-source hash calculation shared across candidates.
- [ ] Integrate the matcher before the existing `BuildSizeMapAsync` fallback; preserve the current duplicate decision windows and target-date boundary.
- [ ] Re-run matcher and existing `LibraryContentScannerTests`; expect all to pass.
- [ ] Commit with `feat: match same-date imports by origin comment`.

### Task 4: Write origin comments after verified transfer

**Files:**
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Create: `tests/HanabePhotoManager.App.Tests/ImportOriginMetadataFlowTests.cs`

**Interfaces:**
- Consumes: verified `ImportPlanItem.Files`, source hasher and metadata store.
- Produces: one verified comment write per newly transferred primary media file; sidecars remain untouched.

- [ ] Add a failing flow test proving the hash is taken from the source before comment writing and the destination path receives the metadata.
- [ ] Add failure-path coverage showing import remains successful while its report explicitly states that the comment could not be written.
- [ ] Implement metadata creation immediately before transfer or from the verified source, then write only after `TransferGroupAsync` succeeds and the item was not an identical skip.
- [ ] Preserve move-after-verify behavior by computing the source identity before the source can be deleted.
- [ ] Re-run focused import tests and all App import tests.
- [ ] Commit with `feat: persist imported origin identity in comments`.

### Task 5: Release verification and documentation

**Files:**
- Modify: `docs/current-status.md`
- Modify: `docs/agent-change-log.md`
- Modify: `docs/known-issues.md` only if runtime testing reveals a tracked limitation.

- [ ] Run `dotnet build HanabePhotoManager.sln -c Release --no-restore /warnaserror`; expect zero warnings and errors.
- [ ] Run `dotnet test HanabePhotoManager.sln -c Release --no-build --no-restore`; expect all suites to pass.
- [ ] Publish self-contained output to a new D-drive artifact directory.
- [ ] Import disposable D-drive media, inspect Windows Details/Comments, verify the pre-write source hash stored in the comment, and repeat the same-date import.
- [ ] Exercise an unsupported or read-only sample and confirm explicit safe fallback with no file corruption.
- [ ] Append exact automated and runtime evidence to project documentation.
- [ ] Commit with `docs: record origin comment duplicate verification`.
