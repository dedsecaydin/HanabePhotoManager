# 设置中心功能存在性审计与信息架构

> 审计日期：2026-07-23  
> 审计范围：仅 `src/`、`tests/` 中与候选设置相关的 XAML、ViewModel、服务、配置模型和测试。  
> 状态口径：完整支持 / 部分支持 / 仅功能页参数 / UI 占位 / 底层存在但无 UI / 未实现 / 不适合设置化。  
> 持久化基线：`AppSettingsStore` 默认写入 `%LocalAppData%/HanabePhotoManager/settings.json`；主题另存 `%LocalAppData%/HanabePhotoManager/ui-theme.txt`；媒体元数据、人物相册、地图缩略图和云会话使用各自存储。

## 功能存在性审计矩阵

| 功能/候选设置 | 功能是否存在 | 完整度 | UI入口 | ViewModel/Command | Service | 持久化 | 实际消费位置 | 测试 | 是否应进入设置中心 | 建议分类 | 缺失工作 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 开机自启动 | 是 | 部分支持 | 设置页“启动与窗口” | `MainWindowViewModel.LaunchAtStartup` | 注册表读写内嵌于 VM：`SetLaunchAtStartup` / `IsLaunchAtStartupEnabled` | `AppSettings.LaunchAtStartup` + `HKCU/.../Run` | 属性修改立即写注册表；初始化读取注册表 | 无专项测试 | 是 | 常规 / 启动与关闭 | 抽离 OS 服务；写入失败不能静默吞掉；增加权限/路径失效反馈和测试 |
| 关闭行为 | 仅正常关闭清理 | 未实现（设置） | 无 | `MainWindow.OnClosed` 取消人脸任务、释放地图/云页 | 无关闭策略服务 | 无 | 仅窗口关闭事件 | 无 | 暂不进入 | 常规 / 启动与关闭 | 若要“最小化到托盘/退出确认”，需先实现完整后端；本轮不得显示 |
| 窗口宽高恢复 | 是 | 部分支持 | 设置页宽高滑块；窗口双向绑定 | `WindowWidth`、`WindowHeight`、`RememberWindowSize` | `AppSettingsStore` | `WindowWidth`、`WindowHeight` | `MainWindow.xaml` Width/Height 绑定；`SizeChanged` 保存正常态尺寸；初始化读取 | 仅存储往返测试未覆盖宽高；无窗口恢复测试 | 是 | 常规 / 窗口 | 增加非法值、屏幕边界、DPI、多显示器和恢复测试；避免高频并发保存 |
| 窗口位置与状态恢复 | 否 | 未实现 | 无；当前 `CenterScreen` | 无 | 无 | 无 Top/Left/WindowState 字段 | 无 | 无 | 暂不显示 | 常规 / 窗口 | 需新增完整位置、显示器校验、最大化状态持久化链路后再展示 |
| 照片库根目录 | 是 | 完整支持（测试偏弱） | 首页/导入与设置页只读展示；选择库命令 | `LibraryRoot`、`BrowseLibraryCommand`、`RefreshLibraryCommand` | `LibraryDirectoryInitializer`、库扫描/维护逻辑 | `AppSettings.LibraryRoot` | `InitializeAsync` 读取并自动扫描；导入、浏览、人脸查找、容量统计消费 | `LibraryDirectoryInitializerTests`、`LibraryMaintenanceServiceTests`；缺设置集成测试 | 是 | 照片库与导入 / 照片库 | 增加路径不可访问、网络盘、重启消费的集成测试；设置中心提供“更改”入口而非仅展示 |
| 照片库扫描与刷新 | 是 | 部分支持 | 首页/浏览刷新入口 | `RefreshLibraryCommand`、`StreamLibraryPreviewAsync` | `LibraryMaintenanceService`、`PreviewLoadingPolicy` | 仅根目录；无扫描策略字段 | 启动时对有效库自动扫描 | `PreviewPerformanceTests`、`LibraryMaintenanceServiceTests`、`PreviewLoadingPolicyTests` | 仅“启动时自动扫描”等长期策略可候选；当前无字段 | 照片库与导入 / 扫描 | 当前固定行为不可伪装成开关；需先定义、持久化、消费及错误策略 |
| 照片导入 | 是 | 完整功能；非全局设置 | 导入页、设备导入、拖放 | `BrowseSourceCommand`、`AnalyzeSourceCommand`、`ImportSelectedCommand`、`ImportFromDeviceCommand` | `ImportPlanBuilder`、`VerifiedFileTransfer`、`LibraryDirectoryInitializer`、`JsonImportJournal` | 导入日志/计划；源目录和传输模式未作为全局偏好 | 导入执行链直接消费页面选择 | Core/Infrastructure 多组 Imports 与传输测试 | 仅稳定默认项可另行评估；源目录、当次选择留功能页 | 照片库与导入 / 导入默认值 | 不将单次导入目录/文件选择设置化；如需默认传输策略须先补字段和消费测试 |
| 重复文件处理 | 是（导入规划层） | 底层存在但无独立设置 UI | 导入流程内隐式处理 | 导入分析/计划链 | `MediaGroupBuilder`、`ImportPlanBuilder`、`DestinationProbe` | 无用户策略字段 | 构建导入计划时识别重复路径/目标 | `MediaGroupBuilderTests`、`ImportPlanBuilderDuplicateDestinationTests` | 暂不进入，除非明确长期冲突策略 | 照片库与导入 / 重复文件 | 当前没有可选的“跳过/重命名/覆盖”全局策略；先确认产品决策和安全边界 |
| 缩略图尺寸 | 是 | 完整支持 | 浏览页滑块/Ctrl+滚轮；设置状态随浏览快照保存 | `ThumbnailSize`、`AdjustThumbnailSize` | `ShellThumbnailProvider` + VM 缓存 | `AppSettings.ThumbnailSize`，亦进入 `BrowseSnapshot` | 浏览卡片宽高、解码尺寸、恢复策略 | `PreviewPerformanceTests`、`PreviewLoadingPolicyTests`；缺设置消费专项 | 是 | 浏览与 AI / 浏览显示 | 明确它是全局默认还是每库/会话状态，避免双重持久化语义 |
| 缩略图缓存容量/清理 | 是（内存和地图/云缓存） | 底层存在但无 UI | 无全局入口 | VM 静态缓存；云页/地图内部使用 | `PreviewLoadingPolicy.ThumbnailCacheLimit`、`MapThumbnailCache`、`FileCloudCacheStore` | 内存缓存不跨启动；地图/云缓存位于 AppData | 预览、地图、云缩略图消费 | `PreviewLoadingPolicyTests`、`FileCloudCacheStoreTests` | “查看占用/清理缓存”适合高级；容量策略暂不开放 | 高级 / 存储 | 需要统一缓存清单、占用统计、安全清理服务和测试；当前不得放假按钮 |
| 浏览筛选/排序/评分/分类 | 是 | 部分支持（默认项语义混杂） | 浏览页；设置页已有默认评分与排序 | `RatingFilter`、`PreviewSortMode`、分类/标签命令 | `MediaMetadataStore`；评分另有 `FileMetaStore` | 默认评分/排序在 `AppSettings`；评分/标签元数据另存 | 初始化读取默认值；筛选排序实际刷新列表；评分写文件侧元数据 | `AppSettingsStoreTests`、`TagManagerViewModelTests`、浏览性能测试 | 默认评分/排序可进入；即时筛选、当前分类不进入 | 浏览与 AI / 浏览默认值 | `SaveSettingsAsync` 会把当前浏览筛选覆盖“默认值”；应分离默认偏好与会话状态，并补测试 |
| 浏览进入时恢复策略 | 是 | 完整支持 | 设置页“浏览状态” | `BrowseEntryModeSetting`、`BrowseStatePolicy` | `BrowseStatePolicy` | `BrowseEntryMode`、`BrowseSnapshot` | 进入浏览页时 `ApplyBrowseEntryPolicyAsync` 消费 | `BrowseStatePolicyTests` | 是 | 浏览与 AI / 浏览默认值 | 补端到端重启测试；确认缩略图尺寸是否也应随快照恢复 |
| AI 标签识别引擎 | 是 | 完整支持 | 设置页引擎选择；浏览页执行分析 | `PhotoAnalysisViewModel.SelectedEngine`、分析命令 | `PhotoClassifierFactory`、`OnnxPhotoClassifier`、`MobileClipPhotoClassifier`、`RuleBasedPhotoClassifier` | `ClassificationEngine`；结果写 `media-metadata.json` 和 checkpoint | 初始化选择引擎；`AnalyzeAsync` 创建对应分类器并缓存版本 | `PhotoAnalysisViewModelTests`、`OnnxPhotoClassifierTests`、`MobileClipPhotoClassifierTests` | 是 | 浏览与 AI / AI 识别 | 枚举已用中文字符串，但应建立稳定值与中文 Label 分离，避免字符串漂移 |
| AI 标签数量/相似度窗口 | 是 | 完整支持 | 设置页 | `SemanticMaxLabels`、`SemanticSimilarityWindow` | `MobileClipRuntimeOptions` / MobileCLIP 分类器 | 对应 AppSettings 字段 | 初始化设置运行时静态选项；识别时消费 | `AppSettingsStoreTests`；缺分类结果参数化集成测试 | 是（仅对语义引擎有效时显示） | 浏览与 AI / AI 识别 | 增加适用性说明、禁用态和消费测试；非 MobileCLIP 引擎不应造成误解 |
| CPU / GPU 推理 | 是 | 部分支持 | 设置页“推理设备” | `InferenceDevice` | `MobileClipRuntimeOptions`、ONNX Runtime provider 选择 | `AppSettings.InferenceDevice` | 初始化写运行时选项；MobileCLIP 推理消费并回退 CPU | `AppSettingsStoreTests`；缺 GPU 可用/回退测试 | 是 | 高级 / 性能（或浏览与 AI / 运行方式） | 当前只有“自动（NVIDIA 优先）/CPU”；需运行环境检测、状态反馈、回退与测试；中文 Label 与稳定枚举分离 |
| 人物检测与人物相册 | 是 | 部分支持 | 浏览页人物气泡/扫描；设置页有人物识别开关 | `PeopleAlbumViewModel.ScanCommand`、`EnablePersonRecognition` | `PeopleAlbumService`、`LocalPersonClusterer`、`LocalFaceEmbeddingService` | 人物相册 `people-albums.json`；开关字段存在 | 人物扫描保存相册；但 `InitializeAsync` 强制 `EnablePersonRecognition = false`，忽略已存字段 | `PeopleAlbumViewModelTests`、`PeopleAlbumServiceTests` | 人物识别开关当前不得进入第一期 | 浏览与 AI / 人物 | 这是失效设置：补启动读取、保存触发、实际门控、错误反馈与测试后才可显示 |
| 人脸参考图查找 | 是 | 完整功能；功能页参数 | 独立人脸查找页 | `FaceSearchViewModel` 各命令、`MinimumSimilarity` | `FaceSearchService` | 无全局设置 | 当次参考图、阈值和库根目录直接消费 | `FaceSearchServiceTests` | 参考图和阈值留功能页；不设置化 | 不适合设置化 | 无需进入设置中心；若未来有默认阈值需先验证跨任务价值 |
| 地图、GPS、EXIF、位置索引 | 是 | 完整功能；无全局偏好 | 地图页、照片详情 EXIF 面板 | `MapPhotosViewModel` 刷新/分配/清除命令 | `ExifLocationReader`、`PhotoLocationService`、`MapMediaSourceService`、`PhotoDetailMetadataReader`、`MapThumbnailCache` | `media-metadata.json` 中位置和 `MapSourcePaths`；地图缩略图目录 | 地图刷新读取 EXIF/手工位置并生成标记 | `ExifLocationReaderTests`、`MapMediaSourceServiceTests`、`MapPhotosViewModelTests`、`PhotoDetailMetadataReaderTests` | 当前参数均留地图/详情页；缓存管理可归高级 | 地图与元数据 | 暂无真实的全局“位置索引开关/隐私开关”；不得创建假开关 |
| 批量压缩 | 是 | 完整功能；仅功能页参数 | 压缩页 | `CompressionViewModel.StartCommand` 等 | `ImageCompressionPlanner`、`ImageCompressionService`、`ImageInputDiscovery` | 无设置持久化 | 当次输入、目标大小/模式、输出目录直接消费 | `CompressionViewModelTests`、`ImageCompressionPlannerTests`、`ImageCompressionServiceTests` | 否；目标大小、单位、输出目录属于单次任务 | 处理与导出（功能页，不进设置） | 无全局设置工作；未来若做“默认输出命名/保留元数据”需另审计 |
| 批量水印 | 是 | 完整功能；仅功能页参数 | 水印页 | `WatermarkViewModel` 选择/导出命令 | `WatermarkExportService`、`WatermarkLayoutCalculator`、`WatermarkInputDiscovery` | 无设置持久化 | 当次水印图、位置、透明度、平铺、输出目录消费 | `WatermarkExportServiceTests`、`WatermarkLayoutCalculatorTests` | 否；参数留功能页 | 处理与导出（功能页，不进设置） | 无全局设置工作；可未来单独设计“预设”，但不是全局设置 |
| 百度网盘连接 | 是 | 部分支持 | 设置页 OAuth 凭据；独立云盘页 | `SaveBaiduCredentialsCommand`、授权/断开命令；`CloudHubViewModel` | `CloudConnectionSettingsService`、`BaiduOAuthClient`、`BaiduCloudProvider`、云存储基础设施 | AppKey 在 settings；AppSecret DPAPI；会话 `cloud-session.dat` | 设置服务读取凭据/令牌；云功能消费 provider/session | `BaiduOAuthClientTests`、`EncryptedCloudSessionStoreTests`、`CloudHubViewModelTests`；无设置服务专项测试 | 连接与账户管理适合进入 | 云盘与项目 / 百度网盘 | 补 `CloudConnectionSettingsService` 测试、错误状态、令牌失效/刷新集成；设置保存逻辑会整对象覆盖，需验证不丢云字段 |
| 夸克网盘 | 仅网页/客户端跳转与内嵌官网 | UI 占位（相对“网盘功能”）/部分支持（跳转功能） | 设置页客户端路径、独立内嵌网页页 | `OpenQuarkOfficialCommand`；code-behind 选择/启动客户端 | 无官方网盘 provider；底层通用云模型含 Quark 枚举但未接真实 API | `QuarkClientPath`；WebView2 用户数据 | 仅启动本地客户端或访问官网 | 通用云存储测试覆盖 Quark 枚举；无真实 Quark 集成 | 仅“客户端路径/打开官网”可保留；不得表述为连接、自动备份 | 云盘与项目 / 夸克 | 明确 Label 为“官方客户端位置”；移除“已连接/自动备份”暗示；官方 API 不存在前暂缓真实网盘设置 |
| 投稿项目与欣赏项目 | 有静态页面和 WebView 流程 | UI 占位/演示数据 | “投稿项目”“欣赏项目”页 | `ContestViewModel` 构造函数硬编码条目；页面 code-behind | 无项目仓储、同步或可靠数据服务 | 无业务持久化（仅 WebView2 用户数据） | 页面显示硬编码链接/网页，部分下载操作在 code-behind | 无 Contest 业务测试 | 否；这是业务内容，不是全局设置 | 不适合设置化 | 数据源、更新、错误处理和测试均缺失；不进入设置中心 |
| 主题（浅色/深色） | 是 | 完整支持（入口不在设置内容区） | 主窗口主题切换按钮 | code-behind `ToggleTheme_Click` | `ThemeManager` | 独立 `ui-theme.txt` | `App.OnStartup` 调用 `LoadAndApply`，立即替换主题资源 | `ThemeManagerTests`、主题资源测试 | 是 | 外观 / 主题 | 将入口纳入设置 IA 时保持统一服务；补文件 IO/应用切换测试；中文 Label 不显示枚举原文 |
| 背景、玻璃效果、布局 | 是 | 完整支持 | 设置页“外观” | `GlassIntensity`、`BackgroundMode`、`BackgroundImageLayout`、选择/清除命令 | `WindowsWallpaperService`、`PersistentAssetStore` | 对应 AppSettings 字段；素材复制到 AppData/Assets | Shell 背景、面板/遮罩透明度、Stretch 实时消费 | `BackgroundCompositionTests`、`WindowsWallpaperServiceTests` | 是 | 外观 / 背景与材质 | 校验 BackgroundMode/Layout 非法持久值；统一稳定枚举与中文 Label |
| 头像/窗口图标 | 是（实际为窗口图标） | 部分支持 | 设置页称“应用头像/窗口图标” | `CustomAppIconPath`、选择/清除命令 | `PersistentAssetStore` | `AppIconPath` | `MainWindow.ApplyCustomWindowIcon` 消费 | `ApplicationIconTests` | 可进入，但命名需澄清 | 外观 / 应用图标 | 当前不是账户头像；统一称“应用窗口图标”，说明不改变 exe/任务栏所有场景 |
| 缓存、日志、诊断和存储位置 | 部分存在 | 底层存在但无统一 UI | 设置页仅显示库容量；云页有诊断文本 | `DiagnosticsText`、云诊断状态 | AppData 各 Store；模拟云 provider 写诊断日志 | 多处分散固定路径，无用户配置 | 各服务直接消费默认 AppData 路径 | 各 Store 测试分散 | “查看位置/占用/安全清理”适合；自定义存储位置暂不适合 | 高级 / 存储与诊断 | 统一清单、容量统计、打开目录、安全清理、日志脱敏/导出和测试；不允许直接暴露可破坏路径设置 |
| 删除确认与回收站 | 是 | 完整功能；固定安全策略 | 删除对话框、浏览页/查看器删除 | `DeleteSelectedFilesCommand`、查看器删除 | `RecycleBinFileService`；主 VM 另直接调用 VB FileSystem | 无“跳过确认”设置 | 先 `DeleteConfirmationWindow.Confirm`，再送回收站 | `PreviewPerformanceTests`、查看器相关测试 | 不建议提供“关闭确认”；可只展示安全说明 | 高级 / 安全与隐私 | 统一删除服务实现；若要策略设置，必须保留高风险保护并补测试 |
| 隐私安全 | 部分存在 | 部分支持 | 百度凭据说明；无统一隐私页 | 云授权命令 | DPAPI 的 `CloudConnectionSettingsService`、`EncryptedCloudSessionStore` | 密钥/令牌本机加密；WebView2 各自用户数据 | OAuth、会话和浏览器数据消费 | 云会话/授权测试 | 可展示只读数据位置、清除会话；不存在的遥测开关不得显示 | 高级 / 安全与隐私 | 统一列出本地数据、WebView2 数据、清除范围；补凭据服务测试与隐私文案 |
| 快捷键 | 是（硬编码） | 底层存在但无 UI | 浏览页/查看器键盘事件 | `MainWindow_KeyDown`、`PhotoViewerViewModel` | 无快捷键服务 | 无 | Ctrl+A、Esc、Delete、S、数字评分、方向键等直接消费 | `PreviewPerformanceTests` 部分覆盖，缺完整映射测试 | 第一阶段仅只读“快捷键说明”可选；不可做可编辑设置 | 高级 / 快捷键 | 若要自定义需命令映射、冲突校验、持久化与测试；当前不得显示编辑控件 |
| 更新与版本信息 | 否 | 未实现 | 无 | 无 | 无更新服务 | 无 | 无 | 无 | 版本信息可来自程序集但当前未实现；更新开关不得显示 | 高级 / 关于 | 先实现可靠版本读取；自动更新需完整更新源、签名、失败恢复后再规划 |

