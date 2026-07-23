# Hanabe Photo Manager — 技术审查报告

生成时间：2026-07-23T19:31:51.479+08:00

> 说明：本报告基于对仓库源码与模型/资产文件的静态审查生成。未修改任何代码，也未安装或运行任何依赖。所有结论基于可读到的文件内容；无法从源码直接确认的项已明确标注为“无法确认”。

---

## 目录

1. 解决方案概况
2. 项目目录结构
3. 当前架构
4. 已实现功能
5. AI 与模型相关内容
6. NuGet 与第三方依赖
7. 数据存储
8. 启动和依赖注入
9. 风险和问题
10. 新增 SigLIP 2 的建议

附录 A. 关键文件清单

附录 B. 当前项目结构树

附录 C. “新增 SigLIP 2 的最小改动方案”

附录 D. 仍然无法确认、需要人工补充的信息

---

## 1. 解决方案概况

- 仓库内主要项目（按物理路径）
  - src\HanabePhotoManager.App\HanabePhotoManager.App.csproj — 桌面应用（WPF + WinForms 支持）
  - src\HanabePhotoManager.Core\HanabePhotoManager.Core.csproj — 核心域/类型定义
  - src\HanabePhotoManager.Infrastructure\HanabePhotoManager.Infrastructure.csproj — 基础设施（文件/云/SQLite 等）
  - tests\* — 多个测试项目（App.Tests、Core.Tests、Infrastructure.Tests）

- 每个项目的职责
  - App：WPF 桌面应用层（视图、ViewModel、服务实现、模型文件/资产复制到输出），包含模型推理代码（MobileCLIP、mobilenet、SFace 等）与 JSON 持久化实现。
  - Core：定义域模型、接口、云相关基础类型（如 CloudObject 等）。
  - Infrastructure：外部系统集成、云索引存储（SqliteCloudIndexStore）、文件元数据读取（MetadataExtractor）等。

- 项目之间的引用关系
  - App 引用 Core 和 Infrastructure
  - Infrastructure 引用 Core

- .NET 版本 / 桌面框架 / 目标平台
  - App 项目 TargetFramework: net8.0-windows（UseWPF=true，UseWindowsForms=true）
  - Core / Infrastructure: net8.0
  - 输出类型为 WinExe（桌面 WPF 应用，目标平台 Windows）

- UI 框架：WPF（XAML，UseWPF=true）。


## 2. 项目目录结构（重点目录与用途）

- src\HanabePhotoManager.App\
  - App.xaml / App.xaml.cs — 应用入口（OnStartup 会加载 Theme）
  - MainWindow.xaml / MainWindowViewModel.cs — 主窗口与主 VM
  - Models/ — 各种模型和模型文件/资产
    - MediaMetadata.cs — 媒体元数据 snapshot / 条目结构（AutomaticLabels、PeopleIds、ExifLocation 等）
    - MobileCLIP/ — MobileCLIP 模型与 label_embeddings.json
    - Classification/ — mobilenet 模型与 imagenet_classes.txt
    - Face/ — 人脸相关模型（SFace、yunet、haar cascade xml）
  - Services/ — 业务与 AI 服务实现
    - MobileClipPhotoClassifier.cs、OnnxPhotoClassifier.cs、LocalFaceEmbeddingService.cs、FaceSearchService.cs、PhotoClassifierFactory.cs、MediaMetadataStore.cs、PeopleAlbumService.cs 等
  - ViewModels/ — 各页面的 ViewModel（PeopleAlbumViewModel、PhotoAnalysisViewModel、FaceSearchViewModel 等）
  - Compression、Watermark、Map 等功能模块

- src\HanabePhotoManager.Core\
  - 定义域模型、云抽象、接口和基础类型

- src\HanabePhotoManager.Infrastructure\
  - Cloud\SqliteCloudIndexStore.cs — 使用 Microsoft.Data.Sqlite 实现云索引持久化
  - 其他基础设施集成

- tests\* — 单元测试项目

