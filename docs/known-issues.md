# Known Issues

## 2026-10-23 — HEIF 自动转 JPG 与导入查重缓慢

- **Status:** Implemented and verified with real HEIF fixture and controlled performance test; actual NAS throughput and Sony HDR rendering remain unmeasured.
- **Cause:** 导入前对每个来源重复读取整日期目录的 Windows 备注，且逐次创建 STA 线程；HEIF 先前仅归类和复制，没有生成 JPG。
- **Fix:** 精确哈希优先；备注按目标目录建立索引并持久缓存；保留 HEIF 原件并用随包解码库生成 JPG，已有文件/计划路径不覆盖。转换回执与导入恢复协作。
- **Verification:** 867 项通过。受控场景 800→40 次备注读取，约 12.18→0.63 秒；含旧跳过记录与部分移动后丢失原件计划映射的红/绿回归。

## 2026-10-23 — 显示“全部跳过”但重复文件仍被导入

- **Status:** Automated reproduction and fix verified; real-library reimport not performed.
- **Root cause:** 导入页查重开关和单选项写死为选中/禁用，未绑定实际状态；实际设置关闭查重。另有续传不查重的问题，已用旧计划复现“成功 1、跳过 0”。
- **Fix:** 真实开关和持久化策略绑定；默认开启/跳过；续传对未开始校验的项目应用当前查重策略。修复后测试得到“成功 0、跳过 1”，源文件与已有副本保留。
- **Verification:** 854 项测试通过，含复制/移动、续传、明确仍导入、HEIF/HEIC/HIF 首次导入与二次跳过；Light/Dark 控件检查。已有用户照片未删除。

## 2026-10-02 — 设备接入提示后未进入导入 / 便携包提示缺少 FFmpeg

- **Status:** Fix Attempted; physical insertion and target-PC interactive acceptance remain pending.
- **Evidence:** 旧提示使用系统 MessageBox；确认后的导航依赖刷新设备枚举的精确路径匹配，还会在未选图库时被二次选择框取消。便携包的 FFmpeg 在 `tools` 中，旧视频调用只查找 PATH。
- **Fix:** 使用主题化提示和明确操作；按已检测的来源路径准备导入，无图库时在页面内选择；使用包内 FFmpeg 的绝对路径启动。
- **Verification:** 新增 8 项来源路径、目标安全边界、提示按钮及 FFmpeg 定位回归；全量 840 项通过。浅色和深色截图已检查。

## 2026-08-29 中断导入续传无响应

- **Status:** Resolved
- **Symptom:** 启动时显示系统“继续导入”询问框，点击“是”后仍停留在浏览页，看不到恢复进度；旧快照目标信息不足时还可能静默停止。
- **Fix:** 改为主题化三选项窗口，继续前导航至导入页；恢复与核对阶段持续显示进度；旧快照仅在目标日期目录唯一时迁移，否则保留记录并显示原因；目标目录按组解析以避免重复 UNC 探测。
- **Verification:** 新增 4 项恢复窗口及目标解析回归测试；Release 全量 701 项通过。

## 2026-08-14 UI 修复批次（B1/B5–B22）已知项

- **日期分组标题「红色矩形框」（B17）**：已排查，全仓无硬编码红色（唯一 `Status.Danger` 仅用于关闭按钮/导入失败/看图器错误），日期分组头 XAML 无红元素；判定为日期头被 `VirtualizingWrapPanel HeaderHeight=56` 裁切的渲染残留，随 B19 把 `HeaderHeight` 提到 64 一并消除。若真人验收仍见红色框，需截图定位。
- **云盘（网盘）功能已移除（B12）**：一级菜单「网盘」、CloudPage、CloudHubViewModel、百度/夸克 Provider、云传输队列/索引等全部删除；视频预览（LibVLCSharp）保留。设置页「云盘与项目」分区整块删除。相关测试同步删除（994→585 全绿）。

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

- **Status:** Resolved (2026-08-13)
- **Symptom:** After opening treemap, a few thumbnails appear then loading stops permanently. Remaining tiles stay as placeholders.
- **Impact:** Most visible tiles never show their photo content.
- **Clue:** Multiple `CancelPreviewThumbnailLoading()` calls from `BrowseDisplayMode` setter + `SelfHealTreemapThumbnailsAsync`. Also `RebuildVisiblePreviewPage` clearing thumbnails.
- **Fix applied:** Removed duplicate cancel calls; added `skipCancel` parameter; `_treemapLoadActive` guard + `_treemapLoadGeneration` generation counter so only the current drain loop re-queues on completion.
- **Verification:** `DrainTreemapThumbnailQueue` is single-flight and drains the pending set on each batch completion; `CompleteTreemapThumbnailBatch` drops stale generations after `CancelPreviewThumbnailLoading`.
- **Files:** `MainWindowViewModel.cs` (StartPreviewThumbnailLoading, SelfHealTreemapThumbnailsAsync, DrainTreemapThumbnailQueue)

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
- **Note (2026-08-13):** the layout itself is now memoized per (ItemsSource + RootKey + width), so re-layout after aspect-ratio arrivals no longer costs O(n) per scroll frame — but the visual "still resembles grid" tuning (row height / min-fill) remains outstanding.
- **Files:** `JustifiedGalleryLayout.cs`, `TreemapItemViewModel.cs`, `ProgressiveTreemapViewModel.cs`, `PhotoTreemapControl.cs`