## 证据文件与调用链

### 设置与启动链

- `src/HanabePhotoManager.App/Services/AppSettingsStore.cs`：`AppSettingsStore` 与全部设置字段；默认 `settings.json`。
- `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`：`InitializeAsync` 读取并应用设置；各属性修改触发 `SaveSettingsAsync`；`SaveSettingsAsync` 重写设置对象。
- `src/HanabePhotoManager.App/MainWindow.xaml`：当前设置页、窗口 Width/Height 绑定及各功能页入口。
- `src/HanabePhotoManager.App/MainWindow.xaml.cs`：Loaded 初始化、SizeChanged、主题按钮、夸克客户端选择/启动、快捷键和关闭清理。
- `src/HanabePhotoManager.App/App.xaml.cs` → `Services/ThemeManager.cs`：应用启动读取并应用主题。
- `tests/HanabePhotoManager.App.Tests/AppSettingsStoreTests.cs`、`ThemeManagerTests.cs`、`BackgroundCompositionTests.cs`、`ApplicationIconTests.cs`：目前相关测试证据。

### 照片库、导入、浏览和删除链

- 根目录：`AppSettings.LibraryRoot` → `MainWindowViewModel.InitializeAsync` → `RefreshLibraryAsync` / `StreamLibraryPreviewAsync` → 浏览、导入、人物与人脸查找。
- 导入：`MainWindowViewModel` 导入命令 → `Core/Imports/MediaGroupBuilder.cs`、`ImportPlanBuilder.cs` → `Infrastructure/Files/LibraryDirectoryInitializer.cs`、`VerifiedFileTransfer.cs`、`JsonImportJournal.cs`。
- 缩略图：`MainWindowViewModel.LoadThumbnail*` → `ShellThumbnailProvider` → `ThumbnailCache`，容量由 `Core/Performance/PreviewLoadingPolicy.cs` 限制。
- 元数据：浏览评分/标签与 `Services/MediaMetadataStore.cs`、`FileMetaStore`；AI 分类结果写入 `media-metadata.json`。
- 删除：`DeleteConfirmationWindow` → `MainWindowViewModel.DeletePreviewFiles` / `PhotoViewerViewModel` → Windows 回收站。
- 测试：`Core.Tests/Imports/*`、`Infrastructure.Tests/Files/*`、`PreviewPerformanceTests.cs`、`BrowseStatePolicyTests.cs`、`TagManagerViewModelTests.cs`。

