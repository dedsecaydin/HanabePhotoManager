# Current Status — Feature-by-Feature Implementation State

> **Purpose:** Real-time overview of what's done, what's partial, and what's planned.
> **Last Updated:** 2026-08-16
> **Current Version:** `0.3.2-alpha.10`（WPF 安装外壳与主题可读性修复）
> **Status Labels:** Stable / Implemented-Unverified / Partial / In Progress / Planned / Known Issue / Blocked

---

## UI 修复批次 B1/B5–B22（2026-08-14）

> 全应用 UI 修复收尾：大标题+内容合一容器、强调色统一紫色（默认切 Violet）、全局 checkbox→Switch、移除网盘、主页快速操作按使用排序、设备检测只留外部设备等 19 项。验证：Debug/Release build 0 警告 0 错误；`dotnet test` **585 全绿**（Core 159 / Infra 54 / App 372）；已覆盖安装待真人验收。

| 项 | 状态 | 说明 |
|----|------|------|
| B1 大标题+内容合一容器 | Stable | TopBar 仅留窗口控制按钮；Home/Import/Preview/FaceSearch/Map 各页标题并入 `Layout.PagePanel` |
| B6 强调色紫色 | Stable | 默认主题切 Violet（`#8B4AA6`），保留 6 套主题与「动态色彩=蓝」语义 |
| B10/B16 全局 Switch | Stable | 隐式 CheckBox → 药丸胶囊轨道 + 圆形滑块；多选复选框同款 |
| B12 移除网盘 | Stable | 云盘全链路删除，保留 LibVLCSharp 视频预览与投稿/欣赏项目 |
| B20 快速操作排序 | Stable | 主页快速操作按最近使用排序（未使用不参与），持久化；图片小工具各工具分计 |
| B22 设备检测 | Stable | 只检测外部设备（U盘/存储卡/相机/网络），不再列本机磁盘，显示设备类型 |
| B5/B7/B8/B9/B11/B13/B14/B15/B17/B18/B19/B21 | Stable | 见 `docs/agent-change-log.md` 本批条目 |

---

## 进度总览（2026-08-14）

> 按 `docs/HERMES_MASTER_GUIDE.md` #68-#73 的 master guide 阶段定义跟踪；每 10% 阶段需经评审（含 ChatGPT Desktop 强制 Review）并通过 Progress Gate（#74：无 P0、阶段无未解决 P1）才能宣布完成。

| 阶段 | 定义（master guide） | 状态 |
|------|---------------------|------|
| 50% | #68 Home + Mandatory Mid Review | ✅ 完成（已评审：结论「基本达标（条件通过）」，6 项 P1 已修复） |
| 60% | #69 Primary Gallery / Main Content | ✅ 完成（917 测试全绿；发现并修复 1 个 P1 缩略图播种问题） |
| 70% | #70 Inspector + Contextual UI | ✅ 完成（评审后补跑验证通过） |
| **80%** | **#71 Remaining Main Pages** | **🔄 进行中** — 功能页重设计已全部完成（人物/相册/导入/设置/工具/地图/网盘，含预设计 mockup 004-010 + 实施），但**尚未正式宣布 80% 完成**，因仍有 6 项 UI 修复待收尾（见下） |
| 90% | #72 Final Polish + Mandatory Final Review | ⏳ 计划中 |
| 100% | #73 Final Verification | ⏳ 计划中 |

### 80% 收尾清单（6 项 UI 修复，未完成 → 完成后宣布 80%）

| # | 项 | 状态 | 说明 |
|---|----|------|------|
| 1 | 左上角图标高清圆角 | 未完成 | 生成 512px PNG logo 替换 ico 引用 |
| 2 | 导入备注对话框保存按钮字体不明显 | 未完成 | 需增强对比/字重 |
| 3 | 全局字体对比度排查 | 未完成 | 全页面字号/颜色对比度巡检 |
| 4 | 网盘页右侧 CloudHubViewModel 真实接线 | ✅ 完成 | 百度/夸克均接入 CloudHubViewModel；夸克 QuarkCloudProvider + 登录按钮已实现（962 测试全绿） |
| 5 | 工具页卡片顶部色块圆角 | 未完成 | `CornerRadius="28,28,0,0"` |
| 6 | 网盘页 WebView2 0x8007139F | ✅ 完成 | `UserDataFolder` 被锁时自动改用独立唯一子目录重试 |

### 后续计划（master guide 定义）

- **80% 收尾**：完成上述 6 项修复 → 回归（构建 + 917 测试 + 截图复核）→ 宣布 80% 完成
- **90%（#72 Final Polish）**：Motion consistency / Spacing / Alignment / Typography / Icon / Hover / Focus / Loading / Empty / Error / Keyboard / Performance 12 项 + **强制 ChatGPT Desktop Final Review**
- **100%（#73 Final Verification）**：Release Build / Full Tests / 主要用户流程 / Bug Hunt / Regression / Feature Inventory 对比 / 文档更新 / Handoff / 最终截图 / Known Issues
- **发布**：版本升级 `0.3.0-alpha` + 开源发布（push + 转 public）

### 开源并行线（不影响 master guide 主线）

| 项 | 状态 |
|----|------|
| 三语 README（中/英/日） | ✅ 已完成（`README.zh-CN.md` / `README.md` / `README.ja.md`） |
| LICENSE(MIT) | ✅ 已完成（仓库根 `LICENSE`，未跟踪待提交） |
| 原创 icon（icon_2） | ✅ 已完成 |
| 赞助区块（微信赞赏码 + 爱发电 afdian.com/a/hanabededsec） | ✅ 已完成（三语 README 均已含） |
| 版本升级 `0.3.0-alpha` + 开源发布 | ✅ 已完成（2026-08-14） |
| push + 转 public | ⏳ 待执行 |

