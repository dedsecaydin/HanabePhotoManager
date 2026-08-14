# Changelog

## 0.3.1 — 2026-08-15

### Added
- 导入「高级选项」收纳/展开：转移方式、AI 人物识别、拖入后期/素材 默认收起，点击展开（复用高级筛选折叠模式）
- 「导入时检查重复」设置开关（默认关闭）：开启后检查目标日期文件夹已有照片 vs 本次导入（SHA-256 同大小同哈希），不扫描整个图库
- 首次导入引导：照片库根目录为空时拖入/点导入自动弹出文件夹选择框，选完自动保存，后续直接使用不再提示
- 照片墙虚拟化（P0）：`VirtualizingWrapPanel` 接入（分节头全宽行 + 视口 ±1 行 realize），5000+ 照片不一次性实例化
- 查看器/标题栏/IconButton/Slider 键盘焦点指示（`IsKeyboardFocused` 触发器 + `Brush.Border.Focus`）；视频 `[`/`]` 倍速快捷键；设置中心快捷键文档与实际对齐
- 浏览页/地图/列表空状态、MapPage WebView2 加载/错误/重试面板、缩略图加载占位
- 桌面图标圆角化（`HanabeApp.ico` 四角透明圆角）

### Changed
- 导入页两按钮（开始分析与分类 / 手动开始继续导入）合并为单按钮「开始分析与导入」（`AnalyzeAndImportCommand`：队列为空先分析再导入，否则直接继续导入），移除两个蓝色 V 形引导 Popup
- 备注弹窗去重：同批日期分析只弹一次（`_dateRemarksPromptedFor` 会话级去重），拖入后点按钮不再重复弹
- 照片库路径 UNC 支持：`LibraryRootNormalizer` 优先识别丢失反斜杠的 UNC（`\Hanabe\拍照` → `\\Hanabe\拍照`），绝不 `GetFullPath` 转成盘符绝对路径；设置加载自动修复并回写 settings.json
- 导入查重范围从全库 → 目标日期文件夹；A 批 token 化（107 处 FontSize、20 处 emoji → 设计 Token）

### Fixed
- 导入中断「Transfer paths must be fully qualified」：`ImportPlanBuilder` 对根相对路径/UNC 路径统一规范化
- 设置导航/查看器/CloudPage 若干 StaticResource → DynamicResource（修复无 Application 测试实例化崩溃）
- 焦点/键盘零反馈（`FocusVisualStyle={x:Null}` 补触发器）

## 0.3.0-alpha — 2026-08-14

### Added
- 主窗口 DWM 亚克力/Blur 材质系统：Win11 系统背板（亚克力→Blur）优先，Win10 ACCENT 亚克力降级，设置页开关 + tooltip（`IsAcrylicEnabled`）
- 夸克网盘集成：`QuarkCloudProvider`（封装夸克官方 CLI `quark-drive.cjs`）+ `QuarkCliRunner`（NDJSON 解析/超时/进程管理）+ 登录按钮；右侧总览显示真实夸克账户状态
- 左侧一级导航滚轮滚动（`Sidebar.NavigationScroller`，复用 `ScrollBar.Default` 样式）
- 用户操作说明书 `docs/user-manual.md`（11 章，面向普通用户）

### Changed
- 人物查找页/浏览页黄色 `TertiaryContainer` 胶囊全部改为中性色（`Surface.ContainerHigh` + `OnSurfaceVariant`）
- 自定义相册存储位置固定到应用数据目录（`AppDataPaths.CustomAlbumsFile` = `%LOCALAPPDATA%\HanabePhotoManager\custom-albums.json`）
- 设置导航滑动动画；一体化标题栏（Memory Diary 风格，CaptionHeight=0 + WM_GETMINMAXINFO 钳制）
- 查看器无边框 + 亚克力工具栏（截取 MediaRoot + BlurEffect + OpacityMask 胶囊裁剪）+ 视频播放器（LibVLC，快进/倍速/全屏）

### Fixed
- 网盘页 WebView2 `0x8007139F`：UserDataFolder 被锁时自动改用独立唯一子目录重试
- settings.json 损坏自动备份回退（`.corrupt-时间戳`）
- 单击/双击区分（延迟 GetDoubleClickTime 判定）

### Tests
- 全量 962 测试全绿（Core 373 / Infrastructure 178 / App 411）

---

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