### AI、人物与地图链

- AI：设置页 → `PhotoAnalysisViewModel.SelectedEngine` / `MobileClipRuntimeOptions` → `PhotoClassifierFactory` → `OnnxPhotoClassifier` / `MobileClipPhotoClassifier` → `MediaMetadataStore`。
- 人物：`PeopleAlbumViewModel.ScanCommand` → `PeopleAlbumService` → `LocalPersonClusterer` / `LocalFaceEmbeddingService` → `people-albums.json`。
- 人脸查找：`FaceSearchViewModel.StartSearchCommand` → `FaceSearchService.SearchAsync`，参考图与阈值仅属于当次任务。
- 地图：`MapPhotosViewModel.RefreshCommand` → `MapMediaSourceService` / `ExifLocationReader` / `PhotoLocationService` → `MediaMetadataStore`；缩略图由 `MapThumbnailCache` 管理。
- 测试：`PhotoAnalysisViewModelTests.cs`、分类器测试、`PeopleAlbum*Tests.cs`、`FaceSearchServiceTests.cs`、`MapPhotosViewModelTests.cs`、`ExifLocationReaderTests.cs`。

### 处理、云盘与项目链

- 压缩：`CompressionPage` → `CompressionViewModel` → `ImageCompressionPlanner` → `ImageCompressionService`；参数不持久化。
- 水印：`WatermarkPage` → `WatermarkViewModel` → `WatermarkLayoutCalculator` → `WatermarkExportService`；参数不持久化。
- 百度：设置页 OAuth 命令 → `CloudConnectionSettingsService` → `BaiduOAuthClient` / `EncryptedCloudSessionStore` → `BaiduCloudProvider` / `CloudHubViewModel`。
- 夸克：设置页客户端路径或 `OpenQuarkOfficialCommand` → `Process.Start` / 官方网页；没有真实官方 API provider。
- 投稿/欣赏：`ContestViewModel` 硬编码项目 → `ContestOpenPage` / `ContestJudgedPage` WebView；没有业务数据服务和持久化。

