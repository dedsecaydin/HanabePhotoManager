# Known Issues

## 2026-08-08 KI-14 update: semantic panorama overview

- **Status:** Fix Attempted; manual real-library visual QA remains required.
- **Applied:** At `TreemapZoom <= 0.20`, `PanoramaPhotoLayout` places every current-directory photo in a 1px-gap justified wall. Its logical canvas expands inversely to the zoom, keeping rendered thumbnail height at 32px (with a 24px constructor floor).
- **Performance:** the renderer and thumbnail queue still process only rectangles intersecting the visible viewport.
- **Automated evidence:** Core tests cover a 6,217-photo all-item layout, minimum rendered size, and the threshold; WPF tests cover scale binding and viewport-coordinate conversion.

> **Purpose:** Track all known bugs, incomplete features, and verification-pending fixes.  
> **Last Updated:** 2026-08-06  
> **Current Version:** `0.2.0-alpha.3`  
> **Status Labels:** Open / Fix Attempted / Resolved / Blocked / Cannot Reproduce

---

## Treemap Rendering

### KI-01 — Treemap only loads first batch of thumbnails, then stops

- **Status:** Fix Attempted
- **Symptom:** After opening treemap, a few thumbnails appear then loading stops permanently. Remaining tiles stay as placeholders.
- **Impact:** Most visible tiles never show their photo content.
- **Clue:** Multiple `CancelPreviewThumbnailLoading()` calls from `BrowseDisplayMode` setter + `SelfHealTreemapThumbnailsAsync`. Also `RebuildVisiblePreviewPage` clearing thumbnails.
- **Fix applied:** Removed duplicate cancel calls; added `skipCancel` parameter; `_treemapLoadActive` guard.
- **Verification:** Not yet verified at scale.
- **Files:** `MainWindowViewModel.cs` (StartPreviewThumbnailLoading, SelfHealTreemapThumbnailsAsync)

### KI-02 — Thumbnails once appeared only in single column

- **Status:** Resolved
- **Symptom:** Only middle column of treemap tiles had thumbnails; left and right columns were blank.
- **Fix:** Viewport intersection calculation fixed in `DrawRoot` and `DrawItems`. `regions.Add` moved before `IntersectsWith` check.
- **Verified:** 2026-08-05
- **Files:** `PhotoTreemapControl.cs`

### KI-03 — Justified Gallery still resembles fixed grid

- **Status:** In Progress
- **Symptom:** After `JustifiedGalleryLayout` implementation, tiles still appear near-square with internal whitespace. Not forming tight photo-wall.
- **Clue:** `AspectRatio` defaults to 1.0 until thumbnails load. Layout computed before real ratios arrive. No re-layout trigger after ratio updates.
- **Impact:** Justified gallery effect not visible to user.
- **Files:** `JustifiedGalleryLayout.cs`, `TreemapItemViewModel.cs`, `ProgressiveTreemapViewModel.cs`, `PhotoTreemapControl.cs`

### KI-04 — Large white gaps inside tile rects

- **Status:** Partial
- **Symptom:** Tiles show photo centered with white bars on two sides (letterbox/pillarbox). Not filling tile edge-to-edge.
- **Clue:** `DrawThumbnail` uses `Math.Max` (UniformToFill) for close-fit, but tile Bounds may not match image aspect ratio. Background fill in non-borderless mode.
- **Fix applied:** Borderless mode skips bg fill; UniformToFill used.
- **Files:** `PhotoTreemapControl.cs` (DrawTile, DrawThumbnail)

### KI-05 — 6217+ items only show first ~dozen

- **Status:** Partial
- **Symptom:** When subtree has 6217 items, only the first ~visual tiles are rendered. Remainder invisible/cropped.
- **Clue:** `DrawSubtreeWithJustifiedLayout` + `ContentHeight` implemented. `UpdateTreemapSize` sets `Height = Max(vpHeight, ContentHeight)`. May not work at large scale.
- **Files:** `PhotoTreemapControl.cs`, `MainWindow.xaml.cs`

### KI-06 — Bottom items clipped to thin slivers

- **Status:** Partial
- **Symptom:** Last few rows of tiles appear as 1-2px horizontal lines instead of full tiles.
- **Clue:** `ClipToBounds` or bounds clipping in `DrawRoot`. Content height calculation may be off.
- **Related:** KI-05
- **Files:** `PhotoTreemapControl.cs`

### KI-07 — UI hang on large treemap open

- **Status:** Resolved (2026-08-09)
- **Symptom:** App shows "Not Responding" when opening treemap with many items.
- **Root cause:** `ImageDimensionReader.ReadDimensions()` called synchronously on UI thread in `PublishNow` → `ResolveAspectRatio`. Opening 121+ files = 300-500ms block.
- **Fix applied:** Removed sync IO from `ResolveAspectRatio`. Added `LoadTreemapDimensionsAsync` (Task.Run, 32/batch). `_isPublishing` mutual exclusion.
- **Additional root cause (2026-08-09):** The startup all-library scan published a complete immutable treemap for every 64-item scan batch, and the dimension reader republished it for every 32-item result. At panorama zoom, each redraw also recomputed the layout for every photo. This multiplied all-library work until the process saturated a CPU core.
- **Resolution:** Incremental scan publication is now bounded to the first batch, each 1,024 newly discovered items, and completion. Dimension results are submitted in the same-sized batches, and unchanged panorama snapshots reuse their calculated layout.
- **Verification:** A 30-second launch of the self-contained published app was responsive and consumed 0.17 CPU seconds (7 threads); the prior running build measured 97-123% processor time over five samples.
- **Files:** `ProgressiveTreemapViewModel.cs`, `MainWindowViewModel.cs`

