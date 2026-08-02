# Progressive Photo Treemap Design

## Goal

Add a second, modular Browse presentation that visualizes the selected photo directory as a progressively updating treemap while the existing card grid remains available.

## User-visible behavior

- Browse exposes `网格` and `空间树图` display modes; switching does not restart the scan or lose the selected date and filters.
- The treemap exposes `按文件大小` and `按照片数量` weighting. File-size mode allocates area from media byte length. Photo-count mode gives every media item equal weight while folders aggregate descendant counts.
- The first discovered batch is rendered immediately. Later batches resize the existing rectangles and add new ones without waiting for the full directory scan.
- Scan updates are coalesced to a target interval of 150 ms so large libraries do not trigger one WPF layout pass per file.
- Category or folder rectangles contain media rectangles. Clicking a folder zooms into it; a breadcrumb returns to the selected date root.
- A media rectangle large enough to be useful shows its thumbnail and filename. Small rectangles use a category/file-type surface and defer thumbnail loading.
- Selection remains synchronized with the existing preview selection and opens the existing viewer command.
- Loading, partial scan, empty directory, cancellation, and inaccessible file states remain visible and do not discard already discovered media.

## Architecture

### Portable layout policy

`HanabePhotoManager.Core/Browsing/Treemap` owns immutable node, bounds, tile, and weight-mode records plus a deterministic squarified-treemap layout policy. It has no WPF or filesystem dependency. Invalid or zero weights are excluded, output stays inside the requested bounds, and equal inputs produce stable ordering.

### Incremental application model

`ProgressiveTreemapViewModel` in App receives the existing `LibraryDateSnapshotBatch` stream. It deduplicates media paths, aggregates category weights, coalesces layout invalidations, and publishes one immutable tile snapshot at a time. It owns zoom and weight-mode state so the already oversized `MainWindowViewModel` only coordinates the selected date and selected media.

The existing `LibraryDateSnapshotService` remains the scan owner. Its current batches already include full path, category, length, and discovered count, so the initial version reuses that contract instead of creating a second disk scan.

### WPF rendering

`PhotoTreemapControl` is a focused WPF control that draws rectangles with `DrawingContext`/visual children rather than materializing thousands of `Border` controls. It requests thumbnails only for visible tiles above a minimum rendered area. Theme colors come from existing semantic brushes; no page-local raw colors or duplicated templates are introduced.

### Persistence

App settings persist the last Browse display mode and treemap weight mode. Existing Browse snapshot data remains compatible; missing values default to grid and file-size weighting.

## Performance and stability

- Scan IO and layout calculation stay off the UI thread; only immutable snapshots are applied on the dispatcher.
- Layout refresh is cancelable and generation-checked so stale scans cannot replace a newer selected date.
- The layout policy is tested at 1, 10, 1,000, and degenerate inputs.
- The WPF control does not decode thumbnails for tiles below the visibility threshold.
- Stable tie-breaking uses normalized path so repeated layouts do not randomly reorder equal-sized files.

## Reuse decision

- Reuse `LibraryDateSnapshotService`, `LibraryDateSnapshotBatch`, existing thumbnail cache/loading, viewer command, semantic brushes, and navigation segment styles.
- Create a portable layout policy because no current component owns proportional rectangle layout.
- Create a focused view model and renderer because adding this state and drawing behavior to `MainWindowViewModel` would violate the documented MVVM boundary.

## Non-goals

- Scanning arbitrary system drives or non-media files.
- Replacing the existing grid.
- File deletion or filesystem mutation directly from the treemap.
- Reproducing SpaceSniffer branding, colors, or exact private implementation.

## Verification

- Core layout unit tests and App incremental-state tests.
- Release build and full automated test suite.
- Manual scan of a disposable directory while observing progressive resize.
- Light/Dark, compact window, 100%/150% DPI, keyboard focus, empty/error states, and viewer-open smoke tests.

