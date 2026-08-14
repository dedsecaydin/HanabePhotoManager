# Agent Handoff — Current Project State

> **Purpose:** First-read document for any agent taking over this project.  
> **Last Updated:** 2026-08-14
> **Current Version:** `0.2.0-alpha.3`  
> **Current Branch:** `codex/photo-treemap-browser`  
> **Project Path:** `D:\HanabePhoto`

## 2026-08-14 人物页功能补全（合并命令 + 详情照片虚拟化）

- 补上轮遗留技术债第 1、2 项（用户确认先补「合并」）；第 3 项「待确认角标」数据缺失、第 4 项「浏览/人物筛选收敛」本轮未做。
- **A 合并命令**：`PeopleAlbumViewModel` 暴露 `MergeCommand`（`CanMerge = SelectedAlbum != null && Albums.Count >= 2`）；`MergeSelectedAsync` 通过可注入 `_mergeTargetPicker`（默认弹新增 `People/MergePersonDialog` 模态窗）选目标 → 复用既有 `PeopleAlbumService.MergeAsync(target, source)`（逻辑零改动）→ `RefreshAlbumsAsync` 刷新列表 → `SelectedAlbum` 落到目标 + `StatusText` 提示。人物详情 hero 加「合并到…」`Button.Secondary`（`PeopleAlbums.MergeCommand`）。破「VM 不动」约束的唯一例外（用户确认）。
- **B 详情照片虚拟化**：新增 `Controls/VirtualizingWrapPanel`（`VirtualizingPanel`+`IScrollInfo`，固定 142×142 步长按视口换行、只 realize 可见行）+ `PersonPhotoViewModel`（懒加载 `Thumbnail`：`Interlocked` 三态 + 静态 `SemaphoreSlim(4)` + `Task.Run` 解码 + Dispatcher 回填）。`PersonAlbumItemViewModel` 保持 `PhotoPaths`（`HashSet<string>`）不变（浏览页筛选仍依赖），另增 `ObservableCollection<PersonPhotoViewModel> Photos` 镜像集合。详情照片区 `ItemsControl`+`WrapPanel`+`PathThumb` → `ListBox`（`List.Default` + `VirtualizationMode=Standard` + `VirtualizingWrapPanel`，`Loaded` 触发懒加载）；详情区从外层 `ScrollViewer` 抽出为 `Grid`（hero 固定 + 照片 ListBox 独立滚动，否则无限高度无法虚拟化）。Tab 1 可见性改由 code-behind `RefreshPeopleTabContent()` 统一驱动（订阅 `PeopleAlbums.SelectedAlbum`）。
- 新增 `--select-first-person` 截图旗标（`App.xaml.cs`）+ `.artifacts/capture-m3-merge.ps1`（播种 120 张人物 A + B/C）。
- 验证：`dotnet build -c Debug /warnaserror` 0 警告 0 错误；`dotnet test` 917 全绿（Core 373 / Infra 164 / App 380，+2 合并命令测试）；截图 `m3-facesearch-merge-{light,dark}.png`（1600×980，含合并 UI）。
- 详见 `docs/agent-change-log.md` 2026-08-14「人物页功能补全」条目、`docs/current-status.md`。

## 2026-08-14 M3 人物页 + 相册页重新设计（对齐 004/005 mockup）

