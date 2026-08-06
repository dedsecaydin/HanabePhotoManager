# Photo Library Feature Document

> **Purpose:** Reference for all photo library browse/filter/display features.  
> **Last Updated:** 2026-08-06  
> **Current Version:** `0.2.0-alpha.3`  
> **Related:** [`docs/architecture/photo-treemap.md`](architecture/photo-treemap.md), [`docs/current-status.md`](current-status.md)

---

## Filter Pipeline

```
AllMediaItems
  → Exclude PSD/PSB (unless ShowPsdFiles enabled)
  → Date filter (single-date or all)
  → Person filter
  → Business category filter
  → Retouch status filter
  → File type filter
  → Rating filter
  → Search text
  → Smart category
  → Custom tags
  → CurrentFilteredItems
```

All filters apply as **intersection** (AND), not replacement.

---

## Business Categories vs File Types

**Business categories** describe the resource's role in the photo workflow:

| Category | Meaning |
|----------|---------|
| RAW生图 | RAW original captures |
| JPG生图 | JPG original captures |
| 修后 | Retouched/edited output files |
| 视频 | Video files |
| action视频 | Action camera videos |
| 素材 | Material/asset files |

**File types** describe the actual disk format:

| Filter | Extensions |
|--------|------------|
| RAW | ARW, CR2, CR3, NEF, NRW, RAF, ORF, RW2, DNG |
| JPG | JPG, JPEG, JPE |
| PNG | PNG |
| 视频 | MP4, MOV, M4V, AVI, MKV |
| PSD | PSD, PSB (hidden by default) |

A retouched JPG file has: **Business Category = 修后, File Type = JPG**.

---

## File Type Filter Interaction

- ToggleButton/Chip style — click to toggle on/off
- Multiple types can be selected simultaneously (RAW + JPG + Video)
- Clicking already-selected type deselects it
- Deselecting all specific types auto-restores "全部"
- Clicking "全部" clears all specific selections
- State persisted in `AppSettings.SelectedFileTypeFilters`
- Filter applies via `_selectedFileTypeFilters` in `ApplyFilters()`

---

## Date Filter

- Calendar defaults to **single-date selection**
- Clicking a new date replaces the previous (no range accumulation)
- "全部日期" clears the selected date
- `SelectedDatePath` drives `SelectDateAsync` → `LoadDateContentAsync` → `RefreshFilteredCache`
- Async tasks use `CancellationTokenSource` — old tasks cancelled on new selection

### Known issues
- Switching from a later date to an earlier date may show empty results (KI-09)
- Date filter may not include all retouched content (KI-10)

---

## Retouch Status Filter

| Value | Behavior |
|-------|----------|
| 全部 | No filter |
| 已修 | Only retouched output files (`IsRetouched && PreviewPath == FullPath`) |
| 未修 | Files without retouched counterpart (`!IsRetouched`) |

### Known issues
- "已修" may crash app (KI-08) — try/catch applied, pending verification

---

## Retouched Directory Rules

1. Recursively scan all subdirectories under 修后/ (not just top-level)
2. Identify as "修后" by checking if path is under retouch root (not just parent directory name)
3. Supported formats (JPG/PNG/etc.) are indexed; PSD skipped silently
4. Single corrupted file does not halt directory enumeration

### Date attribution for retouched files (priority order):
1. Associated original's CaptureDate
2. SourceCaptureDate from retouch metadata
3. Retouched file's own EXIF date
4. File creation time
5. File last-modified time

---

## PSD / PSB Handling

- **Default: hidden** from browse results, treemap, and counts
- `IsShowingPsdFiles` setting controls visibility
- When enabled, PSD appears as a file type filter option
- PSD files are still scanned into the database for future use
- Scan does not halt on encountering PSD in a directory

---

## Thumbnail Loading

- **Layout and thumbnail loading are separate** — items have layout positions regardless of thumbnail state
- **Viewport-driven**: only visible items load; priority by distance from viewport center
- 150ms debounce on scroll/zoom/pan before triggering load
- `RefreshTreemapViewportLoading(paths)` → `StartPreviewThumbnailLoading(items, 512)`
- Concurrent decode limit: `PreviewLoadingPolicy.ThumbnailConcurrency` (default 4)
- `ImageDimensionReader` reads JPEG/PNG headers for aspect ratio (no pixel decode)

---

## Item Counts

| Display | Source |
|---------|--------|
| Top bar "当前范围: X 个媒体文件" | `FilteredPreviewCount` (grid) or tree item count |
| Bottom-right "共 X 项" | `CurrentViewItemCount` — subtree-aware in treemap mode |

### Known issues
- Subtree count may not update on all navigation paths (KI-12)
- Both counts should always reflect the same `CurrentFilteredItems` source

---

## Display Modes

| Mode | Implementation |
|------|---------------|
| Grid | `ZoomableUniformSquarePanel`, progressive thumbnails, Ctrl+scroll zoom |
| Treemap | `PhotoTreemapControl`, Squarified outer + Justified inner |
| Timeline | Standard timeline view |
| List | Data list view |

---

## Key Source Files

| File | Purpose |
|------|---------|
| `MainWindowViewModel.cs` (~7000 lines) | Filter orchestration, date loading, treemap bridge, thumbnail loading |
| `MainWindow.xaml` | Browse page XAML (filters, grid, treemap ScrollViewer) |
| `MainWindow.xaml.cs` | Treemap zoom/pan/viewport events |
| `PhotoTreemapControl.cs` | Treemap rendering (OnRender, DrawRoot, DrawTile) |
| `ProgressiveTreemapViewModel.cs` | Treemap data (BeginScan, ApplyBatch, PublishNow) |
| `AppSettingsStore.cs` | Persistent settings |
| `ExtensionBadgeConverter.cs` | File type badge for grid tiles |
| `LibraryDateSnapshotService.cs` | Date directory scanning |
