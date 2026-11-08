# 交接：Hanabe PhotoManager macOS 版（全功能移植）

> 接手关键词：`HANABE-MACOS-FULL-PORT`
>
> 用户要求：**macOS 版必须移植全部功能**（9 个功能页 + 所有窗口与工具），不是只做导入和图库。
> 用户要求：不要让用户手动在电脑上运行脚本或命令（包括在 Mac 上敲 Terminal 命令），能自动化的都在脚本里完成。
> 设计由 Claude 负责（见 `docs/macos-port/design.md`）；本文件给执行 Agent（ChatGPT Desktop work 模式）照做。

## 1. 接手前必读

依次读取：`AGENTS.md` → `docs/HERMES_MASTER_GUIDE.md` → `docs/architecture.md` → 本文件 → `docs/macos-port/design.md`。

项目根目录 `D:\HanabePhoto`。开始前 `git status`：工作区有大量用户未提交改动（约 60 个），**全部保留**，不要 reset / clean / stash。
改动前的快照：`.artifacts\claude-backup-20261105-before-shared`（src + tests + sln）。

## 2. 当前真实状态（2026-11-05）

### 2026-11-05 后续迁移进展（未完成）

- Shared 已新增调度、图片、对话框、密钥、回收站等平台边界；导入预览/关系图、图库单项、看图、人物相册、按脸查找呈现、语义搜索、压缩拼图、照片分析、设置、声音调度、Agent 协议和 HEIF 转换流程已分批迁入。Windows 在启动时注册现有平台实现。
- 主窗口协调器及其所有 partial 文件已迁入 Shared；图片分割、健康检查、恢复及像素画的业务视图模型也已迁入，Windows 专属实现留在平台层。共享视频工具已按平台选择 ffmpeg / ffprobe 文件名，Mac 已注册用于颜色分组的 ImageSharp 像素读取器（这不是人脸识别）。
- 最新完整构建成功；1,089 项测试通过。此前 HEIF 和规则分类测试曾在删除临时图片时遇到短暂文件占用，测试清理已增加有限重试；占用来源未确认。Windows 隔离环境的九个页面启动截图位于 `.artifacts/macos-port-windows-pages/`，最新图库启动截图为 `.artifacts/macos-port-windows-20261108.png`，显示沙盒中的 35 个素材；UI Automation 已成功依次点击九个侧栏导航按钮，尚不能替代页面内逐项交互验收。Mac 主机也已在 Windows 隔离环境启动并截图 `.artifacts/macos-port-mac-windows-debug.png`。
- Mac 已注册 Shared 图像健康检查和存储容量读取、基于 sips 的 HEIF 解码适配、基于 xattr 的来源标记适配、LaunchAgent 开机启动、Security framework Keychain 和 NSFileManager 废纸篓适配；共享删除确认已支持异步平台对话框。Windows 测试用注入的替身验证临时文件清理、标记回读、密钥用途隔离及废纸篓委派，但尚未在 Mac 上运行这些系统能力或验证登录启动，Mac 第一阶段界面尚未接入 Shared 对应流程。
- 像素画网格尺寸与导出放大倍数已统一到 Shared，新增 ImageSharp 栅格处理及 Mac Avalonia 位图适配；尚未完成 Mac 像素画窗口，两个平台缩放结果尚未逐像素对照。
- 人脸 ONNX 输出解码、五点对齐、ImageSharp 预处理和可移植推理引擎已加入 Shared，Mac 和 Windows 人物相册均使用该引擎；按脸查找的扫描、评分和缓存也已进入 Shared。30 张沙盒照片中新旧引擎均检测 29 张脸、分成 2 组且组内照片完全一致。Windows 和 Mac 按脸查找现使用 Shared 引擎；历史 Haar/OpenCV 对照结果如下：同一沙盒参考图、阈值 0.42 时旧版返回 17 张、新版返回 15 张，差异为 JK0008 与 JK0019；JK0008 目视没有人物，JK0019 的匹配正确性尚未确定，不能宣称新版召回率等同。证据见 `.artifacts/face-port-qa/results`、`.artifacts/face-search-qa/results`。参考图改为优先选择最大人脸；Mac 人物页面尚未接入，Mac 实机未验证。
- 规则分类阈值和模型后处理已共享，MobileNet、MobileCLIP 的 ONNX Runtime 推理与 ImageSharp 预处理已加入 Shared 并注册到 Mac。30 张沙盒 JPG 的首选分类对照：MobileNet 29/30 一致，MobileCLIP 23/30 一致，Windows 保留原分类器。对 JK0019 的诊断显示同一张量的 OpenCV/ONNX 输出余弦相似度 0.9999999999848；把解码后的照片保存为无损 PNG 后预处理及首选标签几乎一致，差异在该样本中来自 JPEG 解码路径。详见 `.artifacts/classification-port-qa` 和 `.artifacts/clip-runtime-qa`；无法据此判断哪套结果更准确。Mac 模型运行及页面接线仍需验证。
- **4.1 尚未完成验收**：Windows 页面交互仍须逐项核验。Mac ICC 转换已加入但未经 Mac 实机验证；多个专用对话框及页面仍缺失；Keychain 和废纸篓系统调用未经 Mac 实机验证；没有真实 Mac 实机验证。Mac 旧设置现迁移至 Shared 设置文件，旧文件保留、已有共享路径优先，Windows 隔离启动验证通过；完整设置中心仍未完成。
- 视频：官方 VLC 3.0.24 两种架构已校验并加入测试打包；LibVLC 会话和地图控件已实现独立测试，但尚未接入 Mac 页面。先前采用独立输出目录的两个测试包因缺失程序集和模型而无效；原地签名后重新生成的测试包位于 `.artifacts/macos/run-d0cf19a4314442c8a2bfea45d9675daf/`，资源、压缩包及权限检查通过。签名器报告嵌套非 Mach-O 文件警告，生成的 CodeResources 未列出 MacOS 目录中的托管 DLL；需按 Apple 的 bundle 资源布局规则调整并在 Mac 上验证，不能作为正式安装包。实际播放也未验证。详见 .artifacts/videolan-official 和变更日志。
- 遇到未配置平台功能时采用拒绝或明确失败，不把占位实现当作已完成。各批详细记录见 `docs/agent-change-log.md`。