---

## M3 功能页重设计第三批 — 工具页 + 地图页 + 网盘页（008/009/010 mockup）(2026-08-14)

> 按用户确认的预设计 008/009/010 重做三个功能页，仅视觉/布局/交互组织，零 VM 改动；全程语义 Token；动效 150/180/220ms。分块构建验证（工具页 → 地图页 → 网盘页）。

| 项 | 状态 | 说明 |
|----|------|------|
| 工具页卡片网格 | Stable | `CompressionPage` 新增落地网格（`ToolGridHost`）：「图片小工具」hero + 6 卡（压缩/拼图/水印/微信发送/投稿/欣赏，M3 tonal 容器纯色封面 + 大图标 + 名称/描述）；卡点击进详情（4 工具）或导航（投稿/欣赏） |
| 工具页详情工作台 | Stable | `ToolDetailHost`：「← 返回工具」+ 原 `ImageToolModeTabs` 分段 chips（restyle 全圆角 tonal）+ 左参数 360px（原压缩/拼图/输出目录卡原样）+ 中队列/预览（水印/微信子页原样嵌入）+ 右 320px Inspector 运行统计（`Items.Count`/`OriginalTotalBytes`/`OutputTotalBytes`/`ProgressValue` + `Results`，零 VM 新增） |
| 工具页深链 | Stable | code-behind 订阅 `SelectedToolMode`：onboarding 第 8 步 / `ShowWatermarkCommand` 先设模式再导航时自动进详情（保持深链行为） |
| 地图页 Inspector | Stable | 右列 380→320px；「地图照片」卡 `Inspector.Panel`；地点浏览新增三格统计（当前地点/已定位/聚合点）+ 原当前位置照片列表；手动标记（Ctrl/Shift 多选 + 地图取点 + 经纬度/地点名 + 保存）全保留；map.css 聚合徽标蓝→红 |
| 网盘页 Inspector | Stable | 主区内嵌浏览器（后退/前进/刷新/首页 + 加载/失败/重试面板）行为不变；右侧新增 320px 云盘总览（账户卡 + 用量环 + 三格统计 + 传输队列 + 「可后续接入」说明），因 `CloudHubViewModel`/`CloudTransferJob` 未接入 DataContext，为视觉占位 |
| 合规 | ✅ | 三页零 `#hex`；工具卡/用量环刻意不用 mockup 渐变（遵「无强渐变」铁律）；无 Card 套 Card；CloudPage 新增 StaticResource 改 DynamicResource（运行时构造测试无主题资源） |
| 验证 | ✅ | Debug+Release build 0 警告 0 错误；`dotnet test` **917 全绿 exit 0**（Core 373 / Infra 164 / App 380）；截图 `m3-tools/m3-map/m3-cloud-{light,dark}.png` |

下一阶段建议：M3-5 回归（构建 + 917 测试 + 大库实测 + 6 主题截图复核）；网盘页总览/传输队列可接入 `CloudHubViewModel`/`CloudTransferJob` 真实数据（需 VM 层接线，属行为改动需用户确认）。

---

## M3 导入页 + 设置页重新设计（006/007 mockup）(2026-08-14)

> 按用户确认的预设计 006/007 重做两功能页，仅视觉/布局/交互组织。导入页三段布局、设置页左导航 + 分组列表 + 常驻 Inspector；6 套主题切换整合进外观分区色卡；全程语义 Token；动效 150/180/220ms。

| 项 | 状态 | 说明 |
|----|------|------|
| 导入页三段布局 | Stable | `ImportPage` 改 `320/16/*/16/320`：左源面板（拖放区/来源/转移方式/人物识别/修后素材拖放/分析+导入按钮，绑定命令全保留）· 中队列（目标日期/报告/进度卡/6 分类 section + 预览卡）· 右 Inspector |
| 导入页 Inspector | Stable | 导入设置（精确查重/相似审查/修后只读保护三行只读开关，修后置灰铁律）+ 去重结果（三选项只读，实际走既有模态）+ 本次导入摘要（成功/跳过/失败） |
| 导入 VM 新增 | Stable（最小化） | `ImportSuccessCount`/`ImportSkippedCount`/`ImportFailedCount` 三个只读 int + `SetImportSummary`，导入完成/取消赋值、`ImportItems.Clear()` 处复位；未改导入/去重/转移/分类逻辑 |
| 设置页布局 | Stable | `TabControl` → 216px 左分区导航（6 分区）+ 右 M3 分组列表（group header + 分隔线 + 设置行）+ 320px 常驻 Inspector（主题预览/数据存储/关于） |
| 外观 6 套主题色卡 | Stable | 6 张色卡（配色×明暗）`ThemeManager.Apply` 即时换肤 + 当前高亮 + Inspector 色板/窗口预览随 Token 联动；色卡色值入 `Colors.ThemeSwatches.xaml`（非页内写死） |
| 设置功能保留 | Stable | 6 分区全部功能项照抄（自启/窗口恢复/版本树/照片库/浏览默认值/AI/百度凭据/夸克/推理/人脸/ArcFace/快捷键等）；顺手修复死绑定 `LibraryCapacityText` → `LibraryHealthText`+`DiscoveredDateCount` |
| 合规 | ✅ | 下拉统一 `Input.SettingsComboBox`；页内无 `#hex`（色卡走共享资源）；code-behind `Button`/`Brush` 二义性全限定 |
| 验证 | ✅ | Debug+Release build 0 警告 0 错误；`dotnet test` 917 全绿（Core 373 / Infra 164 / App 380）；截图 `m3-import-{light,dark}.png` + `m3-settings-{light,dark}.png` + `m3-settings-violet-dark.png` |

