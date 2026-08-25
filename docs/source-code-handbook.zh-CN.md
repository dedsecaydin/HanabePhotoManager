# Hanabe Photo Manager 源码手册

> 范围：`src/` 当前全部 207 个代码、界面、项目和内嵌前端资源文件。  
> 目标：帮助维护者快速判断“功能在哪里、文件为何存在、修改会影响什么”。  
> 约定：测试文件参见各 `tests/*` 对应项目；发布与安装参见 `docs/release.md`。

## 1. 总体架构

```text
WPF View / Window / WebView2
        ↓ Binding、Command、WPF 事件
App ViewModel 与应用服务
        ↓ 领域模型和接口
Core（纯规则、规划、策略）
        ↑ 接口实现
Infrastructure（磁盘、SQLite、模型推理）
```

| 项目 | 核心职责 | 修改原则 |
|---|---|---|
| `HanabePhotoManager.Core` | 不依赖 UI 和磁盘实现的领域模型、导入规划、布局和性能策略 | 保持确定性，优先用单元测试证明行为 |
| `HanabePhotoManager.Infrastructure` | 文件系统、持久化、哈希、SQLite 和 CLIP 推理实现 | 保证失败可恢复，不破坏临时文件与原子发布顺序 |
| `HanabePhotoManager.App` | WPF 界面、ViewModel、桌面集成、图像解码和功能编排 | 保持 Binding、Command、事件和主题资源契约 |

## 2. 关键运行链路

### 2.1 应用启动

`App.xaml` 组合全局资源 → `App.xaml.cs` 处理单实例和启动参数 → `MainWindow.xaml` 创建主 Shell → `MainWindowViewModel` 初始化照片库、导航和后台扫描。

### 2.2 照片导入

`ImportSourcePicker` 选择来源 → `MainWindowViewModel.Import` 分析文件夹 → Core 的 `MediaGroupBuilder`、`MediaClassifier`、`ImportPlanBuilder` 生成计划 → Infrastructure 的 `VerifiedFileTransfer` 复制/校验/发布 → `JsonImportJournal` 支持恢复。

### 2.3 图库浏览

`LibraryDateSnapshotService` 读取日期目录 → `MainWindowViewModel` 生成日期/文件夹分组 → `UniformSquarePanel` 或 `PhotoTreemapControl` 排版 → `ShellThumbnailProvider` 按视口加载缩略图 → 单击由 Inspector 展示元数据，双击由 `PhotoViewerWindow` 打开。

### 2.4 人物与地图

人物：`PeopleAlbumService` 扫描 → `FaceRecognitionRuntime` 检测/嵌入 → `LocalPersonClusterer` 聚类 → `PeopleAlbumViewModel` 展示和合并。  
地图：`MapMediaSourceService` 枚举媒体 → `ExifLocationReader` 读取 GPS → `MapPhotosViewModel` 管理状态 → `MapPage` 与内嵌 Leaflet 页面通信。

## 3. Core：逐文件说明

### 3.1 项目与自定义相册

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.Core/HanabePhotoManager.Core.csproj` | Core 项目定义；设置目标框架、编译项和依赖边界。Core 不应引用 WPF 或 Infrastructure。 |
| `src/HanabePhotoManager.Core/Albums/CustomAlbum.cs` | 自定义相册领域模型，保存相册身份、显示名和文件夹引用等稳定数据。 |
| `src/HanabePhotoManager.Core/Albums/ICustomAlbumStore.cs` | 相册持久化抽象，定义读取、保存等能力；具体 JSON 实现在 Infrastructure。 |

### 3.2 图库布局

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.Core/Browsing/Treemap/TreemapModels.cs` | 空间树、等高布局和命中区域使用的纯数据模型，例如边界、权重和布局结果。 |
| `src/HanabePhotoManager.Core/Browsing/Treemap/SquarifiedTreemapLayout.cs` | Squarified Treemap 布局算法，根据权重把容器切分为尽量接近正方形的区域。 |
| `src/HanabePhotoManager.Core/Browsing/Treemap/JustifiedGalleryLayout.cs` | 等高照片墙算法，按宽高比组织行并计算每张照片的最终矩形。 |
| `src/HanabePhotoManager.Core/Browsing/Treemap/PanoramaPhotoLayout.cs` | 低缩放级别的密集全景布局，避免大量普通卡片缩小后形成视觉噪声。 |