解决方案现在有 5 个产品项目：

| 项目 | 目标框架 | 作用 |
|---|---|---|
| `HanabePhotoManager.Core` | net8.0 | 领域策略：导入计划、分类、日期、场次、布局算法 |
| `HanabePhotoManager.Infrastructure` | net8.0 | 文件系统、校验复制、查重、元数据、SQLite、CLIP |
| `HanabePhotoManager.Shared` **（新）** | net8.0 | 两个平台共用的 App 逻辑（83 个文件，命名空间仍是 `HanabePhotoManager.App.*`） |
| `HanabePhotoManager.App` | net8.0-windows | Windows WPF 版（界面 + 仍依赖 WPF 的视图模型） |
| `HanabePhotoManager.Mac` **（新）** | net8.0 | macOS 版，Avalonia 11.2.3（也能在 Windows 上运行用于调试） |

已完成：

- **第 1 步（共享层）完成**：App 中 83 个与界面无关的 `.cs` 已移入 Shared，App / Mac 都引用 Shared。清单见 `.artifacts\claude-build\shared-moved.txt`。WPF XAML 中引用 Shared 类型的地方用带 `;assembly=HanabePhotoManager.Shared` 的前缀（`albumss:`、`navigations:` 等，前缀名 = 原前缀 + `s`）。
- **Mac 版第一阶段可用**：主页、三步导入（选来源 / 图形化检查确认 / 导入与完成）、照片图库（日期文件夹 + 缩略图）、设置（照片库位置、深浅主题）。导入引擎复用 Core/Infrastructure/Shared，结果与 Windows 版逐文件一致（沙盒实测 35 个文件）。
- `VerifiedFileTransfer` 已加 Unix 分支（“校验后移动”在 macOS 上用 大小+创建时间+修改时间 身份核对后按路径删除）。
- 打包脚本 `src\HanabePhotoManager.Mac\Packaging\build-macos.ps1`：在 Windows 上产出 `Hanabe.app`（osx-arm64、osx-x64，自带运行时），zip 内保留 Unix 可执行位。输出 `.artifacts\macos\`。
- 全量测试 **987 项通过**（Core 196、Infrastructure 75、App 704、InstallerShell 12）。

尚未验证：**没有在真实 Mac 上运行过**。Apple 芯片的主程序未签名（见第 6 节）。

## 3. 架构规则（必须遵守）

1. **业务逻辑只写一份**，放在 Shared（或 Core/Infrastructure）。Windows 与 macOS 只各自写界面和平台实现。禁止把 Windows 版视图模型复制一份改成 Mac 版。
2. Shared 禁止引用：`System.Windows.*`、WinForms、`Microsoft.Win32`、`DllImport` 到 Windows DLL、WebView2、OpenCvSharp、LibVLC 的平台包。
3. 平台能力一律通过 `HanabePhotoManager.App.Platform` 命名空间下的接口（第 4.1 节），由 App（Windows）和 Mac 各自实现并在启动时注册。
4. 保持命名空间不变，文件移动不改调用方；每完成一批就 `dotnet build HanabePhotoManager.sln` + `dotnet test HanabePhotoManager.sln`，**任何时候 Windows 版都必须能构建、测试全绿、正常启动**。
5. 铁律照旧：「修后」目录永不写入、永不覆盖；导入先写临时文件 → SHA-256 校验 → 原子改名；查重“跳过”计入跳过不计入成功。

## 4. 任务分解与验收

### 4.1 第 2 步：视图模型去 WPF 化并移入 Shared

在 Shared 新建 `Platform/`：

```csharp
namespace HanabePhotoManager.App.Platform;

