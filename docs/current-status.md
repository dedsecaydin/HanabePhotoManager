# Current Status — Feature-by-Feature Implementation State

> **Purpose:** Real-time overview of what's done, what's partial, and what's planned.  
> **Last Updated:** 2026-08-06  
> **Current Version:** `0.2.0-alpha.3`  
> **Status Labels:** Stable / Implemented-Unverified / Partial / In Progress / Planned / Known Issue / Blocked

---

## Photo Library — Browse Conditions

| Feature | Status | Notes |
|---------|--------|-------|
| Date calendar filter | Implemented-Unverified | Single-date mode. 27→25 switching may show empty (KI-09). `SelectDateAsync` + `RefreshFilteredCache`. |
| Person filter | Stable | Face search integration |
| Business category filter | Stable | RAW生图/JPG生图/修后/视频/action视频/素材 |
| Retouch status filter | Implemented-Unverified | "已修" may crash (KI-08). try/catch applied. |
| File type filter | Implemented-Unverified | RAW/JPG/PNG/Video multi-select chips. PSD excluded. |
| Rating filter | Stable | |
| Search | Stable | |
| Smart category | Stable | |
| Custom tags | Stable | |
| Manual classification | Stable | |

## Photo Library — Display

| Feature | Status | Notes |
|---------|--------|-------|
| Grid view | Stable | Zoomable, square tiles, UniformToFill, progressive thumbnails |
| Timeline view | Stable | |
| List view | Stable | |
| Item count (bottom-right) | Implemented-Unverified | `CurrentViewItemCount` — subtree-aware. May not track all edge cases. |
| Top bar item count | Implemented-Unverified | `PreviewSummaryText` — same data source as bottom count |

## Space Treemap — Architecture

| Feature | Status | Notes |
|---------|--------|-------|
| Outer Squarified layout | Stable | Category area allocation by count or file size |
| Category headers | Stable | Dynamic labels with separator, follows container bounds |
| Justified Gallery inner layout | Partial | `JustifiedGalleryLayout.cs` exists. `DrawRoot` uses it with `_galleryLayout.Arrange()`. Real-world testing shows it still resembles fixed grid; whitespace not fully eliminated. |
| Aspect ratio data | Partial | `ImageDimensionReader` reads JPEG/PNG headers. `ResolveAspectRatio` fallback to 1.5. Background `LoadTreemapDimensionsAsync`. May not refresh layout after dimensions arrive. |
| Borderless mode | Stable | `IsBorderless` DP, persisted. Skip white bg, zero-radius draw. |
| Semantic zoom | Planned | Threshold-based detail levels (color block / thumbnail / badge). Root overview reverted. |

## Space Treemap — Navigation

| Feature | Status | Notes |
|---------|--------|-------|
| Subtree enter/exit | Stable | `ZoomTo(key)`, `NavigateToAncestor(null)` |
| Breadcrumbs | Stable | `TreemapBrowser.Breadcrumbs` |
| Space+drag panning | Stable | ScrollViewer offset manipulation |
| Ctrl+scroll zoom | Stable | 0.5x–30x range |
| Root overview "fit all" | Planned | Reverted (dd1a573). Needs simpler approach. |
| "适应全部" button | Reverted | Was in UI, non-functional, removed |

## Space Treemap — Thumbnail Loading

| Feature | Status | Notes |
|---------|--------|-------|
| Viewport-driven loading | Partial | 150ms debounce. `RefreshTreemapViewportLoading()` called on scroll/zoom/pan. May not cover all triggers. |
| Priority queue | Partial | Current viewport items submitted first. No explicit priority levels. |
| Pipeline stall recovery | Implemented-Unverified | `SelfHealTreemapThumbnailsAsync` (skipCancel). `_treemapLoadActive` guard. |
| First-batch-only bug (KI-01) | Fix attempted | Removed duplicate Cancel calls. Unverified. |
| Single-column-only bug (KI-02) | Resolved | Fixed viewport intersection logic |
| Async dimension reading | Implemented-Unverified | `LoadTreemapDimensionsAsync` — Task.Run batch read. |

## 修后 (Retouched) Directory

| Feature | Status | Notes |
|---------|--------|-------|
| Recursive scan | Implemented-Unverified | `RecurseSubdirectories=true` in Task.Run. Merged into PreviewFiles. |
| Date attribution | Implemented-Unverified | Documented priority: CaptureDate > EXIF > creation > modified. Not exhaustively tested. |
| Standalone file merge | Implemented-Unverified | `retouchMap.EditedFiles` → PreviewFiles + RetouchedFiles. |
| PSD skip in scan | Implemented-Unverified | Single unsupported format does not halt enumeration. |

## Settings & Persistence

| Feature | Status | Notes |
|---------|--------|-------|
| `IsTreemapBorderless` | Stable | Saved to `AppSettings.IsTreemapBorderless` |
| `ShowPsdFiles` | Implemented-Unverified | Controls PSD visibility in browse |
| `SelectedFileTypeFilters` | Implemented-Unverified | Persisted filter chip state |
| `TreemapWeightMode` | Stable | Saved in settings |
| `TreemapZoom` | Stable | Reset on overview trigger |

## Performance

| Concern | Status | Notes |
|---------|--------|-------|
| UI hang on large treemap (KI-07) | Fix attempted | Sync `ImageDimensionReader` removed from UI thread. Background batch reading added. |
| 6217+ items scrolling | Partial | `ContentHeight` → ScrollViewer.ExtentHeight. Unverified at scale. |
| Bottom items clipped (KI-06) | Partial | Same fix as above. |
| 10k+ items layout time | Unknown | Not benchmarked. `JustifiedGalleryLayout` is O(n). |

## Cross-cutting

| Item | Status |
|------|--------|
| MapPage WebView2 crash | Resolved |
| Duplicate detection | Stable | SHA-256 exact matching after size prefilter; visual hash remains review-only; explicit import decision dialog |
| Retouched output write protection | Stable | `<root>\<month>\<date>\修后` files remain scan-visible but are disabled in review, filtered before delete, and skipped by resequencing |
| Cloud provider pages | Stable |
| Face recognition | Stable |
| Import flow | Stable |