---

## Filter & Browse

### KI-08 — Clicking "已修" may crash app

- **Status:** Fix Attempted
- **Symptom:** Selecting retouch status "已修" causes entire app to close.
- **Clue:** `PreviewRetouchFilter` setter triggers `RefreshFilteredCache` → `ApplyFilters`. The `Where(f => f.IsRetouched && PreviewPath == FullPath)` predicate may throw on null/missing fields.
- **Fix applied:** Wrapped predicate in try/catch with Trace logging. Single file failure skips, doesn't crash.
- **Verification:** Not yet regression tested with all retouch states.
- **Files:** `MainWindowViewModel.cs` (ApplyFilters)

### KI-09 — Date 27→25 switching shows empty result

- **Status:** Fix Attempted
- **Symptom:** Click July 27 → shows content. Click July 25 → page goes empty.
- **Clue:** Calendar is single-date mode. May be async race (old 27 task completing after 25 task started). Or 25 truly has no files.
- **Related:** KI-10 (retouched files may not be in date scope).
- **Files:** `MainWindowViewModel.cs` (SelectDateAsync, SelectedDate setter)

### KI-10 — Date filter may miss retouched content

- **Status:** Fix Attempted
- **Symptom:** Selecting a date shows RAW/JPG/Video but no retouched files, even when retouched files should belong to that date.
- **Clue:** Retouched files scanned recursively and merged. Date attribution uses file dates, which may differ from associated original's capture date.
- **Fix:** Recursive scan added; date attribution documented but not exhaustively tested.
- **Files:** `MainWindowViewModel.cs` (RebuildRetouchTrackingAsync)

### KI-11 — Retouched subdirectories not recursively indexed

- **Status:** Fix Attempted
- **Symptom:** Only files directly under 修后/ appear; files in 修后/第一批/, 修后/精修/ etc. are missing.
- **Fix:** `RecurseSubdirectories=true` in `Task.Run` within `RebuildRetouchTrackingAsync`. Merged `retouchMap.EditedFiles` into `PreviewFiles`.
- **Files:** `MainWindowViewModel.cs`

### KI-12 — Subtree count once showed global total

- **Status:** Resolved
- **Symptom:** Entering "修后" subtree, bottom-right still showed "共 11739 项" instead of filtered subtree count.
- **Fix:** `CurrentViewItemCount` uses `CurrentContainerKey` filtering instead of `_filteredCache.Count`.
- **Verified:** 2026-08-05
- **Files:** `MainWindowViewModel.cs`, `MainWindow.xaml`

### KI-13 — PSD default exclusion not fully wired

- **Status:** Partial
- **Symptom:** PSD files may still appear in browse or treemap in edge cases.
- **Clue:** `IsShowingPsdFiles` default false. `ApplyFilters` first step checks this. But PSD may slip through if filter pipeline not applied to all entry points.
- **Files:** `MainWindowViewModel.cs` (ApplyFilters, AppSettingsStore)

### KI-14 — Root overview "fit all" not implemented

- **Status:** Planned / Blocked
- **Symptom:** Opening photo library at root level shows only partial content; user must scroll to see all categories.
- **Target:** All categories visible at once, auto-scaled to viewport, semantic zoom for detail levels.
- **Attempt:** Implemented then reverted (`dd1a573`) due to complexity issues.
- **Next:** Simpler approach — viewport-sized control + Squarified fills naturally + semantic zoom thresholds.
- **Files:** `PhotoTreemapControl.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `MainWindowViewModel.cs`

---

## 2026-08-08 Automated Verification Update

- **KI-01:** The viewport loader no longer cancels and restarts on every debounce. It queues visible paths in bounded batches and ignores stale completion callbacks. Automated treemap tests pass; manual 6217+ library confirmation remains pending.
- **KI-03 / KI-04:** Header dimensions now republish treemap state on the UI context, and the justified-layout tests cover aspect-proportional rows and sparse final rows. Manual visual inspection remains pending.
- **KI-07:** Dimension reads are batched in background work and UI-bound publication is marshalled to the captured synchronization context. Automated verification passes; a 11739-item manual responsiveness run remains pending.
- **KI-08:** No behavior changed; the existing guarded predicate remains in place. A real-library `已修` filter regression is still pending.

## Verification Checklist

For each issue marked "Fix Attempted", verify the following before marking "Resolved":

1. Can reproduce the original symptom
2. Apply the fix
3. Symptom no longer occurs
4. Run full regression: date switching, filter combinations, subtree navigation, large library scrolling
5. No new crashes or hangs introduced
6. Build: 0 errors, 0 warnings
7. Test: all tests pass
