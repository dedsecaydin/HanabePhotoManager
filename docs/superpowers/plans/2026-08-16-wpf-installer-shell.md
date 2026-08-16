# WPF Installer Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建可单文件分发的 WPF 安装外壳，实现分步安装、须知滚动门禁、浅深色主题和统一品牌图标。

**Architecture:** 新建自包含 WPF 安装器项目，将构建好的 MSI 作为嵌入资源封装进单一 EXE。外壳负责界面和状态，安装事务仍由 `msiexec` 执行；发布脚本先生成 MSI，再发布外壳并将其作为正式安装包。

**Tech Stack:** .NET 8、C# 12、WPF、xUnit、WiX 5 MSI、PowerShell

## Global Constraints

- 所有构建与日志输出仅位于 D 盘，不写入 OneDrive。
- 安装器为 win-x64 自包含单文件，不要求目标机器预装 .NET Desktop Runtime。
- 浅色和深色使用相同语义资源键，黑白灰为主，品牌紫仅作强调。
- 安装事务、升级与卸载继续由 MSI 负责。

---

### Task 1: 状态机与须知门禁

**Files:**
- Create: `installer/HanabePhotoManager.InstallerShell/InstallerStep.cs`
- Create: `installer/HanabePhotoManager.InstallerShell/LicenseReadGate.cs`
- Create: `installer/HanabePhotoManager.InstallerShell/InstallerFlowState.cs`
- Create: `tests/HanabePhotoManager.InstallerShell.Tests/InstallerFlowStateTests.cs`

**Interfaces:**
- Produces: `LicenseReadGate.HasReachedEnd(double offset, double viewport, double extent)`；`InstallerFlowState` 的步骤推进和可用状态。

- [ ] 写入滚动阈值、无需滚动内容、未同意不可继续、安装后不可返回的失败测试。
- [ ] 运行 `dotnet test tests/HanabePhotoManager.InstallerShell.Tests`，确认类型缺失导致失败。
- [ ] 实现最小状态机与纯函数门禁。
- [ ] 再次运行测试并确认通过。

### Task 2: 安装引擎与日志

**Files:**
- Create: `installer/HanabePhotoManager.InstallerShell/InstallerEngine.cs`
- Create: `installer/HanabePhotoManager.InstallerShell/InstallerExitCode.cs`
- Test: `tests/HanabePhotoManager.InstallerShell.Tests/InstallerEngineTests.cs`

**Interfaces:**
- Produces: `InstallerEngine.InstallAsync(string msiPath, string installFolder, IProgress<int>, CancellationToken)`；`InstallerExitCode.Classify(int)`。

- [ ] 写入退出码 0、1602、1641、3010 和未知失败的映射测试。
- [ ] 运行测试确认失败。
- [ ] 实现带 MSI 日志、路径转义和退出码分类的进程调用。
- [ ] 运行测试确认通过。

### Task 3: WPF 分步外壳与双主题

**Files:**
- Create: `installer/HanabePhotoManager.InstallerShell/HanabePhotoManager.InstallerShell.csproj`
- Create: `installer/HanabePhotoManager.InstallerShell/App.xaml`
- Create: `installer/HanabePhotoManager.InstallerShell/MainWindow.xaml`
- Create: `installer/HanabePhotoManager.InstallerShell/MainWindow.xaml.cs`
- Create: `installer/HanabePhotoManager.InstallerShell/Themes/Light.xaml`
- Create: `installer/HanabePhotoManager.InstallerShell/Themes/Dark.xaml`
- Create: `installer/HanabePhotoManager.InstallerShell/Assets/license.txt`

**Interfaces:**
- Consumes: Task 1 状态机、Task 2 安装引擎、应用 `HanabeApp.ico` 与两套 Logo。

- [ ] 建立 WPF 项目及资源键一致性测试。
- [ ] 实现欢迎、须知、安装、完成四个页面和统一圆角窗口。
- [ ] 将 ScrollViewer 事件接入 `LicenseReadGate`，滚到底后才解锁同意项。
- [ ] 实现系统主题初始选择与手动浅色/深色切换。
- [ ] 运行项目测试与 Release 构建。

### Task 4: 单文件封装与正式发布

**Files:**
- Modify: `tools/Publish-Clean.ps1`
- Modify: `tests/Installer/InstallerAuthoring.Tests.ps1`
- Modify: `HanabePhotoManager.sln`

**Interfaces:**
- Consumes: MSI 路径；Produces: `artifacts/<version>/HanabePhotoManager-Setup-x64.exe` 和 SHA-256。

- [ ] 修改发布流程：先生成 MSI，再以 `InstallerMsiPath` 嵌入并单文件发布 WPF 外壳。
- [ ] 增加安装器项目、单文件、自包含、图标和 MSI 嵌入的静态断言。
- [ ] 运行安装器 PowerShell 测试。
- [ ] 生成版本化安装包并校验 SHA-256。

### Task 5: 回归、视觉检查与记录

**Files:**
- Modify: `docs/agent-change-log.md`
- Modify: `docs/current-status.md`

**Interfaces:**
- Consumes: 正式安装包；Produces: 构建、测试、启动和视觉证据。

- [ ] 运行 Release 全解决方案构建与完整测试。
- [ ] 启动最终安装包，验证四步结构、滚动门禁、浅色/深色和退出不安装。
- [ ] 记录安装包路径、校验和、验证结果和限制。
- [ ] 仅暂存本任务文件并提交。