### 3.3 导入领域

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.Core/Imports/Models.cs` | 导入源、媒体组、目标、分类、计划项和结果等共享领域记录，是导入链路的数据合同。 |
| `src/HanabePhotoManager.Core/Imports/IFileHasher.cs` | 文件哈希能力抽象，使导入规则不依赖具体 SHA-256 实现。 |
| `src/HanabePhotoManager.Core/Imports/CameraFolderDateResolver.cs` | 从相机目录结构和文件信息推导目标日期，处理无法直接读取拍摄时间的来源。 |
| `src/HanabePhotoManager.Core/Imports/LibraryRootNormalizer.cs` | 规范化并验证照片库根目录，避免路径尾分隔符、相对路径和大小写造成重复身份。 |
| `src/HanabePhotoManager.Core/Imports/MediaClassifier.cs` | 按扩展名和规则判断 RAW、JPG、视频、修后素材等导入分类。 |
| `src/HanabePhotoManager.Core/Imports/MediaGroupBuilder.cs` | 把同名 RAW/JPG/XML 等相关文件组合为媒体组，保证配对文件按同一业务单位处理。 |
| `src/HanabePhotoManager.Core/Imports/ImportNamingFormatter.cs` | 根据命名模板、序号、原文件名和日期生成导入后的文件名。 |
| `src/HanabePhotoManager.Core/Imports/ImportPlanBuilder.cs` | 把来源媒体组、目标日期、分类和命名规则转换为可执行导入计划，不直接写磁盘。 |
| `src/HanabePhotoManager.Core/Imports/ImportProgress.cs` | 导入进度快照与统计模型，统一当前项、完成数、总数、成功/跳过/失败等语义。 |

### 3.4 性能与语义搜索合同

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.Core/Performance/PreviewLoadingPolicy.cs` | 根据视口、预取范围和并发上限决定缩略图加载优先级。 |
| `src/HanabePhotoManager.Core/Performance/ThrottledProgress.cs` | 对高频进度通知节流，减少 UI 线程刷新压力，同时保证最终状态送达。 |
| `src/HanabePhotoManager.Core/Search/SemanticSearchModels.cs` | 语义索引条目、查询结果和模型状态等跨层数据合同。 |
| `src/HanabePhotoManager.Core/Search/ISemanticIndexStore.cs` | 语义向量索引的存取接口；Infrastructure 使用 SQLite 实现。 |
| `src/HanabePhotoManager.Core/Search/ISemanticSearchService.cs` | 图片/文本编码和相似度检索能力接口，使 App 不绑定具体 CLIP 运行时。 |

## 4. Infrastructure：逐文件说明

### 4.1 项目与相册持久化

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.Infrastructure/HanabePhotoManager.Infrastructure.csproj` | Infrastructure 项目定义；引用 Core，并声明 SQLite、ONNX 等外部实现依赖。 |
| `src/HanabePhotoManager.Infrastructure/Albums/JsonCustomAlbumStore.cs` | `ICustomAlbumStore` 的 JSON 实现，负责相册配置读写、兼容空文件和安全发布。 |

### 4.2 文件系统与导入安全

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.Infrastructure/Files/Sha256FileHasher.cs` | `IFileHasher` 的流式 SHA-256 实现，用于查重和传输复验。 |
| `src/HanabePhotoManager.Infrastructure/Files/DestinationProbe.cs` | 检查目标路径容量、可写性、冲突和父目录状态，在执行导入前尽早报告问题。 |
| `src/HanabePhotoManager.Infrastructure/Files/LibraryDirectoryInitializer.cs` | 创建照片库需要的标准目录结构，保证后续分类路径存在。 |
| `src/HanabePhotoManager.Infrastructure/Files/RetouchedDirectoryPolicy.cs` | 定义“修后”目录识别及只读保护策略，防止误覆盖成片。 |
| `src/HanabePhotoManager.Infrastructure/Files/VerifiedFileTransfer.cs` | 导入传输核心：写临时文件、计算哈希、复验目标、原子发布，并在失败时回滚。 |
| `src/HanabePhotoManager.Infrastructure/Files/JsonImportJournal.cs` | 保存未完成导入的逐项日志，使应用中断后可以判断并恢复安全步骤。 |
| `src/HanabePhotoManager.Infrastructure/Files/LibraryContentScanner.cs` | 枚举照片库媒体文件并输出统计/快照；处理扩展名、不可访问目录和取消。 |
| `src/HanabePhotoManager.Infrastructure/Files/LibraryResequenceService.cs` | 按命名规则重新编号图库文件，维护 RAW/JPG 等配对关系并避免命名碰撞。 |
| `src/HanabePhotoManager.Infrastructure/Files/PersistentAssetStore.cs` | 把应用需要长期引用的用户资源复制到应用数据目录，避免原路径失效。 |