下一阶段建议：008 工具/地图页等其余功能页按同款 M3 排版深化；导入页可进一步把「去重结果」内联到真实去重流程（需 VM 暴露 `DuplicateMatchCount` + 决策态，属行为改动需用户确认）。

---

## 人物页功能补全 — 合并命令 + 详情照片虚拟化 (2026-08-14)

> 补上轮遗留技术债第 1、2 项（用户确认先补「合并」）。只改人物相关；`MergeAsync` 服务逻辑不改，只加命令层 + UI；全程语义 Token；动效 150/180/220ms。

| 项 | 状态 | 说明 |
|----|------|------|
| 人物「合并」命令 | Stable | `PeopleAlbumViewModel` 暴露 `MergeCommand`（`CanMerge = SelectedAlbum != null && Albums.Count >= 2`）；`MergeSelectedAsync` 选目标（`_mergeTargetPicker` 可注入，默认弹 `MergePersonDialog` 模态窗）→ `_service.MergeAsync(target, source)` → `RefreshAlbumsAsync` 刷新列表 → `SelectedAlbum` 落到目标 + `StatusText` 提示；`Albums.CollectionChanged`/`SelectedAlbum` setter 通知 `CanExecuteChanged` |
| 合并入口 UI | Stable | 详情 hero 可编辑姓名行追加「合并到…」`Button.Secondary`（`PeopleAlbums.MergeCommand`），与「保存姓名」同排 |
| `MergePersonDialog` | Stable | 新模态选择窗（`Dialog.*` + `List.Default`/`ListItem.Default` + `Button.Secondary`/`Button.Primary`），候选人 ListBox（`Name` + `PhotoCount`）+ 取消/合并，双击候选直接合并，`SelectedTarget`/`DialogResult` 回传 |
| 详情照片虚拟化 | Stable | 新 `Controls/VirtualizingWrapPanel`（`VirtualizingPanel`+`IScrollInfo`，固定 142×142 步长按视口换行、只 realize 可见行）；详情照片区 `ItemsControl`+`WrapPanel` → `ListBox`（`List.Default` + `VirtualizationMode=Standard` + `VirtualizingWrapPanel`）；详情区从外层 `ScrollViewer` 抽出为 `Grid`（hero 固定 + 照片 ListBox `*` 行独立滚动），否则无限高度无法虚拟化 |
| 缩略图异步解码 | Stable | 新 `PersonPhotoViewModel`（`Path` + 懒加载 `Thumbnail`）：`EnsureThumbnailLoaded` 幂等（`Interlocked` 三态 + 静态 `SemaphoreSlim(4)` + `Task.Run` 解码 + `Freeze` + Dispatcher 回填），`Loaded` 事件按 realize 触发，失败保留占位；`PhotoPaths`（`HashSet<string>`）**数据源不变**，另增 `Photos` 镜像集合 |
| 验证 | ✅ | `dotnet build /warnaserror` 0 警告 0 错误；`dotnet test` 917 全绿（Core 373 / Infra 164 / App 380，+2 合并命令测试）；截图 `.artifacts/m3-facesearch-merge-{light,dark}.png`（1600×980，含合并 UI） |

下一阶段建议（剩余技术债）：①「待确认人物角标」需补人脸检测/聚类确认数据；②「浏览页人物筛选」与「人物页管理视图」职责收敛（合并/去重入口统一）；③ 相册页「合并/导出」命令（同款 `MergeAsync` 模式可复用）。

---

## M3 大改 — 主题基建 (M3-1) + Shell 改版 (M3-2)

> 2026-08-14 用户拍板方向大改（旧「克制桌面工具风」→ M3 浓烈版），按 `docs/M3_DESIGN_FINAL.md` 落地 M3-1 / M3-2。仅视觉层，未改业务逻辑 / 命令 / 绑定 / API / 数据流。

| 项 | 状态 | 说明 |
|----|------|------|
| 6 套主题 | Stable | 3 配色（动态色彩/森林绿/紫罗兰）× 浅深；`ThemeManager` 扩展 `AppColorScheme`，主题入口 `Themes/Themes/{Scheme}.{Mode}.xaml`，偏好 `ui-theme.txt` 持久化；默认动态色彩·浅色（`App.xaml` → `Dynamic.Light.xaml`） |
| M3 语义色值 | Stable | 6 个 `Colors.<Scheme>.<Mode>.xaml`（M3 tonal：primary/secondary/tertiary + container + surface 五层 + on-* + outline）；`Brushes.*` 新增 `Brush.Primary` / `Brush.Surface.Container*` / `Brush.OnSurfaceVariant` 等，既有 `Color.*`/`Brush.*` 键只换值不换键 |
| 主题切换 UI | Stable | 设置 → 外观：配色方案（动态色彩/森林绿/紫罗兰）+ 明暗（浅色/深色）组合，即时生效并持久化 |
| Navigation Rail | Stable | Sidebar 232px → Rail 88px（`Size.Rail.Width`），导航项图标+文字竖排，选中态 = secondary-container + primary-container 圆 icon + on-* 色；保留拖拽排序 + ↑↓ Cycle 键盘导航 |
| Topbar / Statusbar | Stable | Topbar 改 `surface-container-low` 大圆角（`Radius.Container` 28）；Statusbar 改 44px `surface-container` 大圆角 |
| Radius / Sizing | Stable | `Radius.Small` 8、`Radius.Control` 12、新增 `Radius.Container` 28、`Radius.Full` 999；`Size.Rail.Width` 88 |
| 验证 | ✅ | `dotnet build /warnaserror` 0 警告 0 错误；`dotnet test` 915 全绿；6 套截图 `.artifacts/m3-theme-<scheme>-<mode>.png` |

