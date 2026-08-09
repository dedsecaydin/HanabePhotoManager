# Agent Change Log

## 2026-08-09 - Codex (Compression asynchronous input scan and single-instance guard)

### Task
Prevent the Compression page from blocking the WPF UI while recursively scanning a large network folder, and prevent concurrent application instances from competing for I/O.

### Files Changed
- `CompressionViewModel.cs`: replaced synchronous `AddInputs` with cancellable `AddInputsAsync`. Directory discovery and file-length metadata reads run in `Task.Run`; UI-bound collections update only after the awaited operation resumes on the WPF context. A newer selection cancels an older scan without allowing the older completion handler to clear the newer scan state.
- `CompressionPage.xaml(.cs)`: file, folder, and drop input handlers await the asynchronous scan; progress becomes indeterminate while scanning and the existing Cancel button cancels scanning or compression.
- `App.xaml.cs`: owns the named mutex `HanabePhotoManager.SingleInstance` from startup through application exit. A second launch receives an information dialog and shuts down before loading application services. `OnExit` now releases the mutex only when this process created and owns it, preventing the second instance from throwing `ApplicationException` during shutdown.
- `CompressionViewModelTests.cs`: added async queue and cancellation regression coverage.

### Verification
- Focused compression ViewModel tests: 4/4 passed.
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror --artifacts-path .artifacts\\agent-verification`: 0 warnings, 0 errors (2026-08-09 05:21 +08:00).
- `dotnet test HanabePhotoManager.sln -c Release --no-build --artifacts-path .artifacts\\agent-verification`: Core 365/365, Infrastructure 160/160, App 336/336.

### Remaining Issues
- Manual WPF validation with the real large SMB library remains required. The current user process was intentionally not stopped or inspected interactively; it continues to run its previously loaded executable until restarted.

## 2026-08-08 - Codex (Treemap UI-hang mitigation)

### Task
Mitigate UI stalls while scanning and viewing a large treemap without stopping the user-running application.

### Files Changed
- `ProgressiveTreemapViewModel.cs`: thumbnail arrivals and background image-dimension batches now share the existing 150ms coalesced publication path. The first scan batch, navigation, weight changes, and completion keep their immediate publication behavior; zero-delay test ViewModels remain synchronous.
- `PhotoTreemapControl.cs`: only viewport-intersecting tiles create hit regions or draw/request thumbnails. Disabled debug telemetry no longer enumerates every treemap item on each render.
- `MainWindow.xaml.cs`: parent lookup now supports both visual and content elements, preventing `VisualTreeHelper.GetParent` from throwing for `Run`.
- `App.xaml.cs`: dispatcher and AppDomain unhandled-exception logging and user notification added.
- Treemap App tests: added coalesced thumbnail-publication and viewport intersection coverage.

### Verification
- Focused treemap tests: 19/19 passed.
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror --artifacts-path .artifacts\\agent-verification`: 0 warnings, 0 errors (2026-08-08 20:33 +08:00).
- `dotnet test HanabePhotoManager.sln -c Release --no-build --artifacts-path .artifacts\\agent-verification`: Core 365/365, Infrastructure 160/160, App 335/335.

### Remaining Issues
- Manual WPF validation on the 11,741-item SMB library remains required before KI-07 can be marked resolved. WebView2 initialization was already guarded by the existing `MapPage_Loaded` try/catch and retry path; no running user process was stopped.

## 2026-08-08 - Codex (Startup all-library treemap)

### Task
Open the application directly on the Browse page in Space Treemap mode and load the complete scanned library without requiring a date selection, display-mode switch, or other manual action.

### Files Changed
- `MainWindowViewModel.cs`: defaults to the Browse page and Treemap mode; initialization clears persisted date/category/file-type/rating/search/retouch/smart-category filters before the existing asynchronous root scan begins. The root scan continues to stream batches to `TreemapBrowser`, while dimension and thumbnail work remain background/viewport-driven. Added a root-path guard so filtering a new ViewModel cannot start a treemap scan with an empty path.
- `AppSettingsStore.cs`: new settings default to `Treemap`.
- `BrowseTreemapIntegrationTests.cs`: verifies startup defaults, neutral all-library initialization contract, settings default, and no-root filtering boundary.

