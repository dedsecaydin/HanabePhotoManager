# Agent Handoff — Current Project State

> **Purpose:** First-read document for any agent taking over this project.  
> **Last Updated:** 2026-08-09
> **Current Version:** `0.2.0-alpha.3`  
> **Current Branch:** `codex/photo-treemap-browser`  
> **Project Path:** `D:\HanabePhoto`

## 2026-08-09 Semantic Search Integration

- Semantic search is now embedded in Photo Library browse conditions; the standalone navigation destination/page host is removed.
- First query performs cancellable background indexing and then feeds CLIP-ranked paths through the existing browse grid/treemap and filters.
- Published inference requires the explicit `System.Numerics.Tensors` 9.0.0 dependency now declared by Infrastructure.
- Final verification for this change: Release build 0 warnings/0 errors; Core 369, Infrastructure 163, App 349 tests passed; installed one-photo semantic smoke query reached “已按语义相关度排序”。

---

## Quick Status

> **Latest verification:** Release build completed with 0 warnings / 0 errors at **2026-08-09 05:21 +08:00**; full tests passed (Core 365, Infrastructure 160, App 336). The isolated `.artifacts/agent-verification` output was used and did not touch a user-running executable.

| Item | State |
|------|-------|
| Last build | ✅ 0 errors, 0 warnings (Release, 2026-08-09 05:21 +08:00) |
| Last pushed commit | `dd1a573` — Revert overview mode |
| Active area | 照片图库 → 空间树图 (photo library → treemap) |
| Critical bugs | None known to crash app on normal use |
| Blocked | Root overview mode (reverted, needs redesign) |

---

## Completed (Verified)

| Feature | Verified | Notes |
|---------|----------|-------|
| Outer Squarified Treemap categories | ✅ | RAW生图/JPG生图/修后/视频/action视频/素材 |
| Category header labels | ✅ | Dynamic `item.Label`, left-aligned with separator |
| Subtree navigation & breadcrumbs | ✅ | `NavigateToAncestor`, `ZoomTo` |
| File type multi-select filter | ✅ | RAW/JPG/PNG/Video chips, toggle, PSD excluded |
| Space+drag canvas panning | ✅ | Hold Space + left-drag to scroll treemap |
| `CurrentViewItemCount` subtree-aware count | ✅ | Uses `CurrentContainerKey` filtering |
| Borderless mode | ✅ | `IsBorderless` DP, persisted in settings |
| MapPage WebView2 crash fix | ✅ | try/catch 0x800700AA, deferred retry |
| Calendar single-date mode | ✅ | `SelectedDate` setter replaces previous |
| Import exact-duplicate decision | ✅ | SHA-256 after size prefilter; explicit skip/import/Explorer decision with side-by-side thumbnails |
| Retouched directory write protection | ✅ | `<root>\<month>\<date>\修后` is scan-only; duplicate deletion and resequencing exclude it |
| Recursive 修后 scan | ✅ | `RecurseSubdirectories=true` in Task.Run |
| File type badges (grid) | ✅ | `ExtensionBadgeConverter`, dark-bg white text |

---

## Partial / Unverified

| Feature | Status | Notes |
|---------|--------|-------|
| Justified Gallery inner layout | **Partial** | `JustifiedGalleryLayout.cs` exists, `DrawRoot` uses it. Aspect ratios from `ImageDimensionReader` (file header). Real-world verification shows it still needs tuning. |
| Viewport-driven thumbnail loading | **Partial** | `RefreshTreemapViewportLoading` + 150ms debounce. Works for current viewport but may not reliably catch all edge cases. |
| Subtree full-content scrolling | **Partial** | `DrawSubtreeWithJustifiedLayout` + `ContentHeight` → `UpdateTreemapSize`. Works for smaller sets but unverified for 6217+ items. |
| "已修" filter | **Unverified** | `PreviewRetouchFilter` logic merged standalone retouched files. Not fully regression-tested. |
| Date→修后 attribution | **Unverified** | Recursive scan merged; date inheritance rules documented but not exhaustively tested. |

---

## Known Issues (Not Yet Resolved)

