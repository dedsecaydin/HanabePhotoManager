# Agent Change Log

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