- 按用户确认的预设计（`sketches/004-m3-facesearch` / `005-m3-albums`）重做两个功能页，仅视觉/布局/交互组织，未改 ViewModel / Command / Binding / 数据流。
- **人物页**（`MainWindow.xaml` `FaceSearchPage` + code-behind）：双 Tab（人物相册 / 按脸查找）`Navigation.Segment` 切换；Tab1 = 扫描状态条 + 96px 圆形头像网格（`PeopleAlbums.Albums`）+ 点击进详情（hero 可编辑姓名 + 该人物照片网格）；Tab2 = 左侧参考图/范围/匹配强度滑块 + 右侧结果网格（相似度徽章）。新增 View 层转换器 `NullToVisibilityConverter` / `PathThumbnailConverter` / `FileSizeConverter`。浏览页人物筛选面板保留未动。
- **相册页**（`Albums/CustomAlbumsPage.xaml` + `.cs`）：卡片流总览（16:10 封面 + 名称 + 路径 + 不可用⚠ 角标 + 虚线新建卡 + FAB）→ 详情（hero + 重命名/刷新/移除引用 + 网格/列表切换）+ 320px Inspector（选中照片 EXIF，复用 `PhotoDetailMetadataReader`）；总览/详情/FAB 由 code-behind `_showingAlbumDetail` 驱动。
- 未臆造：mockup 的「合并/导出/待确认角标/人脸数·日期角标/相册卡数量徽章/列表日期」在现有 VM 无对应数据，铁律下未新增，见 change-log。
- 验证：`dotnet build -c Debug /warnaserror` 0 警告 0 错误；`dotnet test` 915 全绿（Core 373 / Infra 164 / App 378）；截图 `.artifacts/m3-facesearch-{light,dark}.png` + `m3-albums-{light,dark}.png`（`.artifacts/capture-m3-pages.ps1`）。
- ⚠ 截图脚本首两版含中文 .ps1（UTF-8 无 BOM）被 GBK 误读导致播种失败，一度改写 `settings.json` LibraryRoot；已恢复 `LibraryRoot=\\Hanabe\拍照`（详见 change-log）。

## 2026-08-14 浏览页筛选面板三轮调整（排序方式进高级筛选）

- 按用户最新要求：把「排列方式（排序）」从主区第 2 行收进高级折叠区，主区只保留「分类 / 修图状态 / 评分分类」。仅表现层，未改 ViewModel / Command / Binding / API / 数据流。
- 高级折叠区由「显示方式 / 面积」变为「显示方式 / 面积 / 排列方式」；`排列方式` `ComboBox`（`PreviewSortComboBox`，绑定 `PreviewSortChoices`/`PreviewSortMode`）原样平移（仅右间距 16→20 对齐兄弟项），绑定 / 命令 / 样式不动。
- 高级折叠动画 180ms `CubicEase` 不变；截图基建新增 `.artifacts/capture-m3-filter3.ps1`。
- 验证：`dotnet build -c Debug /warnaserror` 0 警告 0 错误；`dotnet test` 915 全绿（Core 373 / Infra 164 / App 378）；截图 `m3-filter3-collapsed-light.png` / `m3-filter3-expanded-light.png` 均 exit 0 非空白。
- 详见 `docs/agent-change-log.md` 2026-08-14「筛选面板三轮调整」条目。

## 2026-08-14 浏览页筛选面板二轮调整

- 按用户二轮要求重排浏览页筛选面板（仅表现层，未改 ViewModel / Command / Binding / API / 数据流）：
  - **修图状态、评分分类外置**：从高级折叠区移到主区第 2 行（分类 → 修图状态 → 评分分类 → 排列方式），样式/绑定原样平移。
  - **显示方式收进高级折叠区**：从面板下方常显位置移到高级折叠区第一行；**面积**（树图面积计算方式 `TreemapWeightModes`）同步收进高级折叠区（`IsTreemapBrowseMode` 可见）。
  - **文件类型删除**：移除 RAW/JPG/PNG/视频 segment（XAML 移除；VM `ToggleFileTypeFilter` 与 code-behind `FileTypeFilter_Click` 保留不删）。
  - 原常显「显示方式/面积」DockPanel 整段删除；「扫描过程中会持续更新矩形大小」提示语迁移到高级折叠区末尾右对齐保留。
  - 高级折叠动画 180ms `CubicEase` 不变；截图基建新增 `.artifacts/capture-m3-filter2.ps1`。
- 验证：`dotnet build -c Debug /warnaserror` 0 警告 0 错误；`dotnet test` 915 全绿（Core 373 / Infra 164 / App 378）；截图 `m3-filter2-collapsed-light.png` / `m3-filter2-expanded-light.png` 均 exit 0 非空白。
- 详见 `docs/agent-change-log.md` 2026-08-14「筛选面板二轮调整」条目。

## 2026-08-14 浏览页筛选面板精简合并 + 高级折叠

