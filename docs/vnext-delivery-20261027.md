# vNext 开发交付与验证记录

本次交付为可独立运行的 `0.3.2-alpha.27` 验证版。保留 .NET 8 / WPF / MVVM 分层和旧图库结构；没有执行旧数据库迁移，没有改写用户原片。日用的 `app` 目录未被新版替换。

## 运行与回退

- 程序：`D:\HanabePhoto\releases\vnext-20261027\HanabePhotoManager.App.exe`。整个目录必须一起保留；包含 .NET 运行时、本地模型、媒体运行库、FFmpeg 和 ffprobe，无需联网处理。
- 正常运行沿用原设置和单实例机制。先在日用程序中完成或安全停止正在进行的导入，再退出日用程序并运行新版。不要同时以相同设置目录运行两个版本。
- 新增场次、拼图偏好及视频队列默认保存到 `D:\HanabePhoto\data`，不保存到 OneDrive。指定 `HANABE_APP_DATA_DIR` 时全部新增处理状态位于该隔离目录的 `processing` 子目录。
- 回退：退出新版，启动原 `D:\HanabePhoto\app\HanabePhotoManager.App.exe`。本次没有要求旧版本读取新增场次文件，也没有改造旧图库索引格式。
- 更新前的七份状态备份位于 `.artifacts/vnext-20261026/before-update`；恢复副本位于同级 `restore-check`，校验及 JSON 读取结果见 `restore-check-result.json`。这验证了备份可读，不表示执行过真实库回滚。

## 功能入口

### 拍摄场次

素材管理中的“拍摄场次”使用当前素材；导入完成后可一次性查看本批成功导入的素材。默认间隔为 30 分钟，严格超过才新增边界。先查看范围、数量与成员，再提交虚拟分组。选择成员可在它之前分段，选择场次可合并下一场；“撤销最近提交”支持重启后撤销。

读取 EXIF DateTimeOriginal、亚秒与偏移，不拿文件修改时间冒充拍摄时间。未知时区的不同相机保持独立，缺失/异常时间进入待确认组。相机修正只影响虚拟分组，不回写 EXIF。新存储有版本检查、原子提交、备份及修订冲突保护。

### 智能拼图与传统拼接

“图片小工具 → 智能拼图”：选图、按文件名/拍摄时间排序或拖动，选择网格/自适应行/主图杂志/长图，生成三套候选后微调并导出。锁序会阻止重排；默认完整适配，不拉伸。高级设置可调整分辨率、边距、间隔和背景。

JPG、PNG、TIFF 输出采用同一排版及裁切计划。带 ICC 的输入经 Windows 色彩转换统一到 sRGB；无配置的图片明确按 sRGB 假定。人脸建议复用本地模型，失败回退几何/中心策略。6000 万画布像素和估算内存预算会拒绝超限任务；并非任意数量原尺寸照片都能同时合成。

“传统拼接”独立保留原尺寸横向/纵向、JPEG 目标体积限制及虚化留白工作区。

### 视频还原

“图片小工具 → S-Log3 视频还原”：添加视频，核对相机实际录制色域、范围和矩阵，确认本批确为 S-Log3。文件标记只作提示，不能证明录制曲线。可用标准 Rec.709 数学变换或自定义 `.cube`。

节省空间、高质量、自定义三档；高级设置包含 H.264/H.265、软件/NVENC/AMF、8/10-bit、恒定质量/目标码率及软件回退。默认保留输入尺寸和帧率，输出 SDR Rec.709 4:2:0；标准变换裁切超出显示色域的值，不是创意调色或 HDR 色调映射。

预览与导出复用色彩配置；批次冻结配置和自定义 LUT 内容。队列支持取消、失败重试和重启后恢复待重试状态；成功项不会重复导出。输出先写私有临时文件，通过探测后无覆盖发布，原视频不改写。

## 验证证据及边界

- Release 解决方案构建 0 警告、0 错误；全量 925 项通过（Core 196、Infrastructure 71、App 646、InstallerShell 12），0 失败、0 跳过。TRX 位于 `.artifacts/vnext-20261026/release-tests`。
- 最终 alpha.27 自包含包已实际启动并正常完成内置截图后退出；`final-package-tools.png` 确认智能拼图与传统拼接入口同时存在。随包 `SHA256.json` 记录主程序和 FFmpeg/ffprobe 校验值。
- 三个新窗口的 Light/Dark WPF 初始化、布局及传统拼接入口在 STA 测试中验证；截图位于 `rendered-ui`。实际独立程序启动、主页面、拼图窗口和选图对话框已打开检查。文件对话框后续自动交互受工具限制，未完成三条工作流的完整人工逐步验收。
- 自动化涵盖场次边界、时区、撤销/重启、损坏/未知版本拒写、并发修订、真实 EXIF、拼图顺序/主图/边界/输出格式/ICC、真实软件转码/10-bit/色彩标记、取消/重试/LUT冻结/输出竞争和原片不变。
- ICC 合成样本验证线性 RGB 灰阶 128 转到 sRGB 约 188，sRGB 灰阶保持约 128，避免只复制配置而不转换像素。
- 十万条合成成员、十万场次分组的实测结果位于 `.artifacts/sessions-tests-results/planner-100k-benchmark.json`。这是纯分组算法测试，不含磁盘 EXIF 扫描，不能作为真实大库浏览/启动/搜索性能数字。
- 本机 FFmpeg 一帧实际编码证据位于 `hardware-320`：NVIDIA H.264 8-bit、H.265 10-bit 输出和像素格式检查通过；AMD AMF 因缺少运行库失败。已把程序探测尺寸从 64×64 调整到 320×240，避免硬件最小尺寸限制导致误判。未做长时间 GPU 压力测试。
- 未做真实 Sony 机内素材的视觉校准、所有相机 ICC/RAW 兼容性遍历、完整大库交互性能对比或安装器升级回滚验收。因此此包是验证版，不能把上述未验证项标为正式验收通过。

## 重现

在仓库根目录设置 TEMP/TMP 到 `.artifacts/vnext-20261026/temp`，HANABE_APP_DATA_DIR 到隔离目录后运行：

```powershell
dotnet build HanabePhotoManager.sln -c Release --no-restore /warnaserror /m:1 /p:NuGetAudit=false /p:SelfContained=false
dotnet test HanabePhotoManager.sln -c Release --no-build --no-restore /m:1 /p:SelfContained=false --logger trx --results-directory .artifacts/vnext-20261026/release-tests
```

本次没有执行 NuGet 漏洞审计，不据此声称依赖安全审计通过。