说明：Media、PeopleAlbum、Face embeddings 等主要以 JSON 文件保存（见第 7 节）。


## 3. 当前架构与设计模式

- 主要架构模式
  - MVVM：WPF 的 View + ViewModel 明显采用 CommunityToolkit.Mvvm。
  - 分层架构：App（UI + 具体实现） ⇄ Core（域模型/接口） ⇄ Infrastructure（持久化/外部系统）。
  - DI 使用情况：项目引用 Microsoft.Extensions.DependencyInjection，但未找到集中式 AddSingleton/AddTransient 的注册点。多数服务通过 new 显式构造或静态工厂（PhotoClassifierFactory）创建。

- 各类放置位置
  - View：XAML（MainWindow.xaml 等）
  - ViewModel：src\HanabePhotoManager.App\ViewModels\*
  - Service：src\HanabePhotoManager.App\Services\*
  - Repository/持久化：MediaMetadataStore、PeopleAlbumService（JSON），Infrastructure 的 SqliteCloudIndexStore（SQLite）
  - Model：src\HanabePhotoManager.App\Models\*

- 跨层调用或职责混淆
  - 推理/模型加载实现集中在 App 层 Services 内（MobileCLIP、Onnx、SFace 等），若要将推理移到独立服务或 Infrastructure，需要重构。
  - 总体无严重跨层违规，但“将模型推理放在 App 层”不利于复用与分离关注点。


## 4. 已实现功能

- 导入 / 浏览 / 缩略图与缓存
  - MapThumbnailCache、Preview 缓存、缩略图相关实现可见。

- 搜索 / 标签 / 分类
  - AutomaticLabels、ManualTags 存入 media-metadata.json（MediaMetadataStore），PhotoClassifierFactory 支持 RuleBased/Onnx/MobileCLIP。

- EXIF / 元数据读取
  - ExifLocationReader、PhotoDetailMetadataReader 与 MetadataExtractor（Infrastructure）配合使用。

- 人脸检测与识别（已实现主要路径）
  - LocalFaceEmbeddingService：HaarCascade + SFace ONNX 提取 embedding。
  - FaceSearchService、PeopleAlbumService：缓存、聚类（余弦相似度）、保存 MatchCentroids 到 people-albums.json。

- AI 图像识别
  - MobileCLIP（本地语义）、mobilenet ONNX（OnnxPhotoClassifier）实现并可在 PhotoAnalysis 中被调用。

- 数据库与缓存
  - JSON snapshot（media-metadata.json、people-albums.json、face-features.json）与 SqliteCloudIndexStore（SQLite）并存。

- 设置页与批量处理
  - SettingsCenterPage、AppSettingsStore、PhotoAnalysisViewModel 支持批量分析/扫描。


## 5. AI 与模型相关内容（逐项）

> 说明：以下结论基于代码和仓库内实际文件。若有“无法确认”字段则明确标注。

- MobileCLIP / mobileclip
  - 文件：src\HanabePhotoManager.App\Models\MobileCLIP\mobileclip_s2_visual.onnx
  - 嵌入表：src\HanabePhotoManager.App\Models\MobileCLIP\label_embeddings.json
  - 代码：src\HanabePhotoManager.App\Services\MobileClipPhotoClassifier.cs
  - 调用：PhotoClassifierFactory 支持并可返回该实现；PhotoAnalysis 可触发该分类器 → 可被调用
  - 状态：实现完整（模型+label_embedding+分类器代码），测试也包含在 tests
  - 模型文件：为实际 .onnx 文件（已被列为 Content 并 CopyToPublishDirectory）
  - 输入尺寸与向量维度：代码显示预处理尺寸为 256×256；label_embeddings.json 中向量长度需要统计 JSON 来确认（静态审查未统计，故“无法确认”具体维度）

- CLIP（通用）
  - 以 MobileCLIP 的具体实现出现；未发现其它 CLIP 变体实现。

- SigLIP / SigLIP2
  - 仓库中未发现 SigLIP 相关代码或模型（未实现）