下一阶段：M3-3 浏览页改版（Workspace 网格 + Chips + FAB + Inspector 320px）→ M3-4 其余页面 → M3-5 回归。

---

## M3 大改 — 浏览页改版 (M3-3) + 其余页面适配 (M3-4)

> 2026-08-14 完成。按变体 001 落地浏览页 Workspace 网格 + Chips + Inspector 320px + FAB，其余页面经共享 Token 自动对齐 M3。仅视觉层，未改 ViewModel / Command / Binding / API / 数据流；treemap/justified-gallery 虚拟化与视口优先级逻辑不变。

| 项 | 状态 | 说明 |
|----|------|------|
| Workspace 外壳 | Stable | `BrowseUnifiedWorkspace` → `surface-container-lowest` + `Radius.Container` 28 + `outline-variant` 描边；treemap 内嵌 Card 去除（避免 Card 套 Card） |
| 照片网格 | Stable | 瓷砖沿用 `Radius.Card` 12 + 悬停状态层；`ZoomableGridTileSize` 默认 150≈140px，缩放/滑块逻辑不变 |
| Chips 筛选栏 | Stable | 分类 Chips：未选 `surface-container-high`、选中 `primary-container`；修图/文件类型 segment：选中 `secondary-container` |
| Inspector 320px | Stable | 底部 dock → 右侧 320px 面板（`surface-container-low` + `Radius.Container` 28），单张 EXIF 纵向 info-row + 操作 Chips，多选批量操作纵向堆叠，新增无选中占位态 |
| FAB | Stable | 56×56 圆形 `primary`，右下角「＋」导入（`ShowImportCommand`），不遮挡 Statusbar；「共 N 项」改左下 |
| 其余页面（M3-4） | Stable | 人物/相册/导入/图片小工具/水印/地图/网盘/投稿/欣赏/设置页均 Token-first，经共享 Token 自动对齐 M3，无硬编码颜色残留；设置页 6 套主题切换 UI 复核正常 |
| 验证 | ✅ | `dotnet build /warnaserror` 0 警告 0 错误；`dotnet test` 915 全绿；截图 `.artifacts/m3-browser-<scheme>-<mode>.png`（6）+ `.artifacts/m3-page-<name>-dynamic-light.png`（11） |

下一阶段：M3-5 回归（构建 + 测试 + 大库实测 + 6 主题截图复核）。

---

## UI/UX Refactor — App Shell (30%)

> 2026-08-11 完成 30% App Shell。全程只用 Design System Token，未改动 ViewModel / Command / Binding / API / 数据流。

| Shell 区域 | 状态 | 说明 |
|-----------|------|------|
| Unified Shell（统一外壳） | Stable | 根 `Layout.Shell` + 连续 Shell 背景（`Brush.Shell.*`）+ Sidebar/工作区共享背景，不再互相割裂 |
| Navigation container（导航容器） | Stable | `Sidebar.Container`（88px Navigation Rail，M3-2）+ `Navigation.RailItem` + 拖拽排序 |
| Top area（顶部区域） | Stable | `Layout.TopBar`：PageTitle/PageSubtitle + 首页快捷按钮 |
| Workspace（工作区） | Stable | `Layout.Workspace` + 页面宿主 Grid（首页/导入/浏览/人物/地图/云盘/投稿/设置） |
| Inspector container（检查器容器） | Stable | `Inspector.Container/Header/SectionLabel/FieldLabel/FieldValue` 组件；浏览页结构化元数据检查器（`SelectedFileMetadata`）接入为上下文检查器（70% 完整化） |
| Status / background task area（状态/后台任务区） | Implemented-Unverified | `Layout.StatusBar`：StatusMessage + IsBusy 进度（ProgressLabel/ProgressBar/%） |

Motion 对齐：页面切换 240/280ms + 18px → 180ms + 6px（Motion.Normal，cross-fade + 微位移）；移除隐式 Button scale hover（§14）；`PreviewItemContainer` 载入 0.24/0.28s → `Motion.Duration.Normal`。

---

## UI/UX Refactor — Navigation + Motion (40%)

> 2026-08-13 完成 40% Navigation + Motion（`HERMES_MASTER_GUIDE.md` #67）。全程只用 Design System Token，未改动 ViewModel / Command / Binding / API / 数据流。

