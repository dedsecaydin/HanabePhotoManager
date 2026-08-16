# Agent Change Log

## 2026-08-16 — 像素画尺寸自定义 + 按键对比度修复

### Task
① 像素画（PixelArt）工具的尺寸由固定 64/128/256 预设改为可自定义：新增「自定义」数字输入框，可输入任意像素尺寸（如 96、512），选中自定义时用输入值，输入无效（0/负数/非数字）回退默认 128；保留 64/128/256 预设按钮，点击预设时自定义输入框同步显示数值，最终生成与导出 PNG 均使用有效尺寸。② 修复像素画工具按键对比度不足、按键内文字看不清：参照已有修复方案（`Themes/Controls/Buttons.xaml` 的 `ContentPresenter` 加 `TextElement.Foreground="{TemplateBinding Foreground}"`），处理像素画页内联按钮样式被隐式 `TextBlock` 样式强制成低对比度前景色的问题。铁律：0 警告 0 错误、错误提示全中文、不动 GitHub、每项改动单独 build、跑 `dotnet test` 确认通过。

### 改动
- **`PixelArtViewModel.cs`**：删除字符串 `SelectedSize` 与 `TargetSizes` 列表（改名为 `PresetSizes` 仅作文档）；新增 `SelectedSize`(int，默认 128)、`IsCustom`、`CustomSizeText`、`SelectPreset(int)`（写入尺寸并退出自定义模式，同时把数值同步到自定义输入框）、`SelectCustom()`、`ResolveEffectiveSize()`（自定义模式解析输入、无效回退 128，预设模式用 `SelectedSize`，沿用 8–4096 钳制）；`Generate()` 改为经 `ResolveEffectiveSize()` 取有效尺寸，自定义输入无效时生成前打「已回退到 128」中文提示。
- **`PixelArtView.xaml`**：尺寸选择由「可编辑 ComboBox」改为「3 个预设 RadioButton（64/128/256）+ 1 个『自定义』RadioButton + 1 个数字 `TextBox`（绑定 `CustomSizeText`、`IsEnabled` 绑定 `IsCustom`）」，全部同 `GroupName="PixelArtSize"`；新增页内 `PixelArt.SizeOption` RadioButton 样式（选中态 `Brush.Primary` 底 + `Brush.OnPrimary` 字，未选 `Brush.Text.Secondary`）。
- **`PixelArtView.xaml.cs`**：新增 `SizeOption_Checked` 处理器，按 `Tag`（64/128/256/custom）分发到 `SelectPreset`/`SelectCustom`（含 DataContext 未就绪时的空防护）。
- **按键对比度修复（需求 2）**：新增的 `PixelArt.SizeOption` 模板中 `ContentPresenter` 显式 `TextElement.Foreground="{TemplateBinding Foreground}"`（与 `Button.Primary/Secondary` 的 B9 修复同源），避免「64/128/256/自定义」字符串被隐式 `TextBlock` 样式强制成低对比度前景色；像素画页既有「选择图片/生成像素画/导出 PNG」按钮均已使用 `Button.Secondary/Primary`（已含该修复），复核无遗漏。
- **`PixelArtViewModelTests.cs`（新增测试）**：覆盖默认 128、`SelectPreset` 退出自定义并同步输入框、自定义有效值（96/512/5000→钳制 4096）、无效值（空/0/负数/非数字→回退 128）。

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug /warnaserror`：0 警告 / 0 错误。
- `dotnet test HanabePhotoManager.sln -c Debug --no-build`：**596 全绿 exit 0**（Core 159 / Infra 54 / App 383，App 较上轮 372 增加 11 个像素画尺寸选择测试）。

### Notes（风险）
- 尺寸选择 UI 由 ComboBox 改为 RadioButton + TextBox，属表现层重构；生成/导出渲染逻辑（`PixelArtRenderer`）零改动，仅把取数源从旧字符串 `SelectedSize` 改为 `ResolveEffectiveSize()`。
- 「回退 128」为生成时回退并提示，不改写用户输入框内容；钳制上限仍为 4096（沿用既有安全边界），避免异常尺寸导致内存/渲染问题。
- 新 RadioButton 样式为页内 keyed 样式（`PixelArt.SizeOption`），未改动共享 `Navigation.SegmentItem`，避免影响浏览页修图状态/文件类型等其它 segment 的表现。

---

## 2026-08-14 — 全应用 UI 修复 B1/B5–B22（收尾批次）

### Task
按「下一轮任务交接」文档逐项修复 UI：B1 大标题+内容合一容器、B5 归属确认溢出、B6 强调色统一紫色、B7 筛选面板间距、B8 功能说明位置、B9 扫描重复按钮对比度、B10/B16 全局 checkbox→Switch、B11 相册右键菜单、B12 移除网盘、B13 导入 tips 轮播、B14 导入来源合并、B15 人物面板精简、B17 日期分组红色框排查、B18 缩略图滑块裁切、B19 日期分组横向条不完整、B20 快速操作按使用排序、B21 快速操作间距、B22 设备检测只留外部设备。铁律：0 警告 0 错误、错误全中文、不用 `#hex`（Theme 之外）、每项 build。

### 逐项
- **B6 紫色统一**：用户选择「默认主题切到 Violet」。改 `App.xaml` 默认入口 `Dynamic.Light.xaml`→`Violet.Light.xaml`；`ThemeManager` 的 `CurrentScheme` 默认、`ParseSchemePreference`/`ParseCombinedPreference` 回退全部由 Dynamic→Violet；更新 `ThemeManagerTests`（`ParseSchemePreference_UsesVioletAsSafeDefault`）与 `DesignSystemResourceTests.App_LoadsTheLightThemeEntryPoint`。保留 6 套主题体系与「动态色彩=蓝」语义。
- **B9 扫描重复按钮对比度**：根因是 `Button.Primary/Secondary` 模板的 `ContentPresenter` 未传 `Foreground`，字符串内容被隐式 `TextBlock` 样式强制为 `Text.Primary`，导致「蓝底深字」。给两个 `ContentPresenter` 加 `TextElement.Foreground="{TemplateBinding Foreground}"`（`Themes/Controls/Buttons.xaml`）。
- **B11 相册右键菜单**：`CustomAlbumsPage.xaml` 相册卡加 `ContextMenu`（管理/重命名/删除），`CustomAlbumsPage.xaml.cs` 加 `AlbumMenu_*` 处理器（经 `ContextMenu.PlacementTarget` 取相册项）；重命名进详情并聚焦 `AlbumRenameTextBox`，删除带确认。
- **B12 移除网盘（大）**：删除 `Core/Cloud`、`Infrastructure/Cloud`、`App/Cloud`、`Services/CloudConnectionSettingsService.cs` 及 16 个云测试文件；清掉 `MainWindow.xaml`（网盘导航项 + `CloudPageContainer`）、`MainWindowViewModel`（云命令/属性/onboarding 步骤/导航项/页面标题）、`MainWindow.xaml.cs`（`--cloud-provider`/`AnimateCloudProvider`/云 host 映射/百度夸克处理器）、`App.xaml.cs`、`SettingsCenterPage`（云盘与项目分区）、`NavigationOrderPolicy`、`AppSettingsStore`（Baidu/Quark 字段）、`ReleaseNotesViewModel` 云相关条目、`Infrastructure.csproj` 的 DPAPI 引用。**保留 LibVLCSharp 视频预览与投稿/欣赏项目**。测试 994→585（云 ~387 例 + 少量导航/onboarding/controltheme 断言更新）。
- **B1 大标题+内容合一容器**：`MainWindow.xaml` 移除 `Layout.TopBar` 的标题/副标题/首页库按钮（仅留透明拖拽区 + 右上角窗口控制按钮）；Home/Import/Preview/FaceSearch 四页在各自 `Layout.PagePanel` 顶部加入 `Layout.PageTitle`+副标题（Import/Preview/FaceSearch 用 `DockPanel` 包标题+内容，Home 标题进滚动区并迁入「选择库根目录/刷新」）；`MapPage.xaml` 同样加「地图照片」标题。更新 `ControlThemeTests.ShellChrome_UsesRoundedM3Containers...`。
- **B13 导入 tips 轮播**：移除左面板 RAW/视频格式 tip 与右 Inspector 归属确认 tip，右 Inspector 顶部加 `ImportTipCard` 单 tip；`MainWindow.xaml.cs` 加 `DispatcherTimer`（6s）在两条 tip 间轮播。
- **B14 导入来源合并**：把「拖入相机文件夹」拖放区与「来源」卡合并为一个卡片（拖放头 + 分隔线 + 来源文件夹 + 两个选择按钮）。
- **B5 归属确认溢出**：右 Inspector「归属确认」的 ComboBox+按钮由同行挤压改为上下两行（ComboBox 全宽 + 按钮全宽），消除灰色方框溢出。
- **B15 人物面板精简**：移除浏览页左栏人物面板的 `PeopleRecognitionModelPanel`（模型说明/版本/阈值/前往设置），只留扫描按钮 + 显示全部人物；更新 `PeopleAlbumViewModelTests` 断言。
- **B10/B16 全局 Switch**：`App.xaml` 与 `MainWindow.xaml` 的隐式 `CheckBox` 样式改为「正常药丸胶囊轨道 + 圆形滑块」Switch 模板（40×22 轨道、18×18 圆形滑块、选中 `Brush.Primary`）；浏览页多选复选框 22×22→40×22。
- **B7 筛选面板间距**：智能搜索模式下拉 `Margin 12→8`、`Width 120→116`；高级折叠区「识别当前范围/停止」按钮间距 8→14。
- **B8 功能说明位置**：`AppSettings` 加 `FeatureDescriptionPosition`（Top/Left，默认 Top）+ VM 属性（持久化）+ 设置「照片库与导入」分区加「功能说明位置」下拉；`分类` 筛选标签按该设置在上/左间切换。
- **B18 缩略图滑块裁切**：缩略图大小标签行重排（值文本 Dock=Right 在前、标签填充），消除「211px」显示不全。
- **B19 日期分组横向条不完整**：`VirtualizingWrapPanel HeaderHeight 56→64`，修复日期分组头底部被裁切。
- **B17 日期分组红色框排查**：全仓排查无硬编码红色（唯一 `Status.Danger` 用于关闭按钮/导入失败/看图器错误），日期分组头 XAML 无红元素；判定为日期头被裁切的渲染残留，已随 B19 的 HeaderHeight 修复一并消除。
- **B20 快速操作按使用排序**：`AppSettings.QuickActionUsage`（字典）持久化；主页 6 个硬编码快速操作按钮改为 `ItemsControl` 绑定 `QuickActions`（使用过的按最近在前、未使用保持原序）；`CurrentPage` setter 记录页面使用、`Compression.SelectedToolMode` 变化按工具分开计 `Compression:<tool>` 记录。
- **B21 快速操作间距**：快速操作按钮 `Margin 8→14`。
- **B22 设备检测只留外部设备**：`RefreshConnectedDevices` 跳过 `DriveType.Fixed`（本机 C:/D:）；`FormatDriveType` Removable→「U盘 / 存储卡」；首页设备副标题改「自动检测 U盘、存储卡、相机、网络设备等外部设备」。

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug /warnaserror`：0 警告 0 错误。
- `dotnet test HanabePhotoManager.sln -c Debug --no-build`：**585 全绿 exit 0**（Core 159 / Infra 54 / App 372）。
- `dotnet publish`（Release/win-x64/self-contained/PublishReadyToRun）成功，覆盖安装并启动供真人验收。

### Notes
- B12 云盘移除后测试数从 994 降为 585，差值由云测试（~387 例）与少量云相关导航/onboarding/controltheme 断言更新组成，无业务逻辑回归。
- B17 未能复现「红色矩形框」的具体来源，已全仓排查确认无硬编码红；诚实记录为「随 B19 高度修复消除」，若真人验收仍见红色框请截图反馈定位。
- **额外修复（启动验证发现）**：`CompressionPage.xaml` 的压力测试结果区引用了设置页专属的 `{StaticResource Settings.Divider}`（`Settings.Divider` 定义在 `SettingsCenterPage.xaml` 的 UserControl.Resources，CompressionPage 作用域不可见），导致启动时 `Application.DoStartup` 抛 `XamlParseException: 无法找到名为 Settings.Divider 的资源`（此前历次启动日志均有记录）。改为内联 `<Border Height="1" Background="{DynamicResource Brush.OutlineVariant}" .../>`，重发布后启动无新异常。

---

## 2026-08-14 — M3 功能页重设计第三批（工具页 + 地图页 + 网盘页，对齐 008/009/010 mockup）

### Task
按用户确认的预设计（`sketches/008-m3-tools` / `009-m3-map` / `010-m3-cloud`）重做「图片小工具」「地图照片」「网盘」三个功能页。铁律：仅改视觉/布局/交互组织，VM/Command/Binding 尽量不动（本轮零 VM 改动）；只用语义 Token；动效 150/180/220ms；禁 Card 套 Card / 粗边框 / 巨大圆角 / 强渐变 / 玻璃拟态 / Emoji 功能图标；分块构建验证（工具页 → 地图页 → 网盘页）。

### A. 工具页（`Compression/CompressionPage.xaml` + `.xaml.cs`）
- **工具卡片网格落地视图**（`ToolGridHost`）：`Layout.PageTitle`「图片小工具」hero + `WrapPanel` 6 张工具卡（压缩/拼图/水印/微信发送/投稿项目/欣赏项目，`ToolCardButton` keyed style：surface-container-low 大圆角 + hover/pressed/focus 状态层，`ClipToBounds` 封面 140px + 名称 + 描述）。封面用 **M3 tonal 容器纯色 + 大图标**（`Icon.Compress/Album/Watermark/Export/Star` + `Brush.PrimaryContainer/SecondaryContainer/TertiaryContainer` 循环），**刻意不用 mockup 的线性渐变**（遵铁律「无强渐变」）。
- **详情工作台**（`ToolDetailHost`）：顶栏「← 返回工具」`Button.Secondary` + 原 `ImageToolModeTabs` 分段 chips（restyle 为全圆角 `secondary-container` 选中态，`ToolModes`/`SelectedToolMode` 绑定不动）；下方 `* / 16 / 320` 三列 = 左参数区（360px 原「添加图片/压缩设置/拼图设置/输出目录」卡原样保留，全部命令/绑定不动）+ 中队列/预览（压缩/拼图队列原样；`watermark:WatermarkPage` 与 `wechat:WeChatSenderView` 子页原样嵌入）+ 右 320px `Inspector.Panel` 运行统计（输入项 `Items.Count` / 原始字节 `OriginalTotalBytes` / 输出字节 `OutputTotalBytes` / 进度 `ProgressValue` 四格统计 + 处理结果 `Results` 列表 + 提示，**零 VM 新增**）。
- **code-behind 新增**：`ToolCard_Click`（Tag 分发：4 工具 → `ShowTool`；投稿/欣赏 → `ShowContestOpenCommand`/`ShowContestJudgedCommand` 导航）、`BackToGrid_Click`、`ShowDetail`；**`SelectedToolMode` PropertyChanged 订阅**：onboarding 第 8 步 / `ShowWatermarkCommand` 深链先设 `SelectedToolMode` 再导航时，自动进详情工作台而非卡片网格（保持深链行为）。
- 压缩/拼图/水印/微信四工具切换沿用既有 `DataTrigger.EnterActions` 180ms 淡入。

### B. 地图页（`Map/MapPage.xaml` + `Map/assets/map.css`）
- 右列 380px → **320px**（对齐 M3 Inspector 标准）。「地图照片」卡改 `Inspector.Panel`（`Inspector.Header` + 读取 EXIF `RefreshCommand` + 选择照片/文件夹 + `StatusText`）。
- **地点浏览面板**新增三格统计（当前地点 `SelectedLocationPhotos.Count` / 已定位 `LocatedPhotos.Count` / 聚合点 `Markers.Count`，`MapStatBlock` tonal 块）+ 原「当前位置照片」列表（`SelectedLocationPhotos` + 单击放大浏览）。
- **手动标记面板**保留：全选/反选/取消选择/移出所选/清空导入 chips + `ManualPhotosList`（Ctrl/Shift 多选）+ 目标坐标（`PendingLatitude`/`PendingLongitude`）+ 地点名（`PendingDisplayName`）+ 保存 `AssignSelectedCommand`。
- **map.css**：聚合数量徽标由蓝 `#0284c7` → 红 `#f43f5e`（对齐 mockup 红色数量徽标）；`map.js`/`MapPhotosViewModel`/`MapPage.xaml.cs` 逻辑零改动（标记堆叠缩略图 + 数量徽标 + 弹窗照片网格为既有 map.js 行为）。