- ONNX
  - 存在并被使用的模型：mobilenetv2-7.onnx、mobileclip_s2_visual.onnx、face_recognition_sface_2021dec.onnx、face_detection_yunet_2023mar.onnx（后者可能未使用）
  - 使用方式：通过 OpenCvSharp.Dnn 的 ReadNetFromOnnx + Net.Forward 执行推理（在多个服务实现中可见）

- InferenceSession / Microsoft.ML.OnnxRuntime
  - 在源码中未找到 InferenceSession 或 Microsoft.ML.OnnxRuntime 的使用或引用（搜索未命中）→ 当前未使用 ONNX Runtime API

- label_embeddings / embedding / classifier
  - label_embeddings.json 为 label→float[] 的映射；MobileClipPhotoClassifier 使用该文件计算语义相似度
  - embedding 的生成：MobileCLIP（图像 embedding）与 LocalFaceEmbeddingService（face embedding）

- face_recognition / OpenCv / SFace
  - 模型：src\HanabePhotoManager.App\Models\Face\face_recognition_sface_2021dec.onnx
  - 检测：haarcascade_frontalface_alt2.xml（Haarcascade）用于检测，随后将人脸裁剪并将 112x112/其他尺寸输入 SFace（代码中有相关 resize/normalize 逻辑）
  - 调用：LocalFaceEmbeddingService 与 FaceSearchService 使用该模型

- OpenCvSharp
  - 库引用：OpenCvSharp4 + OpenCvSharp4.runtime.win 在 App.csproj 中声明
  - 用途：用于 ONNX 加载（Dnn）与图像前处理

- ImageSharp
  - 库引用：SixLabors.ImageSharp 在 App.csproj
  - 用途：图像压缩与水印处理（ImageCompressionService、WatermarkExportService、MapThumbnailCache 等）

结论：仓库包含实际的 ONNX 模型文件并使用 OpenCvSharp DNN 实现推理；未使用 Microsoft.ML.OnnxRuntime。


## 6. NuGet 与第三方依赖

- src\HanabePhotoManager.App\HanabePhotoManager.App.csproj
  - CommunityToolkit.Mvvm (8.4.0) — MVVM 支持
  - Microsoft.Extensions.DependencyInjection (8.0.1) — DI 库（引用但未见集中注册）
  - Microsoft.Web.WebView2 — Web 内嵌
  - OpenCvSharp4 + OpenCvSharp4.runtime.win — ONNX 加载与 DNN 推理、图像处理
  - SixLabors.ImageSharp — 图像处理（压缩、水印、缩放）

- src\HanabePhotoManager.Infrastructure\HanabePhotoManager.Infrastructure.csproj
  - MetadataExtractor — EXIF/元数据读取
  - Microsoft.Data.Sqlite、SQLitePCLRaw.bundle_e_sqlite3 — SQLite 支持

- tests 项目：xunit、FluentAssertions、coverlet 等

未使用 / 冲突
- Microsoft.Extensions.DependencyInjection 被引用，但是源码未体现明显的集中注册；不是冲突，但表示 DI 使用分散。
- 未在任何 csproj 中发现 Microsoft.ML.OnnxRuntime 的引用（因此未使用 ONNX Runtime）

GPU / DirectML 推理支持
- 未在源码或 csproj 中发现对 GPU/DirectML 的显式支持或 provider 包；是否可用需要运行时与发布包验证 → 标注为“无法确认”。


## 7. 数据存储

- JSON 文件（主存储）
  - media-metadata.json（路径：%LocalAppData%/HanabePhotoManager/media-metadata.json 除非通过 HANABE_APP_DATA_DIR 覆写）
    - 存储结构：MediaMetadataSnapshot（Version、CustomTags、MapSourcePaths、Entries）
    - MediaMetadataEntry 字段：Path、Fingerprint、AutomaticLabels (List<PhotoLabelScore>)、ManualTags、ClassifierVersion、AnalyzedAt、PeopleIds、ExifLocation/ManualLocation 等
  - people-albums.json
    - 存储 PersonAlbum，包括 MatchCentroids（List<float[]>）用于人脸匹配
  - face-features.json（FaceSearch 缓存）
  - photo-analysis.checkpoint.jsonl（分析 checkpoint）