| ID | Issue | Status |
|----|-------|--------|
| KI-01 | Treemap only loads first batch of thumbnails, then stops | Fix attempted, unverified |
| KI-02 | Thumbnails once appeared only in single column | Resolved (viewport intersection) |
| KI-03 | Justified Gallery still resembles fixed grid at times | In progress |
| KI-04 | Large white gaps inside tile rects | Partial (UniformToFill used, aspect ratio still being tuned) |
| KI-05 | 6217+ items may only show first ~dozen | Partial (subtree scrolling implemented) |
| KI-06 | Bottom items clipped to thin slivers | Partial (ContentHeight fix) |
| KI-07 | UI hang on large treemap open | Fix attempted (async dimension reading) |
| KI-08 | Click "已修" may crash app | Fix attempted (try/catch + standalone merge) |
| KI-09 | Date 27→25 switching shows empty result | Fix attempted (single-date mode) |
| KI-10 | Date filter may miss 修后 content | Fix attempted (recursive scan) |
| KI-11 | 修后 subdirectories not recursively indexed | Fix attempted |
| KI-12 | Subtree count once showed global total | Resolved (CurrentViewItemCount) |
| KI-13 | PSD default exclusion not fully wired | Partial |
| KI-14 | Root overview "fit all" not implemented | Reverted, needs redesign |

For details see [`docs/known-issues.md`](docs/known-issues.md).

---

## Next Priority (Recommendation)

1. **Stabilize Justified Gallery** — ensure aspect ratios are correct before layout, re-layout after thumbs load
2. **Root overview redesign** — simpler approach: just viewport-size control + Squarified fills naturally + semantic zoom thresholds
3. **Full regression test** — date switching, filter combinations, subtree navigation, large library scrolling

---

## Key Code Files

| File | Role |
|------|------|
| `src/HanabePhotoManager.App/Browsing/Treemap/PhotoTreemapControl.cs` | Main treemap rendering (OnRender, DrawRoot, DrawTile, DrawSubtreeWithJustifiedLayout) |
| `src/HanabePhotoManager.App/Browsing/Treemap/ProgressiveTreemapViewModel.cs` | Treemap data model (BeginScan, ApplyBatch, Complete, PublishNow, UpdateThumbnail) |
| `src/HanabePhotoManager.App/Browsing/Treemap/TreemapItemViewModel.cs` | Treemap node record (Key, Label, AspectRatio, Thumbnail, etc.) |
| `src/HanabePhotoManager.App/Browsing/Treemap/ImageDimensionReader.cs` | Fast JPEG/PNG header dimension reader |
| `src/HanabePhotoManager.Core/Browsing/Treemap/JustifiedGalleryLayout.cs` | Justified gallery layout algorithm |
| `src/HanabePhotoManager.Core/Browsing/Treemap/SquarifiedTreemapLayout.cs` | Outer squarified treemap for categories |
| `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs` | Main VM (~7000 lines): filters, date, retouch, treemap bridge, thumbnail loading |
| `src/HanabePhotoManager.App/MainWindow.xaml` | Main window XAML (treemap ScrollViewer, breadcrumbs, filter UI) |
| `src/HanabePhotoManager.App/MainWindow.xaml.cs` | Code-behind: zoom, pan, viewport loading, fit-to-view |

---

## Do Not Modify Without Explicit Permission

- Outer Squarified treemap area allocation
- Business category definitions (RAW生图/JPG生图/修后/视频)
- File scanning infrastructure
- Build/test commands
- `Directory.Build.props` (WarningsAsErrors)
- `global.json` (SDK version)

---

## Verification Commands

```
dotnet restore HanabePhotoManager.sln
dotnet build HanabePhotoManager.sln -c Debug /warnaserror
dotnet test HanabePhotoManager.sln -c Debug --no-build
```

---

## Document Sync Rules

After any code change:
1. Append to [`docs/agent-change-log.md`](docs/agent-change-log.md)
2. If a bug was fixed → update [`docs/known-issues.md`](docs/known-issues.md)
3. If feature state changed → update [`docs/current-status.md`](docs/current-status.md)
4. If version bumped → update this file + [`CHANGELOG.md`](CHANGELOG.md)