### C. 网盘页（`Cloud/CloudPage.xaml`）
- **主区保留**：后退/前进/刷新/首页条 + WebView2 `CloudLoginBrowser` + 加载/失败/空/重试状态面板（`CloudStatusPanel/Title/Description/RetryButton` 与 `CloudPage.xaml.cs` 全部行为不动）。
- **右侧新增 320px `Inspector.Panel` 云盘总览**：账户卡（头像/「百度网盘 · 超级会员」/邮箱 + 「已连接」徽章）+ 云存储总览（用量环 68% `Ellipse`+`Path` 弧线 + 已用/总容量）+ 三格统计（已同步 12,408 / 本月上传 1,024 / 传输中 3）+ 传输队列（上传/下载/校验/完成 4 行含进度条，`CloudTqIcon` tonal 图标块）+ 「说明」明确标注「当前为视觉占位，可后续接入真实数据」。
- **数据说明**：`CloudHubViewModel`（`CloudAccountState`）与 `Core/Infrastructure` 的 `CloudTransferJob`/`JsonCloudTransferQueueStore` 基建**未接入** `CloudPage` 的 DataContext（CloudPage 为 WebView2 内嵌浏览器，无 VM 绑定），故总览/队列为视觉适配 + 「可后续接入」标注，未伪造可交互假数据。
- **合规修复（关键）**：CloudPage 因 `CloudPageTests` 在无 Application 主题资源的 STA 线程上运行时构造（`RunOnSta`），页内新增的 `{StaticResource Radius.*/Typography.*}` 在 `InitializeComponent` 即时求值抛 `XamlParseException`（未处理异常落在后台线程 → testhost 崩溃）。改为 `{DynamicResource Radius.Control/Radius.Full/Typography.Caption/Typography.BodySmall}`（与旧 CloudPage 全 DynamicResource 一致，延迟求值）；本地 keyed style（`CloudStatBlock`/`CloudTqIcon`）与 `Inspector.Panel` 等保持正常。

### 合规（测试驱动）
- `DesignSystemResourceTests.CompressionPage_IsPresentedAsImageToolsWithCollageControls`：原断言 `NotContain("图片小工具")` 与新版网格落地视图 hero 冲突 → 改为 `Contain("Text=\"图片小工具\" Style=\"{DynamicResource Layout.PageTitle}\"")`；同时恢复被误删的「纵向拼接 · 横向拼接」hint（测试断言依赖）。
- `ApplicationXaml_HasNoRawColorsOutsideThemeColorDictionaries`：三页 XAML 零 `#hex`；map.css 属 WebView2 独立资源（既有暗色硬编码风格），仅调徽标色，不在该回归测试范围。
- code-behind 二义性：`CompressionPage.xaml.cs` 的 `Button` 因 `global using System.Windows.Forms` 冲突，全限定 `System.Windows.Controls.Button`。

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug /warnaserror`：0 警告 / 0 错误。
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror`：0 警告 / 0 错误。
- `dotnet test HanabePhotoManager.sln -c Debug --no-build`：**917 全绿，exit 0**（Core 373 / Infra 164 / App 380）。
- 截图 `.artifacts/capture-m3-tools-map-cloud.ps1`（headless `--screenshot --page Compression/MapPhotos/Cloud`，播种 10 张小样图为 LibraryRoot）：`m3-tools/m3-map/m3-cloud-{light,dark}.png`（1344×986，像素采样确认非空白）。

### Notes（诚实记录）
- 工具卡封面**未用** mockup 的彩色线性渐变（铁律「无强渐变/霓虹」），改用 M3 tonal 容器纯色 + 图标；网盘用量环未用 mockup 的 conic-gradient，改用 `Path` 实色弧线（铁律「禁止彩色渐变」）。
- 网盘右侧总览/传输队列为**视觉占位**（`CloudHubViewModel`/`CloudTransferJob` 未接入 CloudPage DataContext），如实标注「可后续接入」；未伪造可交互假数据、未改 WebView2 行为。
- map 页地图本体为 Leaflet（WebView2），headless `RenderTargetBitmap` 只渲染 WPF 层（右侧 320px Inspector 完整呈现，地图瓦片区域可能空白）——这是 WebView2 截图固有局限，非布局缺陷。
- 第一轮测试出现「test host process crashed」（App 372~374/380）并非 onnxruntime 预存在问题，而是 CloudPage 页内 StaticResource 在无主题资源的 STA 测试线程上即时求值抛 `XamlParseException` 所致；经上述 DynamicResource 修复后 917 全绿。onnxruntime 的 graph 初始化警告仅写入 stderr、不影响结果。

---

## 2026-08-14 — M3 导入页 + 设置页重新设计（对齐 006/007 mockup）

### Task
按用户确认的预设计（`sketches/006-m3-import` / `007-m3-settings`）重做「导入页」与「设置页」：导入页三段布局（左 320 源面板 + 中队列 + 右 320 Inspector）、设置页左分区导航 + 右 M3 分组列表 + 常驻 Inspector（主题实时预览 + 数据/关于）。铁律：仅改视觉/布局/交互组织，VM/Command/Binding 尽量不动；只用语义 Token；动效 150/180/220ms；禁 Card 套 Card / 粗边框 / 巨大圆角 / 强渐变。

### A. 导入页（`MainWindow.xaml` + `ViewModels/MainWindowViewModel.cs` + `.Import.cs`）
- **三段布局**：`ImportPage` 由 380px+* 两列改为 `320 / 16 / * / 16 / 320` 五列。左「导入源」`Inspector.Panel`（320px，surface-container-low 大圆角）：相机拖放区（`Icon.Import` 图标 + `SourceAutoImportDropTarget_DragOver/Drop` 原样保留）、来源卡（`SourceFolder` + `BrowseSourceCommand`/`BrowseSourceFilesCommand`）、转移方式 `ComboBox`（`TransferModes`/`SelectedTransferMode` 绑定不动）、本地 AI 人物识别 `CheckBox`（`EnablePersonRecognition`）、修后/素材拖放区（`EditedDropTarget_Drop`/`MaterialDropTarget_Drop`）、`AnalyzeSourceCommand` + `ImportSelectedCommand` + 两个 onboarding Popup（`OnboardingAnalyzeButton`/`OnboardingImportButton` 命名与 PlacementTarget 全保留）。
- **中队列**：`Surface.ContainerLowest` 大圆角容器，队列头（`TargetDateText`/`ImportReport`/`ImportActionHint`）+ 进度卡（`IsImportRunning`/`ProgressLabel`/`ProgressValue`/`CancelCurrentTaskCommand`）+ 6 分类 section（`ImportSections`，分区头加 `Items.Count` 数量徽章，预览卡 `ImportPreviewItemViewModel` 重排为「缩略图 + 队列号/CategoryBadge scrim 角标 + 名称/文件名/大小/日期 + 人物徽章 tertiary-container + 人工确认徽章 primary-container + 勾选 + 分类下拉」，`ShowMoreCommand`/`HiddenCount`/`HasHiddenItems` 保留）。
- **右 Inspector（320px，`Inspector.Panel`）**：① 导入设置——精确查重(SHA-256)/相似照片审查(感知哈希)/修后目录只读保护 三行只读开关（`IsEnabled=False`，修后保护行 `Opacity=0.72` 置灰铁律，如实呈现「始终开启/铁律不可关」而不伪造可切换行为）；② 去重结果——三选项（全部跳过推荐/全部仍导入/逐个选择）只读 radio + 说明（实际去重仍走既有 `ImportDuplicateBatchDecisionWindow` 模态，此处仅信息展示不改变流程）；③ 本次导入摘要——成功/跳过/失败三格统计卡。
- **VM 最小化新增（记录）**：`ImportSuccessCount`/`ImportSkippedCount`/`ImportFailedCount` 三个只读 int + `SetImportSummary(success, skipped, failed)`，在 `RunImportAsync` 完成/取消时赋值、5 处 `ImportItems.Clear()` 复位处 `SetImportSummary(0,0,0)`。仅新增只读属性，未改任何导入/去重/转移/分类逻辑。

### B. 设置页（`SettingsCenterPage.xaml` + `.xaml.cs` + 主题资源）
- **布局**：`TabControl`（`Navigation.SettingsTabs`）改为 216px 左分区导航（`ListBox`+`List.Default`/`ListItem.Default`，6 分区：外观/常规/照片库与导入/浏览与AI/云盘与项目/高级，`SelectionChanged` code-behind 切换 6 个 `StackPanel` 可见性）+ 右 `ScrollViewer` M3 分组列表（group header=primary 小标题 + outline-variant 分隔线 + `Settings.Group`（surface-container-low 大圆角）+ 设置行（标题/描述 + 控件，`Settings.Divider` 行分隔））+ 320px 常驻 Inspector。
- **外观分区 6 套主题色卡**：`ThemeCard`（keyed Button，surface-container-lowest 大圆角）+ 6 张色卡（动态色彩/森林绿/紫罗兰 × 浅/深），点击 `ThemeCard_Click` 读 `Tag="Scheme.Mode"` → `ThemeManager.Apply(theme, scheme)`；当前主题高亮（`UpdateThemeIndicators` 用 `Brush.Primary` 描边 + ✓ 角标）+ Inspector 色板/窗口迷你预览随 Token 实时联动（全 DynamicResource）。
- **Inspector 常驻**：当前主题（6 色板 + 窗口迷你预览 + `CurrentThemeTag`）+ 数据与存储（设置目录 `{x:Static services:AppDataPaths.Root}` + `LibraryHealthText` + `DiscoveredDateCount`）+ 关于（`ReleaseNotes.CurrentVersionLabel` + net8.0-windows + 个人使用）。
- **保留全部功能项**：开机自启/窗口恢复/`WindowStateSummary`、新手指南 `ReplayOnboardingCommand`、版本树（`ReleaseVersionTree`/`ReleaseNotes.Versions`/`SelectedVersion`/`SelectedReleaseTitle`/`SelectedReleaseNotes`/`CurrentVersionLabel` 完整保留）、`LibraryRoot`/`BrowseLibraryCommand`、浏览默认值（评分/排序/恢复策略/缩略图）、AI 识别（引擎/标签数/相似度窗口）、百度凭据（AppKey/AppSecret/保存/授权/断开）、夸克路径、推理设备、人脸引擎/ArcFace 路径/阈值/许可、快捷键/安全隐私等——全部照抄旧 6 分区，仅改视觉。**顺手修复旧死绑定 `LibraryCapacityText`（VM 已无此属性，静默绑定失败）**：改用 `LibraryHealthText` + `DiscoveredDateCount`。
- **主题色卡资源**：新增 `Themes/Colors/Colors.ThemeSwatches.xaml`（18 个 `Brush.ThemeSwatch.<Scheme>.<Mode>.<Role>`，色值与 `Colors.<Scheme>.<Mode>.xaml` 的 primary/secondary/tertiary 一致），合并进 6 套主题入口；页面不再写死 `#hex`（满足 `ApplicationXaml_HasNoRawColorsOutsideThemeColorDictionaries` 回归测试）。

### 合规修复（测试驱动）
- `AppearanceAndCompressionSelectors_UseTheSharedThemedComboBoxTemplate`：设置页所有下拉改用共享 `Input.SettingsComboBox`（`Style` 紧邻 `ItemsSource` 满足断言）。
- `ApplicationXaml_HasNoRawColorsOutsideThemeColorDictionaries`：主题色卡由页内 `#hex` 改为 `Colors.ThemeSwatches.xaml` 资源引用。
- code-behind 二义性：`Button`/`Brush` 因 `global using System.Drawing/System.Windows.Forms` 与 WPF 类型冲突，全限定为 `System.Windows.Controls.Button`/`System.Windows.Media.Brush`。

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug /warnaserror`：0 警告 / 0 错误。
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror`：0 警告 / 0 错误。
- `dotnet test HanabePhotoManager.sln -c Debug --no-build`：917 全绿（Core 373 / Infra 164 / App 380）。
- 截图 `.artifacts/capture-m3-import-settings.ps1`（headless `--screenshot --page Import/Settings`，播种 home-fix-fixture 为 LibraryRoot）：`m3-import-{light,dark}.png` + `m3-settings-{light,dark}.png`（1344×986）+ 切主题 `m3-settings-violet-dark.png`；像素采样确认各主题 surface/primary 正确渲染。

### Notes（诚实记录）
- 导入页 Inspector 三处（导入设置开关 / 去重结果 / 完成摘要）中，「去重结果」与「导入设置」为**信息展示**（真实去重仍走模态窗、精确查重/相似审查无 VM 开关、修后只读保护为铁律），仅「完成摘要」新增 3 个只读 int 绑定真实计数。未伪造可交互的假开关、未改去重流程。
- mockup 的「分析报告进度卡常显」「预览卡 Emoji 图标」「inspector 去重缩略图/size·time 摘要」等无对应 VM 数据的装饰项未臆造（用文字/语义 token 呈现）。

---

## 2026-08-14 — 人物页功能补全：合并命令 + 详情照片虚拟化

### Task
上轮遗留技术债第 1、2 项（用户确认先补「合并」命令）：① 人物「合并」命令——`PeopleAlbumService.MergeAsync` 已存在但 VM 未暴露命令，人物详情只有「保存姓名」没有合并入口；② 人物详情照片虚拟化——`PhotoPaths` 用 `WrapPanel` + 同步缩略图解码（`PathThumb` 转换器），百张以上卡顿。铁律：只改人物相关；复用 `MergeAsync` 服务逻辑不改，只加命令层 + UI；全程语义 Token；动效 150/180/220ms。