| 区域 | 状态 | 说明 |
|------|------|------|
| Navigation 选中态 | Stable | 侧边栏导航项新增 `NavSelectionSurface`（`Brush.Surface.Selected` 色调）+ `NavSelectionIndicator`（`Brush.Accent.Default` 左侧指示条）；`Key == CurrentPage`（`CategoryEqualityMultiConverter`）驱动，180ms 淡入 / 150ms 淡出（Motion Normal/Fast） |
| Sidebar 悬停态 | Stable | 沿用 `Button.Ghost` 语义悬停（`Brush.Surface.Interactive` 即时反馈，符合设计系统）；页脚「设置」项同步选中态（`IsSettingsPage`） |
| 方向键导航 | Stable | `PrimaryNavigationList` 增加 `KeyboardNavigation.DirectionalNavigation="Cycle"` + `TabNavigation="Once"`，↑/↓ 循环遍历、Enter/Space 激活 |
| 键盘快捷键 | Stable | `Ctrl+F` 聚焦智能搜索框（浏览页，自动展开浏览条件并选中文本） |
| Workspace switch 状态保持 | Stable | 页面宿主常驻（Visibility 切换，不重建）；切出预览页时 `CaptureBrowseSnapshot` 保留会话快照，`_browseStatePolicy` + `_previewScanVersion` 防旧状态覆盖 |
| 页面切换动画 | Stable | `AnimateVisiblePage` 重构为可中断 `BeginAnimation`（cross-fade + 6px 微位移，180ms Motion.Normal）；补齐全部 12 个页面映射（修复 CustomAlbums/Watermark/ContestOpen/ContestJudged 未映射、Settings 映射到废弃 ScrollViewer 的 bug） |

Navigation Bug Hunt（§39）：全部 12 页 × 20 轮快速切换回归测试通过；子 VM 引用恒等（MapPhotos/Compression/Watermark/CustomAlbums/PhotoViewer/TreemapBrowser 仅在构造器创建一次）；无页面错乱、无 VM 重复创建、无状态覆盖（详见 `docs/agent-change-log.md` 2026-08-13）。

---

## UI/UX Refactor — Home + Mid Review (50%)

> 2026-08-13 完成 50% Home + Mandatory Mid Review 准备（`HERMES_MASTER_GUIDE.md` #68）。全程只用 Design System Token，未改动 ViewModel / Command / Binding / API / 数据流。

| 区域 | 状态 | 说明 |
|------|------|------|
| 首页摘要（Summary） | Stable | `Layout.HomeSummary` 三列统计（照片库 / 已发现日期 / 当前预读取）；数值字号对齐 `Typography.Display`（28）/ `Typography.Title`（20）Token，标签沿用 `Brush.Text.Secondary` |
| 快速入口（Quick Entries） | Stable | 新增 7 个 `HomeQuickEntry` 入口卡片（图标 + 标题 + 一行描述）：导入照片 / 照片图库 / 自定义相册 / 人物查找 / 地图照片 / 图片小工具 / 网盘；全部复用现有 `Show*Command`，与左侧导航一致；图标复用共享 `Icon.*` + `Brush.Accent.*` Token |
| 最近照片（HomePreviewFiles） | Stable | 缩略图卡片圆角对齐 `Radius.Card` / `Radius.Control`；扩展名 / 文件名字号对齐 `Typography.Body` / `Typography.Caption` |
| 设备（Devices） | Stable | 设备卡 / 详情面板圆角与字号全部 Token 化（`Radius.Card` / `Radius.Control`，`Typography.Title` / `TitleSmall` / `Label` / `BodySmall` / `Caption`） |
| 旧底部按钮 | 移除 | 首页底部「去导入 / 去预览 / 打开当前文件夹」删除：前两者由快速入口替代（同一 Command）；「打开当前文件夹」在首页无选中日期、恒为禁用，命令仍保留在 VM 中（未删除业务逻辑） |

Motion：首页未新增动画，沿用 Motion.Normal 180ms 页面切换与 150/180ms 侧边栏选中态。

中期 Review 材料：`.artifacts/mid-review-package/`（context-package.md + 截图 + design-system 摘要），由 ChatGPT Desktop（aurora gpt-5-6）完成评审（2026-08-14），结论「基本达标（条件通过），无 P0，6 项 P1」。

---

## UI/UX Refactor — Home Mid Review P1 修复（50% 补修）

> 2026-08-14 修复中期评审的 6 项 P1（报告：`C:\Users\fulia\wxdecrypt\hanabephoto_midreview.md`）。全程只用 Design System Token；除 P1-3 的 Home 缩略图加载根因（最小 ViewModel 行为修复）与 P1-5 标题栏（code-behind 调 DWM）外，均为纯表现层改动，未改业务逻辑 / 命令 / 绑定 / 数据流。

| P1 | 位置 | 修复 |
|----|------|------|
| P1-1 | Home 信息架构 | 重排为「轻量库状态行 → 最近照片主视觉 → 快速操作 Compact → 设备沉底」；`Layout.HomeSummary` 由大 Summary Card（三列统计 + Emphasis 阴影）改为轻量状态行（`照片库已连接 · N 日期 · N 媒体文件` + 库路径），不再用 Display 大数字 Card |
| P1-2 | 扫描缩略图区 | 固定 6 列裁切 → `WrapPanel` 自适应（TileMinWidth≈140px，`AvailableWidth/TileMinWidth` 自然折算列数，1280px≈7 张、900px≈5 张）；移除内层 `MaxHeight=188` 滚动与固定 104px 瓷砖，主视觉约 40% 高度 |
| P1-3 | 缩略图媒体表达 | ① 修复根因：应用默认启动在 Preview，Home 缩略图从未加载（导航到 Home 不触发 `StartPreviewThumbnailLoading`）→ `CurrentPage` setter 增加 `else if (IsHomePage)` 分支触发加载；② 缩略图优先（ImageBrush 覆盖占位，失败回退统一 placeholder）；③ 视频（MP4/MOV）增加居中播放指示（`Icon.Play` + `Brush.Overlay.Scrim` 圆底）与右下角类型角标；④ **Duration Badge 延后**：VM 无视频时长数据，读取需 MediaFoundation/Shell 元数据（数据流改动，超出「仅视觉层」约束） |
| P1-4 | Sidebar 主页选中态（Dark） | 仅调 Dark Token：`Color.Surface.Selected` #343A3E → #485058（与侧栏背景对比度 ~1.4 → ~2.0），`NavSelectionIndicator` 3px 指示条随新 Surface 更清晰；未写死颜色 |
| P1-5 | Windows 标题栏（Dark） | `DwmSetWindowAttribute` + `DWMWA_USE_IMMERSIVE_DARK_MODE`（20，回退 19）：深色主题→深色系统标题栏，浅色→浅色；订阅 `ThemeManager.ThemeChanged`，`MainWindow_Loaded` 初始化应用一次 |
| P1-6 | 快速入口区 | 2×3 卡片网格（第 6 格空置）→ 横向 `WrapPanel` 紧凑 Toolbar（`Button.Toolbar` + `Icon.*`，7 入口全部保留）；删除页面级 `HomeQuickEntry` 样式；**实际数量确认为 7**（XAML 硬编码 7 个 `Show*Command`，无 QuickActions 集合，故 7 入口不折叠） |