public interface IUiDispatcher            // 替代 Application.Current.Dispatcher
{
    bool CheckAccess();
    void Post(Action action, bool background = false);
    Task InvokeAsync(Func<Task> work);
    Task<T> InvokeAsync<T>(Func<T> work);
}

public interface IDialogService           // 替代 MessageBox / OpenFileDialog / 自定义确认窗口
{
    Task<bool> ConfirmAsync(string title, string message, bool dangerous = false);
    Task InfoAsync(string title, string message);
    Task<string?> PickFolderAsync(string title);
    Task<IReadOnlyList<string>> PickFoldersAsync(string title);
    Task<IReadOnlyList<string>> PickFilesAsync(string title, IReadOnlyList<(string Name, string[] Extensions)> filters, bool multiple);
    Task<string?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<(string Name, string[] Extensions)> filters);
}

public interface IImageFactory            // 视图模型里的图片属性一律是 object?，由平台创建
{
    object? FromFile(string path, int decodePixelWidth = 0);
    object? FromEncoded(byte[] data, int decodePixelWidth = 0);
    object? FromBgra32(int width, int height, byte[] pixels);
    Task<object?> LoadThumbnailAsync(string path, int size, CancellationToken token);   // Windows: Shell 缩略图缓存；Mac: ImageSharp + sips
}

public interface IPlatformShell           // 打开文件夹 / 回收站 / 剪贴板 / 开机启动 / 密钥 / 文件来源标记
{
    void RevealInFileManager(string path);   // Explorer /select vs `open -R`
    void OpenWithDefaultApp(string path);
    Task MoveToTrashAsync(IReadOnlyList<string> paths);  // 回收站 vs ~/.Trash（NSFileManager trashItem）
    void SetClipboardText(string text);
    bool StartWithSystem { get; set; }       // 注册表 Run vs ~/Library/LaunchAgents/com.hanabe.photomanager.plist
    ISecretStore Secrets { get; }            // DPAPI vs Keychain（security add-generic-password）
    IFileOriginMetadataStore OriginMetadata { get; }  // NTFS 备用数据流 vs xattr com.hanabe.origin
}

public static class PlatformServices { /* 启动时由平台赋值；测试默认用同步/空实现 */ }
```

改造清单（`.artifacts` 里有逐文件 WPF 依赖统计，重新生成：见第 7 节命令）：

| 依赖 | 处理 |
|---|---|
| `ImageSource` / `BitmapImage` / `BitmapSource`（MainWindowViewModel 16 处、PeopleAlbum、FaceSearch、PhotoViewer、Treemap、Search、PixelArt、Split、Recovery） | 属性类型改为 `object?`，创建改用 `IImageFactory`；WPF 绑定照常工作 |
| `Dispatcher` / `Application.Current` | `IUiDispatcher` |
| `MessageBox.Show`（MainWindowViewModel 12 处、AgentAssistant 11 处） | `IDialogService.ConfirmAsync/InfoAsync` |
| `Microsoft.Win32.OpenFileDialog`（Watermark、FaceSearch、Recovery） | `IDialogService.PickFilesAsync` |
| `Brush` / `Color`（ImportDateGraphGroupViewModel、分类配色） | 改成 `string` 十六进制（`#FFB9A2FF`），两个平台的绑定都能把字符串转成画刷 |
| `GridLength`、`Thickness`、`Visibility` | 改成 `double` / 自定义 record / `bool` |
| 自定义窗口（ImportResumePromptWindow、ImportMoveSafetyConfirmationWindow、DuplicateReviewWindow、Sessions、Collage…） | 视图模型只发事件或调用 `IDialogService` 的专用方法（如 `ConfirmMoveAsync`、`ReviewDuplicatesAsync`），窗口留在平台层 |