- 按用户确认方案优化浏览页筛选面板（仅表现层，未改 ViewModel / Command / Binding / API / 数据流）：
  - **搜索合并**：确认已在 VM 层落地（`UnifiedSearchText` + `BrowseSearchMode` Auto/File/Semantic + `ApplyUnifiedSearch`），本轮仅把「智能搜索」卡片提到筛选面板第一行做统一入口，保留模式下拉/取消/进度。
  - **布局重排**：第 1 行「智能搜索」→ 第 2 行高频「分类 Chips / 文件类型 segment / 排列方式」+ 右侧「⚙ 高级筛选」折叠按钮 → 第 3 行高级折叠区（修图状态 / 评分 / 智能识别 / 智能类别 / 识别按钮）。「显示方式」保留在面板下方始终可见。
  - **高级折叠**：默认收起，`MaxHeight`+`Opacity` 双通道 180ms `CubicEase` 动画，折叠状态持久化到设置（`AppSettings.IsAdvancedFiltersExpanded` + `MainWindowViewModel.IsAdvancedFiltersExpanded`/`ToggleAdvancedFiltersCommand`）。
  - **去掉手动类别/自定义标签**：删除多选 Inspector 中「归入分类」「添加标签」两个区块（VM 命令保留未删）。
- 截图基建：新增 `--advanced-filters` 旗标 + `.artifacts/capture-m3-filter.ps1`，产出 `m3-filter-collapsed-light.png` / `m3-filter-expanded-light.png`。
- 验证：`dotnet build -c Debug` 0 警告 0 错误；`dotnet test` 915 全绿（Core 373 / Infra 164 / App 378）。
- 详见 `docs/agent-change-log.md` 2026-08-14 条目。

## 2026-08-14 M3-3 浏览页改版 + M3-4 其余页面适配

- 按 `docs/M3_DESIGN_FINAL.md` 变体 001 完成 M3-3 浏览页改版 + M3-4 其余页面适配。仅视觉层，未改 ViewModel / Command / Binding / API / 数据流。
- M3-3（`MainWindow.xaml` + `Buttons.xaml`/`Inspector.xaml`/`Icons.xaml`）：
  - Workspace 外壳 → `surface-container-lowest` + `Radius.Container` 28；treemap 内嵌 Card 去除。
  - 分类 Chips 改 M3 Chip（`surface-container-high` 未选 / `primary-container` 选中）；修图/文件类型 segment 改 `secondary-container` 选中 tonal。
  - **Inspector 由底部 dock 移到右侧 320px 面板**（`Inspector.Panel`：`surface-container-low` + `Radius.Container` 28），单张 EXIF 纵向 info-row + 操作 Chips、多选批量操作纵向堆叠、新增无选中占位态；绑定/命令/处理器全部复用既有。
  - **新增 56px FAB**（`Button.Fab` + `Icon.Plus`），右下角「＋」复用 `ShowImportCommand`，不遮挡 Statusbar；「共 N 项」改左下。
- M3-4：人物/相册/导入/图片小工具/水印/地图/网盘/投稿/欣赏/设置页均为 Token-first，经共享 Token 自动对齐 M3，**无硬编码颜色残留**；设置页 6 套主题切换 UI 复核正常。
- 截图基建：新增 `--browse-showcase` 旗标（Grid + 展开 Chips），`.artifacts/capture-m3-final.ps1`。
- 验证：`dotnet build -c Debug /warnaserror` 0 警告 0 错误；`dotnet test` 915 全绿（Core 373 / Infra 164 / App 378）；截图浏览页 6 主题 + 11 页面默认主题全部 exit 0 非空白。
- Next：M3-5 回归（构建 + 测试 + 大库实测 + 6 主题截图复核）。
- 详见 `docs/agent-change-log.md` 2026-08-14 M3-3/M3-4 条目、`docs/current-status.md`。

## 2026-08-14 M3-1 主题基建 + M3-2 Shell 改版（大改方向定稿后）