验证：Debug build 0 警告/0 错误；测试 Core 373 / Infra 164 / App 371（908 total，+5 `HomeP1FixTests` 回归）；Home 浅/深截图 `.artifacts/home-fix-50-light.png` / `home-fix-50-dark.png`（`--screenshot --page Home` + JPG/MP4 fixture）。

---

## UI/UX Refactor — Primary Gallery / Main Content (60%)

> 2026-08-13 完成 60% Primary Gallery（`HERMES_MASTER_GUIDE.md` #69）。以当前版本最核心媒体浏览模块（照片墙 TreemapBrowser / PreviewPage / Browse 页）为准。仅渲染/性能层改动，未改动 ViewModel / Command / Binding / API / 数据流，全程 Design System Token。

| 验收项 | 状态 | 说明 |
|--------|------|------|
| Virtualization（虚拟化） | 通过 | 树图为自绘 `FrameworkElement`：按视口 `VisibleRowRange`（对 Y 单调的 justified 行做二分查找）只绘制视口内行，`DrawRoot`/`DrawSubtreeWithJustifiedLayout`/`DrawPanorama` 三路径全部应用；每帧从 O(n) 全量遍历降为 O(可见行 + log n) |
| Thumbnail（缩略图） | 通过（修复 P1） | 有界并发队列（`ThumbnailConcurrency`=4）+ 3s 超时 + `LoadPreviewThumbnailsAsync` 逐批回调；修复 P1：增量全库/日期扫描经 `ApplyBatch` 填充树图、绕过 `RepopulateTreemapFrom`，导致 `_treemapSourceFiles` 从未播种、首屏照片墙无缩略图 → `RefreshFilteredCache` 完成后播种源 + 触发 `TreemapRepopulated`，`EnsureTreemapSourceLookup` 懒重建兜底 |
| Viewport priority（视口优先级） | 通过 | `RefreshTreemapViewportLoading` 仅加载 `VisibleItemPathsNeedingThumbnail`（当前视口内且无缩略图）；离开视口即不再入队 |
| Filter（筛选） | 通过（原已实现，复核） | 分类/修图状态/文件类型/评分/智能类别/人物/搜索/排序均同步 `RefreshFilteredCache`；`RequiresTreemapRepopulation()` 门控树图重建 |
| Selection（选择） | 通过（原已实现，复核） | 网格单选/Ctrl/Shift 范围/框选/全选/清除完整；树图单选经 `SelectedPath → SelectedPreviewFile`；删除后无 Ghost Selection |
| Zoom / Pan | 通过（原已实现，复核） | Ctrl+滚轮 0.5x–30x、Space+拖拽平移、中键平移；缩放/平移经 `SyncTreemapVisibleRect` + 防抖加载 |
| Scroll（滚动） | 通过 | 布局缓存 + 视口行二分查找后，6217+ 子树与 11739 项全库滚动为 O(可见)；`ContentHeight → ScrollViewer.ExtentHeight`（KI-05/KI-06 修复） |
| Hover（悬停） | 通过（新增） | 树图 tile 新增 `OnMouseMove`/`OnMouseLeave` 悬停态（`Brush.Surface.Interactive` 填充 + `Brush.Border.Strong` 描边，无边框模式 1.5px 描边）；选中态 `Brush.Border.Focus` 优先；网格卡悬停沿用 Token 化 |
| Performance（性能） | 通过 | 移除每帧 O(n) 重排与重扫（缓存派生分组 + justified 布局）、每帧 ~n 次分配与每次防抖/排空的 O(n) 字典重建；`JustifiedGalleryLayout.Arrange` 11739 项测试限时通过 |
| Race condition（竞态） | 通过 | §40 复核：日期切换（`_dateLoadGeneration`）、语义搜索（`_operationCancellation`）、树图缩略图队列（`_treemapLoadGeneration` + `_treemapLoadActive`）均 Latest-Request-Wins；见 `docs/agent-change-log.md` 60% 竞态表 |