验收：`ViewModels/`、`Services/` 下除平台实现外全部在 Shared；第 1 步移回 App 的 16 个文件（`LocalMcpServer`、`OpenAiCompatibleAgent`、`Collage*`、`ImageCollageService*`、`HeifJpegConverter`、`SemanticSearchViewModel`、`SemanticBrowseRanking`、`AppSettingsStore`、`HanabeSoundService`、`PhotoClassifierFactory`、`PeopleAlbumService`、`CompressionViewModel`、`PhotoAnalysisViewModel`）都已移入；全量测试绿；Windows 版启动后逐页人工过一遍（导入、图库、人物、相册、地图、工具、日期文件夹、设置）。

### 4.2 Windows 专属库的 macOS 替代

| 功能 | Windows 现状 | macOS 方案 |
|---|---|---|
| 人脸检测 / 识别（人物页、导入时识别） | OpenCvSharp（YuNet + SFace，cv::dnn） | 直接用 ONNX Runtime 跑同一组 YuNet / SFace ONNX 模型，预处理和后处理用 ImageSharp 重写；两个平台都改用这一实现，删掉 OpenCvSharp，结果与旧实现对比验证（同一批照片聚类数一致或更好） |
| 照片分类器（MobileClip、规则分类） | OpenCvSharp 读图 / 缩放 | 换 ImageSharp |
| 地图照片页 | WebView2 + Leaflet | Avalonia 自绘瓦片地图控件（OSM 瓦片，HTTP 拉取 + 本地缓存，平移缩放、照片聚合点）；两个平台都可以用它，WebView2 可保留在 Windows |
| 视频播放 | LibVLCSharp.WPF | `LibVLCSharp.Avalonia` + `VideoLAN.LibVLC.Mac` |
| 缩略图 | Windows Shell 缩略图缓存 | ImageSharp；RAW/HEIC 用 `/usr/bin/sips`（已在 `Mac/Services/ThumbnailService.cs`） |
| HEIF 解码 / 转 JPG | Windows HEIF 扩展（DllImport） | `sips -s format jpeg` |
| 回收站 | Shell API | `~/.Trash`（优先 NSFileManager `trashItemAtURL`，通过 ObjC 互操作；不行再移动到 ~/.Trash） |
| 密钥（Agent 令牌、API Key） | DPAPI | Keychain：`/usr/bin/security add-generic-password -s com.hanabe.photomanager -a <name> -w <secret> -U` |
| 开机启动 | 注册表 Run | LaunchAgent plist |
| 设备插拔 | WMI / DriveInfo | 每 3 秒对比 `/Volumes`（已有 `Mac/Services/VolumeService.cs`） |
| 存储卡恢复（只读扫描原始设备） | `\\.\X:` + DeviceIoControl | `/dev/rdiskN`，需要管理员授权：用 `osascript -e 'do shell script "…" with administrator privileges'` 拉起只读 `dd` 管道；界面明确说明只读 |
| 文件来源标记 | NTFS 备用数据流 | 扩展属性 `xattr -w com.hanabe.origin` |
| 桌面助手悬浮窗 | WPF 透明置顶窗口 | Avalonia `TransparencyLevelHint=Transparent`、`Topmost`、无边框；行为保持 `HanabeAssistantWindowPolicy`（主窗口最小化时才显示） |
| 声音 | WAV 原生输出 | `afplay`（系统自带） |
| 本机 MCP 接口 | HttpListener localhost | 原样可用（HttpListener 支持 macOS） |

### 4.3 第 3 步：macOS 各页面界面

按 `docs/macos-port/design.md` 的设计实现，逐页对齐 Windows 版功能。每页验收 = Windows 版该页的功能点全部可用 + 在 Windows 上运行 Mac 版截图对照设计稿。

