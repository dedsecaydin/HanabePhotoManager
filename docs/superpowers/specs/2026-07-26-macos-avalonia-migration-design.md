# Hanabe Photo Manager macOS Avalonia 迁移设计

## 目标

将现有 Hanabe Photo Manager 扩展为可在 Apple Silicon Mac 上运行的完整桌面应用，同时保留现有 Windows WPF 客户端。macOS 客户端覆盖当前照片管理、导入、浏览、搜索、评分、标签、人脸、地图、压缩、水印、比赛筛选、设置与云连接功能。

首期发布目标如下：

- 处理器：Apple Silicon，M1 及后续芯片。
- 运行时标识：`osx-arm64`。
- 最低系统：macOS 11 Big Sur。
- 部署方式：self-contained。
- 交付格式：未签名 `.app`、未签名 `.dmg`、SHA-256 校验值和首次启动说明。
- 暂不包含 Apple Developer 签名、公证和 App Store 发布。

## 现状

当前应用基于 .NET 8、C# 12、WPF、Windows Forms、WebView2、CommunityToolkit.Mvvm 和 Windows 版 OpenCvSharp 运行库。Core 与 Infrastructure 已使用 `net8.0`，具备跨平台复用基础；App 和对应测试使用 `net8.0-windows`，包含 WPF 图像、窗口、Shell、回收站、壁纸、启动项、WebView2、DPAPI 等 Windows 专用能力。

迁移不直接替换现有 WPF 项目，避免在迁移期间破坏 Windows 客户端。

## 架构

### 项目边界

- `HanabePhotoManager.Core` 保持平台无关，继续承载领域模型、接口和确定性业务策略。
- `HanabePhotoManager.Infrastructure` 保留 SQLite、文件导入、云服务与外部系统实现。平台相关的数据保护和路径能力通过接口注入。
- `HanabePhotoManager.App` 保留为 Windows WPF 客户端。
- 新增 `HanabePhotoManager.Desktop`，使用 Avalonia 承载 macOS UI、应用组合与桌面集成。该项目未来也可以承载统一的 Windows 客户端，但首期只发布 macOS。
- 平台能力通过窄接口隔离。界面和 ViewModel 不直接调用 Windows 或 macOS API。

### 共享与迁移原则

- 优先复用 Core、Infrastructure、CommunityToolkit.Mvvm ViewModel 和纯 C# 服务。
- WPF XAML 转换为 Avalonia XAML，不追求逐像素复制，但保留信息架构、功能和设计令牌。
- 将 WPF `BitmapSource` 等类型移出可共享接口，接口传递平台无关的文件、流、字节或图像描述，由客户端负责最终位图转换。
- Windows 和 macOS 平台实现遵守相同契约，平台差异不进入领域策略。
- 不在迁移过程中进行与 macOS 支持无关的重构。

## 平台能力替代

| 能力 | Windows 当前实现 | macOS 设计 |
|---|---|---|
| UI | WPF/XAML | Avalonia/XAML |
| 图片呈现 | WPF imaging | 平台无关解码加 Avalonia Bitmap |
| 人脸与分类 | OpenCvSharp Windows runtime | macOS ARM64 native runtime，复用现有模型 |
| 地图 | WebView2 | Avalonia 可用的跨平台 WebView，复用地图 HTML/JS |
| 删除 | Windows 回收站 | macOS Trash |
| 缩略图 | Windows Shell | macOS Quick Look，失败时回退 ImageSharp |
| 云令牌保护 | DPAPI | macOS Keychain |
| 文件选择 | Windows dialogs | Avalonia StorageProvider |
| 打开文件位置 | Windows Explorer | Finder |
| 启动项 | Windows 启动注册 | macOS 登录项适配器 |
| 壁纸 | Windows 壁纸服务 | macOS 壁纸适配器；不可用时安全降级 |

任何平台能力失败时必须返回可诊断错误并向用户明确提示。删除操作不得因平台适配失败而退化为未经确认的永久删除。

## 功能与数据流