大库实测：`JustifiedGalleryLayout` 11739 项布局在宽松限时（2s）内完成（回归测试，实际毫秒级），缓存后滚动帧不再重算布局；自动化测试覆盖 6217 项全景布局与视口行二分查找前置条件。**环境限制**：真实库 `\\Hanabe\拍照`（UNC，11739+ 项）在本会话不可达，且会话桌面为 headless（PrintWindow/屏幕截取返回空白）——因此「真实大库滚动/响应」的手工 QA 未能复现；截图改用新增的无头 `--screenshot` 模式（`RenderTargetBitmap`）针对 96 张合成样片库生成，验证树图照片墙、justified 拼贴与缩略图加载正常。截图：`.artifacts/gallery-60-light.png` / `gallery-60-dark.png`。

P1 门槛：发现并修复 1 个 P1——增量全库/日期扫描路径（`ApplyBatch`）不播种缩略图源，导致首屏照片墙无缩略图（`RefreshFilteredCache` 播种 + `EnsureTreemapSourceLookup` 兜底，已回归测试）。KI-07 已 Resolved；KI-05/KI-06 本轮以「视口行二分查找 + 布局缓存」进一步收敛；KI-03/KI-04 为视觉调优，不构成进入 70% 的阻断。

---

## UI/UX Refactor — Inspector + Contextual UI (70%)

> 2026-08-14 完成 70% Inspector + Contextual UI（`HERMES_MASTER_GUIDE.md` #70）。把 30% 的上下文检查器做完整，补上下文操作与多选操作。复用既有 `PhotoDetailMetadataReader`（结构化 EXIF/GPS，与 `PhotoViewerWindow` 共用），仅表现层，未改业务逻辑 / 命令 / 绑定 / API / 数据流，全程 Design System Token。

| 验收项 | 状态 | 说明 |
|--------|------|------|
| Inspector（检查器） | Implemented（构建待补跑） | 浏览页底部上下文检查器：头部（文件名 + 关闭）+ 文件信息（类型/大小/分辨率/分类）+ 拍摄参数（相机/镜头/ISO/光圈/快门/焦距）+ 时间与位置（拍摄时间/位置）；数据源为结构化 `SelectedFileMetadata`（`PhotoDetailMetadataReader`），缺失字段显示「未记录」占位（不臆造） |
| Context Action（上下文操作） | Implemented（构建待补跑） | Inspector 内联操作：评分 1–5、标签（人像/风光/废片）、打开、在资源管理器打开、复制路径、移入回收站——复用既有 code-behind 处理器；右键菜单原有条目未动 |
| Multi-select actions（多选操作） | Implemented（构建待补跑） | 多选时（`IsMultiSelection`）底部多选操作条：`已选择 N 张` + 批量复制到…/移动到…/智能识别/批量归入分类/批量添加标签/移入回收站/清除选择——复用既有 `BatchCopyFilesTo`/`BatchMoveFilesTo`/`AnalyzeSelectedPhotosCommand`/`AssignCategoryToSelectedCommand`/`AssignTagToSelectedCommand`/`DeleteSelectedFilesCommand` |
| Metadata display（元数据展示） | Implemented（构建待补跑） | 结构化字段行（`Inspector.FieldLabel`/`FieldValue` Token 化），替换原扁平单行 EXIF 文本 |
| Selection / Inspector Bug Hunt（§43/§60） | 待补跑 | 选择切换、Ctrl/Shift/框选/全选/清除、单张↔多选互斥（`SelectedPreviewFile` vs `IsMultiSelection`）已在代码层面自审，运行时回归需子进程恢复后补跑 |

⚠️ **验证受限**：本会话验证阶段 subprocess（pwsh/grep/glob）以 `STATUS_DLL_INIT_FAILED`（exit 3221225794）失败，`dotnet build` / `dotnet test` / 截图脚本未能实际运行，需补跑（详见 `docs/agent-change-log.md` 2026-08-14 70% 条目）。

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
| Search | Implemented-Unverified | One smart box supports automatic file/path matching and semantic descriptions; semantic candidates progressively appear after each indexed batch. Real-library validation pending. |
| Smart category | Stable | |
| Custom tags | Stable | |
| Manual classification | Stable | Assignment remains supported outside the browse-condition surface. |

## Import (2026-08-09 update)

| Feature | Status | Notes |
|---------|--------|-------|
| Multi-file picker | Implemented-Unverified | New Ctrl/Shift picker uses `OpenFileDialog.Multiselect` and routes every selected path through the existing multi-root analysis flow. Drag-and-drop already accepts all `FileDrop` paths. |
| Import progress and cancellation | Implemented-Unverified | Import page shows x/N, percentage, progress bar, cancellation and a success/skipped/failed summary. |
| Exact duplicate import decision | Implemented-Unverified | Existing size prefilter plus SHA-256 now preflights a whole batch and offers skip-all, import-all, or per-item decisions. Retouched output remains read-only. |

## Photo Library — Display

| Feature | Status | Notes |
|---------|--------|-------|
| Grid view | Stable | Zoomable, square tiles, UniformToFill, progressive thumbnails |
| Timeline view | Stable | |
| List view | Stable | |
| Startup browse state | Implemented-Unverified | Opens Browse in Space Treemap with neutral all-library filters; root scan streams files asynchronously. Real 11,741-item startup QA pending. |
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
| Semantic zoom | Implemented-Unverified | At zoom ≤ 0.20, `PanoramaPhotoLayout` renders every current-directory photo as a dense justified wall with 24px+ rendered minimum size; normal tree/Justified modes resume above the threshold. Real-library visual QA pending. |

## Space Treemap — Navigation

