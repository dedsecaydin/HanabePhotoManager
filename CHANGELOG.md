# Changelog

## Unreleased — 2026-09-07

## 0.3.2-alpha.26 — 2026-09-08

- 发布增强版安装包，包含人物整理撤销、RAW 恢复预览、重复项安全隔离、视频配套 JPG 归并和可移动设备导入提醒。

- 增强人物整理、恢复预览、重复项安全隔离和照片库健康检查；人物误识别修正支持撤销，隔离文件可恢复原路径。

- 浏览支持 Sony 视频与 CxxxxT01.JPG 配套缩略图关联：JPG 用作视频封面，不再独立占据照片墙或筛选计数；缺少对应视频时仍展示 JPG。
- 新增可移动存储卡媒体检测与导入询问，显示目标照片库，确认后进入导入页；设置可关闭，同次插入只提醒一次。

- 恢复模块新增 ARW、CR2、CR3、NEF、DNG RAW 照片识别与安全导出。ARW/CR2/NEF/DNG 按 TIFF 目录与图像数据范围校验，CR3 按 Canon ISO BMFF 容器、RAW 轨道和采样范围校验；目录边界缺失或碎片化时只展示证据，不自动导出。
- 参考 PhotoRec 的 TIFF 文件雕刻思路与 canon_cr3 的公开格式说明独立实现，未复制 GPL 源码；LibRaw 仅作为 CRX 编解码能力的后续集成参考。

- 所有主功能页面新增右下角“本页说明”入口，随当前页面自动切换用途、操作顺序和安全提示。
- 新增 `docs/user-guide.md`，整理主页、导入、图库、相册、人物、地图、工具、水印、日期文件夹、恢复和设置的功能说明。
- 统一页面伸展行为：主页最近文件夹、人物相册、浏览条件和各功能页容器按可用宽度布局，修复页面内容缩在左侧的问题。
- 恢复页日期时间筛选统一为 40px 控件高度、日期与时间成组显示，并统一状态开关的垂直对齐。

## 0.3.2-alpha.25 — 2026-09-07

- 恢复页拆分镜像来源、查找范围和结果区域；时间筛选按需展开，开始扫描入口与范围设置相邻。
- 候选列表使用中文状态与大小摘要；结构详情集中到右侧证据区，空白状态提供操作引导。
- Release 构建和 792 项自动测试通过。

## 0.3.2-alpha.24 — 2026-09-07

- 恢复助手支持按卡内本地最后写入日期和时间快速筛选，读取 exFAT 目录定位候选，支持保留时间未知项；目录丢失时需使用完整扫描。
- 修正空白状态、停止按钮布局及换镜像后的旧候选残留；新增统计、候选证据和输出目录入口。
- 导出前重新检查结构，使用独立输出文件夹；取消或失败清理本次临时文件。
- Release 构建通过，792 项自动测试通过；真实受损存储卡的恢复效果仍需实物验证。

## 0.3.2-alpha.22 — 2026-09-06

### Settings navigation polish
- 设置二级菜单收窄为 196px，右侧三级目录随当前二级分区重建，并随滚动自动显示当前分组。

### Changed
- 导入中断后保留冻结目标、命名计划与校验凭据，支持继续未完成导入；移动模式在删除来源前保存恢复凭据。
- 重复检测持久复用未变化文件的 SHA-256 和视觉指纹；新增缓存、扫描并行度与相似阈值设置。传输及删除前仍实时校验。
- 相似复核增加并排图片、8×8 亮暗差异标注和相似原因说明，视觉相似项默认保留。
- 设置增加启动恢复提示、缓存清理和差异网格显示控制。

### Validation
- Release 构建成功；782 项测试通过。相似复核窗口的完整交互验收仍待完成。
- 新提示音制作、按事件开关及设置分区重排正在进行，不包含在本节完成项中。

### Sound and settings update
- 新增开始/恢复、完成、失败、跳过、功能打开和安全停止六类提示音开关与逐项试听；三种风格均提供 48 kHz 单声道 WAV。
- 用 Audacity 3.7.7 本地脚本管线完成音效项目整理和导出校验；生产辅助脚本保存在 `tools/Build-HanabeSounds.py`，音频工具与项目文件均位于 D 盘 `.artifacts`。
- 设置中心增加 AI 与人物、工具默认值分区；压缩、拼图、水印默认参数可保存；右侧目录支持同页滚动定位。

## 0.3.2-alpha.21 — 2026-08-31

### Added
- 新增相机机械、Q 版提示和混合三套 Hanabe 状态音效，以及独立“音效”设置分区。
- 重复清理完成后显示包含成功、跳过、失败和释放空间的结果报告。