### 4.3 CLIP 与语义索引

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.Infrastructure/Search/ModelCatalog.cs` | 集中描述模型文件名、输入输出节点和本地查找路径，避免推理代码散落硬编码。 |
| `src/HanabePhotoManager.Infrastructure/Search/ClipImagePreprocessor.cs` | 将图片缩放、裁剪、归一化并转换成 CLIP ONNX 所需张量。 |
| `src/HanabePhotoManager.Infrastructure/Search/ClipTokenizer.cs` | 对搜索文本分词、添加特殊标记并生成固定长度 token 序列。 |
| `src/HanabePhotoManager.Infrastructure/Search/ClipSemanticSearchService.cs` | 管理图片/文本 ONNX 会话、生成向量、归一化并计算相似度；实现 Core 搜索接口。 |
| `src/HanabePhotoManager.Infrastructure/Search/SqliteSemanticIndexStore.cs` | 使用 SQLite 保存媒体路径、时间和向量数据，串行化数据库访问并支持增量索引。 |

## 5. App：启动、Shell 与通用组件

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/HanabePhotoManager.App.csproj` | WPF 可执行项目定义；声明图片、模型、WebView2、LibVLC、主题资源和发布属性。修改依赖或资源复制规则时重点检查。 |
| `src/HanabePhotoManager.App/AssemblyInfo.cs` | WPF 程序集级元数据和主题/资源查找声明。 |
| `src/HanabePhotoManager.App/App.xaml` | 应用全局 ResourceDictionary 组合入口，按顺序加载颜色、Token、控件样式和默认主题。加载顺序会影响资源覆盖。 |
| `src/HanabePhotoManager.App/App.xaml.cs` | 应用入口：单实例、启动参数、异常日志、截图模式和主窗口生命周期。 |
| `src/HanabePhotoManager.App/MainWindow.xaml` | 应用主 Shell 和绝大多数主页面布局；包含导航、主页、导入、图库、人物、Inspector、状态栏及命令绑定。 |
| `src/HanabePhotoManager.App/MainWindow.xaml.cs` | 主窗口视图适配：窗口控制、导航动画、拖放、图库选择、框选、缩放、键盘和截图；业务状态应留在 ViewModel。 |
| `src/HanabePhotoManager.App/DeleteConfirmationWindow.xaml` | 删除确认窗口布局，显示选中组数和实际文件数量。 |
| `src/HanabePhotoManager.App/DeleteConfirmationWindow.xaml.cs` | 设置删除提示内容、模态结果和无边框窗口拖动。 |
| `src/HanabePhotoManager.App/RemarkPromptWindow.xaml` | 日期目录备注输入对话框布局。 |
| `src/HanabePhotoManager.App/RemarkPromptWindow.xaml.cs` | 初始化日期文本并返回清理后的备注或跳过结果。 |
| `src/HanabePhotoManager.App/CategoryEqualsConverter.cs` | 单值分类相等转换器，供 XAML 根据当前分类切换可见性或选中状态。 |
| `src/HanabePhotoManager.App/CategoryEqualityMultiConverter.cs` | 多绑定分类比较转换器，处理当前值和目标值来自不同 Binding 的情况。 |
| `src/HanabePhotoManager.App/ExtensionBadgeConverter.cs` | 将文件扩展名转换成图库卡片徽标文字/状态。 |
| `src/HanabePhotoManager.App/FileSizeConverter.cs` | 将字节数格式化为 KB、MB、GB 等易读文本。 |
| `src/HanabePhotoManager.App/NullToVisibilityConverter.cs` | 将 null/非 null 转换成 WPF Visibility，支持 Inspector 和空状态切换。 |
| `src/HanabePhotoManager.App/PathThumbnailConverter.cs` | 从文件路径生成可绑定缩略图，主要用于简单列表；高性能图库使用专门加载器。 |
| `src/HanabePhotoManager.App/Collections/RangeObservableCollection.cs` | 为 ObservableCollection 增加批量更新能力，降低一次加入大量媒体时的通知开销。 |
| `src/HanabePhotoManager.App/Controls/VirtualizingWrapPanel.cs` | 支持 `IScrollInfo` 的虚拟化换行面板，只实现视口附近项目，供人物照片等大量卡片列表使用。 |

## 6. App：图库浏览与查看器

### 6.1 网格和空间树

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Browsing/Grid/GalleryZoomPolicy.cs` | 把 Ctrl+滚轮输入转换成受限缩略图尺寸，并计算以指针为锚点的滚动补偿。 |
| `src/HanabePhotoManager.App/Browsing/Grid/UniformSquarePanel.cs` | 图库方形网格的自定义布局/虚拟化面板，按卡片尺寸和可用宽度计算列与可见范围。 |
| `src/HanabePhotoManager.App/Browsing/Treemap/ImageDimensionReader.cs` | 快速读取图片尺寸和方向，为等高布局提供宽高比，避免完整解码大图。 |
| `src/HanabePhotoManager.App/Browsing/Treemap/TreemapItemViewModel.cs` | 空间树节点的 UI 模型，保存层级、权重、路径、缩略图和选中状态。 |
| `src/HanabePhotoManager.App/Browsing/Treemap/ProgressiveTreemapViewModel.cs` | 逐步构建/加载空间树节点并发布进度，避免大图库一次性阻塞 UI。 |
| `src/HanabePhotoManager.App/Browsing/Treemap/PhotoTreemapControl.cs` | 自绘图库控件：空间树、等高墙、全景语义缩放、可见项上报和鼠标命中；布局结果有缓存。 |

### 6.2 查看器

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/PhotoViewerWindow.xaml` | 无边框照片/视频查看器布局，包含媒体区域、浮动工具栏、播放控制和信息面板。 |
| `src/HanabePhotoManager.App/PhotoViewerWindow.xaml.cs` | 照片缩放平移、LibVLC 播放、全屏、沉浸工具栏、HwndHost 遮挡规避和截图适配。 |
| `src/HanabePhotoManager.App/ViewModels/PhotoViewerViewModel.cs` | 管理当前媒体、上一张/下一张、元数据、旋转/评分等查看器状态和命令。 |
| `src/HanabePhotoManager.App/Services/PhotoViewportMath.cs` | 纯数学计算照片适应窗口、缩放中心和平移边界，便于脱离 WPF 测试。 |
| `src/HanabePhotoManager.App/Services/ShellThumbnailProvider.cs` | 调用 Windows Shell/解码器获取图片、RAW 和视频缩略图，并处理缓存与降级占位。 |
| `src/HanabePhotoManager.App/Services/PhotoDetailMetadataReader.cs` | 读取 Inspector/查看器需要的尺寸、EXIF、相机、镜头、曝光、时间和 GPS 信息。 |

## 7. App：自定义相册

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Albums/CustomAlbumItemViewModel.cs` | 单个自定义相册卡片的状态、封面、数量、选中和重命名命令。 |
| `src/HanabePhotoManager.App/Albums/CustomAlbumPhotoScanner.cs` | 枚举相册引用文件夹中的媒体，生成照片列表和封面候选。 |
| `src/HanabePhotoManager.App/Albums/CustomAlbumsViewModel.cs` | 加载、添加、刷新、重命名和移除相册引用，协调 Core 模型与持久化 Store。 |
| `src/HanabePhotoManager.App/Albums/CustomAlbumsPage.xaml` | 自定义相册总览、详情、网格/列表和右侧信息区域布局。 |
| `src/HanabePhotoManager.App/Albums/CustomAlbumsPage.xaml.cs` | 文件夹选择、卡片点击、详情切换、照片选择和元数据读取等视图适配。 |

## 8. App：导入与重复处理

### 8.1 导入入口

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Imports/ImportSourcePicker.cs` | 封装 Windows 单文件夹/多文件夹选择器，返回规范化且去重的来源目录。 |
| `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.Import.cs` | 主 ViewModel 的导入 partial：多文件夹选择、拖入来源和散文件兼容入口。 |
| `src/HanabePhotoManager.App/Services/ImportResumeStore.cs` | 在应用数据目录保存可恢复导入信息，并验证日志仍对应当前照片库。 |

