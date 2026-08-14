# Changelog

## 0.2.0-alpha.3 — 2026-08-06

### Added
- File type multi-select filter (RAW/JPG/PNG/Video) with toggle chips in browse conditions
- Extension-to-type-group mapping (RAW: ARW/CR2/CR3/NEF/NRW/RAF/ORF/RW2/DNG; Video: MP4/MOV/M4V/AVI/MKV)
- Justified Gallery inner layout (`JustifiedGalleryLayout.cs`) for treemap category children
- Image dimension fast reader (`ImageDimensionReader.cs`) — JPEG SOF / PNG IHDR header parsing
- `AspectRatio` field on `TreemapItemViewModel`
- Space+drag canvas panning (hold Space + left-drag)
- Category header labels on treemap container tiles (dynamic, left-aligned with separator)
- "适应全部" button (reverted — not functional in this version)
- `CurrentViewItemCount` subtree-aware item count (replaces global `FilteredPreviewCount`)

### Changed
- `DrawThumbnail` from Uniform (contain) to UniformToFill (close-fit) for tighter tile fill
- Borderless mode (`IsBorderless` DP): skip white tile backgrounds, zero-radius images
- Extension badges on grid tiles: dark-bg white text style, stacked with retouch status
- `RefreshFilteredCache` now notifies `IsTreemapRootOverview` and `CurrentViewItemCount`
- Date selection: confirmed single-date mode (click replaces previous, no range accumulation)
- Recursive 修后 directory scan: `RecurseSubdirectories=true` in `Task.Run`
- Treemap subtree layout: `DrawSubtreeWithJustifiedLayout` computes full `ContentHeight`
- `UpdateTreemapSize`: `Height = Max(vpHeight, ContentHeight)` for scrollable subtree content

### Fixed
- MapPage WebView2 `0x800700AA` crash: try/catch with deferred retry
- Treemap initial render blank: `Loaded` event → `UpdateTreemapSize` + `InvalidateVisual`
- Retouched files missing after date filter: merge `retouchMap.EditedFiles` into `PreviewFiles`
- UI freeze from sync file IO: removed `ImageDimensionReader.ReadDimensions()` from UI thread `PublishNow`
- Thumbnail pipeline stall: removed duplicate `CancelPreviewThumbnailLoading` calls; added `_treemapLoadActive` guard
- `PreviewRetouchFilter` "已修" wrapped in try/catch to prevent single-file crash
- PSD/PSB default exclusion from browse results (`IsShowingPsdFiles` default false)

### In Progress
- Justified Gallery layout still needs tuning (aspect ratio accuracy, whitespace reduction)
- Viewport-driven thumbnail loading (150ms debounce) needs edge-case coverage
- Large library (6217+) full-content scrolling stability
- Root overview "fit all" mode (reverted, awaiting redesign)

### Known Issues
Refer to [`docs/known-issues.md`](docs/known-issues.md) for the complete list with reproduction steps.

### Documentation
- AGENTS.md updated with version, new doc links, feature docs
- AGENT_HANDOFF.md rewritten as comprehensive handoff doc
- New: CHANGELOG.md, docs/current-status.md, docs/features/photo-library.md
- New: docs/architecture/photo-treemap.md, docs/known-issues.md, docs/agent-change-log.md

---

## 0.2.0-alpha.2 — 2026-08-04

- Content-level duplicate detection (file hash + visual fingerprint)
- Duplicate review panel with merge/delete
- Apple Photos-style Ctrl+scroll wheel grid zoom (pointer-centered)
- Square grid tiles with UniformToFill cropping
- Progressive thumbnail loading at zoom levels
- Breadcrumb navigation for grid categories
- Scroll/pan with scrollbars and middle-mouse drag

## 0.2.0-alpha.1 — 2026-08-03

- Progressive photo treemap on browse page
- Version tree and scrollable changelog in Settings
- Windows installer with upgrade flow

## 0.1.0-alpha — 2026-07-29

- Foundation: photo management, classification, import, local preview
- Theme, auto-start, basic settings
