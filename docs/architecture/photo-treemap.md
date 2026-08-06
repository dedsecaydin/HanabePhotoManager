# Photo Treemap Architecture

> **Purpose:** Architecture and data flow reference for the space treemap feature.  
> **Last Updated:** 2026-08-06  
> **Current Version:** `0.2.0-alpha.3`  
> **Related:** [`docs/features/photo-library.md`](../features/photo-library.md), [`docs/current-status.md`](../current-status.md)

---

## Two-Layer Layout Architecture

```
┌─────────────────────────────────────────────┐
│ Outer Layer: SquarifiedTreemapLayout        │
│   Input: Category items (containers)        │
│   Output: Category rects (area by count or  │
│           file size)                         │
│   Areas: RAW生图 / JPG生图 / 修后 / 视频   │
│          / action视频 / 素材                │
├─────────────────────────────────────────────┤
│ Inner Layer: JustifiedGalleryLayout         │
│   Input: Child items + aspect ratios        │
│   Output: Dynamic rects per image aspect    │
│   Mode: Row-based justified packing         │
│   Gap: 0–2px between tiles                 │
└─────────────────────────────────────────────┘
```

---

## Key Classes & Responsibilities

### Core Layer

| Class | File | Role |
|-------|------|------|
| `SquarifiedTreemapLayout` | `Core/Browsing/Treemap/SquarifiedTreemapLayout.cs` | Outer category area allocation algorithm |
| `JustifiedGalleryLayout` | `Core/Browsing/Treemap/JustifiedGalleryLayout.cs` | Inner row-based justified packing. Parameters: `targetRowHeight=180`, `minAspect=0.35`, `maxAspect=3.5`, `gap=1`. `Arrange(items, containerWidth)` → `IReadOnlyList<JustifiedItem>` |
| `TreemapBounds` | `Core/Browsing/Treemap/TreemapModels.cs` | Layout coordinate struct (X, Y, Width, Height) |
| `TreemapNode` | `Core/Browsing/Treemap/TreemapModels.cs` | Input node for squarified layout (Key, Label, Weight, IsContainer) |

### App Layer

| Class | File | Role |
|-------|------|------|
| `PhotoTreemapControl` | `App/Browsing/Treemap/PhotoTreemapControl.cs` | WPF `FrameworkElement` — `OnRender`, `DrawRoot`, `DrawTile`, `DrawSubtreeWithJustifiedLayout`, hit testing |
| `ProgressiveTreemapViewModel` | `App/Browsing/Treemap/ProgressiveTreemapViewModel.cs` | Data pipeline: `BeginScan` → `ApplyBatch` → `PublishNow`. Owns `Items`, `Breadcrumbs`, `CurrentContainerKey`. Handles `UpdateThumbnail(path, bitmap)` |
| `TreemapItemViewModel` | `App/Browsing/Treemap/TreemapItemViewModel.cs` | Immutable record: Key, ParentKey, Label, Weight, IsContainer, FullPath, Length, Category, Extension, Thumbnail, AspectRatio |
| `ImageDimensionReader` | `App/Browsing/Treemap/ImageDimensionReader.cs` | Fast JPEG SOF / PNG IHDR header parser. `ReadDimensions(path)` → `(width, height)?`. Used for aspect ratio without pixel decode. |
| `TreemapHitRegion` | `App/Browsing/Treemap/TreemapHitRegion.cs` | Item + Bounds pair for mouse hit testing |

### ViewModel Bridge

| Location | Role |
|----------|------|
| `MainWindowViewModel.TreemapBrowser` | `ProgressiveTreemapViewModel` instance |
| `MainWindowViewModel.RepopulateTreemapFrom()` | Rebuilds treemap from `FilteredPreviewFiles` |
| `MainWindowViewModel.RefreshTreemapViewportLoading()` | Triggers viewport-driven thumbnail loading |
| `MainWindowViewModel.LoadTreemapDimensionsAsync()` | Background dimension pre-reading |

---

## Rendering Pipeline

```
PropertyChanged / ItemsSource updated
    ↓
OnRender (FrameworkElement)
    ↓
[Root mode]              [Subtree mode]
DrawRoot()               DrawSubtreeWithJustifiedLayout()
    ↓                        ↓
CalculateLayout()        _galleryLayout.Arrange()
(categories)             (all children)
    ↓                        ↓
For each category:       For each child:
  DrawTile(container)      DrawTile(child)
  _galleryLayout.Arrange() 
  For each child:
    DrawTile(child)
    ↓
DrawTile:
  1. Compute rect from bounds
  2. Track visible items (VisibleItemPaths)
  3. If borderless: skip bg, draw image + badge
  4. If bordered: draw rounded rect + image + badge
  5. Container: draw header bar + label
  6. Semantic zoom: skip thumbnails < 4px, badges < 48px
```

---

## Thumbnail Loading Flow

```
RepopulateTreemapFrom(files)
    ↓
TreemapBrowser.BeginScan → ApplyBatch → Complete → PublishNow
    ↓
OnRender → collect VisibleItemPaths → VisibleItemPathsNeedingThumbnail
    ↓
TreemapViewportDebounceTimer (150ms)
    ↓
RefreshTreemapViewportLoading(paths)
    ↓
StartPreviewThumbnailLoading(items, 512)
    ↓
LoadPreviewThumbnailsAsync (SemaphoreSlim, 4 concurrent)
    ↓
TryLoadThumbnail(filePath, decodeWidth) → BitmapSource
    ↓
Dispatcher.InvokeAsync:
  item.Thumbnail = bitmap
  TreemapBrowser.UpdateThumbnail(path, bitmap)
    ↓
PublishNow → _items updated → PropertyChanged → InvalidateVisual → OnRender
```

---

## Hit Testing

- `OnRender` populates `_hitRegions` (List of TreemapHitRegion)
- `OnMouseLeftButtonDown`: `FindItemAt(_hitRegions, x, y)` → Z-order reverse search
- Container click → `ZoomCommand.Execute(key)` → `ZoomTo(key)` (subtree)
- Non-container double-click → `OpenItemCommand.Execute(FullPath)`
- Overview mode: `ToContentCoordinates()` reverses scale/offset transform

---

## Viewport & Scrolling

- `ScrollViewer` in XAML wraps `PhotoTreemapControl`
- `VisibleRect` DP synced from `ScrollViewer.HorizontalOffset/VerticalOffset/ViewportWidth/ViewportHeight`
- `SyncTreemapVisibleRect()` called on SizeChanged, ScrollChanged, pan start/end
- Subtree mode: `ContentHeight` computed from last justified item → `UpdateTreemapSize` sets `Height = Max(vpHeight, ContentHeight)`
- Root overview (planned): set Height = vpHeight, Squarified fills naturally

---

## Data Flow Diagram

```
LibraryDateSnapshotService.LoadAsync(datePath)
    ↓
PreviewFiles (List<PreviewFileViewModel>)
    ↓
RebuildRetouchTrackingAsync:
  - Detect retouched files in PreviewFiles
  - Recursively scan 修后/ subdirectories
  - Merge standalone into PreviewFiles + RetouchedFiles
    ↓
RefreshFilteredCache:
  - ApplyFilters(categoryItems)
  - _filteredCache = result.ToList()
    ↓
RepopulateTreemapFrom(_filteredCache):
  - TreemapBrowser.BeginScan → ApplyBatch → Complete
  - LoadTreemapDimensionsAsync (background)
    ↓
OnRender:
  - VisibleItemPathsNeedingThumbnail
    ↓
RefreshTreemapViewportLoading(paths)
  - StartPreviewThumbnailLoading(unloaded, 512)
```