### 8.2 重复决策

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Duplicates/DuplicateCandidateGroup.cs` | 一组完全重复或视觉相似候选的 UI 数据模型。 |
| `src/HanabePhotoManager.App/Duplicates/DuplicateReviewWindow.xaml` | 图库重复扫描后的分组复核界面。 |
| `src/HanabePhotoManager.App/Duplicates/DuplicateReviewWindow.xaml.cs` | 动态构建候选组、预览文件并收集用户选择的删除路径。 |
| `src/HanabePhotoManager.App/Duplicates/ImportDuplicateDecisionPolicy.cs` | 单个重复项的默认决策与允许动作规则。 |
| `src/HanabePhotoManager.App/Duplicates/ImportDuplicateDecisionWindow.xaml` | 单个待导入文件与现有文件的对比窗口。 |
| `src/HanabePhotoManager.App/Duplicates/ImportDuplicateDecisionWindow.xaml.cs` | 加载双方预览并返回跳过或仍导入决定。 |
| `src/HanabePhotoManager.App/Duplicates/ImportDuplicateBatchDecisionPolicy.cs` | 批量重复项统一处理的选择规则。 |
| `src/HanabePhotoManager.App/Duplicates/ImportDuplicateBatchDecisionWindow.xaml` | 批量重复项摘要与三种处理方式界面。 |
| `src/HanabePhotoManager.App/Duplicates/ImportDuplicateBatchDecisionWindow.xaml.cs` | 限量展示匹配清单并返回全部跳过、全部导入或逐项确认。 |

## 9. App：主 ViewModel 与通用状态

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs` | 应用核心编排器：导航、设置、设备检测、图库扫描/筛选/分组、缩略图、导入执行、Inspector、主页和后台状态。体积最大，修改前必须锁定具体区域。 |
| `src/HanabePhotoManager.App/Models/LibraryDateSnapshot.cs` | 日期目录扫描结果和增量身份模型，供图库判断某日内容是否变化。 |
| `src/HanabePhotoManager.App/Models/MediaMetadata.cs` | UI 使用的结构化媒体元数据模型，汇总文件、拍摄参数、时间和位置。 |
| `src/HanabePhotoManager.App/Navigation/NavigationDisplayMode.cs` | 一级导航显示文字、图标或图标加文字的枚举。 |
| `src/HanabePhotoManager.App/Navigation/NavigationItemViewModel.cs` | 单个导航入口的标题、图标、页面键、选中和可见状态。 |
| `src/HanabePhotoManager.App/Navigation/NavigationOrderPolicy.cs` | 默认导航排序及用户自定义排序的合并/修复规则。 |
| `src/HanabePhotoManager.App/ReleaseNotes/ReleaseNotesViewModel.cs` | 版本更新说明和首次展示状态，为新版本提示提供数据。 |
| `src/HanabePhotoManager.App/ViewModels/TagManagerViewModel.cs` | 标签集合、添加/删除及媒体标签分配状态。 |