### A. 人物合并命令（`PeopleAlbumViewModel.cs` + `People/MergePersonDialog.xaml` + `MainWindow.xaml`）
- **VM 暴露 `MergeCommand`**（`IAsyncRelayCommand`，`CanMerge = SelectedAlbum != null && Albums.Count >= 2`）：`MergeSelectedAsync` 取 `SelectedAlbum` 为源、排除源后选目标（`_mergeTargetPicker(candidates)` 可注入，默认 `ShowMergeDialog` 弹 `MergePersonDialog` 模态窗），调 `_service.MergeAsync(target.Id, source.Id, default)`（目标=参数1、源=参数2，逻辑未改），随后 `RefreshAlbumsAsync`（`_service.LoadAsync` → `ReplaceAlbums`）刷新人物列表，`SelectedAlbum` 落到合并目标并写 `StatusText`（「已将「源」合并到「目标」」）。`Albums.CollectionChanged` + `SelectedAlbum` setter 均 `MergeCommand.NotifyCanExecuteChanged()`。
- **UI 入口**：人物详情 hero 可编辑姓名 DockPanel 追加「合并到…」按钮（`Button.Secondary`，`Command="{Binding PeopleAlbums.MergeCommand}"`），与「保存姓名」同排右对齐。
- **`MergePersonDialog`**（新，`Dialog.Window`/`Dialog.Surface`/`Dialog.Title`/`Dialog.Body` + `List.Default`/`ListItem.Default` + `Button.Secondary`/`Button.Primary`）：标题 + 说明文案 + 候选人 ListBox（`Name` + `PhotoCount`）+ 取消/合并；双击候选可直接合并；`SelectedTarget` + `DialogResult` 回传。复用 `ContestPickerWindow` 模式。

### B. 人物详情照片虚拟化（`Controls/VirtualizingWrapPanel.cs` + `PersonPhotoViewModel`）
- **新增 `VirtualizingWrapPanel`**（`VirtualizingPanel` + `IScrollInfo`，固定 142×142 瓷砖步长 `ItemWidth/ItemHeight`）：按视口宽度折算 `ItemsPerRow` 换行，只 realize 视口内行（上下各缓冲一行），`MeasureOverride`/`ArrangeOverride`/`CleanUpItems` 走 `IItemContainerGenerator`；`SetVerticalOffset`/`MakeVisible`/`LineUp…` 等 IScrollInfo 成员全量实现（`CanVerticallyScroll=true`）。`Size`/`Point` 因 `UseWindowsForms` 与 `System.Drawing` 二义性用 `using Size/Point = System.Windows.*` 别名。
- **`PersonPhotoViewModel`**（新，`ObservableObject`）：`Path` + 懒加载 `Thumbnail`（`EnsureThumbnailLoaded` 幂等，`Interlocked` 三态 + 静态 `SemaphoreSlim(PreviewLoadingPolicy.ThumbnailConcurrency)`=4 + `Task.Run` 解码 + `Freeze` + Dispatcher 回填，失败保留占位）。
- **`PersonAlbumItemViewModel`**：`PhotoPaths`（`HashSet<string>`）**保持不变**（浏览页 `RefreshFilteredCache` 仍在 `person.PhotoPaths.Contains(...)` 过滤，数据源不变），新增 `ObservableCollection<PersonPhotoViewModel> Photos` 镜像 `PhotoPaths`（构造 + `UpdateFromProgress` 时 `RebuildPhotos` 复用已加载缩略图）。
- **`MainWindow.xaml`**：详情照片区 `ItemsControl`+`WrapPanel`+`PathThumb` 换 `ListBox`（`List.Default` + `VirtualizationMode=Standard` + `ItemsPanel=VirtualizingWrapPanel` + 自定义 `ItemContainerStyle` 去 MinHeight/Padding + `ItemTemplate` 绑 `Thumbnail`、`Loaded="PersonPhoto_Loaded"` 触发懒加载）；**详情区从外层 `ScrollViewer` 抽出为同级 `Grid`（hero 固定 + 照片 ListBox 独立 `*` 行滚动）**，否则 `StackPanel` 内无限高度会令 ListBox 无法虚拟化。Tab 1 可见性改由 `RefreshPeopleTabContent()`（code-behind）统一驱动（`PeopleGroupsPanel`/`PeopleGroupsDetail`/`PeopleSearchPanel` 三态，订阅 `PeopleAlbums.SelectedAlbum` 变更）。

### Tests
- `PeopleAlbumViewModelTests` 新增 2 例：`MergeCommand_MergesSelectedPersonIntoChosenTargetAndRefreshesList`（注入 `candidates => candidates.First()` 假 picker，断言合并后 1 人、照片并集、`SelectedAlbum` 指向目标、`StatusText` 含「合并」）+ `MergeCommand_IsDisabledWithoutASelectionOrWithOnlyOnePerson`（单选/单人 `CanExecute=false`）；补 `FakeEmbeddingService` 嵌套桩。

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug /warnaserror`：0 警告 / 0 错误。
- `dotnet test HanabePhotoManager.sln -c Debug --no-build`：917 全绿（Core 373 / Infra 164 / App 380，+2）。
- 截图 `.artifacts/capture-m3-merge.ps1`（headless `--screenshot --page FaceSearch --select-first-person`，播种 120 张人物 A + B/C 合并目标）：`m3-facesearch-merge-{light,dark}.png`（1600×980，含合并 UI：详情 hero「合并到…」按钮 + 虚拟化照片网格）。

### Notes（诚实记录）
- `MergeAsync` 服务逻辑零改动（仅命令层 + UI）；合并把源人物的照片/脸谱并入目标并删除源（`MergeAsync` 既有行为，服务测试 `RenameMergeAndRemoval_AreDurableManualOverrides` 已覆盖）。
- 「待确认角标」数据缺失与「浏览/人物筛选收敛」两项遗留技术债本轮未做（按任务范围）。

---

## 2026-08-14 — M3 人物页 + 相册页重新设计（对齐 004/005 mockup）

### Task
把「人物页」（FaceSearchPage + 浏览页 PeopleAlbum 人物功能合并）与「相册页」（CustomAlbumsPage）按预设计 mockup 重做：人物页双 Tab（人物相册 / 按脸查找）、相册页卡片流 + 详情 + Inspector EXIF。铁律：仅改视觉/布局/交互组织，VM / Command / Binding / 数据流不动；全程语义 Token；动效沿用 150/180/220ms；禁 Card 套 Card / 粗边框 / 巨大圆角 / 强渐变。

### 人物页（`MainWindow.xaml` + `MainWindow.xaml.cs`）
- **双 Tab 分段切换**：`FaceSearchPage` 顶部 `Navigation.Segment`（`RadioButton` × 2，`GroupName="PeopleMainTab"`），`Checked="PeopleMainTab_Checked"` code-behind 切换 `PeopleGroupsPanel` / `PeopleSearchPanel` 可见性（含 InitializeComponent 期间的 null 防护）。
- **Tab 1 人物相册**：顶部扫描状态条（`PeopleAlbums.RecognitionEngineText` 模型徽章 tertiary-container + `SummaryText` 汇总 + `StatusText` + `ScanProgressValue` 进度 + `ScanCommand`/`CancelScanCommand`）；人物头像网格（96px 圆形头像 `CoverPath` + `Name` + `PhotoCount`，`SelectCommand` 进详情）；空态（`Albums.Count == 0` DataTrigger）；详情 hero（112px 头像 + 可编辑 `Name` + `SaveNameCommand` 保存 + `PhotoCount`）+ 该人物照片网格（`SelectedAlbum.PhotoPaths`，`PathThumb` 缩略图 + 双击打开）。
- **Tab 2 按脸查找**：左侧 300px 控制列（参考图 dropzone + `ChooseReferenceCommand` + 查找范围 ComboBox + 匹配强度 Slider 30–72% + `StartSearchCommand`/`CancelCommand`/`ClearCommand`）；右侧结果网格（`SimilarityText` 徽章 + `Name` + `Folder`，双击 `FaceResult_MouseLeftButtonDown`）。保留 `FaceReferenceClipSurface` 圆角 OpacityMask（回归测试要求）。
- 新增 View 层转换器：`NullToVisibilityConverter`（null ↔ 可见，`Invert` 参数反转，驱动总览/详情切换）、`PathThumbnailConverter`（路径字符串 → 降采样冻结缩略图，`ConverterParameter` 控制宽度）。

### 相册页（`Albums/CustomAlbumsPage.xaml` + `.cs`）
- **卡片流总览**：`AlbumCardButton`（16:10 封面 + `Icon.Album` 占位 + 不可用文件夹⚠ tertiary 角标 + `DisplayName` + `FolderPath`）；末尾虚线「＋新建相册」卡 + 右下 FAB（均 `AddFolder_Click`）；`Albums.Count` 徽章。
- **详情**：`BackToAlbums_Click` 返回 → hero（封面 + 可编辑 `EditableDisplayName` + `FolderPath` 路径胶囊 + `RenameSelectedCommand`/`RefreshSelectedCommand`/`RemoveSelectedCommand`）→ 照片区 `网格/列表` 切换（`Navigation.SegmentItem` RadioButton × 2，`AlbumViewMode_Checked` code-behind 切 `AlbumPhotoGridPanel`/`AlbumPhotoListPanel`）；列表行（缩略图 + `Name` + `Length`→`FileSizeConverter`）。
- **Inspector 320px**：点击照片（`AlbumPhoto_Click`）→ 读 `PhotoDetailMetadataReader`（异步 Task.Run）填充 尺寸/拍摄时间/相机/镜头/ISO/文件大小/所属相册 + 缩略图 + 「打开原图/复制路径」；无选中占位态。
- 总览/详情/ FAB 可见性由 code-behind `_showingAlbumDetail` + `ApplyAlbumViewState()` 驱动（不引入 VM 状态）。

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug /warnaserror`：0 警告 / 0 错误。
- `dotnet test HanabePhotoManager.sln -c Debug --no-build`：915 全绿（Core 373 / Infra 164 / App 378）。
- 截图 `.artifacts/capture-m3-pages.ps1`（headless `--screenshot` + 播种 fixture 数据）：`m3-facesearch-light/dark.png` + `m3-albums-light/dark.png`（1344×1258，浅色均亮 226–228 / 深色均暗 22，非空白）。

### Notes（诚实记录 / 未实现项）
- **未臆造数据**：mockup 中「合并/导出」「待确认人物角标」「照片人脸数/日期角标」「相册卡数量徽章」「列表日期列」在现有 VM 中无对应数据/命令（`PeopleAlbumViewModel` 仅 `SaveNameCommand`/`SelectCommand`；`PersonAlbumItemViewModel.PhotoPaths` 为纯路径 `HashSet`；`CustomAlbumItemViewModel` 无照片计数；`CustomAlbumPhoto` 仅 `Name/FullPath/Length`），铁律「VM/Command/Binding 不动」下不予新增，故省略/以现有数据替代（重命名、PhotoCount、文件大小、Inspector 拍摄时间等已实现）。
- **「合并」命令**：`PeopleAlbumService.MergeAsync` 存在但 VM 未暴露 MergeCommand，故人物详情 hero 仅提供「保存姓名」，合并/导出未接线。
- **浏览页人物筛选面板保留未动**：浏览页左侧「人物」气泡筛选是浏览条件（选人过滤图库），与人物页管理视图职责不同，未删除以保留业务功能（后续可评估合并）。
- **⚠ 截图播种脚本首两版用 `powershell -File` 且 .ps1 含中文、UTF-8 无 BOM**，被 PowerShell 按 GBK 误读致 `Get-ChildItem` 返回空（播种失败 + `finally` 未跑），一度把 `%LOCALAPPDATA%\HanabePhotoManager\settings.json` 的 `LibraryRoot` 写成 fixture；已改用纯 ASCII .ps1 重跑并**手动恢复 `LibraryRoot = \\Hanabe\拍照` + 删除误建的 `custom-albums.json`**。settings.json 当前 2002 字节（原 1999，仅窗口尺寸因截图运行重存有 ±3 字节漂移，其余设置与库路径已还原）。

---

## 2026-08-14 — 浏览页筛选面板三轮调整（排序方式进高级筛选）

### Task
按用户最新要求：把「排列方式（排序方式）」从主区第 2 行收进高级筛选折叠区，主区只保留「分类 / 修图状态 / 评分分类」。铁律：仅改 UI 位置，VM / Command / Binding 不动，全程语义 Token，折叠动画 180ms 不变。

### 落地方式（`MainWindow.xaml`）
- **主区第 2 行**：`WrapPanel` 由「分类 / 修图状态 / 评分分类 / 排列方式」改为「分类 / 修图状态 / 评分分类」；原 `排列方式` `StackPanel`（`ComboBox`，`Style=PreviewSortComboBox`，绑定 `PreviewSortChoices`/`PreviewSortMode`，`AutomationProperties.Name="照片排列方式"`）整体从主区移除。
- **高级折叠区**：原「显示方式 / 面积」`WrapPanel` 末尾追加该 `排列方式` `StackPanel`，形成「显示方式 / 面积 / 排列方式」；绑定 / 命令 / 样式原样平移（仅 `Margin` 右间距由 16 对齐为兄弟项 20，纯间距），`ItemsControl.ItemTemplate` 与 `DataTemplate DataType={x:Type vm:PreviewSortChoice}` 均不变。
- 同步更新主区注释「高频筛选（分类 / 修图状态 / 评分分类）」。
- 高级折叠动画保持不变：`Border` + `ClipToBounds`，`MaxHeight` 0↔400 + `Opacity` 0↔1，`Motion.Duration.Normal`（180ms）+ `CubicEase EaseOut`；折叠态内容 `IsEnabled` 随展开态禁用。

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug /warnaserror`：0 警告 / 0 错误。
- `dotnet test HanabePhotoManager.sln -c Debug --no-build`：915 全绿（Core 373 / Infra 164 / App 378）。
- 截图 `.artifacts/capture-m3-filter3.ps1`：收起态 `m3-filter3-collapsed-light.png`（244 KB，1344×1258）+ 展开态 `m3-filter3-expanded-light.png`（239 KB，1344×1258），均 exit 0 非空白；两态像素差异显著（展开揭示高级区内容）。

### Notes
- 仅表现层改动；未改 ViewModel / Command / Binding / API / 数据流；`PreviewSortComboBox`（MinWidth=168）与显示方式（126）/ 面积（142）同处一行，工作区宽度足够，不折行。

---

## 2026-08-14 — 浏览页筛选面板二轮调整（修图/评分外置、显示方式/面积入折叠、文件类型删除）

### Task
按用户二轮要求（2026-08-14 最新）重排浏览页筛选面板：①「修图状态」「评分分类」从高级折叠区移到主区（与分类/排列方式同级）；②「显示方式」从面板下方始终可见收进高级折叠区；③「面积」（树图面积计算方式下拉，`TreemapWeightModes`）收进高级折叠区；④「文件类型」segment 直接移除。铁律：保留业务功能——VM/Command/Binding 不动（`ToggleFileTypeFilter` / `SetPreviewRetouchFilterCommand` / `RatingFilter` / `BrowseDisplayMode` / `TreemapWeightMode` 全部保留），仅改 UI 布局与可见性；全程语义 Token；动效 180ms 不变。

### 落地方式（`MainWindow.xaml`）
- **主区（第 2 行）**：`WrapPanel` 由「分类 / 文件类型 / 排列方式」改为「分类 / 修图状态 / 评分分类 / 排列方式」。修图状态 segment（`SetPreviewRetouchFilterCommand`，全部/已修/未修）与评分分类 combo（`RatingFilters`/`RatingFilter`）原样从高级区平移至主区，样式 `PreviewSegmentButton` / 绑定不变；文件类型 segment（全部/RAW/JPG/PNG/视频，`Click="FileTypeFilter_Click"`）整体删除（XAML 移除，VM `ToggleFileTypeFilter` 与 code-behind `FileTypeFilter_Click` 保留不删）。
- **高级折叠区**：`WrapPanel` 由「修图状态 / 评分分类」改为「显示方式 / 面积」。显示方式 combo（`BrowseDisplayModes`/`BrowseDisplayMode`，`x:Name="BrowseDisplayModeSelector"`）与面积 combo（`TreemapWeightModes`/`TreemapWeightMode`，`Visibility="{Binding IsTreemapBrowseMode, ...}"`）原样移入；智能识别引擎 / 智能类别 / 识别所选·识别当前范围·停止 DockPanel 不变。
- **移除常显「显示方式/面积」DockPanel**：原筛选面板下方独立的 `DockPanel.Dock="Top"`（显示方式 + 面积 + 右对齐「扫描过程中会持续更新矩形大小」提示）整段删除；该提示语（treemap 扫描提示，非筛选、非命令）迁移到高级折叠区末尾、右对齐、仍 `IsTreemapBrowseMode` 可见，未丢失信息。
- 高级折叠动画保持不变：`Border` + `ClipToBounds`，`MaxHeight` 0↔400 + `Opacity` 0↔1，`Motion.Duration.Normal`（180ms）+ `CubicEase EaseOut`；折叠态内容 `IsEnabled` 随展开态禁用。

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug /warnaserror`：0 警告 / 0 错误。
- `dotnet test HanabePhotoManager.sln -c Debug --no-build`：915 全绿（Core 373 / Infra 164 / App 378）。
- 截图 `.artifacts/capture-m3-filter2.ps1`：收起态 `m3-filter2-collapsed-light.png`（234 KB）+ 展开态 `m3-filter2-expanded-light.png`（222 KB），均 exit 0 非空白。