## 已支持 / 部分支持 / 功能页参数 / 占位 / 未实现分类

### 完整支持

- 照片库根目录的持久化与启动消费。
- 缩略图尺寸及浏览状态恢复策略。
- AI 分类引擎、语义标签数量、相似度窗口的保存与识别消费。
- 主题切换与启动应用。
- 背景来源、背景布局、玻璃强度与自定义背景。
- 删除确认并移动到 Windows 回收站。

### 部分支持或需要补链路

- 开机自启动：有真实注册表链路，但失败静默、缺服务边界和测试。
- 窗口恢复：仅宽高，无位置、显示器和状态。
- 浏览默认筛选/排序：当前值与“默认值”混写，需分离。
- CPU/GPU：设置能消费，但缺硬件检测、回退状态和专项测试。
- 人物识别开关：字段和 UI 存在，但启动强制关闭，是失效设置。
- 百度连接：真实 OAuth/DPAPI/Provider 存在，但设置服务缺专项测试和更完整错误状态。
- 应用图标：真实影响窗口图标，但不是账户头像。
- 缓存/日志/诊断/隐私：底层分散存在，尚无统一管理链。

### 仅属于功能页的单次参数

- 导入源目录、当次文件选择、当次传输模式。
- 压缩输入、目标大小/单位/模式、输出目录。
- 水印素材、位置、透明度、平铺参数、输出目录。
- 人脸参考图与本次相似度阈值。
- 地图本次导入源、经纬度和手工位置名称。
- 浏览页当前搜索词、即时筛选和当次选择（除明确的默认偏好外）。