## 10. App：人物、人脸与照片分析

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/ViewModels/PeopleAlbumViewModel.cs` | 人物相册扫描、列表、选择、改名、合并、人物照片虚拟化和进度状态。 |
| `src/HanabePhotoManager.App/ViewModels/FaceSearchViewModel.cs` | 以参考脸查找相似照片的选择、阈值、结果和取消命令。 |
| `src/HanabePhotoManager.App/ViewModels/PhotoAnalysisViewModel.cs` | 批量照片分类/分析任务的队列、进度、检查点和结果状态。 |
| `src/HanabePhotoManager.App/People/MergePersonDialog.xaml` | 选择人物合并目标的模态列表布局。 |
| `src/HanabePhotoManager.App/People/MergePersonDialog.xaml.cs` | 填充候选人物，处理双击/确认并返回目标人物。 |
| `src/HanabePhotoManager.App/Services/FaceRecognitionModels.cs` | 人脸框、关键点、嵌入向量和识别结果等内部数据模型。 |
| `src/HanabePhotoManager.App/Services/FaceRecognitionEngineFactory.cs` | 根据可用模型和配置创建检测/识别引擎，集中处理模型组合选择。 |
| `src/HanabePhotoManager.App/Services/FaceRecognitionRuntime.cs` | 管理 YuNet/SFace 或兼容 ONNX 会话，执行检测、对齐和向量提取。 |
| `src/HanabePhotoManager.App/Services/LocalFaceEmbeddingService.cs` | 对照片批量提取本地人脸嵌入，控制并发、失败隔离和缓存。 |
| `src/HanabePhotoManager.App/Services/LocalPersonClusterer.cs` | 按向量距离把人脸聚类成人物，并保留可调阈值和未确认结果。 |
| `src/HanabePhotoManager.App/Services/PeopleAlbumService.cs` | 人物相册的磁盘扫描、聚类结果持久化、改名、合并和照片索引协调。 |
| `src/HanabePhotoManager.App/Services/FaceSearchService.cs` | 从参考图片生成人脸向量并与图库索引比较，返回相似度排序结果。 |
| `src/HanabePhotoManager.App/Services/IPhotoClassifier.cs` | 照片分类器统一接口，隔离规则分类与 ONNX 分类实现。 |
| `src/HanabePhotoManager.App/Services/RuleBasedPhotoClassifier.cs` | 不依赖模型的规则分类降级方案，根据路径、扩展名和基础元数据判断类别。 |
| `src/HanabePhotoManager.App/Services/OnnxPhotoClassifier.cs` | 通用 ONNX 图像分类器，负责预处理、推理和标签映射。 |
| `src/HanabePhotoManager.App/Services/MobileClipPhotoClassifier.cs` | 使用 MobileCLIP 图片向量和预计算标签向量完成零样本分类。 |
| `src/HanabePhotoManager.App/Services/PhotoClassifierFactory.cs` | 根据模型可用性和用户设置选择 MobileCLIP、ONNX 或规则分类器。 |
| `src/HanabePhotoManager.App/Services/PhotoAnalysisCheckpointStore.cs` | 保存批量分析已处理位置和结果身份，支持暂停后增量继续。 |
| `src/HanabePhotoManager.App/Models/MobileCLIP/label_embeddings.json` | MobileCLIP 分类标签及预计算嵌入数据；更换模型时必须同步维度和标签含义。 |

## 11. App：地图照片

### 11.1 WPF 与 ViewModel

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Map/MapPage.xaml` | 地图 WebView2、加载/错误状态、地点照片和手动位置标记 Inspector 布局。 |
| `src/HanabePhotoManager.App/Map/MapPage.xaml.cs` | 初始化 WebView2、加载 Leaflet、发送标记、接收地图点击消息和提供缩略图。 |
| `src/HanabePhotoManager.App/ViewModels/MapPhotosViewModel.cs` | 地图扫描检查点、已定位媒体、聚合点、当前地点、批量选中和写入位置命令。 |
| `src/HanabePhotoManager.App/Services/ExifLocationReader.cs` | 从 EXIF 读取 GPS 经纬度并处理方向、分数值和无位置情况。 |
| `src/HanabePhotoManager.App/Services/PhotoLocationService.cs` | 读取或写入应用管理的位置元数据，为手动标记提供持久化入口。 |
| `src/HanabePhotoManager.App/Services/MapMediaSourceService.cs` | 增量枚举照片库媒体、读取位置并保存扫描游标，避免每次从头扫描。 |
| `src/HanabePhotoManager.App/Services/MapThumbnailCache.cs` | 地图弹窗缩略图的内存/磁盘缓存，控制尺寸和重复解码。 |

### 11.2 内嵌 Leaflet 前端

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Map/assets/index.html` | WebView2 地图宿主页，加载 Leaflet、地图样式和桥接脚本。 |
| `src/HanabePhotoManager.App/Map/assets/leaflet.js` | 随应用分发的 Leaflet 运行库，提供地图、图层、标记和交互 API。 |
| `src/HanabePhotoManager.App/Map/assets/leaflet.css` | Leaflet 官方基础样式，定义瓦片、控件、弹窗等结构。 |
| `src/HanabePhotoManager.App/Map/assets/map.js` | Hanabe 地图逻辑：接收 WPF 标记数据、聚合、选择地点并回传点击事件。 |
| `src/HanabePhotoManager.App/Map/assets/map.css` | Hanabe 地图标记、聚合徽标、弹窗和深浅外观样式。 |

## 12. App：压缩、拼图、像素画与水印

### 12.1 图片小工具入口与压缩/拼图

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Compression/CompressionPage.xaml` | 图片工具首页和压缩/拼图工作区布局，绑定参数、队列、结果和进度。 |
| `src/HanabePhotoManager.App/Compression/CompressionPage.xaml.cs` | 选择输入/输出目录、响应工具深链和视图切换。 |
| `src/HanabePhotoManager.App/ViewModels/CompressionViewModel.cs` | 压缩与拼图参数、输入集合、任务取消、进度、输出统计和结果命令。 |
| `src/HanabePhotoManager.App/Compression/ImageInputDiscovery.cs` | 从文件和文件夹发现支持的图片输入，去重并跳过不可读项。 |
| `src/HanabePhotoManager.App/Compression/ImageCompressionPlanner.cs` | 根据尺寸、质量、格式和输出策略生成每个文件的压缩计划。 |
| `src/HanabePhotoManager.App/Compression/ImageCompressionService.cs` | 执行图片解码、缩放、编码和安全写出，汇报批量进度与失败。 |
| `src/HanabePhotoManager.App/Compression/ImageCollageService.cs` | 计算拼图网格/画布并合成多张图片，处理间距、背景和输出格式。 |