| Feature | Status | Notes |
|---------|--------|-------|
| Subtree enter/exit | Stable | `ZoomTo(key)`, `NavigateToAncestor(null)` |
| Breadcrumbs | Stable | `TreemapBrowser.Breadcrumbs` |
| Space+drag panning | Stable | ScrollViewer offset manipulation |
| Ctrl+scroll zoom | Stable | 0.5x–30x range |
| Root overview "fit all" | Implemented-Unverified | Panorama mode uses all photos rather than a fixed sample, with logical canvas dimensions inverse to zoom so minimum thumbnails remain recognizable. |
| "适应全部" button | Reverted | Was in UI, non-functional, removed |

## Space Treemap — Thumbnail Loading

| Feature | Status | Notes |
|---------|--------|-------|
| Viewport-driven loading | Stable | 150ms debounce. `RefreshTreemapViewportLoading()` called on scroll/zoom/pan; `VisibleRowRange` ensures only viewport rows are considered (60%). |
| Priority queue | Stable | Current viewport items submitted first; single-flight `_treemapLoadActive` + `_treemapLoadGeneration` guard (60%). |
| Pipeline stall recovery | Implemented-Unverified | `SelfHealTreemapThumbnailsAsync` (skipCancel). `_treemapLoadActive` guard. |
| First-batch-only bug (KI-01) | Resolved | Removed duplicate Cancel calls + generation guard; viewport queue drains on completion. |
| Single-column-only bug (KI-02) | Resolved | Fixed viewport intersection logic |
| Async dimension reading | Implemented-Unverified | `LoadTreemapDimensionsAsync` — Task.Run batch read. |
| Thumbnail source lookup | Stable | `_treemapSourceLookup` prebuilt once per repopulation; no O(n) dictionary rebuild per debounce/drain (60%). |

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

## 2026-08-08 Treemap Performance Update

| Concern | Status | Notes |
|---|---|---|
| Justified-gallery aspect refresh | Implemented-Unverified | Background header dimensions republish on the UI context; layout tests pass. |
| Viewport thumbnail pipeline | Implemented-Unverified | Debounced visible paths use a generation-safe bounded queue rather than cancellation/restart. |
| Root overview | Implemented-Unverified | Root control uses viewport dimensions, Squarified category fill, and semantic detail thresholds. |
| Large-library manual QA | Pending | Required for 6217+ scrolling and 11739-item responsiveness. |

## Performance

## 2026-08-16 图库交互与控件可读性

| 功能 | 状态 | 说明 |
|---|---|---|
| 图库滚轮 | 稳定 | 普通滚轮只滚动，保留系统滚动惯性与边界行为 |
| 图库缩放 | 稳定 | Ctrl + 滚轮以指针锚点缩放；工具栏四种入口共用同一状态 |
| 按钮可读性 | 稳定 | 主按钮强制前景色；禁用态使用语义颜色，不再整体淡化 |
| 选择控件 | 稳定 | 单选框、复选框使用自定义 M3 模板，无原生灰色矩形残留 |
| 六套主题 | 已复查 | 动态、森林、紫罗兰的浅色与深色均完成运行时切换检查 |

## 2026-08-16 WPF 安装外壳与中性主题

| 功能 | 状态 | 说明 |
|---|---|---|
| WPF 安装外壳 | 稳定 | 自包含单文件外壳嵌入 MSI，实际启动通过 |
| 分步安装 | 稳定 | 安装选项、使用须知、安装进度、完成四步 |
| 须知阅读门禁 | 稳定 | 滚动到末尾后解锁同意框，勾选后解锁安装 |
| 安装器主题 | 已复查 | 浅色、深色即时切换，统一应用图标和圆角视觉 |
| 经典黑白主题 | 已实现 | 原 Dynamic 方案改为低饱和黑白灰浅色/深色主题 |
| 深色文字可读性 | 已修复 | 设置页正文与行标题使用 OnSurface 语义前景色 |

| Concern | Status | Notes |
|---------|--------|-------|
| UI hang on large treemap (KI-07) | Resolved | Startup publication is bounded to 1,024-item scan/dimension batches and panorama layout is snapshot-cached; UNC startup also skips recursive auto-cleanup, reuses media scan capacity statistics, and avoids rebuilding the completed all-library treemap solely to apply neutral filters. |
| 6217+ items scrolling | Resolved | `ContentHeight` → ScrollViewer.ExtentHeight, plus 60% viewport-row binary search + layout/group memoization keep scroll frames O(visible). |
| Bottom items clipped (KI-06) | Resolved | Same fix as above. |
| 10k+ items layout time | Resolved | `JustifiedGalleryLayout` is O(n) once per republish, now memoized per (ItemsSource+RootKey+width); no per-frame re-layout. 11,739-item layout bounded in a regression test. |

## Cross-cutting

## Semantic Search

| Feature | Status | Notes |
|---|---|---|
| Chinese-CLIP semantic search | Implemented-Unverified | Integrated into Photo Library browse conditions. First query automatically indexes in the background with progress/cancel; the App layer emits only deduplicated score-descending Top 50 candidates to the grid/treemap, and remains composable with date, rating, category, retouch, file-type, smart-category, and people filters. The standalone sidebar page was removed. Real-library query QA remains required. |

| Item | Status |
|------|--------|
| MapPage WebView2 crash | Resolved |
| Duplicate detection | Stable | SHA-256 exact matching after size prefilter; visual hash remains review-only; explicit import decision dialog |
| Retouched output write protection | Stable | `<root>\<month>\<date>\修后` files remain scan-visible but are disabled in review, filtered before delete, and skipped by resequencing |
| Cloud provider pages | Stable |
| Face recognition | Stable |
| Import flow | Stable |