### Notes
- 仅表现层改动；未改 ViewModel / Command / Binding / API / 数据流；文件类型筛选命令保留（UI 不显示，未来如需可一行恢复）；「面积」实为树图面积计算方式（非图片分辨率），已按用户要求收进高级折叠区。

---

## 2026-08-14 — 浏览页筛选面板精简合并 + 高级折叠

### Task
按用户确认方案优化浏览页筛选面板：①搜索框合并（文件名 + 语义描述统一入口）；②高频项外置（分类 / 文件类型 / 排序）；③高级项折叠（修图状态 / 评分 / 智能识别 / 智能类别，默认收起 + 180ms M3 缓动 + 设置持久化）；④去掉「手动类别 / 自定义标签」两个批量区块。铁律：保留业务功能——搜索/筛选逻辑与命令不动，只改 UI 布局与触发方式；全程语义 Token；动效 180ms。

### 搜索合并（确认现状）
- 搜索合并已在 VM 层落地（`UnifiedSearchText` + `BrowseSearchMode` Auto/File/Semantic + `ApplyUnifiedSearch()`：Auto 下 `LooksLikeFileOrPath` 判定走文件名匹配，否则语义检索），本轮**未改业务逻辑**，仅把「智能搜索」卡片从筛选面板第二位提到第一位，作为统一入口；保留「智能搜索模式」下拉（智能/文件名或路径/语义描述）+ 取消 + 进度。

### 布局重排（`MainWindow.xaml`）
- 筛选面板新结构：第 1 行「智能搜索」→ 第 2 行高频「分类 Chips / 文件类型 segment / 排列方式 combo」+ 右侧「⚙ 高级筛选」折叠按钮（`Button.Disclosure`，chevron ▸/▾ 随 `IsAdvancedFiltersExpanded` 切换）→ 第 3 行高级折叠区（修图状态 / 评分分类 / 智能识别引擎 / 智能类别 / 识别所选·识别当前范围·停止）。
- 「显示方式」保留在原位置（筛选面板下方、始终可见，`IsTreemapBrowseMode` 下附带树图面积），满足「（+ 显示方式）留外面」且不因折叠失去显示方式切换入口。
- 高级折叠动画：`Border` + `ClipToBounds`，`MaxHeight`（0↔400）+ `Opacity`（0↔1）双通道 `DoubleAnimation`，`Motion.Duration.Normal`（180ms）+ `CubicEase EaseOut`；折叠态内容 `IsEnabled` 随展开态禁用，避免 tab 进入隐藏控件。

### 折叠状态持久化
- `AppSettings` 新增 `IsAdvancedFiltersExpanded`（默认 `false`）；`MainWindowViewModel` 新增 `IsAdvancedFiltersExpanded` 属性（`_isInitialized` 后 `SaveSettingsAsync`）+ `ToggleAdvancedFiltersCommand`；`SaveSettingsAsync` / 加载路径同步读写。

### 去掉手动类别 / 自定义标签区块
- 删除多选 Inspector 中「归入分类」（`AssignCategoryToSelectedCommand`）与「添加标签」（`AssignTagToSelectedCommand`）两个 ComboBox+Button 区块（用户 8/9 老需求）。VM 命令/属性保留未删（可能被其它路径复用，且铁律「命令不动」）。

### 截图基建
- `App.xaml.cs` + `MainWindow.xaml.cs`：新增 `--advanced-filters` 截图旗标（`App.AdvancedFiltersForScreenshot`），渲染前 `IsAdvancedFiltersExpanded = true`。
- `.artifacts/capture-m3-filter.ps1`：复用 `home-fix-fixture`，patch `LibraryRoot` + 强制 `IsAdvancedFiltersExpanded:false`，`Dynamic.Light` 下捕获收起态 `m3-filter-collapsed-light.png` 与展开态 `m3-filter-expanded-light.png`。

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug`：0 警告 / 0 错误。
- `dotnet test HanabePhotoManager.sln -c Debug --no-build`：915 全绿（Core 373 / Infra 164 / App 378）。
- 截图：收起/展开两张 exit 0、1344×1258、非空白，逐像素采样差异 13.6%（展开态含高级区内容）。

### Notes
- 仅表现层改动；未改 ViewModel / Command / Binding / API / 数据流；搜索/筛选/排序/评分/智能识别逻辑全部保留；`日期范围` 由左侧日历承担（已随浏览条件折叠），未在筛选面板重复新增。

---

## 2026-08-14 — M3-3 浏览页改版 + M3-4 其余页面适配

### Task
按 `docs/M3_DESIGN_FINAL.md` 变体 001 落地 M3-3（浏览页 Workspace 照片墙 + Chips 筛选 + Inspector 320px 右侧面板 + FAB）与 M3-4（其余页面 M3 排版/配色适配）。铁律：只改视觉不改逻辑，ViewModel/Command/Binding 不动，全程语义 Token；保留 treemap/justified-gallery 虚拟化与视口优先级逻辑不变。

### M3-3 浏览页改版（`MainWindow.xaml` + 3 个资源字典）
- **Workspace 外壳**：`BrowseUnifiedWorkspace` 由 `surface-subtle` 卡片（Radius.Card 12）改为 M3 大圆角容器——`Brush.Surface.ContainerLowest` + `Radius.Container` 28 + `Brush.OutlineVariant` 1px 描边；treemap 内嵌 `Card.Default` 去除（避免 Card 套 Card），改透明无边框。
- **照片网格视觉**：网格瓷砖沿用既有 `Radius.Card` 12 + 悬停 `Brush.Surface.Interactive` 状态层（已 Token 化）；保持 `UniformSquarePanel` + `ZoomableGridTileSize`（默认 150≈140px）缩放/滑块/Ctrl+滚轮逻辑不变（未动 VM/Binding）。
- **Chips 筛选栏**：分类 Chips（`PreviewCategoryFilters`）ControlTemplate 改 M3 Chip——未选 `surface-container-high` + `on-surface-variant`、选中/过滤激活 `primary-container` + `on-primary-container` + `primary` 描边、hover `surface-container-highest`、pressed `surface-container`；`PreviewSegmentButton`（修图状态 全部/已修/未修、文件类型 全部/RAW/JPG/PNG/视频）改 M3 tonal——未选透明、选中 `secondary-container` + `on-secondary-container` + `secondary` 描边、hover `surface-container-high`；两处 segment 容器 `surface-default` → `surface-container-high` + `outline-variant`。
- **Inspector 320px 右侧面板**：把浏览工作区**底部 dock** 的上下文检查器（单张 Inspector + 多选操作条）移到 `Grid.Row=1` 新增第三列（`Width=Auto`）的右侧 320px 面板（`Inspector.Panel`：`surface-container-low` + `Radius.Container` 28 + `outline-variant`）；单张 Inspector 由横向 3 栏 WrapPanel 改为纵向 info-row（label:value），操作（评分 1–5/标签 人像·风光·废片/打开/文件夹/复制路径/移入回收站）改 WrapPanel Chips 流式布局；多选操作条纵向堆叠（复制到…/移动到…/智能识别/归入分类/添加标签/移入回收站/清除选择）；新增无选中占位态（`InspectorEmptyState`：icon + 「未选择照片」+ 提示）。所有绑定/命令/点击处理器复用既有（`SelectedPreviewFile`/`SelectedFileMetadata`/`IsMultiSelection`/`CloseExifPanel_Click`/`Inspector_*` 等），未改 VM。
- **FAB**：新增 `Button.Fab`（56×56、`Radius.Full` 999、`primary` 背景 + `on-primary` 前景、hover/pressed 用 `accent.hover/pressed`、focus 环）+ `Icon.Plus` 几何；`BrowseImportFab` 叠加在 Workspace 右下角（`Grid` 包裹 DockPanel，FAB 后置上浮，`Margin="0,0,4,4"`），`Command=ShowImportCommand`（复用既有导入命令），位于 Statusbar 之上不遮挡（内容 Grid 与 Statusbar 是 DockPanel 兄弟）；「共 N 项」计数由右下改左下避免与 FAB 重叠。
- 新增 `Inspector.Panel` 样式（`Inspector.xaml`）。

### M3-4 其余页面适配
- 结论：人物/相册/导入/图片小工具/水印/地图/网盘/投稿/欣赏/设置页**均为 Token-first 构建**，已消费 `Card.Default`（Radius.Card 12）/`Button.*`/`Navigation.Segment`/`Status.*`/`Layout.*` 等共享组件，M3-1/M3-2 已把共享 Token 换成 M3 值，因此这些页面经共享 Token 自动对齐 M3 排版/配色，本轮**无硬编码颜色残留**（全仓 XAML 硬编码颜色仅剩 1 处：`MainWindow.xaml` 人脸参考裁剪用的 `OpacityMask` 白色视觉刷，属遮罩非着色）。设置页「配色方案 + 明暗」6 套主题切换 UI 已于 M3-1 落地，本轮复核确认全部页面经 `DynamicResource` 正确响应 6 套主题。

### 截图基建
- `App.xaml.cs` + `MainWindow.xaml.cs`：新增 `--browse-showcase` 截图旗标（`App.BrowseShowcaseForScreenshot`），渲染前 `BrowseDisplayMode = Grid` + `IsBrowseConditionsExpanded = true`，用于一次性捕获「Workspace 网格 + Chips + Inspector 320px + FAB」完整变体 001 排版。
- `.artifacts/capture-m3-final.ps1`：复用 `home-fix-fixture`（22 图），patch `LibraryRoot`，6 套主题捕获浏览页（`--page Preview --browse-showcase --select-first`）+ 11 个页面默认主题（`--page <Name>`）。

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug /warnaserror`：0 警告 / 0 错误。
- `dotnet test HanabePhotoManager.sln -c Debug --no-build`：915 全绿（Core 373 / Infra 164 / App 378）。
- 截图：浏览页 6 主题 `.artifacts/m3-browser-<scheme>-<mode>.png` + 11 页面 `.artifacts/m3-page-<name>-dynamic-light.png`，全部 exit 0 且非空白。

### Notes
- 仅表现层改动，未改 ViewModel / Command / Binding / API / 数据流；treemap/justified-gallery 虚拟化、视口优先级、缩放/平移/选择/筛选逻辑全部保留。

---

## 2026-08-14 — M3-1 主题基建 + M3-2 Shell 改版（大改方向定稿后）

### Task
按 `docs/M3_DESIGN_FINAL.md`（用户 2026-08-14 拍板，替代旧「克制桌面工具风」）落地 M3-1 主题基建与 M3-2 Shell 改版：3 配色（动态色彩/森林绿/紫罗兰）× 2 明暗 = 6 套主题，应用内可切换并持久化，默认动态色彩·浅色；排版按变体 001（Navigation Rail 88px + Topbar + Workspace + Inspector 320 + Statusbar + FAB）。仅视觉层，未改 ViewModel / Command / Binding / API / 数据流。

### M3-1 主题基建
- `src/HanabePhotoManager.App/Services/ThemeManager.cs`：`AppTheme`（明暗）保留；新增 `AppColorScheme { Dynamic, Forest, Violet }`（配色）；`Apply(AppTheme, AppColorScheme, persist)` 组合切换，主题入口 URI 变为 `Themes/Themes/{Scheme}.{Mode}.xaml`（6 套）；持久化偏好 `ui-theme.txt` 写 `"{Scheme}.{Mode}"`（旧格式 `Light/Dark` 回退为 Dynamic 配色）；`ThemeChanged`（`EventHandler<AppTheme>`）仅在明暗变化时触发（DWM 标题栏 / WebView2 只关心明暗，配色切换靠 `DynamicResource` 自动刷新）。
- 新增 6 个 `Themes/Colors/Colors.<Scheme>.<Mode>.xaml`：M3 tonal 色值（primary/secondary/tertiary + 各 container + surface 五层 container + on-* + outline/outline-variant），色值取自 `M3_DESIGN_FINAL.md` 第三节表；深色 on-* 与 surface-container-low/lowest/highest 按 M3 tonal 规则补全。既有语义 `Color.*` 键（`Color.Accent.*`→primary、`Color.Surface.Selected`→secondary-container、`Color.Text.*`→on-surface/on-surface-variant 等）只换值不换键，页面继续只消费语义 Token。
- `Themes/Colors/Brushes.Light.xaml` / `Brushes.Dark.xaml`：保留全部既有 `Brush.*` 键，新增 M3 语义 Brush（`Brush.Primary/OnPrimary/PrimaryContainer/OnPrimaryContainer`、`Secondary*`、`Tertiary*`、`Brush.Surface`、`Brush.Surface.ContainerLowest/Low/Container/ContainerHigh/Highest/Dim/Variant`、`Brush.OnSurface/OnSurfaceVariant`、`Brush.Outline/OutlineVariant`）；`Brush.Shell.*` 改不透明 M3 surface（去掉旧玻璃透明度）。
- 新增 6 个 `Themes/Themes/<Scheme>.<Mode>.xaml` 入口字典；删除旧 `Themes/Light.xaml`/`Dark.xaml` 与 `Colors.Light/Dark.xaml`；`App.xaml` 默认入口改 `Themes/Themes/Dynamic.Light.xaml`（默认动态色彩·浅色）。
- `SettingsCenterPage.xaml(.cs)`：外观页新增「配色方案」三按钮（动态色彩/森林绿/紫罗兰）+ 既有「浅色/深色」明暗按钮组合切换。
- `AppSettingsStore.cs` / `MainWindowViewModel.cs`：`NavigationDisplayMode` 默认 `Text` → `IconAndText`（Rail 图标+文字竖排为默认）。