### 12.2 像素画

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/PixelArt/PixelArtRenderer.cs` | 像素化、调色板量化和网格渲染算法，输出可冻结的 WPF 位图。 |
| `src/HanabePhotoManager.App/PixelArt/PixelArtViewModel.cs` | 源图片、像素尺寸、调色板、预览、生成与导出命令状态。 |
| `src/HanabePhotoManager.App/PixelArt/PixelArtView.xaml` | 像素画参数面板、原图/结果预览和导出按钮布局。 |
| `src/HanabePhotoManager.App/PixelArt/PixelArtView.xaml.cs` | 打开图片与保存 PNG 的系统对话框适配。 |

### 12.3 水印

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Watermark/WatermarkViewModel.cs` | 水印输入、位置、透明度、缩放、批处理目录、预览和导出命令。 |
| `src/HanabePhotoManager.App/Watermark/WatermarkPage.xaml` | 单图/批量水印编辑器布局，包含拖放区、九宫格定位、预览和输出选项。 |
| `src/HanabePhotoManager.App/Watermark/WatermarkPage.xaml.cs` | 文件拖放、Shift+PNG 更换水印、预览拖动定位和导出对话框。 |
| `src/HanabePhotoManager.App/Watermark/WatermarkInputDiscovery.cs` | 发现并去重支持水印处理的图片输入。 |
| `src/HanabePhotoManager.App/Watermark/WatermarkLayoutCalculator.cs` | 根据画布、图片、水印比例和归一化坐标计算最终水印矩形。 |
| `src/HanabePhotoManager.App/Watermark/WatermarkExportService.cs` | 合成水印并安全输出单图/列表任务，提供取消和逐项结果。 |
| `src/HanabePhotoManager.App/Watermark/WatermarkFolderBatchService.cs` | 保留来源目录结构的批量水印扫描与导出流程。 |
| `src/HanabePhotoManager.App/Watermark/BooleanNotConverter.cs` | 布尔取反转换器，用于互斥控件的 IsEnabled/Visibility Binding。 |

## 13. App：语义搜索

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Search/SemanticSearchView.xaml` | 搜索框、索引状态、取消/重建按钮和结果列表布局。 |
| `src/HanabePhotoManager.App/Search/SemanticSearchView.xaml.cs` | 结果双击打开媒体的 WPF 输入适配。 |
| `src/HanabePhotoManager.App/Search/SemanticSearchViewModel.cs` | 查询防抖、索引进度、取消、结果集合和模型不可用提示。 |
| `src/HanabePhotoManager.App/Search/SearchResultItemViewModel.cs` | 单条搜索结果的路径、分数、缩略图和打开动作。 |
| `src/HanabePhotoManager.App/Search/SemanticBrowseRanking.cs` | 把语义得分与文件名/路径匹配组合成图库浏览排序。 |

## 14. App：设置、主题与桌面集成服务

### 14.1 设置页面

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/SettingsCenterPage.xaml` | 设置二级导航、外观、常规、照片库与导入、浏览、高级及右侧主题/存储信息布局。 |
| `src/HanabePhotoManager.App/SettingsCenterPage.xaml.cs` | 同步导航显示模式、主题预览、二级菜单滚动和赞助链接等视图交互。 |

### 14.2 配置与桌面集成

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Services/AppDataPaths.cs` | 集中计算设置、缓存、索引、人物数据等应用数据路径，避免个人绝对路径散落。 |
| `src/HanabePhotoManager.App/Services/AppSettingsStore.cs` | 设置读取、兼容迁移和原子保存；承载照片库、主题、浏览和导入偏好。 |
| `src/HanabePhotoManager.App/Services/StartupRegistrationService.cs` | 通过 Windows 注册表管理开机启动。 |
| `src/HanabePhotoManager.App/Services/WindowsWallpaperService.cs` | 调用 Windows API 把选中照片设为桌面壁纸。 |
| `src/HanabePhotoManager.App/Services/RecycleBinFileService.cs` | 使用 Windows Shell 将文件移入回收站，而非直接永久删除。 |

### 14.3 图库和资料维护服务

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Services/BrowseStatePolicy.cs` | 浏览模式、筛选器、日期选择和默认值之间的状态归一化规则。 |
| `src/HanabePhotoManager.App/Services/EdgeAutoScrollPolicy.cs` | 框选接近视口边缘时计算自动滚动方向和速度。 |
| `src/HanabePhotoManager.App/Services/LibraryDateFolderService.cs` | 发现并解析日期目录，支持“解析后日期标题”和原文件夹名称显示。 |
| `src/HanabePhotoManager.App/Services/LibraryDateSnapshotService.cs` | 为每个日期目录生成可比较快照，支持图库增量刷新而非全量重扫。 |
| `src/HanabePhotoManager.App/Services/LibraryMaintenanceService.cs` | 协调重复扫描、重新编号和其他需要用户确认的图库维护操作。 |
| `src/HanabePhotoManager.App/Services/MediaMetadataStore.cs` | 保存评分、标签、分类、备注和位置等应用侧元数据。 |
| `src/HanabePhotoManager.App/Services/RetouchedMediaIndex.cs` | 建立修后文件与原始媒体的对应索引，供筛选和配对状态显示。 |