- 用户 2026-08-14 拍板方向大改：旧「克制桌面工具风」→ **M3 浓烈版**（`docs/M3_DESIGN_FINAL.md`），排版用变体 001（Navigation Rail 88px + Topbar + Workspace + Inspector 320 + Statusbar + FAB），三套配色（动态色彩靛蓝 / 森林绿 / 紫罗兰）× 浅深 = **6 套主题**，默认动态色彩·浅色。
- M3-1 主题基建：`ThemeManager` 扩展为「配色 × 明暗」6 套组合（`AppColorScheme { Dynamic, Forest, Violet }` + `AppTheme { Light, Dark }`），主题入口 `Themes/Themes/{Scheme}.{Mode}.xaml`，偏好 `ui-theme.txt` 持久化 `"{Scheme}.{Mode}"`（旧 `Light/Dark` 回退 Dynamic）；新增 6 个 `Colors.<Scheme>.<Mode>.xaml`（M3 tonal 色值）+ 6 个入口字典；`Brushes.Light/Dark.xaml` 新增 M3 语义 Brush（`Brush.Primary` / `Brush.Surface.Container*` / `Brush.OnSurfaceVariant` 等），既有语义键只换值不换键。设置 → 外观新增「配色方案」选择（动态色彩/森林绿/紫罗兰）与明暗组合。默认 `App.xaml` 加载 `Dynamic.Light.xaml`。
- M3-2 Shell：Sidebar 232px → Navigation Rail 88px（`Size.Rail.Width`=88），导航项「图标+文字竖排」，选中态 = secondary-container 背景 + primary-container 圆 icon + on-* 文字/图标色；Topbar 改 `surface-container-low` 大圆角容器，Statusbar 改 44px `surface-container` 大圆角；Radius Token 按 M3 更新（新增 `Radius.Container` 28 / `Radius.Full` 999）。`NavigationDisplayMode` 默认 `Text` → `IconAndText`。
- 验证：`dotnet build -c Debug /warnaserror` 0 警告 0 错误；`dotnet test` 915 全绿（Core 373 / Infra 164 / App 378，+7）；6 套主题截图 `.artifacts/m3-theme-<scheme>-<mode>.png`（`capture-m3-themes.ps1`）。
- 保留全部业务功能（导航 10 项+设置、背景图/玻璃强度/导航显示模式设置、拖拽排序、↑↓ 键盘导航）。**FAB 与 Inspector 320px 属 M3-3（浏览页改版），本轮未做**。
- Next：M3-3 浏览页改版（Workspace 网格 + Chips 筛选 + FAB + Inspector 320px），再 M3-4 其余页面、M3-5 回归。
- 详见 `docs/agent-change-log.md` 2026-08-14 M3-1/M3-2 条目。

## 2026-08-14 UI/UX 70% Inspector + Contextual UI

- 完成 70% Inspector + Contextual UI（`HERMES_MASTER_GUIDE.md` #70）：把 30% 的上下文检查器做完整 + 补上下文操作 + 多选操作。仅表现层，未改业务逻辑 / 命令 / 绑定 / API / 数据流，全程 Design System Token。
- Inspector 完整化：复用既有 `PhotoDetailMetadataReader`（`PhotoViewerWindow` 已用）替换扁平 `ExifSummary`；`MainWindowViewModel` 增 `SelectedFileMetadata`（结构化元数据）、`SelectedFileCount`、`IsMultiSelection`；浏览页底部检查器分区展示 文件信息 / 拍摄参数 / 时间与位置，缺失字段「未记录」占位。
- Context Action：Inspector 内联操作（评分 1–5、标签 人像/风光/废片、打开/文件夹/复制路径/移入回收站）复用既有 code-behind 处理器；右键菜单未动。
- Multi-select actions：多选时底部多选操作条（`已选择 N 张` + 批量复制/移动/智能识别/归入分类/添加标签/移入回收站/清除选择），全部复用既有 VM 命令（`BatchCopyFilesTo`/`BatchMoveFilesTo`/`AnalyzeSelectedPhotosCommand`/`AssignCategoryToSelectedCommand`/`AssignTagToSelectedCommand`/`DeleteSelectedFilesCommand`）。
- 新增 `Inspector.FieldLabel/FieldValue` Token 样式；新增 `--select-first` 截图旗标 + `.artifacts/capture-inspector.ps1`。
- ⚠️ **验证受限**：本会话验证阶段 subprocess（pwsh/grep/glob）以 `STATUS_DLL_INIT_FAILED`（exit 3221225794）失败，`dotnet build` / `dotnet test` / 截图未实际运行，需补跑（详见 `docs/agent-change-log.md` 70% 条目）。
- Next: 80% Remaining Main Pages（`HERMES_MASTER_GUIDE.md` #71）。

## 2026-08-14 UI/UX 50% Home Mid Review P1 修复