### Changed
- 移动导入的系统 MessageBox 改为 Hanabe 统一主题安全确认窗口。
- 重复检测完成后唤回窗口设置移至“照片库与导入”。

## 0.3.2-alpha.20 — 2026-08-26

### Changed
- 分类、修图状态、评分分类和多选开关调整为同一行筛选项。
- “高级筛选”移动到下一行并单独靠左，明确区分常用筛选与高级入口。

### Fixed
- 视频播放引擎改为后台预热，避免首次打开时在 UI 线程同步初始化造成窗口卡住。
- 查看器窗口内复用 MediaPlayer，并启用硬件解码、1 秒文件缓存、迟帧丢弃与跳帧策略，改善大体积视频播放流畅度。

## 0.3.2-alpha.19 — 2026-08-26

### Fixed
- 修正图库筛选区“多选”和“高级筛选”的位置：合并为右对齐操作组，与同排筛选控件底部对齐。
- 两个控件统一为 36px 高度并增加固定间距，避免 Switch 与按钮边界挤在一起。

## 0.3.2-alpha.18 — 2026-08-26

### Changed
- 图库顶部“多选模式”改为 Switch 开关，明确表示模式的开启与关闭。
- 照片卡片、导入队列、水印列表及重复内容复查的项目多选统一为深色选中层与勾选框，不再使用 Switch 外观。
- 延续 alpha.17 的内外统一圆角及 alpha.16 的比例滚动进度样式。

## 0.3.2-alpha.17 — 2026-08-26

### Changed
- 主窗口外部 WindowChrome 与内部工作区统一使用 `Radius.Container` 圆角令牌，四角曲率保持一致。

### Tests
- 全量 644 项测试通过。

## 0.3.2-alpha.16 — 2026-08-26

### Fixed
- 修复图库滚动条只有整条绿色轨道、无法辨认滚动进度的问题：改为中性轨道与深绿色比例滑块。
- 滑块长度对应当前可视内容比例，并支持拖动、点击轨道与滚轮滚动。

## 0.3.2-alpha.15 — 2026-08-26

### Changed
- 主窗口最小尺寸调整为 `1440 × 900`，防止三栏工作区继续缩小后重合错位。
- 一级菜单保留自身深色悬停反馈，彻底移除左侧额外选中条。

## 0.3.2-alpha.14 — 2026-08-26

### Changed
- LRF 保留为导入归组所需的相机伴随文件，但不再进入图库快照和预览内容。
- 图库主滚动条改为常显，移除右下角悬浮“+”导入按钮。
- 调整一级导航选中提示位置，避免覆盖菜单项表面。

## 0.3.2-alpha.13 — 2026-08-26

### Added
- 水印支持在真实图片区域直接拖动，改变签名大小时保持中心位置不变。
- 拼图新增可选的“当前位置图片虚化 + 玻璃质感”补边背景。

### Changed
- 多个铺满水印提升至最大密度，并覆盖新应用页面的切换动画。
- 简化一级菜单选中视觉，补齐图库日期分节浏览、导入页间距和固定高度提示卡切换动画。

### Fixed
- 修复图库日期分节显示不完整及部分新页面缺少切换动画的问题。

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

## 2026-09-08 — 修复全部页面未铺满公共工作区

- 根因：说明 Popup 成为 DockPanel 的最后一个子元素，使页面 Grid 失去 LastChildFill，按内容宽度停靠左侧；之前增加 Stretch 无法解决父布局问题。
- 将帮助 Popup 放在页面 Grid 前，确保全部 11 个功能入口共用的 Grid 填满剩余空间，保留标题栏、状态栏和正常边距。
- Release 构建零警告零错误，793 项测试通过；新增公共宿主顺序与 11 页面覆盖回归检查。独立测试数据下已查看页面总览截图 `.artifacts/fill-all-pages.png`，恢复等页面铺满；导入入口回落主页，其独立视觉验收未完成。日用目录已覆盖并校验 App DLL 一致。

## 2026-09-08 — JPG/JPEG 照片镜像恢复

- 完整扫描支持 JPG/JPEG 与 MP4；exFAT 写入时间目录筛选支持照片。
- JPEG 按段长度、帧、扫描和结束标记确定范围，避免 EXIF 缩略图结束标记截断主照片；单候选上限 128 MiB。
- 照片沿用只读来源、导出前结构复核、隔离输出、取消清理和 SHA-256 报告。界面明确暂不支持相机 RAW、碎片重组；结构完整不代表全部像素可解码，真实损坏卡效果未验证。
- Release 构建通过，796 项测试通过，包含真实编码 JPEG 原样导出、截断拒绝、缩略图标记与照片时间筛选。