### 14.4 主题

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Services/ThemeManager.cs` | 加载 4 套配色 × 明暗主题入口、保持资源键一致并持久化当前选择。 |
| `src/HanabePhotoManager.App/Services/ThemeTransitionService.cs` | 从点击位置向外扩散圆形遮罩完成主题切换，并保证动画可降级。 |

## 15. App：主题与设计系统逐文件说明

### 15.1 颜色与画刷

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Themes/Colors/Colors.Classic.Light.xaml` | 经典黑白浅色方案的原始 Color Token。 |
| `src/HanabePhotoManager.App/Themes/Colors/Colors.Classic.Dark.xaml` | 经典黑白深色方案的原始 Color Token。 |
| `src/HanabePhotoManager.App/Themes/Colors/Colors.Dynamic.Light.xaml` | 动态色彩浅色方案的 M3 primary、surface、outline 等颜色值。 |
| `src/HanabePhotoManager.App/Themes/Colors/Colors.Dynamic.Dark.xaml` | 动态色彩深色方案，与浅色保持完全相同的键集合。 |
| `src/HanabePhotoManager.App/Themes/Colors/Colors.Forest.Light.xaml` | 森林绿浅色方案 Color Token。 |
| `src/HanabePhotoManager.App/Themes/Colors/Colors.Forest.Dark.xaml` | 森林绿深色方案 Color Token。 |
| `src/HanabePhotoManager.App/Themes/Colors/Colors.Violet.Light.xaml` | 紫罗兰浅色方案 Color Token。 |
| `src/HanabePhotoManager.App/Themes/Colors/Colors.Violet.Dark.xaml` | 紫罗兰深色方案 Color Token。 |
| `src/HanabePhotoManager.App/Themes/Colors/Colors.ThemeSwatches.xaml` | 设置页主题色卡预览专用颜色，避免在页面 XAML 内硬编码。 |
| `src/HanabePhotoManager.App/Themes/Colors/Brushes.Light.xaml` | 把浅色 Color Token 映射成页面消费的语义 Brush，并提供浅色应用图标。 |
| `src/HanabePhotoManager.App/Themes/Colors/Brushes.Dark.xaml` | 深色语义 Brush 映射；键必须与浅色文件逐项一致。 |

### 15.2 尺寸、字体、图标和动画 Token

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Themes/Tokens/Spacing.xaml` | 全局间距和 Padding 数值 Token，统一桌面信息密度。 |
| `src/HanabePhotoManager.App/Themes/Tokens/Radius.xaml` | 控件、卡片、容器和全圆角的 CornerRadius Token。 |
| `src/HanabePhotoManager.App/Themes/Tokens/Sizing.xaml` | 导航栏、Inspector、控件高度等公共尺寸。 |
| `src/HanabePhotoManager.App/Themes/Tokens/Shadows.xaml` | 浮层、对话框等有限场景使用的阴影效果资源。 |
| `src/HanabePhotoManager.App/Themes/Tokens/Icons.xaml` | 所有导航和操作图标的 Geometry 数据；修改时保持统一坐标和笔画风格。 |
| `src/HanabePhotoManager.App/Themes/Typography/FontFamilies.xaml` | UI 与等宽技术信息使用的字体族资源。 |
| `src/HanabePhotoManager.App/Themes/Typography/TypeScale.xaml` | 标题、正文、标签、说明等字号和字重层级。 |
| `src/HanabePhotoManager.App/Themes/Motion/Durations.xaml` | Fast、Normal、Slow 等共享动画时长。 |
| `src/HanabePhotoManager.App/Themes/Motion/Easings.xaml` | 页面切换、展开和微交互复用的缓动函数。 |

### 15.3 共享控件样式

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Themes/Controls/Buttons.xaml` | Primary、Secondary、Ghost、Toolbar、FAB 等按钮模板和交互状态。 |
| `src/HanabePhotoManager.App/Themes/Controls/Inputs.xaml` | TextBox、ComboBox、Slider 等输入控件的统一外观。 |
| `src/HanabePhotoManager.App/Themes/Controls/Selection.xaml` | Switch、CheckBox、RadioButton 和选中指示的共享模板。 |
| `src/HanabePhotoManager.App/Themes/Controls/Cards.xaml` | 普通卡片和语义小卡片样式；不应被滥用于包裹所有页面区域。 |
| `src/HanabePhotoManager.App/Themes/Controls/Dialogs.xaml` | 模态窗口背景、标题、正文和操作区样式。 |
| `src/HanabePhotoManager.App/Themes/Controls/Sidebar.xaml` | 一级导航 Rail/侧栏容器基础样式。 |
| `src/HanabePhotoManager.App/Themes/Controls/Navigation.xaml` | 导航项、设置二级菜单和选中/悬停状态模板。 |
| `src/HanabePhotoManager.App/Themes/Controls/Toolbars.xaml` | 页面和查看器工具栏、胶囊操作区样式。 |
| `src/HanabePhotoManager.App/Themes/Controls/Lists.xaml` | ListBox、ListView 和列表项交互状态。 |
| `src/HanabePhotoManager.App/Themes/Controls/Menus.xaml` | ContextMenu、MenuItem 和弹出菜单样式。 |
| `src/HanabePhotoManager.App/Themes/Controls/ScrollBars.xaml` | 横纵滚动条轨道、滑块和深浅主题状态。 |
| `src/HanabePhotoManager.App/Themes/Controls/Status.xaml` | 成功、警告、错误、信息徽标和状态条样式。 |
| `src/HanabePhotoManager.App/Themes/Controls/Inspector.xaml` | 右侧 Inspector 面板、字段标签和值的排版样式。 |
| `src/HanabePhotoManager.App/Themes/Controls/Layout.xaml` | 页面主容器、Shell 面板和常用布局样式。 |

### 15.4 主题组合入口