- 修复中期评审（`C:\Users\fulia\wxdecrypt\hanabephoto_midreview.md`，aurora gpt-5-6）提出的 6 项 P1（评审结论「基本达标，无 P0」）。不碰 60% 浏览模块，不回滚 60% 首屏缩略图 P1。
- P1-1 信息架构：Home 重排为「轻量库状态行 → 最近照片主视觉 → 快速操作 Compact → 设备沉底」；`Layout.HomeSummary` 由大 Card 改轻量状态行。
- P1-2 缩略图自适应：固定 6 列 → `WrapPanel`（TileMinWidth≈140px，自适应列数），移除内层 MaxHeight 滚动。
- P1-3 缩略图媒体表达：修复根因——应用默认启动在 Preview，导航到 Home 从未触发 `StartPreviewThumbnailLoading(HomePreviewFiles)`（首页全灰占位的真正原因），`CurrentPage` setter 增 `else if (IsHomePage)` 分支；视频 MP4/MOV 增 `Icon.Play` 播放指示 + 类型角标；**Duration Badge 延后**（无时长数据，需元数据读取，超出视觉层约束）。
- P1-4 Dark 选中态：仅调 Dark `Color.Surface.Selected` #343A3E→#485058（对比度 ~1.4→~2.0），未写死颜色。
- P1-5 标题栏：`DwmSetWindowAttribute` + `DWMWA_USE_IMMERSIVE_DARK_MODE` 跟随主题（`ThemeChanged` 订阅 + Loaded 应用）。
- P1-6 快速入口：2×3 卡片网格 → 7 个 `Button.Toolbar` 紧凑按钮（横向 WrapPanel）；实际数量确认为 7（XAML 硬编码 7 个 Show*Command，无 QuickActions 集合），故不折叠。
- `--screenshot` 模式扩展 `--page <Name>` 参数，供无头截图导航到 Home。
- 验证：Debug build 0 警告/0 错误；测试 908 全绿（Core 373 / Infra 164 / App 371，+5 `HomeP1FixTests` 回归）；截图 `.artifacts/home-fix-50-light.png` / `home-fix-50-dark.png`。
- 详细逐项说明见 `docs/agent-change-log.md` 2026-08-14 与 `docs/current-status.md`「Home Mid Review P1 修复」。
- Next: 继续 70% Inspector + Contextual UI（`HERMES_MASTER_GUIDE.md` #70）。

## 2026-08-13 UI/UX Refactor 60% Primary Gallery / Main Content