- SQLite
  - SqliteCloudIndexStore 提供 cloud_objects 表（provider、remote_id、full_path、name、kind、size、modified_at、thumbnail_key、is_hanabe_managed）
  - 迁移方式：SQLite schema 由代码中 SQL 的 CREATE TABLE IF NOT EXISTS 管理；JSON snapshot 通过 Version 字段管理升级（无 EF Core migration）

- embedding 保存情况
  - 人脸 embedding：已被保存（people-albums.json 的 MatchCentroids，face-features.json 的 per-photo embeddings）
  - 图像语义 embedding（MobileCLIP 输出）：当前源码中未发现持久化 image embedding 字段，通常只保存 AutomaticLabels（label+score）。


## 8. 启动和依赖注入

- 应用入口：App.xaml / App.xaml.cs（OnStartup 调用 ThemeManager.LoadAndApply）
- 服务注册：未发现集中式 IServiceCollection 注册（AddSingleton/AddTransient 等），许多服务通过 new 显式构造/工厂创建
- 关键服务生命周期：由持有者（调用方）管理；某些资源（OpenCv Net 等）在类内缓存并在不再使用时 Dispose
- 模型与配置路径解析：模型基于 AppContext.BaseDirectory + "Models\..." 路径构造；应用数据路径基于 AppDataPaths.Root（%LocalAppData%\HanabePhotoManager 或 HANABE_APP_DATA_DIR 环境变量覆盖）


## 9. 风险和问题

- 可编译性/可运行性
  - 源码可在 Windows + .NET 8 SDK 下编译，但是否能在目标机器上成功运行取决于本机 native 依赖（OpenCvSharp 运行时和任何 GPU provider 的本机 dll）是否可用。

- TODO / 占位实现 / 硬编码路径
  - 部分 Cloud provider 存在 NotImplemented 的实现。
  - 模型/数据路径使用 AppContext.BaseDirectory 和 AppDataPaths.Root 的硬编码拼接，单文件发布或不同部署策略可能导致路径查找失败。

- 性能
  - 若批量处理时不复用模型实例，会导致重复加载开销。总体代码中部分类会缓存模型并通过 SemaphoreSlim 做保护，但需要在大库上做压力测试。
  - ImageSharp 与 OpenCV 混用在大并发处理时可能增加内存和 GC 压力。

- 线程安全与资源释放
  - 大多数文件读写使用 SemaphoreSlim/queue 来保证顺序写入；OpenCvSharp 本机资源的 Dispose 在代码中多有处理，但需在运行时验证没有泄露路径。

- 大批量图片处理风险
  - 并发推理导致内存/CPU 饱和或本机库冲突（取决于 OpenCV DNN 的线程模型与本机库的线程安全）。
  - 写入 JSON 文件的 I/O 瓶颈或锁竞争（代码使用临时文件写入并 move 替换以减少损坏风险）。

- 模型随发布复制
  - csproj 已将 Models 内容作为 Content 并 CopyToPublishDirectory，设计上会随发布复制，但实际是否出现在最终发布产物需要发布后验证（尤其在单文件/trim/publish 设置下）。


## 10. 新增 SigLIP 2 的建议（保留 MobileCLIP）

目标：新增 SigLIP2 与 MobileCLIP 并存并可切换，尽量保持最小改动。

建议概要：

- 接口放置：在 Core 中新增通用接口 `IImageEmbeddingService`（或更细化的接口），以便上层（App/Infrastructure）通过接口编程访问不同的实现。

- 实现放置：将 SigLIP2 的具体实现放在 App\Services（例如 SigLip2ImageEmbeddingService.cs），与现有 MobileCLIP/Onnx 实现并列。若希望与 UI 层彻底分离，可选择放在 Infrastructure，但会增加跨层更改成本。