### KI-04 — Large white gaps inside tile rects

- **Status:** Partial
- **Symptom:** Tiles show photo centered with white bars on two sides (letterbox/pillarbox). Not filling tile edge-to-edge.
- **Clue:** `DrawThumbnail` uses `Math.Max` (UniformToFill) for close-fit, but tile Bounds may not match image aspect ratio. Background fill in non-borderless mode.
- **Fix applied:** Borderless mode skips bg fill; UniformToFill used.
- **Files:** `PhotoTreemapControl.cs` (DrawTile, DrawThumbnail)

### KI-05 — 6217+ items only show first ~dozen

- **Status:** Resolved (2026-08-13)
- **Symptom:** When subtree has 6217 items, only the first ~visual tiles are rendered. Remainder invisible/cropped.
- **Clue:** `DrawSubtreeWithJustifiedLayout` + `ContentHeight` implemented. `UpdateTreemapSize` sets `Height = Max(vpHeight, ContentHeight)`. May not work at large scale.
- **Fix applied:** `ContentHeight → ScrollViewer.ExtentHeight` sizing, plus 60% `VisibleRowRange` (binary search over Y-monotonic justified rows) so only viewport-intersecting rows are walked/drawn regardless of item count.
- **Files:** `PhotoTreemapControl.cs`, `MainWindow.xaml.cs`

### KI-06 — Bottom items clipped to thin slivers

- **Status:** Resolved (2026-08-13)
- **Symptom:** Last few rows of tiles appear as 1-2px horizontal lines instead of full tiles.
- **Clue:** `ClipToBounds` or bounds clipping in `DrawRoot`. Content height calculation may be off.
- **Related:** KI-05
- **Fix applied:** Same viewport-row range culling + content-height sizing as KI-05.
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

## 2026-08-16 已解决：图库缩放冲突与按钮文字不可见

- **状态：** 已解决并回归验证。
- **原症状：** 图库滚轮与缩放职责混杂；部分主按钮出现有底色但文字不可见；原生选择控件可能留下灰色矩形。
- **修复：** 普通滚轮与 Ctrl 缩放分流，指针锚点补偿；主按钮显式内容前景；选择控件重绘；禁用态改用语义颜色。
- **验证：** 六套主题和八个主要页面完成鼠标复查，610 项自动化测试通过。

For each issue marked "Fix Attempted", verify the following before marking "Resolved":

1. Can reproduce the original symptom
2. Apply the fix
3. Symptom no longer occurs
4. Run full regression: date switching, filter combinations, subtree navigation, large library scrolling
5. No new crashes or hangs introduced
6. Build: 0 errors, 0 warnings
7. Test: all tests pass
## 2026-10-03 — 恢复测试关闭连接时的收尾竞争

- 远端读取测试在关闭会话后，`BinaryWriter.Dispose` 的最后一次 Flush 偶发收到 `Pipe is broken`；异常发生在原 catch 之外。
- 将测试写入器的 using 生命周期移入原 IOException 处理范围，保留全部读取断言；不修改恢复生产逻辑。最终全量 842 项测试通过。

## 2026-10-28 — 导入集成测试曾间歇等待（待观察）
- 在新增 ImportedSession_UsesDestination_AndResumeSnapshotRetainsAssignment 测试中两次出现无进展；诊断转储未发现同步阻塞的业务线程，尚未确证根因。
- 测试增加 20 秒传输等待上限，失败时输出进度和报告；其后 App 单独 662 项及全解决方案 941 项通过。不能据此宣称间歇问题根因已消除。
- 证据保留于 .artifacts/multi-source-20261028/final-app、bounded 与 regression。真实多卡端到端仍待验收。
- 2026-10-28 后续定位：超时报告显示导入成功后进入“正在可视化扫描媒体文件”；测试通过反射调用传输时未切到 Import 页。测试已补齐页面前置条件，662 项通过；未据此宣称图库刷新路径不存在其他等待问题。