用户操作由 Avalonia View 通过绑定和命令进入共享或迁移后的 ViewModel。ViewModel 调用 Core 策略与抽象服务；Infrastructure 负责数据库、文件、云端和模型资产；平台服务只处理 macOS 桌面能力。耗时的扫描、解码、哈希和推理在后台执行，进度与结果切回 UI 调度器。

首期完整功能范围包括：

- 相机与目录媒体导入。
- 日期、文件夹和媒体分组浏览。
- 搜索、智能分类、人脸相册与地图。
- 评分、标签、备注和元数据。
- 压缩、水印与比赛筛选流程。
- 删除到废纸篓、Finder 定位和文件选择。
- 外观、路径、性能和云连接设置。
- 当前支持的云端索引、队列与缓存能力。

macOS 控件、菜单、快捷键、窗口、滚动和文件对话框遵循 macOS 交互习惯，功能语义与 Windows 版保持一致。

## 迁移顺序

1. 建立 Avalonia 应用骨架、主题令牌、依赖注入和 macOS 发布配置。
2. 提取平台服务契约和平台无关图像边界，保证现有 Windows 客户端继续构建。
3. 迁移主窗口、导航、设置和基础照片浏览。
4. 迁移导入、搜索、评分、标签、元数据和照片查看器。
5. 迁移人脸、分类、地图、压缩、水印和比赛功能。
6. 实现 macOS Trash、Quick Look、Keychain、Finder、登录项和壁纸适配器。
7. 完成 macOS ARM64 原生依赖、模型和地图资产打包。
8. 建立 GitHub Actions macOS 构建、测试、应用包和 DMG 流程。
9. 执行完整功能回归和真实 M1 或更新设备的人工验收。

每个阶段都必须保持 Windows 客户端和共享测试可用。

## 测试与验证

- 保持 Core 与 Infrastructure 的现有测试。
- 将不依赖 WPF 的 ViewModel 和应用服务测试迁移到跨平台测试项目。
- 为平台服务建立共享契约测试，并为 macOS 路径、安全存储、回收站、缩略图和应用数据目录增加覆盖。
- Windows 环境运行静态检查、共享项目构建、Core/Infrastructure 测试和 Avalonia 编译验证。
- GitHub Actions 的 macOS runner 运行 Release 构建、自动化测试、`osx-arm64` publish、`.app` 组装、DMG 制作和校验值生成。
- 每批页面迁移验证启动、主要交互、文件操作、取消、异常恢复和设置持久化。
- 最终人工矩阵覆盖导入、浏览、搜索、评分、标签、人脸、地图、压缩、水印、废纸篓、设置和云连接。

Windows 上的成功交叉编译不能替代真实 macOS 启动和功能验证。原生库、应用包、WebView、Keychain、Quick Look 和 Trash 必须在 macOS runner 或真实设备上验证。

## 交付与首次启动

GitHub Actions 产出：

- `Hanabe Photo Manager.app`
- `Hanabe-Photo-Manager-osx-arm64.dmg`
- SHA-256 校验文件
- 构建版本与提交信息

由于首期未签名，用户优先通过 Finder 对应用执行“右键 → 打开”。若系统仍阻止启动，文档可提供针对该应用的隔离属性移除命令。不得建议全局关闭 Gatekeeper。

## 非目标

- Intel Mac 与 `osx-x64`。
- Apple Developer 签名、公证和 App Store。
- iPhone、iPad、Android 或 Web 客户端。
- 与 macOS 迁移无关的功能扩展。
- 在首期删除现有 Windows WPF 客户端。

## 完成标准

满足以下条件后，macOS 迁移视为完成：

- `osx-arm64` 应用可在 macOS 11 或更新系统、M1 或更新芯片上启动。
- 约定的完整功能范围通过自动化与人工验收。
- 文件删除只进入系统废纸篓，导入与编辑操作不会静默丢失数据。
- macOS 原生依赖、模型、地图和资源全部包含在应用包中。
- Windows 客户端仍可构建，现有共享测试继续通过。
- GitHub Actions 可重复生成 `.app`、`.dmg` 和校验值。
- 已知限制被明确记录，不存在未说明的关键功能缺失。