### UI 占位或演示代码

- 夸克“网盘连接/自动备份”语义：当前仅官方网页、内嵌 WebView 和本地客户端跳转。
- 投稿项目/欣赏项目：`ContestViewModel` 使用硬编码数据，无真实项目服务、更新和持久化。
- 人物识别设置开关：UI 可见但保存值不在启动时恢复。

### 未实现或不适合设置化

- 窗口位置、最大化/最小化状态恢复。
- 关闭行为策略、托盘行为。
- 可配置扫描策略、重复冲突策略。
- 统一缓存清理、日志导出、自定义存储位置。
- 可编辑快捷键。
- 自动更新和版本信息页面。
- GPS/位置索引或隐私开关（现无真实门控链）。
- 压缩、水印、投稿/欣赏业务流程本身不应进入全局设置。

## 推荐设置中心结构

采用一体化 AI-native App Shell：左侧二级分类导航，右侧为同一连续滚动内容区。一级分类不做一组一个厚重大 Card；以页面标题、说明、轻分隔线、表单行和必要状态面板构成层级。玻璃、渐变、阴影与发光仅来自现有 Token，普通设置内容保持清晰克制。所有枚举采用稳定内部值 + 中文 `Label`，UI 不显示对象 `ToString()`。

| 一级分类 | 二级分类 | 可展示内容（仅真实可用项） |
|---|---|---|
| 常规 | 启动与关闭 | 开机自启动；关闭行为暂不展示 |
| 常规 | 窗口 | 窗口默认/恢复宽高；位置与状态暂不展示 |
| 照片库与导入 | 照片库 | 当前根目录、更改根目录、有效性与容量状态 |
| 照片库与导入 | 扫描与导入默认值 | 第一期仅说明当前启动自动扫描行为；没有真实字段的开关不展示 |
| 浏览与 AI | 浏览默认值 | 默认评分筛选、默认排序、进入浏览页恢复策略、缩略图尺寸 |
| 浏览与 AI | AI 识别 | 默认识别引擎、语义标签数量、相似度窗口 |
| 浏览与 AI | 人物 | 人物识别开关在链路修复前隐藏 |
| 地图与元数据 | 元数据与位置 | 第一期只展示本地处理说明；无真实偏好时不放控件 |
| 处理与导出 | 默认行为 | 第一期不放压缩/水印单次参数；未来仅考虑跨任务稳定预设 |
| 云盘与项目 | 百度网盘 | AppKey、加密凭据状态、授权、断开连接 |
| 云盘与项目 | 夸克网盘 | 明确为“官方客户端位置/打开官网”，不称连接或自动备份 |
| 外观 | 主题 | 浅色/深色（中文 Label） |
| 外观 | 背景与材质 | 背景来源、显示方式、玻璃强度、自定义背景 |
| 外观 | 应用图标 | 自定义窗口图标，避免称账户头像 |
| 高级 | 性能 | 推理设备；显示实际可用性和 CPU 回退状态后再开放完整体验 |
| 高级 | 存储与诊断 | 第一期只读展示数据位置/容量；清理动作需统一服务后加入 |
| 高级 | 安全与隐私 | 本地数据、回收站策略、云凭据与会话清除说明 |
| 高级 | 快捷键与关于 | 第一期可只读展示快捷键；版本信息需先实现程序集读取 |