### Verification
- `dotnet test tests\HanabePhotoManager.App.Tests\HanabePhotoManager.App.Tests.csproj -c Release --filter FullyQualifiedName~BrowseTreemapIntegrationTests --artifacts-path .artifacts\agent-verification`: 9/9 passed.
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror --artifacts-path .artifacts\agent-verification`: 0 warnings, 0 errors (2026-08-08 19:20 +08:00).
- `dotnet test HanabePhotoManager.sln -c Release --no-build --artifacts-path .artifacts\agent-verification`: Core 365/365, Infrastructure 160/160, App 333/333.

### Remaining Issues
- Manual WPF QA with the real 11,741-item library remains required for startup responsiveness and the existing treemap issues KI-01, KI-03, KI-07, and KI-14. No user-running process was stopped; the isolated artifacts path avoided locked default Release DLLs.

## 2026-08-08 - Codex (Treemap semantic panorama)

### Task
Implement the lowest semantic zoom as an Apple Photos-style panorama without changing import, duplicate detection, retouched-output protection, or normal Justified Gallery behavior.

### Files Changed
- Added `PanoramaPhotoLayout.cs` and tests: every current-directory photo is arranged in a dense 1px-gap justified wall at `TreemapZoom <= 0.20`; logical canvas dimensions are inverse to zoom, preserving 32px rendered tile height (24px constructor floor).
- `PhotoTreemapControl.cs`: bound zoom scale, panorama/tree switch, no root `Take(80)` sample, weak panorama chrome, and retained visible-rect-only rendering/loading.
- `MainWindow.xaml` / `.xaml.cs`: scale binding, viewport-sized panorama extent, and scaled visible-rect coordinates.
- Expanded Core and App xUnit coverage for all-item layout, 6,217 items, threshold, scale binding, and viewport sizing.

### Verification
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror --artifacts-path .artifacts/semantic-panorama`: 0 warnings, 0 errors (2026-08-08 19:05 +08:00).
- `dotnet test HanabePhotoManager.sln -c Release --no-build --artifacts-path .artifacts/semantic-panorama`: Core 365/365, Infrastructure 160/160, App 331/331.
- Isolated artifacts were needed because a user-running app locks default Release DLLs; no process was stopped.

### Remaining Issues
- Manual WPF QA with 6217+ / 11739-item libraries is still required before KI-01, KI-03, KI-07, and KI-14 can be marked resolved.

---

> **Purpose:** Append-only record of every agent modification to this project.  
> **Last Updated:** 2026-08-06  
> **Rule:** Append new entries at top. Never delete or rewrite history.  
> **Related:** [`AGENTS.md`](../AGENTS.md), [`AGENT_HANDOFF.md`](../AGENT_HANDOFF.md), [`CHANGELOG.md`](../CHANGELOG.md)

---

## 2026-08-08 — Codex (Treemap performance and layout)

### Task
Optimize photo-library treemap layout and viewport thumbnail performance without changing import or retouched-output protection behavior.

### Files Changed
- `PhotoTreemapControl.cs` — viewport-sized root overview, semantic-detail threshold/sample cap, clipped category content, and true visible subtree thumbnail paths.
- `ProgressiveTreemapViewModel.cs` — background dimension publication returns to the captured UI context and skips identical dimensions.
- `MainWindow.xaml.cs` — root uses ScrollViewer viewport bounds; content-fit remains subtree-only.
- `MainWindowViewModel.cs` — background header reads use batches; viewport requests use a generation-safe bounded queue instead of cancel/restart.
- Added `JustifiedGalleryLayoutTests.cs`; expanded treemap view-model and control tests.