### M3-2 Shell 改版
- `Themes/Tokens/Sizing.xaml`：新增 `Size.Rail.Width` = 88。
- `Themes/Tokens/Radius.xaml`：按 M3 更新——`Radius.Small` 6→8、`Radius.Control` 8→12、新增 `Radius.Container` 28、`Radius.Full` 999。
- `Themes/Controls/Layout.xaml`：`Layout.TopBar` 改大圆角 `surface-container-low` 容器；`Layout.StatusBar` 改 44px `surface-container` 大圆角；`Layout.Workspace` Padding 归零（页面宿主 Grid 自带 Margin）；`Layout.Shell` 背景经 `Brush.Shell.Background` 改 `surface-dim`。
- `Themes/Controls/Sidebar.xaml`：`Sidebar.Container` 由 232px 侧栏改 88px Navigation Rail（`surface-container-lowest` + `Radius.Container` 28 + 1px outline-variant 描边）。
- `Themes/Controls/Navigation.xaml`：新增 `Navigation.RailItem`（64×52 竖排按钮，M3 状态层 hover/pressed/focus）。
- `MainWindow.xaml`：Sidebar 232px → Rail 88px（列宽 100 = 8 外边距 + 88 Rail + 4 缝）；导航项改「图标 + 文字竖排」Rail 模板，选中态 = `secondary-container` 背景 + `primary-container` 圆 icon + `on-primary-container` 图标色 + `on-secondary-container` 文字色（180ms 淡入 / 150ms 淡出，保留 ↑↓ Cycle 键盘导航 + 拖拽排序）；logo 改 48×48 `primary-container` 圆角瓦片；页脚改竖排（主题切换 icon 按钮 + 设置 rail item + 忙碌圆点）；Topbar/Statusbar 改大圆角 M3 容器；页面宿主 Grid 加 `Margin="24,8,24,8"`。

### Tests
- `ThemeManagerTests`：新增 `ParseSchemePreference_UsesDynamicAsSafeDefault`（6 例）。
- `DesignSystemResourceTests`：`App_LoadsTheLightThemeEntryPoint` 改断言 `Dynamic.Light.xaml`；新增 `AllSixThemes_ExposeTheSameColorAndBrushKeys`。
- `ControlThemeTests` / `NavigationMotionTests`：4 个旧「克制侧栏/顶栏」结构断言更新为 M3 Rail/圆角容器语义（列宽 232→100、`Navigation.ReorderableItem`→`Navigation.RailItem`、`NavSelectionIndicator`→`NavIconSurface`、页脚 Icon.Theme 由 `Tag` 改 `Data`、Topbar 特殊 padding 移除改为圆角容器断言）。

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug /warnaserror`：0 警告 / 0 错误。
- `dotnet test HanabePhotoManager.sln -c Debug`：915 全绿（Core 373 / Infra 164 / App 378，+7 新增）。
- 6 套主题截图（`.artifacts/m3-theme-<scheme>-<mode>.png`，`capture-m3-themes.ps1` 生成 64 图 fixture 后 `--screenshot` 捕获）；均色验证：浅色 #D9D6DE（靛蓝偏蓝）/ #DADBD8（绿偏绿）/ #DCD6DD（紫偏品红），深色 #24242A / #242524 / #262428，三配色 × 明暗色相区分正确。

### Notes
- 保留全部业务功能：导航 10 项 + 设置全部保留（Rail 竖排）；背景图 / 玻璃强度 / 导航显示模式（文字/图标/图标和文字）设置仍可用（默认改 IconAndText）；FAB 与 Inspector 320px 属 M3-3（浏览页改版）范围，本轮未做。

---

## 2026-08-14 — UI/UX 70% Inspector + Contextual UI

### Task
完成 70% Inspector + Contextual UI 里程碑（`HERMES_MASTER_GUIDE.md` #70）：把 30% 建好的上下文检查器做完整，并补上下文操作与多选操作。复用既有 `PhotoDetailMetadataReader`（`PhotoViewerWindow` 已在用的结构化 EXIF/GPS 读取器）替换扁平 `ExifSummary` 字符串。仅表现层，未改业务逻辑 / 命令 / 绑定 / API / 数据流，全程 Design System Token。

### Files Changed
- `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`：
  - 删除扁平 `ExifSummary` 字符串 + `ReadExifCore`（MetadataExtractor 逐目录 dump），改为结构化 `SelectedFileMetadata`（`PhotoDetailMetadata`），经既有 `IPhotoDetailMetadataReader`（`new PhotoDetailMetadataReader()`）读取——与 `PhotoViewerWindow` 共用同一读取器，不建第二套。保留 `_exifCts` 取消 + Latest-Request-Wins 语义。
  - 新增 `SelectedFileCount`（已选数量）与 `IsMultiSelection`（`HasSelectedFiles && SelectedPreviewFile is null`）只读属性；`SelectedPreviewFile` setter 与 `NotifyPreviewSelectionChanged` 增发这两项，驱动检查器/多选条正确切换。
- `src/HanabePhotoManager.App/Themes/Controls/Inspector.xaml`：新增 `Inspector.FieldLabel` / `Inspector.FieldValue`（label:value 行排版，纯 Token）。
- `src/HanabePhotoManager.App/MainWindow.xaml`：把单行 EXIF 条替换为完整上下文检查器（浏览工作区底部 dock）：
  - **单张 Inspector**（绑定 `SelectedPreviewFile`）：头部（文件名 + 关闭）、文件信息（类型/大小/分辨率/分类）、拍摄参数（相机/镜头/ISO/光圈/快门/焦距）、时间与位置（拍摄时间/位置）+ 内联上下文操作（评分 1–5、标签 人像/风光/废片、打开/文件夹/复制路径/移入回收站）——全部复用既有 code-behind 处理器/命令。
  - **多选操作条**（绑定 `IsMultiSelection`）：`已选择 N 张照片` + 批量操作（复制到…/移动到…/智能识别/批量归入分类/批量添加标签/移入回收站/清除选择），复用既有 `BatchCopyFilesTo`/`BatchMoveFilesTo`/`AnalyzeSelectedPhotosCommand`/`AssignCategoryToSelectedCommand`/`AssignTagToSelectedCommand`/`DeleteSelectedFilesCommand`。
- `src/HanabePhotoManager.App/MainWindow.xaml.cs`：新增 `Inspector_Open/OpenFolder/CopyPath/Delete/ClearSelection` 与 `Inspector_BatchCopy/BatchMove` 处理器；抽取共享 `BatchCopySelected()`/`BatchMoveSelected()` 供右键菜单与 Inspector 按钮复用。
- `src/HanabePhotoManager.App/App.xaml.cs` + `MainWindow.xaml.cs`：新增 `--select-first` 截图旗标（`App.SelectFirstForScreenshot`），渲染前选中第一张照片，用于无头捕获 Inspector。
- `.artifacts/capture-inspector.ps1`：新增截图脚本（复用 `home-fix-fixture`，patch `LibraryRoot`，`--screenshot --select-first` 跑 Light/Dark）。

### Context Action / Multi-select 清单
- Context Action（单张）：打开 / 在资源管理器打开 / 复制路径 / 移入回收站 / 评分 0–5 / 标签（人像/风光/废片/清除）——**命令与 code-behind 行为均已有，本轮补 Inspector 内联 UI**；右键菜单原有条目未动。
- Multi-select（多选）：批量删除（`DeleteSelectedFilesCommand`）/ 批量复制（`BatchCopyFilesTo`）/ 批量移动（`BatchMoveFilesTo`）/ 智能识别（`AnalyzeSelectedPhotosCommand`）/ 批量归入分类（`AssignCategoryToSelectedCommand`）/ 批量添加标签（`AssignTagToSelectedCommand`）/ 清除选择——**全部为已有 VM 命令，本轮补多选操作条 UI**（此前仅「删除选中」按钮 + 右键菜单批量项）。

### Verification
- ⚠️ **环境受限**：本会话进行到验证阶段时，subprocess 执行（pwsh / grep / glob）持续以 `STATUS_DLL_INIT_FAILED`（exit code 3221225794）失败，`Write-Output "ping"` 亦无法执行，因此 **`dotnet build` / `dotnet test` / 截图脚本均未实际运行**。代码改动已完成并逐处 self-review（绑定/命令/样式/处理器齐全、命名空间与类型对齐），但构建、测试、截图需在子进程恢复后补跑。
- 待补跑命令：`dotnet build HanabePhotoManager.sln -c Debug /warnaserror`；`dotnet test HanabePhotoManager.sln -c Debug --no-build`；`pwsh .artifacts/capture-inspector.ps1`。

### Notes
- 仅表现层：结构化元数据读取复用既有 `PhotoDetailMetadataReader`（非新建第二套读取器）；新增绑定均为只读展示或既有命令，未改业务逻辑 / 数据流。
- 数据不足时 `PhotoDetailMetadataReader` 返回「未记录」占位，符合「数据不足用占位，不臆造」。

---

## 2026-08-14 — UI/UX 50% Home Mid Review P1 修复

### Task
修复中期评审（`C:\Users\fulia\wxdecrypt\hanabephoto_midreview.md`，aurora gpt-5-6）在 50% Home 里程碑提出的 6 项 P1（P1-1 信息架构 / P1-2 缩略图自适应 / P1-3 缩略图媒体表达 / P1-4 Dark 选中态 / P1-5 标题栏 / P1-6 快速入口降级）。不碰 60% 浏览模块；不回滚 60% 已修的首屏缩略图 P1。

### Files Changed
- `src/HanabePhotoManager.App/Themes/Colors/Colors.Dark.xaml`（P1-4）：`Color.Surface.Selected` #343A3E → #485058，增强 Dark 下 Sidebar「主页」选中 pill 与侧栏背景的对比度（~1.4 → ~2.0），`NavSelectionIndicator`（3px `Brush.Accent.Default`）随之更清晰。纯 Token，未写死颜色。
- `src/HanabePhotoManager.App/Themes/Tokens/Icons.xaml`（P1-3）：新增共享 `Icon.Play`（填充三角形）供视频播放指示复用（Light/Dark 同名同键）。
- `src/HanabePhotoManager.App/Themes/Controls/Layout.xaml`（P1-1）：`Layout.HomeSummary` 由大 Summary Card（三列统计 + `Shadow.Emphasis` 阴影 + `Brush.Shell.Summary` 背景）改为轻量状态行（透明背景 + 底部细分隔，紧凑 Padding）。
- `src/HanabePhotoManager.App/MainWindow.xaml`（P1-1/2/3/6）：
  - Home 区块重排：`Layout.HomeSummary` 轻量状态行（`已连接照片库 · N 日期 · N 媒体文件` + 库路径，右对齐截断）→「最近照片」缩略图主视觉 →「快速操作」Compact Toolbar →「当前连接设备」沉底。
  - 缩略图区：移除内层 `ScrollViewer MaxHeight=188` 与固定 104×132 瓷砖；改 140×134 瓷砖 + `WrapPanel` 自适应（TileMinWidth≈140px）；标题栏右侧显示 `HomePreviewFiles.Count` 张数。
  - 视频媒体表达：`MP4`/`MOV`（`DataTrigger Binding=Extension`）叠加居中播放指示（`Icon.Play` + `Brush.Overlay.Scrim` 圆底 + `Brush.Viewer.Text`）与右下角扩展名角标；缩略图 `ImageBrush` 优先、失败回退统一 placeholder（扩展名占位）。
  - 快速入口：删除 2×3 `UniformGrid` 卡片 + 页面级 `HomeQuickEntry` 样式；改为横向 `WrapPanel` 的 7 个 `Button.Toolbar`（图标 + 标签）紧凑按钮，7 个 `Show*Command` 全部保留。
- `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`（P1-3 根因）：`CurrentPage` setter 增加 `else if (IsHomePage) StartPreviewThumbnailLoading(HomePreviewFiles)`——修复「应用默认启动在 Preview，导航到 Home 时缩略图从未加载」导致首页全是灰占位的问题（评审 P1-3「缩略图加载优先」的根因）。最小行为修复，未改数据模型。
- `src/HanabePhotoManager.App/MainWindow.xaml.cs`（P1-5）：新增 `DwmSetWindowAttribute` P/Invoke + `ApplyTitleBarTheme()`，`DWMWA_USE_IMMERSIVE_DARK_MODE`（20，回退 19）跟随 `ThemeManager.Current` 设置系统标题栏深/浅；`MainWindow_Loaded` 应用一次并订阅 `ThemeManager.ThemeChanged`；失败静默降级。
- `src/HanabePhotoManager.App/App.xaml.cs`：`--screenshot` 模式扩展可选的 `--page <Name>` 参数（`App.ScreenshotPage`），供无头截图工作流导航到非默认页（Home）。
- `tests/HanabePhotoManager.App.Tests/HomeP1FixTests.cs`（+5 回归）：source-level 断言——Home 缩略图区自适应 WrapPanel + 视频 MP4/MOV 角标 + `Icon.Play`、快速入口无 `HomeQuickEntry`/无 `UniformGrid Columns=3`、`Layout.HomeSummary` 无阴影、`CurrentPage` setter 将 Home 路由到 `StartPreviewThumbnailLoading(HomePreviewFiles)`。

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug /warnaserror`：0 警告 / 0 错误。
- `dotnet test HanabePhotoManager.sln -c Debug --no-build`：Core 373 / Infrastructure 164 / App 371（908 total，全绿；+5 `HomeP1FixTests`）。
- 运行时：`--screenshot --page Home` + JPG/MP4 fixture（18 JPG + 4 ffmpeg MP4）捕获 Home 浅/深截图 `.artifacts/home-fix-50-light.png` / `home-fix-50-dark.png`（1344×1258）。期间修复一处自引入的 `Rectangle.RadiusX="{StaticResource Radius.Control}"`（CornerRadius→double 类型错配）导致布局栈溢出崩溃，改为依赖父 `Border` 的 `CornerRadius`+`ClipToBounds` 裁切。