## 第一期范围

1. **常规**：开机自启动；窗口宽高。明确不包含关闭策略、窗口位置与状态。
2. **照片库与导入**：照片库根目录、路径健康状态、容量；导入页单次参数不迁入。
3. **浏览与 AI**：缩略图尺寸、浏览进入恢复策略、默认评分筛选、默认排序、AI 引擎、语义标签数量与相似度窗口。先修正“当前值覆盖默认值”的语义。
4. **外观**：主题、背景来源、背景布局、玻璃强度、自定义背景、应用窗口图标。
5. **高级 / 性能与存储**：推理设备；只读展示 AppData/缓存/元数据位置和占用。清理缓存只有在统一安全清理服务完成后才进入。
6. **第一期必须先补的链路**：人物识别开关隐藏或修复后再上；GPU 可用性/回退反馈；设置保存不丢云字段；窗口值校验与测试；默认偏好和会话状态分离。

## 暂缓范围

- 窗口位置/状态、托盘、关闭确认：后端和持久化不存在。
- 扫描策略、重复文件冲突策略：当前只有固定业务行为，无用户策略模型。
- 人物识别开关：当前是失效设置，必须先补完整链路。
- 地图/GPS/EXIF 全局开关：功能存在但没有需要全局设置化的真实门控。
- 压缩和水印参数：属于单次任务，继续留在功能页。
- 夸克真实连接/自动备份：无官方 API，不得伪装为已支持。
- 投稿/欣赏项目设置：属于业务内容且当前为硬编码演示数据。
- 缓存清理、日志导出、存储迁移：缺统一安全服务、占用统计、迁移/回滚和测试。
- 可编辑快捷键：缺映射、冲突校验和持久化。
- 自动更新：无更新服务、签名验证和失败恢复。