以下文件自身只组合 ResourceDictionary；新增或移除共享字典时必须同步八个入口，避免某一主题运行时缺资源。

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/Themes/Themes/Classic.Light.xaml` | 经典浅色主题组合入口。 |
| `src/HanabePhotoManager.App/Themes/Themes/Classic.Dark.xaml` | 经典深色主题组合入口。 |
| `src/HanabePhotoManager.App/Themes/Themes/Dynamic.Light.xaml` | 动态浅色主题组合入口。 |
| `src/HanabePhotoManager.App/Themes/Themes/Dynamic.Dark.xaml` | 动态深色主题组合入口。 |
| `src/HanabePhotoManager.App/Themes/Themes/Forest.Light.xaml` | 森林绿浅色主题组合入口。 |
| `src/HanabePhotoManager.App/Themes/Themes/Forest.Dark.xaml` | 森林绿深色主题组合入口。 |
| `src/HanabePhotoManager.App/Themes/Themes/Violet.Light.xaml` | 紫罗兰浅色主题组合入口。 |
| `src/HanabePhotoManager.App/Themes/Themes/Violet.Dark.xaml` | 紫罗兰深色主题组合入口。 |

## 16. App：历史/内嵌网页文件

| 文件 | 作用与主要内容 |
|---|---|
| `src/HanabePhotoManager.App/ms.html` | ModelScope 模型列表网页的离线抓取快照；不是当前 WPF 主界面。体积大，若确认无项目引用可作为后续清理候选。 |
| `src/HanabePhotoManager.App/sf.html` | SiliconFlow 模型中心网页抓取快照；同样属于历史/调研资料，清理前应先检查 csproj 和运行时引用。 |

## 17. 按功能快速定位

| 想修改的功能 | 首先阅读 | 继续追踪 |
|---|---|---|
| 主界面布局/导航 | `MainWindow.xaml`、`MainWindow.xaml.cs` | `MainWindowViewModel.cs`、`Navigation/*`、`Themes/Controls/Navigation.xaml` |
| 图库自然滚动/展开 | `MainWindowViewModel.cs`、`MainWindow.xaml` | `UniformSquarePanel.cs`、`LibraryDateSnapshotService.cs` |
| 缩略图大小与锚点缩放 | `GalleryZoomPolicy.cs` | `MainWindow.xaml.cs`、`UniformSquarePanel.cs` |
| 空间树浏览 | `PhotoTreemapControl.cs` | Core `Browsing/Treemap/*`、`ProgressiveTreemapViewModel.cs` |
| 单击 Inspector | `MainWindow.xaml.cs`、`MainWindowViewModel.cs` | `PhotoDetailMetadataReader.cs`、`MediaMetadata.cs` |
| 双击查看照片/视频 | `PhotoViewerWindow.xaml.cs` | `PhotoViewerViewModel.cs`、`ShellThumbnailProvider.cs` |
| 多文件夹导入 | `ImportSourcePicker.cs`、`MainWindowViewModel.Import.cs` | `MediaGroupBuilder.cs`、`ImportPlanBuilder.cs` |
| 导入速度/安全 | `VerifiedFileTransfer.cs` | `Sha256FileHasher.cs`、`JsonImportJournal.cs` |
| 导入重复处理 | `ImportDuplicate*` | `DuplicateReviewWindow*`、`LibraryMaintenanceService.cs` |
| 人物相册 | `PeopleAlbumViewModel.cs` | `PeopleAlbumService.cs`、`FaceRecognitionRuntime.cs` |
| 地图扫描续传 | `MapPhotosViewModel.cs`、`MapMediaSourceService.cs` | `MapPage.xaml.cs`、`map.js` |
| 压缩/拼图 | `CompressionViewModel.cs` | `ImageCompression*`、`ImageCollageService.cs` |
| 水印 | `WatermarkViewModel.cs`、`WatermarkPage.xaml` | `WatermarkExportService.cs`、`WatermarkLayoutCalculator.cs` |
| 深浅主题及圆形扩散 | `ThemeManager.cs`、`ThemeTransitionService.cs` | `Themes/Colors/*`、`Themes/Themes/*` |
| 设置保存失败 | `AppSettingsStore.cs`、`AppDataPaths.cs` | `SettingsCenterPage.xaml`、主 ViewModel 设置属性 |

## 18. 修改代码的标准步骤

1. 从上表找到功能入口，再沿 Binding/Command 或方法调用向下追踪。
2. 先阅读对应测试，记录当前输入、输出、副作用和失败语义。
3. 判断修改归属：纯规则进 Core，外部系统实现进 Infrastructure，界面状态与桌面适配进 App。
4. 保留公开 API、设置字段、路径规则、Binding、Command、事件名和主题资源键。
5. 只做一个可独立验证的变化；不要同时清理无关模块。
6. 执行对应项目测试，最后运行完整 Release 构建与测试。
7. 更新 `docs/agent-change-log.md`、相关功能文档和本手册中已改变的文件说明。

## 19. 容易出错的边界

- `MainWindowViewModel.cs` 很大，但不能仅为减少行数机械拆分；先确定状态所有权和 partial 边界。
- WPF 代码后置中的 Dispatcher、视觉树、鼠标捕获和 WebView2 消息不能直接搬进 Core。
- `VerifiedFileTransfer` 的临时写入、哈希复验、原子发布、源文件删除顺序属于数据安全合同。
- RAW/JPG/XML 以媒体组处理；XML 不应成为图库独立卡片。
- `ThemeManager` 切换的所有主题必须拥有同名资源键；页面不要直接写主题色。
- 缩略图加载必须保持按视口优先和并发限制，不能因为动画阻塞滚动。
- 地图、人脸和照片分析的检查点是性能功能，修改扫描键或路径身份前要考虑旧数据兼容。
- `ms.html`、`sf.html` 是大型网页快照；删除前用 `rg` 和项目资源配置证明运行时未引用。

## 20. 验证命令

```powershell
dotnet restore HanabePhotoManager.sln
dotnet build HanabePhotoManager.sln -c Release /warnaserror
dotnet test HanabePhotoManager.sln -c Release --no-build
```

涉及界面时还应执行浅色/深色、目标页面、键盘、空状态、取消、失败恢复和大图库滚动检查。涉及发布内容或原生依赖时按 `docs/release.md` 构建新的隔离产物。