- Completed the 60% Primary Gallery milestone (`docs/HERMES_MASTER_GUIDE.md` #69) for the core media-browsing module (照片墙 TreemapBrowser / PreviewPage / Browse). Rendering/performance only — no ViewModel / Command / Binding / API / data-flow changes; Design System tokens only.
- `PhotoTreemapControl`: replaced the per-frame full walk of every justified item with `VisibleRowRange` (binary search over Y-monotonic rows) so `OnRender` is O(visible); memoized root categories, per-category children, and justified layouts via `EnsureLayoutCache` so layout is computed once per (ItemsSource + RootKey + width) instead of every scroll frame; added tokenized hover (`Brush.Surface.Interactive` / `Brush.Border.Strong`).
- `MainWindowViewModel`: prebuilt `_treemapSourceLookup` (FullPath → VM) once per repopulation, removing the O(11,739) dictionary rebuild on every debounce tick / batch drain.
- Verification: Debug build 0 warnings/0 errors; tests Core 373 / Infra 164 / App 366 (903 total, +5 new); captured `.artifacts/gallery-60-light.png` / `gallery-60-dark.png` via the new headless `--screenshot` mode against a 96-image synthetic fixture (the real `\\Hanabe\拍照` UNC library is unreachable from this session).
- §40 race audit: date switch / semantic search / treemap thumbnail queue all Latest-Request-Wins (see `docs/agent-change-log.md` 60% table). No P0/P1 found; KI-07 Resolved, KI-05/KI-06 further收敛 by the viewport-range culling + layout cache.
- Next: 70% Inspector + Contextual UI (`HERMES_MASTER_GUIDE.md` #70) — Inspector, Context Action, Multi-select actions, Filter/Search UI, Context Menu, Metadata display + Selection/Inspector Bug Hunt.

## 2026-08-13 UI/UX Refactor 50% Home + Mid Review

- Completed the 50% Home + Mandatory Mid Review milestone (`docs/HERMES_MASTER_GUIDE.md` #68): the Home page now reaches the new design direction (high information density, image-first, restrained motion) using Design System tokens only.
- Rebuilt `HomePage` in `MainWindow.xaml`: hero summary fonts tokenized (`Typography.Title`/`Display`); added a 7-entry **快速入口** grid (导入照片/照片图库/自定义相册/人物查找/地图照片/图片小工具/网盘) reusing existing `Show*Command`s and `Icon.*` geometry tokens; tokenized the recent-photos and devices sections (radius + font sizes); removed the deprecated bottom「去导入/去预览/打开当前文件夹」row (first two superseded by quick entries; `OpenSelectedDateCommand` retained in the VM as a Home no-op).
- Verification: Debug build 0 warnings/0 errors; tests Core 370 / Infra 164 / App 364 (898 total, all green); Home Light/Dark screenshots captured (`.artifacts/home-50-light.png`, `.artifacts/home-50-dark.png`).
- No ViewModel / Command / Binding / API / data-flow changes (presentation-only); no new animations.
- Prepared the Mid Review Context Package at `.artifacts/mid-review-package/` (context-package.md + screenshots + design-system summary). The ChatGPT Desktop Mid Review is arranged by the parent session.
- Next: 60% Primary Gallery (`HERMES_MASTER_GUIDE.md` #69) — virtualization/thumbnail/viewport-priority/filter/selection/zoom/pan/scroll/hover/performance/race-condition.

## 2026-08-13 UI/UX Refactor 40% Navigation + Motion

- Completed the 40% Navigation + Motion milestone (`docs/HERMES_MASTER_GUIDE.md` #67): Navigation, Sidebar, Workspace switch, keyboard behavior, and base animation, plus the §39 Navigation Bug Hunt.
- Sidebar `NavigationItem` now shows a selected state (`NavSelectionSurface` tonal overlay + `NavSelectionIndicator` accent bar, `Key == CurrentPage`) that fades in at `Motion.Duration.Normal` (180ms) / out at `Motion.Duration.Fast` (150ms); footer「设置」item is selected while on the Settings page. Hover stays token-based and instant via `Button.Ghost`.
- Added arrow-key navigation (`KeyboardNavigation.DirectionalNavigation="Cycle"` on the primary nav list) and `Ctrl+F` to focus the browse smart-search box.
- Fixed `AnimateVisiblePage`: all 12 destinations now resolve to their host (previously CustomAlbums/Watermark/ContestOpen/ContestJudged fell through to HomePage and Settings animated a dead collapsed ScrollViewer); the transition is now interruptible `BeginAnimation` (cross-fade + 6px, 180ms).
- Verification: Debug build 0 warnings/0 errors; tests Core 370 / Infra 164 / App 364 (898 total, +5 new `NavigationMotionTests`); Light/Dark window screenshots captured (`.artifacts/nav-motion-40-light.png`, `.artifacts/nav-motion-40-dark.png`).
- No ViewModel / Command / Binding / API / data-flow changes (presentation-only).
- Next: 50% Home + Mandatory Mid Review (`HERMES_MASTER_GUIDE.md` #68).

## 2026-08-11 UI/UX Refactor 30% App Shell

- Completed the 30% App Shell milestone (`docs/HERMES_MASTER_GUIDE.md` #66): Unified Shell, Navigation container, Top area, Workspace, Inspector container, and Status/background task area.
- Added the missing **Inspector container** component (`Themes/Controls/Inspector.xaml`: `Inspector.Container/Header/SectionLabel`) and wired the browse-page EXIF metadata panel into it. All shell chrome continues to use `Brush.Shell.*` / `Layout.*` / `Sidebar.*` tokens only.
- Aligned shell motion with Motion tokens: page-switch cross-fade+translate is now 180ms/6px (was 240/280ms/18px); removed the implicit Button scale-hover (a §14 violation); `PreviewItemContainer` reveal now uses `Motion.Duration.Normal`.
- Verification: Debug + Release builds 0 warnings/0 errors; tests 893/893 (Core 370 / Infra 164 / App 359); app launched and screenshot captured (`.artifacts/appshell-30.png`).
- No ViewModel / Command / Binding / API / data-flow changes (presentation-only).
- Next: 40% Navigation + Motion (sidebar/workspace switch, keyboard, base animation + Navigation Bug Hunt).

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