### Verification
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror` — 0 warnings, 0 errors (2026-08-08 18:53 +08:00).
- `dotnet test HanabePhotoManager.sln -c Release --no-build` — Core 361/361, Infrastructure 160/160, App 328/328.

### Remaining Issues
- Automated tests cover layout and pipeline contracts. Manual WPF QA with a 6217+ / 11739-item real library, including the `已修` filter, remains required before marking KI-01/KI-03/KI-07/KI-08 resolved.

## 2026-08-06 — WorkBuddy (Documentation Pass)

### Task
Update all project documentation to version `0.2.0-alpha.3`. Create missing docs for agent onboarding. Record current implementation state.

### Files Changed
- `src/HanabePhotoManager.App/HanabePhotoManager.App.csproj` — HanabeVersion: `0.2.0-alpha.2` → `0.2.0-alpha.3`
- `src/HanabePhotoManager.App/ReleaseNotes/ReleaseNotesViewModel.cs` — Added `0.2.0-alpha.3` catalog entry
- `AGENTS.md` — Updated version, added feature doc links, revised AI principles
- `AGENT_HANDOFF.md` — Complete rewrite: status, known issues, key files, verification
- `CHANGELOG.md` — **Created** — full changelog `0.1.0-alpha` through `0.2.0-alpha.3`
- `docs/current-status.md` — **Created** — feature-by-feature state with status labels
- `docs/features/photo-library.md` — **Created** — filter pipeline, categories, file types, thumbnail loading
- `docs/architecture/photo-treemap.md` — **Created** — two-layer layout, classes, rendering pipeline, data flow
- `docs/known-issues.md` — **Created** — 14 tracked issues with reproduction steps and status
- `docs/agent-change-log.md` — **Created** — this file

### Implementation
Documentation-only pass. No business logic, UI, or layout code modified.

### Decisions
- Documentation uses standardized status labels: Stable / Implemented-Unverified / Partial / In Progress / Planned / Known Issue / Blocked / Resolved
- Agent entry point order: AGENTS.md → AGENT_HANDOFF.md → current-status.md → feature docs
- Known issues use KI-XX numbering for cross-reference

### Verification
- Build: not run (documentation-only change)
- Git status: clean apart from these doc files

### Remaining Issues
- All 14 known issues documented; none resolved in this pass
- Root overview mode (KI-14) still blocked pending redesign

### Next Recommended Step
- Fix and re-verify KI-01 through KI-07 (treemap rendering stability)
- Redesign root overview mode (KI-14)
- Run full regression test suite

### Risk / Rollback
- Low risk — documentation-only
- Rollback: `git revert` the commit

---

## 2026-08-05 ~ 2026-08-06 — WorkBuddy (Multiple Sessions)

### Summary
Multiple sessions implementing treemap features including: Justified Gallery inner layout, file type filter, retouch filter crash fix, date filter fix, recursive 修后 scan, viewport-driven loading, borderless mode, subtree item count, Space+drag panning, and attempted root overview mode (later reverted).

### Key Commits (on `codex/photo-treemap-browser`)
- `dd1a573` — Revert overview mode
- `5ce0a70` — Subtree full-content scrolling (ContentHeight)
- `236eef3` — Recursive 修后 scan
- `cf31c20` — Justified Gallery fix: file-header aspect ratios + close-fit
- `d4f5ff4` — Root overview mode (reverted)
- `1fe8e33` — Borderless mode + debug border removal
- `c68e824` — File type multi-select filter
- `b2cda53` — UI freeze fix (sync IO removal)
- Many earlier commits for treemap rendering, zoom, pan, category headers

### Remaining Issues
See [`docs/known-issues.md`](known-issues.md) — 14 tracked items.

## 2026-08-08 — Codex

### Summary
- Added explicit SHA-256 exact-duplicate import decisions (skip, import anyway, or locate the existing file) with incoming/existing thumbnail comparison.
- Defined `<library root>\<month>\<date>\修后` as the single read-only retouched-output path policy.
- Kept retouched files visible to exact and perceptual duplicate scans, while preventing their selection/deletion and excluding them from resequencing.
- Tightened viewport thumbnail requests to meaningful tile dimensions and restored the preloaded treemap guard.

### Verification
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror` — 0 warnings, 0 errors.
- `dotnet test HanabePhotoManager.sln -c Release --no-build` — Core 359/359, Infrastructure 160/160, App 327/327.

### Remaining Issues
- Manual WPF smoke test of the new modal (including non-raster/video fallback and Explorer activation) remains pending; automated tests cover the decision policy and filesystem protections.

### Key Architecture Decisions
- Two-layer layout: SquarifiedTreemap (outer) + JustifiedGallery (inner)
- Aspect ratio from file headers (ImageDimensionReader), not thumbnail decode
- Viewport-driven loading with 150ms debounce
- Borderless mode: skip white tile backgrounds, UniformToFill close-fit
- Recursive 修后 scan in background Task.Run
- ContentHeight-based ScrollViewer extent for subtree scrolling
# 2026-08-09 — Semantic search (Chinese-CLIP / ONNX / SQLite)

- Added Core semantic-search contracts and immutable index/query/result/status models.
- Added independently owned Infrastructure tokenizer, 224px ImageSharp preprocessor, SQLite embedding store, local model catalog, and ONNX CPU semantic search service.
- Added independent App semantic search ViewModel, result item ViewModel, view, and code-behind; minimally wired a new navigation page without changing treemap behavior.
- Added Core contract and Infrastructure tokenizer/store tests. Model files remain local-only under LocalApplicationData and are ignored if accidentally placed under the project.