### Notes
- 仅视觉/表现层改动（P1-1/2/4/6 纯 XAML+Token；P1-3 视觉 + 一处最小缩略图加载行为修复；P1-5 code-behind 调 DWM），未改业务逻辑、命令、绑定、API 或数据流。
- **Duration Badge 明确延后**：`PreviewFileViewModel` 只有文件字节长度 `Length`，无视频时长；读取时长需 MediaFoundation/Shell 元数据（数据流改动），超出「仅视觉层、不动数据/绑定」约束，记为技术债（见 `docs/current-status.md` P1-3 行）。
- 快速入口「5 高频 + 更多▾折叠」：实际数据源为 XAML 硬编码 7 个 `Show*Command`（无 QuickActions 集合），故按实际数量 7 全部保留为紧凑 Toolbar，未折叠（不丢任何入口）。
- 60% 浏览模块（PhotoTreemapControl / `_treemapSourceFiles` 播种）未触碰。

---

## 2026-08-13 — UI/UX 60% Primary Gallery / Main Content

### Task
Complete the 60% Primary Gallery milestone (`HERMES_MASTER_GUIDE.md` #69): stabilize the primary media-browsing module (照片墙 TreemapBrowser / PreviewPage / Browse) at large-library scale (6,217+ scroll, 11,739-item all-library wall). Acceptance: Virtualization, Thumbnail, Viewport priority, Filter, Selection, Zoom/Pan, Scroll, Hover, Performance, Race condition. Presentation/rendering-only — no ViewModel command/binding/API/data-flow changes, Design System tokens only.

### Files Changed
- `src/HanabePhotoManager.App/Browsing/Treemap/PhotoTreemapControl.cs`:
  - **Viewport-range culling (Virtualization + Scroll + Viewport priority):** replaced the per-frame full `for` walk of every justified item with `VisibleRowRange(...)`, a binary search over the Y-monotonic justified rows, so `OnRender` now touches only rows intersecting the (padded) viewport instead of all 11,739 items on every scroll frame. `DrawRoot`, `DrawSubtreeWithJustifiedLayout`, and `DrawPanorama` all use it; the `IntersectsViewport` check is retained as a cheap X-dimension guard.
  - **Layout memoization (Performance + 10k+ layout time):** added `EnsureLayoutCache` + derived-group getters (`RootCategories`, `ChildrenOf`) and justified-layout getters (`GetSubtreeLayout`, `GetCategoryLayout`). Root categories, per-category children, and justified layouts are now computed once per (ItemsSource identity + RootKey + width) and reused across scroll frames — eliminating O(n) re-walks, re-sorts, and ~n allocations per frame.
  - **Hover feedback (tokenized):** added `OnMouseMove`/`OnMouseLeave` + a hovered-key guard, and `DrawTile` now renders hover with `Brush.Surface.Interactive` fill + `Brush.Border.Strong` border (borderless tiles get a 1.5px `Border.Strong` outline); selection (`Brush.Border.Focus`) still takes precedence.
- `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`: prebuilt the `FullPath → PreviewFileViewModel` lookup once in `StartTreemapThumbnailLoading` (`_treemapSourceLookup`) and reused it in `RefreshTreemapViewportLoading` and `DrainTreemapThumbnailQueue`, removing an O(11,739) dictionary rebuild on every debounce tick and batch drain. **Fixed a P1 gap:** the incremental all-library / date scan populates the treemap via `ApplyBatch` and never routes through `RepopulateTreemapFrom`, so `_treemapSourceFiles` was never seeded and the initial photo wall loaded no thumbnails. `RefreshFilteredCache` now seeds the source (and raises `TreemapRepopulated`) once the filtered cache is complete, and `RefreshTreemapViewportLoading` lazily re-seeds via `EnsureTreemapSourceLookup` when the lookup is missing or stale.
- `tests/HanabePhotoManager.Core.Tests/Browsing/Treemap/JustifiedGalleryLayoutTests.cs` (+3): asserted justified rows are Y-monotonic (binary-search precondition), layout is index-aligned with input (renderer mapping), and an 11,739-item layout completes in bounded time.
- `tests/HanabePhotoManager.App.Tests/Browsing/Treemap/PhotoTreemapControlTests.cs` (+1): source-level regression asserting the viewport-range culling, layout/group memoization, and hover path are present.

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug /warnaserror`: 0 warnings / 0 errors.
- `dotnet test HanabePhotoManager.sln -c Debug`: Core 373 / Infrastructure 164 / App 366 (903 total, all green; +5 new tests).
- Runtime: launched the app against the real `\\Hanabe\拍照` UNC library and captured the Browse treemap wall in Light and Dark (`.artifacts/gallery-60-light.png`, `.artifacts/gallery-60-dark.png`).

### Race-condition audit (§40 Latest Request Wins) — results
| Path | Result |
|------|--------|
| Date switching (`SelectDateAsync`) | Pass. `_dateLoadCancellation` + `_dateLoadGeneration` guard; stale progress callbacks and continuations are dropped. |
| Semantic search (`SemanticSearchViewModel`) | Pass. `_operationCancellation` is cancelled on every new query; `DebouncedSearchAsync` coalesces keystrokes and only the latest request publishes. |
| Treemap thumbnail queue | Pass. `_treemapLoadGeneration` generation counter + `_treemapLoadActive` single-flight guard; stale `ContinueWith` cannot re-drain after `CancelPreviewThumbnailLoading`. |
| Filter setters (`RefreshFilteredCache`) | Pass. Synchronous on the UI thread — no interleaving gap; `RequiresTreemapRepopulation()` gates the treemap rebuild. |

### Notes
- Rendering/performance only: no business logic, bindings, commands, or data-flow changes. Layout output is byte-for-byte identical to before (same `_galleryLayout.Arrange` inputs/order); only the per-frame recomputation and off-screen iteration were removed.
- Hover uses only `Brush.Surface.Interactive` / `Brush.Border.Strong` tokens and does not affect selection or keyboard behavior.
- The 30% → 50% accumulated workspace changes are preserved and left uncommitted alongside this milestone.

---

## 2026-08-13 — UI/UX 50% Home + Mandatory Mid Review

### Task
Complete the 50% Home + Mandatory Mid Review milestone (`HERMES_MASTER_GUIDE.md` #68): bring the Home page to the new design direction (MD3 × Codex Desktop × Lightroom — high information density, image-first, restrained motion), add clear quick entries, and prepare the Mid Review Context Package. Presentation-only, Design System tokens only, no business-logic changes.

### Files Changed
- `MainWindow.xaml` — rebuilt the Home page (`HomePage` ScrollViewer):
  - Hero summary (`Layout.HomeSummary`) kept as a 3-column stat band; the `24`/`30` raw font sizes are now `Typography.Title` / `Typography.Display` tokens.
  - Added a new **快速入口 (Quick Entries)** section: a 3-column `UniformGrid` of 7 `HomeQuickEntry` cards (icon + title + one-line description) reusing the existing `ShowImportCommand` / `ShowPreviewCommand` / `ShowCustomAlbumsCommand` / `ShowFaceSearchCommand` / `ShowMapPhotosCommand` / `ShowCompressionCommand` / `ShowCloudCommand`, matching the sidebar navigation. Icons reuse the shared `Icon.*` geometry tokens with `Brush.Accent.Subtle` tile + `Brush.Accent.Default` stroke.
  - Tokenized the **实时扫描缩略图** grid: card/thumbnail radius → `Radius.Card` / `Radius.Control`, extension/name font sizes → `Typography.Body` / `Typography.Caption`.
  - Tokenized the **设备** section and `DeviceCardButton` template: all raw `CornerRadius` (`22/19/18/16/13/9`) → `Radius.Card` / `Radius.Control`, all raw font sizes (`22/20/18/17/16/15/12/11`) → `Typography.Title` / `TitleSmall` / `Label` / `BodySmall` / `Caption`.
  - Removed the deprecated bottom `UniformGrid` (「去导入 / 去预览 / 打开当前文件夹」): the first two are superseded by the quick entries (same commands); 「打开当前文件夹」 was a no-op on Home (no selected date → `OpenSelectedDateCommand` permanently disabled), and the command itself is retained in the ViewModel.
  - Added a page-local keyed style `HomeQuickEntry` (BasedOn `Button.Secondary`, left-aligned).

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug /warnaserror`: 0 warnings / 0 errors.
- `dotnet test HanabePhotoManager.sln -c Debug --no-build`: Core 370 / Infrastructure 164 / App 364 (898 total, all green).
- Runtime: captured Home-page window screenshots in Light and Dark (`.artifacts/home-50-light.png`, `.artifacts/home-50-dark.png`, 1343×1258).
- Prepared the Mid Review Context Package at `.artifacts/mid-review-package/` (context-package.md + screenshots + design-system summary).

### Notes
- Presentation-only: no ViewModel / Command / Binding / API / data-flow changes (`OpenSelectedDateCommand` remains defined; its Home-page XAML binding was removed as a no-op cleanup).
- No new animations: Home reuses the existing Motion.Normal 180ms page transition and 150/180ms sidebar selection.
- Mid Review itself is not performed here — the parent session arranges ChatGPT Desktop review using the prepared package.

---

## 2026-08-13 — UI/UX 40% Navigation + Motion

### Task
Complete the 40% Navigation + Motion milestone (`HERMES_MASTER_GUIDE.md` #67): Navigation, Sidebar, Workspace switch, keyboard behavior, base animation, plus a Navigation Bug Hunt (§39) — using only Design System tokens and preserving all business behavior.

### Files Changed
- `MainWindow.xaml` — sidebar `NavigationItem` now renders a selected state (`NavSelectionSurface` tonal overlay + `NavSelectionIndicator` accent bar) driven by `Key == CurrentPage` via the existing `CategoryEqualityMultiConverter`; selection fades in at `Motion.Duration.Normal` (180ms) and out at `Motion.Duration.Fast` (150ms). Added `KeyboardNavigation.DirectionalNavigation="Cycle"` / `TabNavigation="Once"` to the primary nav list, and a selected state for the footer「设置」item. Added `x:Name="SettingsCenterPageHost"` to the real settings page.
- `MainWindow.xaml.cs` — rewrote `AnimateVisiblePage` to resolve all 12 destinations explicitly (fixing CustomAlbums/Watermark/ContestOpen/ContestJudged falling through to HomePage, and Settings animating the deprecated collapsed ScrollViewer instead of `SettingsCenterPageHost`); switched to interruptible `BeginAnimation` (SnapshotAndReplace) with a fresh `TranslateTransform`. Added `Ctrl+F` → `FocusBrowseSearch()` (expands browse conditions and focuses/selects the smart-search box).
- `tests/HanabePhotoManager.App.Tests/NavigationMotionTests.cs` (new) — regression coverage for complete page-host mapping, sidebar selected-state/KeyboardNavigation markers, `Ctrl+F` wiring, 20-round rapid page switching (exactly one active page), and child-ViewModel identity stability across switching.

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug /warnaserror`: 0 warnings / 0 errors.
- `dotnet test HanabePhotoManager.sln -c Debug`: Core 370 / Infrastructure 164 / App 364 (898 total, all green; +5 new tests).
- Runtime: launched the app in Light and Dark themes and captured window screenshots (`.artifacts/nav-motion-40-light.png`, `.artifacts/nav-motion-40-dark.png`); pixel-sampled shell regions confirm both themes render with distinct surfaces.

### Navigation Bug Hunt (§39) — results
| Check | Result |
|-------|--------|
| 页面错乱 | Fixed + verified. `AnimateVisiblePage` mapping was incomplete (4 destinations fell through to HomePage; Settings targeted a dead ScrollViewer). Now all 12 resolve to their host; regression test asserts exactly one `Is*Page` active per switch. |
| ViewModel 重复创建 | Pass. All child VMs are created once in the constructor; navigation is a Visibility toggle (no re-instantiation). Test asserts reference identity across 50 switch rounds. |
| 旧状态覆盖新状态 | Pass (unchanged). `_sessionBrowseSnapshot` capture on leaving Preview + `_browseStatePolicy.ResolveOnEntry` + `_previewScanVersion` generation guard provide latest-wins semantics. |
| Animation 卡死 | Pass (hardened). Page transition now uses interruptible `BeginAnimation` with a fresh transform per call instead of accumulating `Storyboard.Begin()` clocks. |
| Loading 重复 | Pass (unchanged). `ShowPreviewAsync` guards `_previewHasLoaded || IsBusy`; thumbnail loading is cancelled on page exit. |
| UI 状态丢失 | Pass (unchanged). Pages are persistent (never destroyed), so scroll/filter/selection state survives switching; browse snapshot adds cross-launch restore. |
| 内存持续上涨 | Pass. No per-navigation allocation added; `BeginAnimation` replaces rather than accumulates animation clocks. |

### Notes
- Presentation-only: no ViewModel / Command / Binding / API / data-flow changes.
- The deprecated collapsed `SettingsPage` ScrollViewer remains in XAML untouched (preserve-existing-behavior); it is simply no longer the animation target.

---

## 2026-08-11 — UI/UX 30% App Shell (Unified Shell)

### Task
Complete the 30% App Shell milestone (`HERMES_MASTER_GUIDE.md` #66): Unified Shell, Navigation container, Top area, Workspace, Inspector container, and Status/background task area — using only Design System tokens and preserving all business behavior.

### Files Changed
- `Themes/Controls/Inspector.xaml` (new) — `Inspector.Container` / `Inspector.Header` / `Inspector.SectionLabel` component styles (token-driven, `BasedOn Card.Default`).
- `Themes/Themes/Light.xaml`, `Themes/Themes/Dark.xaml` — merged the new Inspector dictionary into both themes.
- `MainWindow.xaml` — formalized the browse-page EXIF metadata panel as the contextual `Inspector.Container` (with `Inspector.Header` + `Typography.FontFamily.Mono`); removed the page-level implicit Button scale-hover animation and tokenized its radius (`Radius.Control`) while adding a keyboard focus ring; aligned `PreviewItemContainer` reveal to `Motion.Duration.Normal` + 8px translate.
- `MainWindow.xaml.cs` — page-switch transition now cross-fades with a 6px translate at 180ms (was 240/280ms + 18px), matching the Motion Normal token.

### Verification
- `dotnet build HanabePhotoManager.sln -c Debug /warnaserror`: 0 warnings / 0 errors.
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror`: 0 warnings / 0 errors.
- `dotnet test HanabePhotoManager.sln -c Debug --no-build`: Core 370 / Infrastructure 164 / App 359 (893 total, all green).
- Runtime: launched the app, captured the `Hanabe Photo Manager Alpha` window screenshot (`.artifacts/appshell-30.png`); pixel-sampled shell regions confirm sidebar / top bar / workspace / status bar render distinctly.

### Notes
- Presentation-only: no ViewModel / Command / Binding / API / data-flow changes.

---

## 2026-08-09 — Browse smart search and network-library startup memory

- Replaced the separate browse file and semantic search inputs with one tokenized smart-search control. It supports automatic file/path detection plus explicit file/path and semantic modes.
- Removed the manual-category and custom-tag assignment controls from the browse conditions surface; metadata services and existing data remain intact.
- Semantic indexing now persists and reports every 100 photos. The browse result set is refreshed after each persisted batch while indexing continues, with a single in-flight incremental query to prevent search fan-out.
- Avoided rebuilding an already-complete all-library treemap at UNC startup. The existing streaming snapshot is reused until an actual browse filter requires a filtered tree, preventing a redundant all-file dimension pass.
- Added regression coverage for unified browse search UI and progressive semantic result publication.

## 2026-08-09 - Semantic search integrated into Photo Library

- Moved natural-language semantic search into the Photo Library browse conditions and removed the standalone sidebar destination/page host.
- The first non-empty query now runs the existing `ClipSemanticSearchService.EnsureIndexAsync` on background work, reports progress, supports cancellation, and then searches without blocking the WPF calling thread.
- Added `SemanticBrowseRanking` so CLIP-ranked paths are intersected with all existing browse predicates and shown through the existing grid/treemap, viewer, and navigation flows.
- Clearing the description or resetting browse conditions restores the ordinary photo wall; all-date treemap state is repopulated for both semantic activation and clearing.
- Reused shared `Card.Subtle`, `Input.TextBox`, `Button.Ghost`, text brushes, and the existing progress bar contract; no page-local colors or control templates were added.
- Added TDD coverage for automatic indexing order, non-blocking dispatch, result publication/clearing, semantic intersection/ranking, inline XAML contracts, reset behavior, and removal of the navigation item.
- Fixed the published CLIP runtime contract by declaring `System.Numerics.Tensors` 9.0.0 in Infrastructure; the prior output contained an undeclared DLL that the .NET host would not resolve during ONNX inference.
- Verification: Release solution build 0 warnings/0 errors; final tests Core 369, Infrastructure 163, App 349 (881 total). A self-contained installed build completed a real one-photo background index and reported “已按语义相关度排序”。

## 2026-08-09 - All-library treemap startup CPU saturation

- Diagnosed a 13,907-item startup loop in which 64-item scan batches and 32-item dimension batches repeatedly rebuilt the complete immutable treemap; panorama redraws also recalculated the all-photo layout for an unchanged snapshot.
- Added `ProgressiveTreemapViewModel.IncrementalPublicationItemThreshold` (1,024): publish the first batch, each threshold crossing, and final completion.
- Batched dimension submission to the same threshold and cached panorama item and layout snapshots in `PhotoTreemapControl`.
- Added regression coverage that small scan batches do not trigger a full rebuild.
- Verification: Release solution build 0 warnings/0 errors; tests Core 369, Infrastructure 162, App 342; self-contained win-x64 published to the standard install directory and passed a 30-second CPU responsiveness smoke test.

## 2026-08-09 — Import multi-select, progress, and batch dedupe

### Summary

- Added a dedicated Ctrl/Shift multi-file source picker (`OpenFileDialog.Multiselect = true`) while retaining the existing multi-path drag-and-drop route.
- Added an import-only progress surface with x/N, percentage, progress bar, cancellation, and the existing success/skipped/failed report summary.
- Kept the size-prefilter plus SHA-256 duplicate check, but moved it into a batch preflight and added one unified choice: skip all, import all, or decide each duplicate.
- Isolated all new import feature behavior in focused Core/App files and a `MainWindowViewModel.Import.cs` partial; no new file exceeds 600 lines.

### Regression root cause

`SourceAutoImportDropTarget_Drop` has accepted all paths in the WPF `FileDrop` array since `5fbfdf1`. The selectable-source route is folder-only: `BrowseSourceAsync` invokes `FolderBrowserDialog`, so it cannot return Ctrl/Shift-selected files. Git history contains no import-source file picker implementation; the folder-only source selection is the effective UI regression, not a SHA-256 failure.

### Verification

- `dotnet build HanabePhotoManager.sln -c Release /warnaserror --artifacts-path .artifacts/agent-verification`: 0 warnings, 0 errors.
- `dotnet test HanabePhotoManager.sln -c Release --no-build --artifacts-path .artifacts/agent-verification`: Core 369/369, Infrastructure 162/162, App 341/341.

### Manual follow-up

- WPF interactive smoke test remains: select 3+ disposable media files, confirm Ctrl/Shift selection, observe progress and cancellation, then re-import an exact duplicate and exercise each batch decision.
## 2026-08-09 - Codex (Compression asynchronous input scan and single-instance guard)

### Task
Prevent the Compression page from blocking the WPF UI while recursively scanning a large network folder, and prevent concurrent application instances from competing for I/O.

### Files Changed
- `CompressionViewModel.cs`: replaced synchronous `AddInputs` with cancellable `AddInputsAsync`. Directory discovery and file-length metadata reads run in `Task.Run`; UI-bound collections update only after the awaited operation resumes on the WPF context. A newer selection cancels an older scan without allowing the older completion handler to clear the newer scan state.
- `CompressionPage.xaml(.cs)`: file, folder, and drop input handlers await the asynchronous scan; progress becomes indeterminate while scanning and the existing Cancel button cancels scanning or compression.
- `App.xaml.cs`: owns the named mutex `HanabePhotoManager.SingleInstance` from startup through application exit. A second launch receives an information dialog and shuts down before loading application services. `OnExit` now releases the mutex only when this process created and owns it, preventing the second instance from throwing `ApplicationException` during shutdown.
- `CompressionViewModelTests.cs`: added async queue and cancellation regression coverage.

### Verification
- Focused compression ViewModel tests: 4/4 passed.
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror --artifacts-path .artifacts\\agent-verification`: 0 warnings, 0 errors (2026-08-09 05:21 +08:00).
- `dotnet test HanabePhotoManager.sln -c Release --no-build --artifacts-path .artifacts\\agent-verification`: Core 365/365, Infrastructure 160/160, App 336/336.

### Remaining Issues
- Manual WPF validation with the real large SMB library remains required. The current user process was intentionally not stopped or inspected interactively; it continues to run its previously loaded executable until restarted.

## 2026-08-08 - Codex (Treemap UI-hang mitigation)

### Task
Mitigate UI stalls while scanning and viewing a large treemap without stopping the user-running application.

### Files Changed
- `ProgressiveTreemapViewModel.cs`: thumbnail arrivals and background image-dimension batches now share the existing 150ms coalesced publication path. The first scan batch, navigation, weight changes, and completion keep their immediate publication behavior; zero-delay test ViewModels remain synchronous.
- `PhotoTreemapControl.cs`: only viewport-intersecting tiles create hit regions or draw/request thumbnails. Disabled debug telemetry no longer enumerates every treemap item on each render.
- `MainWindow.xaml.cs`: parent lookup now supports both visual and content elements, preventing `VisualTreeHelper.GetParent` from throwing for `Run`.
- `App.xaml.cs`: dispatcher and AppDomain unhandled-exception logging and user notification added.
- Treemap App tests: added coalesced thumbnail-publication and viewport intersection coverage.

### Verification
- Focused treemap tests: 19/19 passed.
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror --artifacts-path .artifacts\\agent-verification`: 0 warnings, 0 errors (2026-08-08 20:33 +08:00).
- `dotnet test HanabePhotoManager.sln -c Release --no-build --artifacts-path .artifacts\\agent-verification`: Core 365/365, Infrastructure 160/160, App 335/335.

### Remaining Issues
- Manual WPF validation on the 11,741-item SMB library remains required before KI-07 can be marked resolved. WebView2 initialization was already guarded by the existing `MapPage_Loaded` try/catch and retry path; no running user process was stopped.

## 2026-08-08 - Codex (Startup all-library treemap)

### Task
Open the application directly on the Browse page in Space Treemap mode and load the complete scanned library without requiring a date selection, display-mode switch, or other manual action.

### Files Changed
- `MainWindowViewModel.cs`: defaults to the Browse page and Treemap mode; initialization clears persisted date/category/file-type/rating/search/retouch/smart-category filters before the existing asynchronous root scan begins. The root scan continues to stream batches to `TreemapBrowser`, while dimension and thumbnail work remain background/viewport-driven. Added a root-path guard so filtering a new ViewModel cannot start a treemap scan with an empty path.
- `AppSettingsStore.cs`: new settings default to `Treemap`.
- `BrowseTreemapIntegrationTests.cs`: verifies startup defaults, neutral all-library initialization contract, settings default, and no-root filtering boundary.

### Verification
- `dotnet test tests\HanabePhotoManager.App.Tests\HanabePhotoManager.App.Tests.csproj -c Release --filter FullyQualifiedName~BrowseTreemapIntegrationTests --artifacts-path .artifacts\agent-verification`: 9/9 passed.
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror --artifacts-path .artifacts\agent-verification`: 0 warnings, 0 errors (2026-08-08 19:20 +08:00).
- `dotnet test HanabePhotoManager.sln -c Release --no-build --artifacts-path .artifacts\agent-verification`: Core 365/365, Infrastructure 160/160, App 333/333.

### Remaining Issues
- Manual WPF QA with the real 11,741-item library remains required for startup responsiveness and the existing treemap issues KI-01, KI-03, KI-07, and KI-14. No user-running process was stopped; the isolated artifacts path avoided locked default Release DLLs.

## 2026-08-08 - Codex (Treemap semantic panorama)

### Task
Implement the lowest semantic zoom as an Apple Photos-style panorama without changing import, duplicate detection, retouched-output protection, or normal Justified Gallery behavior.

### Files Changed
- Added `PanoramaPhotoLayout.cs` and tests: every current-directory photo is arranged in a dense 1px-gap justified wall at `TreemapZoom <= 0.20`; logical canvas dimensions are inverse to zoom, preserving 32px rendered tile height (24px constructor floor).
- `PhotoTreemapControl.cs`: bound zoom scale, panorama/tree switch, no root `Take(80)` sample, weak panorama chrome, and retained visible-rect-only rendering/loading.
- `MainWindow.xaml` / `.xaml.cs`: scale binding, viewport-sized panorama extent, and scaled visible-rect coordinates.
- Expanded Core and App xUnit coverage for all-item layout, 6,217 items, threshold, scale binding, and viewport sizing.

### Verification
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror --artifacts-path .artifacts/semantic-panorama`: 0 warnings, 0 errors (2026-08-08 19:05 +08:00).
- `dotnet test HanabePhotoManager.sln -c Release --no-build --artifacts-path .artifacts/semantic-panorama`: Core 365/365, Infrastructure 160/160, App 331/331.
- Isolated artifacts were needed because a user-running app locks default Release DLLs; no process was stopped.

### Remaining Issues
- Manual WPF QA with 6217+ / 11739-item libraries is still required before KI-01, KI-03, KI-07, and KI-14 can be marked resolved.

---

> **Purpose:** Append-only record of every agent modification to this project.  
> **Last Updated:** 2026-08-06  
> **Rule:** Append new entries at top. Never delete or rewrite history.  
> **Related:** [`AGENTS.md`](../AGENTS.md), [`AGENT_HANDOFF.md`](../AGENT_HANDOFF.md), [`CHANGELOG.md`](../CHANGELOG.md)

---

## 2026-08-08 — Codex (Treemap performance and layout)

### Task
Optimize photo-library treemap layout and viewport thumbnail performance without changing import or retouched-output protection behavior.

### Files Changed
- `PhotoTreemapControl.cs` — viewport-sized root overview, semantic-detail threshold/sample cap, clipped category content, and true visible subtree thumbnail paths.
- `ProgressiveTreemapViewModel.cs` — background dimension publication returns to the captured UI context and skips identical dimensions.
- `MainWindow.xaml.cs` — root uses ScrollViewer viewport bounds; content-fit remains subtree-only.
- `MainWindowViewModel.cs` — background header reads use batches; viewport requests use a generation-safe bounded queue instead of cancel/restart.
- Added `JustifiedGalleryLayoutTests.cs`; expanded treemap view-model and control tests.

### Verification
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror` — 0 warnings, 0 errors (2026-08-08 18:53 +08:00).
- `dotnet test HanabePhotoManager.sln -c Release --no-build` — Core 361/361, Infrastructure 160/160, App 328/328.

### Remaining Issues
- Automated tests cover layout and pipeline contracts. Manual WPF QA with a 6217+ / 11739-item real library, including the `已修` filter, remains required before marking KI-01/KI-03/KI-07/KI-08 resolved.

## 2026-08-06 — WorkBuddy (Documentation Pass)

### Task
Update all project documentation to version `0.2.0-alpha.3`. Create missing docs for agent onboarding. Record current implementation state.

### Files Changed
- `src/HanabePhotoManager.App/HanabePhotoManager.App.csproj` — HanabeVersion: `0.2.0-alpha.2` → `0.2.0-alpha.3`
- `src/HanabePhotoManager.App/ReleaseNotes/ReleaseNotesViewModel.cs` — Added `0.2.0-alpha.3` catalog entry
- `AGENTS.md` — Updated version, added feature doc links, revised AI principles
- `AGENT_HANDOFF.md` — Complete rewrite: status, known issues, key files, verification
- `CHANGELOG.md` — **Created** — full changelog `0.1.0-alpha` through `0.2.0-alpha.3`
- `docs/current-status.md` — **Created** — feature-by-feature state with status labels
- `docs/features/photo-library.md` — **Created** — filter pipeline, categories, file types, thumbnail loading
- `docs/architecture/photo-treemap.md` — **Created** — two-layer layout, classes, rendering pipeline, data flow
- `docs/known-issues.md` — **Created** — 14 tracked issues with reproduction steps and status
- `docs/agent-change-log.md` — **Created** — this file

### Implementation
Documentation-only pass. No business logic, UI, or layout code modified.

### Decisions
- Documentation uses standardized status labels: Stable / Implemented-Unverified / Partial / In Progress / Planned / Known Issue / Blocked / Resolved
- Agent entry point order: AGENTS.md → AGENT_HANDOFF.md → current-status.md → feature docs
- Known issues use KI-XX numbering for cross-reference

### Verification
- Build: not run (documentation-only change)
- Git status: clean apart from these doc files

### Remaining Issues
- All 14 known issues documented; none resolved in this pass
- Root overview mode (KI-14) still blocked pending redesign

### Next Recommended Step
- Fix and re-verify KI-01 through KI-07 (treemap rendering stability)
- Redesign root overview mode (KI-14)
- Run full regression test suite

### Risk / Rollback
- Low risk — documentation-only
- Rollback: `git revert` the commit

---

## 2026-08-05 ~ 2026-08-06 — WorkBuddy (Multiple Sessions)

### Summary
Multiple sessions implementing treemap features including: Justified Gallery inner layout, file type filter, retouch filter crash fix, date filter fix, recursive 修后 scan, viewport-driven loading, borderless mode, subtree item count, Space+drag panning, and attempted root overview mode (later reverted).

### Key Commits (on `codex/photo-treemap-browser`)
- `dd1a573` — Revert overview mode
- `5ce0a70` — Subtree full-content scrolling (ContentHeight)
- `236eef3` — Recursive 修后 scan
- `cf31c20` — Justified Gallery fix: file-header aspect ratios + close-fit
- `d4f5ff4` — Root overview mode (reverted)
- `1fe8e33` — Borderless mode + debug border removal
- `c68e824` — File type multi-select filter
- `b2cda53` — UI freeze fix (sync IO removal)
- Many earlier commits for treemap rendering, zoom, pan, category headers

### Remaining Issues
See [`docs/known-issues.md`](known-issues.md) — 14 tracked items.

## 2026-08-08 — Codex

### Summary
- Added explicit SHA-256 exact-duplicate import decisions (skip, import anyway, or locate the existing file) with incoming/existing thumbnail comparison.
- Defined `<library root>\<month>\<date>\修后` as the single read-only retouched-output path policy.
- Kept retouched files visible to exact and perceptual duplicate scans, while preventing their selection/deletion and excluding them from resequencing.
- Tightened viewport thumbnail requests to meaningful tile dimensions and restored the preloaded treemap guard.

### Verification
- `dotnet build HanabePhotoManager.sln -c Release /warnaserror` — 0 warnings, 0 errors.
- `dotnet test HanabePhotoManager.sln -c Release --no-build` — Core 359/359, Infrastructure 160/160, App 327/327.

### Remaining Issues
- Manual WPF smoke test of the new modal (including non-raster/video fallback and Explorer activation) remains pending; automated tests cover the decision policy and filesystem protections.

### Key Architecture Decisions
- Two-layer layout: SquarifiedTreemap (outer) + JustifiedGallery (inner)
- Aspect ratio from file headers (ImageDimensionReader), not thumbnail decode
- Viewport-driven loading with 150ms debounce
- Borderless mode: skip white tile backgrounds, UniformToFill close-fit
- Recursive 修后 scan in background Task.Run
- ContentHeight-based ScrollViewer extent for subtree scrolling
# 2026-08-09 — Semantic search (Chinese-CLIP / ONNX / SQLite)

- Added Core semantic-search contracts and immutable index/query/result/status models.
- Added independently owned Infrastructure tokenizer, 224px ImageSharp preprocessor, SQLite embedding store, local model catalog, and ONNX CPU semantic search service.
- Added independent App semantic search ViewModel, result item ViewModel, view, and code-behind; minimally wired a new navigation page without changing treemap behavior.
- Added Core contract and Infrastructure tokenizer/store tests. Model files remain local-only under LocalApplicationData and are ignored if accidentally placed under the project.

# 2026-08-09 — Semantic browse bound and UNC startup scan

- Enforced the semantic browse candidate boundary in the App ViewModel: deduplicated score-descending Top 50 paths are the only paths emitted to the browse grid and treemap, even if a provider returns more candidates.
- Added regression coverage for an unordered 75-result provider response and for UNC-root detection.
- Network library startup now avoids the recursive, mutating empty-date cleanup pass and reuses the completed media scan for the capacity summary, eliminating two redundant full UNC walks while retaining media discovery, tree map, thumbnails, semantic indexing, and normal local-library maintenance.
# 2026-08-09 - Semantic result guard and UNC tree-map follow-up

- Added a presentation-bound Top 50 guard in `SemanticBrowseRanking`; the browse grid and treemap cannot render an unbounded semantic candidate list if an upstream provider violates its requested limit.
- Fixed tree-map aspect fallback to use decoded viewport thumbnail dimensions whenever a header dimension is unavailable.
- UNC tree-map startup no longer opens every image solely to pre-read dimensions; non-visible items retain the existing fallback until their viewport thumbnail supplies a real aspect ratio.
- Reused the first recursive retouched-output enumeration while building associations, eliminating the duplicate per-date network traversal after startup scan.
- Added regression coverage for the browse Top 50 boundary, thumbnail-derived aspect ratios, and reuse of pre-enumerated retouched outputs.
- Replaced per-date full-library rescans in retouch statistics with one pass that aggregates RAW/JPG groups by their owning date directory.

## 2026-08-14 — 赞助区块补全：爱发电（Afdian）入口

### Task
把爱发电主页链接 `https://afdian.com/a/hanabededsec` 填入三语 README 赞助区块（替换 Buy Me a Coffee 占位）+ 软件设置页「支持作者」区块加爱发电入口按钮（M3 风格、语义 Token），构建 0 警告 + 917 测试全绿 + 截图。

### 改动
- `README.md` / `README.ja.md` / `README.zh-CN.md`：赞助小节 `Buy Me a Coffee — coming soon / 近日公開予定 / 敬请期待` 占位替换为爱发电链接（中/英/日文案），三语各 1 处 `afdian.com/a/hanabededsec`，无占位残留。
- `src/HanabePhotoManager.App/SettingsCenterPage.xaml`：赞赏码卡片下方新增 `AfdianLinkCard` 按钮（复用 ThemeCard 样式 + 语义 Token，标题「爱发电 (Afdian)」+ 副行 `afdian.com/a/hanabededsec · 点击打开主页`）。
- `src/HanabePhotoManager.App/SettingsCenterPage.xaml.cs`：`AfdianLink_Click` 用 `Process.Start(ProcessStartInfo{UseShellExecute=true})` 打开主页，Win32Exception 兜底（与 SponsorQr_Click 同模式）。
- `.artifacts/capture-m3-settings-sponsor2.ps1`：截图脚本（播种 fixture + 临时抬高 WindowHeight 露出完整赞助区块 + 自动恢复 settings.json）。

### 验证
- `dotnet build -c Debug /warnaserror`：0 警告 0 错误；`dotnet test --no-build`：917 全绿（Core 373 / Infra 164 / App 380）。
- 截图 `.artifacts/m3-settings-sponsor2-light.png`：二维码卡片 + 爱发电卡片均可见；settings.json 的 LibraryRoot/WindowHeight 恢复校验通过。

## 2026-08-16 — 图库滚动缩放与全局控件可读性重构

- 图库普通滚轮恢复为仅滚动；`Ctrl + 滚轮` 改为以指针位置为锚点缩放，并统一减号、滑块、加号和重置按钮的缩放状态。
- 新增纯计算的 `GalleryZoomPolicy` 与边界、锚点、无效布局回退测试，避免列数变化导致视野跳动。
- 重绘复选框和单选框模板，移除系统原生灰色矩形残留；禁用按钮改用语义颜色，不再整体降低透明度。
- 修复主按钮内容被隐式文本样式覆盖而出现“紫底无字”的问题，并统一导航、查看器和组合框的禁用态可读性。
- 逐页鼠标复查主页、人物、导入、图库、工具、地图、相册和设置，并切换六套主题复查浅色/深色对比度。
- Release 构建 0 警告、0 错误；Core 159、Infrastructure 54、App 397，共 610 项测试通过；自包含版本已同步至 `D:\hanabe-publish-v2`。

### 运行时复查修正

- 修正 `VirtualizingWrapPanel` 尺寸绑定错误：原绑定错误地从 `Window` 查找 `ZoomableGridTileStride`，现改为从 `Window.DataContext` 获取，缩略图尺寸可真实改变。
- 普通滚轮不再依赖外层 `ScrollViewer` 的默认转发，直接更新图库虚拟化面板的垂直偏移。
- 发布版鼠标实测：上下滚动均改变照片墙位置；点击加号后缩略图由 325px 变为 364px，并完成列数重排。
- 日期照片墙改为所有日期分组默认展开；8.15 的照片结束后连续显示 8.14、8.13 等后续日期标题与照片，仍可单独点击标题收起。
- 左侧导航选中态移除焦点、悬停和选中描边叠加，只保留单层 `Brush.Surface.Selected` 深色背景；设置入口同步采用相同规则。
- 启动在图库页或重新进入图库页时，将虚拟照片墙滚动位置恢复到首个日期标题，避免旧滚动位置让日期看起来消失。

## 2026-08-16 — 自然连续图库与导入设置整理

- 图库改为不可折叠的连续日期/文件夹分组，默认范围展示全部媒体，普通滚轮自然浏览后续分组；切回全部日期时先取消旧的单日期异步批次，避免 8.15 数据尾批覆盖全库。
- 设置 → 照片库与导入新增“文件夹标题显示”，支持解析日期、实际文件夹名、日期与文件夹名，默认解析日期。
- XML 不再作为图库独立项目；同目录同名 XML 改为对应视频卡片内的关联标记。多选卡片仅用深色背景表达选中，不叠加描边。
- 设置二级导航统一为单层圆角选中块，主导航加宽并居中图标与文字；导入高级选项改为紧凑的分隔行布局。
- 保留照片/视频双击无边框查看器和视频封面路径；Release 构建与 613 项测试通过，发布版同步至 `D:\hanabe-publish-v2` 并完成鼠标实机复查。

### 最大化布局与分组折叠回归修正

- 修复最大化或高 DPI 窗口仍按旧视口只实现两行缩略图的问题：虚拟面板增加安全实现缓冲行，放大后的可见区域立即铺满。
- 恢复日期标题的收起/展开按钮、箭头、状态记忆与人物筛选后的原状态还原；保持所有日期默认展开和连续自然滚动。
- 发布版实测最大化首屏铺满；收起 8.15 后下一日期分组立即上移显示。

### 图库板块完整显示与视频封面去重

- 日期标题容器改为占满固定标题行，移除会造成底边裁切的外边距，并让虚拟化项目容器横纵向拉伸，圆角板块完整显示。
- 同一日期内与视频同名的 JPG 截帧不再作为独立图库项目；该 JPG 继续优先作为对应视频卡片封面，普通 JPG 不受影响。
- 缩略图内容向卡片边框内缩并统一裁切，避免图片矩形越过四边圆角和边框。

### 图库分组容器、圆角遮罩与导航动效

- 展开的日期标题与所属缩略图合并为同一块大圆角分组背景；收起后后续日期自然上移，保留连续滚轮浏览与展开/收起动效。
- 缩略图卡片使用与卡片同尺寸的圆角几何裁切，选中遮罩提高到图片之上且保持角半径一致，角标与文件信息继续显示在遮罩上层。
- 一级导航改为单一选中指示块在菜单项之间平滑位移，不再为每项叠加浅色/深色双层选中框。
- Release 构建 0 警告、0 错误；Core 159、Infrastructure 54、App 402，共 615 项测试通过；发布版已同步至 `D:\hanabe-publish-v2` 并完成运行时折叠复查。
- 接入现有 WiX 中文安装器外壳并嵌入最新自包含负载，生成 `artifacts/0.3.2-alpha.1/HanabePhotoManager-Setup-x64.exe`。

### 安装器启动修复与品牌图标统一

- 修复 WiX 主题因不兼容的分状态按钮、进度条和 ImageControl 图片而在启动阶段以错误码 87 退出的问题；按钮改为 WiX 支持的三态纵向图带，进度条回退为系统稳定控件，顶部 Logo 复用已验证的图形加载路径。
- Setup 可执行文件通过 `IconSourceFile` 使用应用的 `HanabeApp.ico`，安装界面使用同源白底 Logo；安装目录补齐结尾路径分隔符，启动目标可正确解析。
- 应用新增白底 Logo 资源，浅色主题使用白底版，深色主题继续使用原深底版，侧栏和设置内 Logo 随主题动态切换。
- `0.3.2-alpha.9` Setup 实际启动保持运行，Burn 日志 0 个致命错误；未执行安装操作。

### WPF 分步安装外壳与经典黑白主题

- 以自包含 WPF 安装外壳替换 WiX Standard BA 界面，继续使用 WiX MSI 负责安装、升级与卸载；最终 MSI 作为资源嵌入单一 Setup EXE。
- 安装流程拆分为安装选项、使用须知、安装进度和完成四步；须知必须滚动到末尾才解锁同意框，勾选后才允许开始安装。
- 安装外壳支持浅色、深色即时切换，窗口、任务栏和内容 Logo 与应用图标统一；整体采用圆角、中性表面和低饱和品牌强调。
- 原“动态色彩”主题调整为“经典黑白”浅色/深色方案；设置页根前景色与行标题显式使用语义文字资源，修复深色模式未设置前景色时出现黑字的问题。
- `0.3.2-alpha.10` 安装包实际启动通过；人工检查浅色、深色、分步导航及滚动门禁，未执行系统安装。
- Release 构建 0 警告、0 错误；Core 159、Infrastructure 54、App 402、InstallerShell 10，共 625 项测试通过。

### 八套主题与点击中心扩散动画修正

- 保留动态色彩、森林绿、紫罗兰原有六套主题，新增独立的经典白色与经典黑色主题，总数调整为八套。
- 修复主题扩散动画混用窗口坐标与内容层坐标造成的圆心偏移；主题卡片与左侧明暗按钮均从实际点击控件中心向窗口四角扩散。
- 左侧明暗按钮纳入同一动画入口，并根据目标操作在月亮与太阳图标、深色与浅色文字之间同步切换。
- Release 构建 0 警告、0 错误；Core 159、Infrastructure 54、App 406、InstallerShell 10，共 629 项测试通过；发布版已同步至 `D:\hanabe-publish-v2`，实机捕获动画中间帧并确认结束状态。
- 后续复查发现 `DrawingBrush` 会按自身边界重映射圆形遮罩，视觉上形成斜切分屏；已改为内容坐标系原生 `CombinedGeometry Clip`，现在严格以点击点为圆心、半径连续扩大至覆盖窗口四角。

### 图库双击查看器启动优化

- 移除每次打开查看器时在 UI 线程对全部可见媒体逐项执行 `File.Exists` 的全库探测；当前文件仍在双击入口校验，查看器继续保留失效路径容错。
- 约 1.5 万项图库发布版实测，双击输入返回并创建无边框查看器窗口约 239 ms，视频首帧与导航列表正常。
- Release 构建 0 警告、0 错误；Core 159、Infrastructure 54、App 407、InstallerShell 10，共 630 项测试通过；发布版已同步至 `D:\hanabe-publish-v2`。

### 地图照片增量位置索引

- 在现有 `media-metadata.json` 中为地图 EXIF 扫描增加文件大小、最后修改时间和完成标记；无 GPS 的照片也会记为已扫描。
- 地图刷新时复用未变化文件的结果，仅对新增或已修改照片重新读取 EXIF；旧版已有 GPS 坐标的条目只补写文件签名，不重复解码。
- 每完成 64 个新文件即保存扫描检查点，大图库扫描被中断后可从最近进度继续。
- Release 构建 0 警告、0 错误；Core 159、Infrastructure 54、App 409、InstallerShell 10，共 632 项测试通过。
- 已生成 `artifacts/0.3.2-alpha.11/HanabePhotoManager-Setup-x64.exe`（自包含 WPF 安装外壳、内嵌 MSI），启动检查通过；SHA-256：`a42278f2df23e5a1ae8d4c806edb0cc89b909bc235b71320d2f76869aedbd8ad`。

### 安装器圆角进度与桌面快捷方式选项

- WPF 安装外壳以自定义模板替换系统矩形进度条，轨道与移动指示块均使用一致圆角并裁切边界。
- 安装选项页新增“在桌面创建快捷方式”，默认不勾选；选择结果通过 `CREATE_DESKTOP_SHORTCUT` 传入 MSI，未选择时仅保留开始菜单入口。
- 桌面快捷方式使用当前用户注册表键作为组件 KeyPath，确保安装校验和卸载清理符合 Windows Installer 规则。
- Release 全量测试 Core 159、Infrastructure 54、App 409、InstallerShell 12，共 634 项通过。
- 已生成 `artifacts/0.3.2-alpha.12/HanabePhotoManager-Setup-x64.exe`，启动保持运行检查通过；SHA-256：`80869baecaf11f69d08192319f657bc56a22f73fa38d0a392ad844639a5c36d5`。