## 仍需人工决定的问题

1. `ThumbnailSize` 应是全局默认、跨启动浏览快照的一部分，还是每库偏好？当前同时承担两种语义。
2. 默认评分筛选和默认排序是否应与用户当前浏览状态彻底分离？建议分离。
3. 窗口设置是“固定启动尺寸”还是“恢复上次窗口状态”？两者需要不同字段和消费规则。
4. 开机自启动失败时采用阻止保存、回滚 UI，还是保存偏好并显示修复提示？
5. 人物识别是自动随扫描执行，还是仅由用户在人物页手动触发？决定后才能定义开关含义。
6. 推理设备只提供“自动/CPU”，还是在检测到可用 provider 后显示具体 GPU？中文 Label 与稳定值需如何定义？
7. 百度凭据是否属于设置中心，还是独立“账户与连接”页？从安全和任务完整性看，建议归“云盘与项目 / 百度网盘”。
8. 夸克入口是否保留客户端路径，还是只保留官方网页？任何方案都不应称为 API 连接。
9. 设置中心是否只读展示数据位置，还是允许“打开目录/清理缓存”？后两者需要新增受控服务后再排期。
10. 应用图标是否继续允许自定义？它只影响窗口图标，不等同于用户头像或完整应用品牌替换。
11. 快捷键第一期是否只做只读帮助列表？当前不具备安全的自定义能力。
12. 删除确认是否始终强制保留？建议保留，不提供全局关闭开关。