- 模型文件放置：src\HanabePhotoManager.App\Models\SigLIP2\，并在 App.csproj 中将该目录声明为 Content 并 CopyToPublishDirectory，以保证随发布复制。

- 是否需要统一 IImageEmbeddingService：推荐新增 IImageEmbeddingService（在 Core），方法例如 `Task<IReadOnlyList<float>> ExtractEmbeddingAsync(string imagePath, CancellationToken ct)`。这样 PeopleAlbumService、FaceSearchService 等可以依赖接口而非具体实现。

- 并存与切换策略：
  - 在 PhotoClassifierFactory 或新增的 EmbeddingServiceFactory 中根据配置（AppSettings.InferenceEngine）返回 MobileCLIP 或 SigLIP2 实例。
  - 保持 MobileClipPhotoClassifier 等现有实现不变（向后兼容），仅在工厂层新增 SigLIP2 分支。
  - 在需要注入的服务处（如 PeopleAlbumService）添加可选构造参数以接受 IImageEmbeddingService（保持默认行为仍使用当前实现）。

- 需要新增 NuGet 包（视 SigLIP2 运行方式而定）：
  - 若继续使用 OpenCvSharp DNN：不需要新增包（复用 OpenCvSharp4）
  - 若希望使用 ONNX Runtime 并利用 DirectML/CUDA provider：需添加 Microsoft.ML.OnnxRuntime 及相应 provider 包（例如 Microsoft.ML.OnnxRuntime.DirectML / Microsoft.ML.OnnxRuntime.Gpu），并处理本机 provider 打包

- 可复用代码：PhotoClassifierFactory、图像预处理函数、模型路径管理、PeopleAlbumService 的聚类/相似度逻辑（余弦相似度），以及 csproj 的模型打包配置。

- 必须避免修改的地方（以降低风险）：
  - 避免直接改变 MediaMetadata JSON schema 与 PeopleAlbum 的保存格式（如果必须更改则提供版本迁移）
  - 不要删除或替换 MobileCLIP 的模型或 label_embeddings.json


---

## 附录 A. 关键文件清单

见下列文件（相对仓库根路径）：

- src\HanabePhotoManager.App\Services\MobileClipPhotoClassifier.cs
- src\HanabePhotoManager.App\Services\OnnxPhotoClassifier.cs
- src\HanabePhotoManager.App\Services\LocalFaceEmbeddingService.cs
- src\HanabePhotoManager.App\Services\FaceSearchService.cs
- src\HanabePhotoManager.App\Services\PhotoClassifierFactory.cs
- src\HanabePhotoManager.App\Services\MediaMetadataStore.cs
- src\HanabePhotoManager.App\Services\PeopleAlbumService.cs
- src\HanabePhotoManager.App\Models\MediaMetadata.cs
- src\HanabePhotoManager.App\Models\MobileCLIP\label_embeddings.json
- src\HanabePhotoManager.App\Models\MobileCLIP\mobileclip_s2_visual.onnx
- src\HanabePhotoManager.App\Models\Classification\mobilenetv2-7.onnx
- src\HanabePhotoManager.App\Models\Classification\imagenet_classes.txt
- src\HanabePhotoManager.App\Models\Face\face_recognition_sface_2021dec.onnx
- src\HanabePhotoManager.App\Models\Face\haarcascade_frontalface_alt2.xml
- src\HanabePhotoManager.Infrastructure\Cloud\SqliteCloudIndexStore.cs
- src\HanabePhotoManager.App\App.xaml.cs
- src\HanabePhotoManager.App\HanabePhotoManager.App.csproj
- src\HanabePhotoManager.Infrastructure\HanabePhotoManager.Infrastructure.csproj
- src\HanabePhotoManager.Core\HanabePhotoManager.Core.csproj


## 附录 B. 当前项目结构树（摘选）