顺序（依赖少的先做）：设置中心 → 日期文件夹管理 → 照片图库（日期树、网格、矩形树图 / 平铺布局、语义搜索、看图窗口、评分标签、右键菜单、查重审查与隔离区）→ 导入补全（断点续传、改分类 / 日期、场次整理、人物识别、命名方案、自动分流、修后 / 素材拖入、逐个重复确认）→ 自定义相册 → 人物查找 → 图片小工具（压缩、水印、拼图、切图、像素画、S-Log3、存储卡恢复）→ 地图照片 → 主页补全（日程、拍摄频率、交付进度、存储空间、设备）→ 桌面助手 → 新手指南 / 更新说明 → 搜索栏 AI 助手 + 外部 Agent 接口 → 多主题（Hanabe / Violet / Forest / Classic × 深浅）。

## 5. 验证方法（全部自动化，不需要用户操作）

- 构建与测试：`dotnet build HanabePhotoManager.sln`、`dotnet test HanabePhotoManager.sln`。
- 在 Windows 上运行 Mac 版：`$env:HANABE_MAC_DATA_DIR='D:\HanabePhoto\.artifacts\claude-build\macdata'` 隔离设置；`$env:HANABE_MAC_AUTOANALYZE='<来源文件夹>'` 启动后自动进入导入并分析。
- 沙盒数据：`.artifacts\claude-build\sandbox\source`（35 个真实文件：5 RAW + 30 JPG）；测试图库用空文件夹，**不要指向用户真实照片库 `\\Desktop-eqvlo2n\拍照`**。
- Windows 版隔离运行：`$env:HANABE_APP_DATA_DIR='D:\HanabePhoto\.artifacts\claude-build\appdata'`。
- 截图：`.artifacts\claude-build\capture.ps1 -ProcessId <pid> -Out <png>`；界面点击：`.artifacts\claude-build\uia.ps1 -ProcessId <pid> -Invoke '<按钮名>'`（Avalonia 在 Windows 上同样支持 UI Automation）。
- macOS 打包：`powershell -ExecutionPolicy Bypass -File src\HanabePhotoManager.Mac\Packaging\build-macos.ps1`；检查 Mach-O 头（arm64 cputype `0x100000C`、x64 `0x1000007`）与 zip 内可执行位 755。

## 6. 签名（不能让用户敲命令）

Apple 芯片不运行未签名的 arm64 程序。在打包脚本里完成 ad-hoc 签名：

1. 优先检查当前 SDK 生成的 apphost 是否已带 `LC_CODE_SIGNATURE`（.NET 8 在非 macOS 上通常没有）。
2. 用 `rcodesign`（apple-codesign，支持在 Windows 上签名）：`rcodesign sign --code-signature-flags runtime Hanabe.app`，在脚本里自动下载固定版本并校验 SHA-256（下载前需征得用户同意）。
3. 用户有 Apple 开发者账号后，改成 Developer ID 签名 + 公证（`rcodesign notary-submit`），用户就可以直接双击打开。

## 7. 常用命令

重新生成“哪些文件还依赖 WPF”的清单：

```powershell
cd D:\HanabePhoto\src\HanabePhotoManager.App
$pat='System\.Windows[\w\.]*|ImageSource|BitmapSource|BitmapImage|Dispatcher|MessageBox|DllImport|Microsoft\.Win32|WebView2|OpenCvSharp|LibVLC\w*|System\.Drawing|\bClipboard\b|\bBrush(es)?\b|GridLength|Application\.Current'
Get-ChildItem -Recurse -Include *.cs | ?{ $_.FullName -notmatch '\\obj\\|\\bin\\' -and $_.Name -notlike '*.xaml.cs' } | % { $h = Select-String -Path $_.FullName -Pattern $pat -AllMatches | % { $_.Matches } | % Value | Group-Object | % { "$($_.Name)x$($_.Count)" }; if ($h) { "{0}  {1}" -f $_.Name, ($h -join ' ') } }
```

把一批文件移入 Shared 并自动回退编译失败的：`.artifacts\claude-build\move-shared-loop.ps1`；XAML 前缀修正：`.artifacts\claude-build\xaml-shared-prefix.ps1`。

## 8. 完成后

在 `docs/agent-change-log.md` 记一条；更新本文件第 2 节“当前真实状态”；Mac 版用 `build-macos.ps1` 重新打包，产物放 `.artifacts\macos\`。