- src/
  - HanabePhotoManager.App/
    - App.xaml, App.xaml.cs
    - HanabePhotoManager.App.csproj
    - MainWindow.xaml, MainWindow.xaml.cs
    - Models/
      - MediaMetadata.cs
      - MobileCLIP/
        - mobileclip_s2_visual.onnx
        - label_embeddings.json
      - Classification/
        - mobilenetv2-7.onnx
        - imagenet_classes.txt
      - Face/
        - face_recognition_sface_2021dec.onnx
        - face_detection_yunet_2023mar.onnx
        - haarcascade_frontalface_alt2.xml
    - Services/
      - MobileClipPhotoClassifier.cs
      - OnnxPhotoClassifier.cs
      - LocalFaceEmbeddingService.cs
      - FaceSearchService.cs
      - PhotoClassifierFactory.cs
      - MediaMetadataStore.cs
      - PeopleAlbumService.cs
    - ViewModels/
      - MainWindowViewModel.cs
      - PhotoAnalysisViewModel.cs
      - PeopleAlbumViewModel.cs
  - HanabePhotoManager.Core/
    - HanabePhotoManager.Core.csproj
  - HanabePhotoManager.Infrastructure/
    - HanabePhotoManager.Infrastructure.csproj
    - Cloud/
      - SqliteCloudIndexStore.cs
- tests/
  - HanabePhotoManager.App.Tests/
  - HanabePhotoManager.Core.Tests/
  - HanabePhotoManager.Infrastructure.Tests/


## 附录 C. “新增 SigLIP 2 的最小改动方案”

步骤（最小改动、向后兼容）：

1. 在 Core 中新增接口 IImageEmbeddingService（例如 src\HanabePhotoManager.Core\Services\IImageEmbeddingService.cs）：
   - 方法签名示例：`Task<IReadOnlyList<float>> ExtractEmbeddingAsync(string imagePath, CancellationToken cancellationToken)`
2. 在 App 中新增 SigLIP2 的实现类（src\HanabePhotoManager.App\Services\SigLip2ImageEmbeddingService.cs），并把 SigLIP2 模型文件放到 src\HanabePhotoManager.App\Models\SigLIP2\，并在 csproj 中加入 Content/CopyToPublishDirectory 配置。
3. 在 PhotoClassifierFactory 中添加对 SigLIP2 的选择逻辑（基于配置 AppSettings.InferenceEngine 或类似字段），保持 MobileCLIP 分支不变。
4. 在需要的调用处（例如 PeopleAlbumService 构造）增加可选的接口注入参数，以支持通过 DI 注入 IImageEmbeddingService，默认仍保持 new LocalFaceEmbeddingService()（向后兼容）。
5. 如需 GPU/DirectML 支持：评估并（如果决定）引入 Microsoft.ML.OnnxRuntime + provider 包，并将 SigLIP2 实现在 OnnxRuntime 下（这一步需要额外的打包/发布配置）。


## 附录 D. 仍然无法确认、需要人工补充的信息

1. 仓库外的 CI/CD 或发布脚本中是否引用 Microsoft.ML.OnnxRuntime 或 GPU provider（无法仅通过源码确认）。建议检查 CI 脚本或运行 `dotnet list package` 在构建环境中确认。
2. label_embeddings.json 中向量的具体维度（可通过读取 JSON 并统计数组长度确认）。
3. 部署时是否提供了 OpenCV 的 GPU 支持库（CUDA/DirectML），以及 OpenCvSharp 是否以支持 GPU 的本机库打包（只能通过发布产物或运行时检查确认）。
4. 生产环境实际的模型推理性能（吞吐、并发限制、内存占用）——需要基准测试。
5. 是否存在其它未列出的模型或外部依赖在仓库外（例如私有子模块或 LFS 存储）——需要开发者确认。

---

如果需要下一步操作：
- 可将本文件保存为仓库文档（已完成）并提交到新分支（我将尝试在一个新分支提交并推送到 origin）。
- 若要我继续：可以授权我创建 PR、或在 PR 描述中包含额外说明/变更建议。


---

*报告结束。*
