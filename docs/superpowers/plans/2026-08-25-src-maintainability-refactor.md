# `src/` 可维护性注释与等价精简 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不改变 Hanabe Photo Manager 现有 UI、公开接口和操作结果的前提下，为 `src/` 生产代码补充高价值中文注释、精简重复与复杂实现，并交付维护指南和用户说明。

**Architecture:** 按依赖方向从 Core、Infrastructure 到 App 分层处理，每批先建立 API/Binding/资源清单，再做小范围等价重构。所有批次独立构建和测试；WPF 层额外比对 Binding、Command、事件与主题资源键，最终进行发布和运行回归。

**Tech Stack:** .NET 8、C# 12、WPF、CommunityToolkit.Mvvm、xUnit、PowerShell、Git

## Global Constraints

- 生产代码修改范围仅限 `src/`；不注释或重构 `tests/` 与 `installer/`。
- 保持公开接口、序列化字段、设置键、Binding、Command、事件处理器、主题资源键和操作结果不变。
- 中文 XML 注释覆盖公共类型、公共接口、核心工作流与困难逻辑；行内注释解释原因和约束，不复述语法。
- 只删除经过 C#、XAML、反射/序列化引用搜索、编译和测试共同证明无用的私有代码。
- 不创建第二套缓存、选择、主题、导入或缩略图系统。
- 不以减少行数为目标，不使用降低可读性的复杂 LINQ 或条件表达式压缩。
- 保留工作区现有未提交修改；每次提交只暂存当前任务明确修改的文件。
- 每个任务完成后更新 `docs/agent-change-log.md`，最终同步维护指南、用户说明和交接文档。

---

### Task 1: 建立可重复的行为保护基线

**Files:**
- Create: `docs/maintainability/src-public-api-before.txt`
- Create: `docs/maintainability/src-wpf-contract-before.txt`
- Create: `docs/maintainability/src-file-inventory.txt`
- Modify: `docs/agent-change-log.md`

**Interfaces:**
- Consumes: 当前 `src/` 中的 C# 声明、XAML Binding/Command/事件和资源键。
- Produces: 后续所有任务用于比对的三个稳定基线文件。

- [ ] **Step 1: 记录干净的构建和测试基线**

Run:

```powershell
dotnet build HanabePhotoManager.sln -c Release /warnaserror
dotnet test HanabePhotoManager.sln -c Release --no-build
```

Expected: 两条命令退出码均为 `0`；若失败，先记录为既有基线问题并停止生产代码重构。

- [ ] **Step 2: 导出 `src/` 文件清单**

Run:

```powershell
rg --files src -g '*.cs' -g '*.xaml' -g '!**/bin/**' -g '!**/obj/**' |
  Sort-Object |
  Set-Content -Encoding utf8 docs/maintainability/src-file-inventory.txt
```

Expected: 文件只来自三个生产项目，不包含 `bin/`、`obj/`、`tests/` 或 `installer/`。

- [ ] **Step 3: 导出公共 API 基线**

Run:

```powershell
rg -n --no-heading '^\s*(public|internal)\s+(sealed\s+|static\s+|partial\s+|abstract\s+)*(class|record|struct|interface|enum)|^\s*public\s+.*\(' src -g '*.cs' -g '!**/bin/**' -g '!**/obj/**' |
  Sort-Object |
  Set-Content -Encoding utf8 docs/maintainability/src-public-api-before.txt
```

Expected: 清单包含路径、行号和声明文本，可用于最终差异审查。

- [ ] **Step 4: 导出 WPF 契约基线**

Run:

```powershell
rg -n --no-heading 'Binding\s+|Command=|Click=|Loaded=|Unloaded=|SelectionChanged=|x:Key=' src/HanabePhotoManager.App -g '*.xaml' |
  Sort-Object |
  Set-Content -Encoding utf8 docs/maintainability/src-wpf-contract-before.txt
```

Expected: 所有 Binding、Command、事件和资源键均有可比对记录。

- [ ] **Step 5: 记录基线并提交**

```powershell
git add -- docs/maintainability/src-public-api-before.txt docs/maintainability/src-wpf-contract-before.txt docs/maintainability/src-file-inventory.txt docs/agent-change-log.md
git commit -m "docs: capture src maintainability baseline"
```

### Task 2: 注释并精简 Core 领域层

**Files:**
- Modify: `src/HanabePhotoManager.Core/Albums/AlbumModels.cs`
- Modify: `src/HanabePhotoManager.Core/Browsing/BrowseModels.cs`
- Modify: `src/HanabePhotoManager.Core/Imports/ImportModels.cs`
- Modify: `src/HanabePhotoManager.Core/Imports/ImportPlanBuilder.cs`
- Modify: `src/HanabePhotoManager.Core/Imports/MediaGroupBuilder.cs`
- Modify: `src/HanabePhotoManager.Core/Performance/PerformanceModels.cs`
- Modify: `src/HanabePhotoManager.Core/Search/SearchModels.cs`
- Modify: remaining `.cs` files listed under `src/HanabePhotoManager.Core/` in `docs/maintainability/src-file-inventory.txt`
- Modify: `docs/agent-change-log.md`

**Interfaces:**
- Consumes: 无外层依赖；Core 是领域契约源头。
- Produces: 含中文 XML 文档的稳定领域 API，以及不改变签名的简化纯逻辑。

- [ ] **Step 1: 对 Core 建立逐文件审查表**

对清单中的每个文件记录：职责、公共成员、领域不变量、重复逻辑、外层调用方。只把能够从实现和测试确认的语义写入注释。

- [ ] **Step 2: 补充领域契约注释**

使用统一格式，例如：

```csharp
/// <summary>
/// 根据已识别的媒体组和导入设置生成不可变的导入计划。
/// </summary>
/// <remarks>
/// 此类型只负责领域决策，不访问文件系统；实际复制由 Infrastructure 执行。
/// </remarks>
```

Expected: 公共类型与关键方法均说明职责、输入约束和副作用边界。

- [ ] **Step 3: 精简可证明等价的纯逻辑**

只允许提前返回、合并重复判断、复用已有值和提取语义明确的私有方法；不得改变返回集合顺序、分组键、命名规则或异常类型。

- [ ] **Step 4: 构建并运行 Core 测试**

```powershell
dotnet build src/HanabePhotoManager.Core/HanabePhotoManager.Core.csproj -c Release /warnaserror
dotnet test tests/HanabePhotoManager.Core.Tests/HanabePhotoManager.Core.Tests.csproj -c Release
```

Expected: 构建与测试全部通过。

- [ ] **Step 5: 审查 API 差异并提交**

```powershell
git diff --check -- src/HanabePhotoManager.Core
git add -- src/HanabePhotoManager.Core docs/agent-change-log.md
git commit -m "refactor: document and simplify core workflows"
```

### Task 3: 注释并精简 Infrastructure 外部系统层

**Files:**
- Modify: `src/HanabePhotoManager.Infrastructure/Files/VerifiedFileTransfer.cs`
- Modify: `src/HanabePhotoManager.Infrastructure/Files/LibraryContentScanner.cs`
- Modify: `src/HanabePhotoManager.Infrastructure/Files/JsonImportJournal.cs`
- Modify: remaining `.cs` files listed under `src/HanabePhotoManager.Infrastructure/` in `docs/maintainability/src-file-inventory.txt`
- Modify: `docs/agent-change-log.md`

**Interfaces:**
- Consumes: Core 的导入、浏览、相册和搜索契约。
- Produces: 保持文件安全、校验、缓存与持久化语义的清晰实现。

- [ ] **Step 1: 标注副作用和恢复边界**

逐文件确认文件创建、覆盖、校验、日志、缓存、异常转换和取消点，并在关键位置说明为何必须保持当前顺序。

- [ ] **Step 2: 补充公共 API 与关键私有流程注释**

注释必须明确：是否访问磁盘、是否可取消、失败后的文件状态、线程安全假设和返回值顺序。

- [ ] **Step 3: 精简重复 I/O 与枚举**

复用已经读取的元数据和规范化路径，合并重复保护判断；不得减少校验步骤、扩大覆盖权限或改变 journal 格式。

- [ ] **Step 4: 构建并运行 Infrastructure 测试**

```powershell
dotnet build src/HanabePhotoManager.Infrastructure/HanabePhotoManager.Infrastructure.csproj -c Release /warnaserror
dotnet test tests/HanabePhotoManager.Infrastructure.Tests/HanabePhotoManager.Infrastructure.Tests.csproj -c Release
```

Expected: 构建与测试全部通过，文件传输和扫描测试没有结果差异。

- [ ] **Step 5: 提交 Infrastructure 批次**

```powershell
git diff --check -- src/HanabePhotoManager.Infrastructure
git add -- src/HanabePhotoManager.Infrastructure docs/agent-change-log.md
git commit -m "refactor: document and simplify infrastructure services"
```

### Task 4: 整理 App 服务、模型与功能协作者

**Files:**
- Modify: `src/HanabePhotoManager.App/Services/LibraryDateSnapshotService.cs`
- Modify: `src/HanabePhotoManager.App/Services/PeopleAlbumService.cs`
- Modify: `src/HanabePhotoManager.App/Services/FaceSearchService.cs`
- Modify: `src/HanabePhotoManager.App/Services/LocalPersonClusterer.cs`
- Modify: `src/HanabePhotoManager.App/Services/FaceRecognitionEngineFactory.cs`
- Modify: `src/HanabePhotoManager.App/Services/AppSettingsStore.cs`
- Modify: `src/HanabePhotoManager.App/Imports/ImportSourcePicker.cs`
- Modify: `.cs` files under `src/HanabePhotoManager.App/Albums/`, `Browsing/`, `Collections/`, `Compression/`, `Duplicates/`, `Imports/`, `Map/`, `Models/`, `Navigation/`, `People/`, `PixelArt/`, `ReleaseNotes/`, `Search/`, `Services/`, `Watermark/` as listed in the inventory
- Modify: `docs/agent-change-log.md`

**Interfaces:**
- Consumes: Core 契约与 Infrastructure 实现。
- Produces: 保持服务注册、缓存、设置和功能协作行为的可维护 App 服务层。

- [ ] **Step 1: 建立服务生命周期与调用图**

记录 singleton/transient、UI 线程要求、事件订阅、CancellationToken 所有者和缓存所有者；对静态引用和长生命周期事件重点标注。

- [ ] **Step 2: 补充服务与模型注释**

公共类型说明其所属功能、生命周期和副作用；复杂私有方法说明取消、并发、缓存和错误降级原因。

- [ ] **Step 3: 合并重复服务逻辑**

只合并同一语义的路径规范化、媒体格式判断、状态转换和错误包装；现有服务接口与依赖注入注册保持不变。

- [ ] **Step 4: 运行 App 相关服务测试**

```powershell
dotnet build src/HanabePhotoManager.App/HanabePhotoManager.App.csproj -c Release /warnaserror
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release
```

Expected: App 构建与测试全部通过。

- [ ] **Step 5: 提交 App 服务批次**

```powershell
git diff --check -- src/HanabePhotoManager.App
git add -- src/HanabePhotoManager.App/Albums src/HanabePhotoManager.App/Browsing src/HanabePhotoManager.App/Collections src/HanabePhotoManager.App/Compression src/HanabePhotoManager.App/Duplicates src/HanabePhotoManager.App/Imports src/HanabePhotoManager.App/Map src/HanabePhotoManager.App/Models src/HanabePhotoManager.App/Navigation src/HanabePhotoManager.App/People src/HanabePhotoManager.App/PixelArt src/HanabePhotoManager.App/ReleaseNotes src/HanabePhotoManager.App/Search src/HanabePhotoManager.App/Services src/HanabePhotoManager.App/Watermark docs/agent-change-log.md
git commit -m "refactor: document and simplify app services"
```

### Task 5: 拆分并整理 ViewModel 层

**Files:**
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: existing `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.*.cs` partial files
- Create only when a responsibility is currently trapped in the 6,000+ line root file: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.<Responsibility>.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/PeopleAlbumViewModel.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/CompressionViewModel.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/FaceSearchViewModel.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MapPhotosViewModel.cs`
- Modify: remaining `.cs` files under `src/HanabePhotoManager.App/ViewModels/`
- Modify: `docs/agent-change-log.md`

**Interfaces:**
- Consumes: App 服务和 CommunityToolkit.Mvvm。
- Produces: 属性名、Command 名和通知时序不变的分责 ViewModel 实现。

- [ ] **Step 1: 导出 MainWindowViewModel 契约**

```powershell
rg -n --no-heading '^\s*(public|internal)\s+|\[ObservableProperty\]|\[RelayCommand' src/HanabePhotoManager.App/ViewModels/MainWindowViewModel*.cs |
  Sort-Object |
  Set-Content -Encoding utf8 docs/maintainability/main-window-view-model-before.txt
```

- [ ] **Step 2: 按既有 partial 模式移动独立职责**

移动时保持类型名、字段、属性、Command、方法签名和可见性不变；只按导入、图库、设备、设置等现有职责边界拆分，不引入新状态容器。

- [ ] **Step 3: 补充异步状态与命令注释**

为最新请求优先、取消源更换、UI Dispatcher、进度摘要、选择状态和导航状态添加原因型注释。

- [ ] **Step 4: 运行 ViewModel 与 App 测试**

```powershell
dotnet build src/HanabePhotoManager.App/HanabePhotoManager.App.csproj -c Release /warnaserror
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release
```

Expected: 所有生成的 RelayCommand 和 ObservableProperty 名称保持兼容，测试通过。

- [ ] **Step 5: 比对契约并提交**

重新运行 Step 1 到 `main-window-view-model-after.txt`，忽略行号后比较声明集合；任何非注释差异都必须逐项解释或回退。

```powershell
git add -- src/HanabePhotoManager.App/ViewModels docs/maintainability/main-window-view-model-before.txt docs/maintainability/main-window-view-model-after.txt docs/agent-change-log.md
git commit -m "refactor: split and document app view models"
```

### Task 6: 整理 WPF 窗口、控件和代码后置

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml.cs`
- Modify: `src/HanabePhotoManager.App/PhotoViewerWindow.xaml.cs`
- Modify: `src/HanabePhotoManager.App/Browsing/Treemap/PhotoTreemapControl.cs`
- Modify: `src/HanabePhotoManager.App/Controls/VirtualizingWrapPanel.cs`
- Modify: remaining `.xaml.cs` and control `.cs` files in `src/HanabePhotoManager.App/`
- Modify: `docs/agent-change-log.md`

**Interfaces:**
- Consumes: XAML 名称作用域、ViewModel 命令和 WPF 生命周期。
- Produces: 事件连接、虚拟化、动画和查看器行为不变的可读代码后置。

- [ ] **Step 1: 标记 WPF 特殊约束**

注释 Dispatcher、输入路由、滚轮、双击、动画中断、虚拟化测量和 BitmapSource 生命周期中的非直观原因。

- [ ] **Step 2: 精简重复事件与视觉树查找**

复用现有辅助方法和局部结果；不得合并语义不同的输入路径，不得改变 handled 标志、焦点或路由顺序。

- [ ] **Step 3: 检查事件订阅释放**

确保新增或移动的订阅仍在对应生命周期解除；不借此任务改变现有页面生命周期策略。

- [ ] **Step 4: 构建并运行 App 测试**

```powershell
dotnet build src/HanabePhotoManager.App/HanabePhotoManager.App.csproj -c Release /warnaserror
dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release
```

- [ ] **Step 5: 提交 WPF 代码后置批次**

```powershell
git add -- src/HanabePhotoManager.App docs/agent-change-log.md
git commit -m "refactor: document and simplify wpf interaction code"
```

### Task 7: 为 XAML 与主题资源增加结构批注并去除确认重复项

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `src/HanabePhotoManager.App/SettingsCenterPage.xaml`
- Modify: `src/HanabePhotoManager.App/PhotoViewerWindow.xaml`
- Modify: `src/HanabePhotoManager.App/App.xaml`
- Modify: remaining `.xaml` files under `src/HanabePhotoManager.App/`
- Modify: ResourceDictionary files under `src/HanabePhotoManager.App/Themes/`
- Modify: `docs/agent-change-log.md`

**Interfaces:**
- Consumes: 当前 Design Tokens、组件样式、Binding 与事件基线。
- Produces: 结构更易读且资源键、视觉树和运行外观不变的 XAML。

- [ ] **Step 1: 给主要区域添加简短 XAML 注释**

只标记 Shell、导航、工作区、Inspector、状态区、主要模板和资源分组；不为每个控件添加噪声注释。

- [ ] **Step 2: 审查重复资源**

只有键、类型、状态和消费者完全等价时才合并；保留旧资源键作为兼容别名，避免 StaticResource/DynamicResource 失效。

- [ ] **Step 3: 比对 WPF 契约**

重新生成 `docs/maintainability/src-wpf-contract-after.txt`，忽略行号后与 before 文件比较。Binding、Command、事件和资源键不得无解释缺失。

- [ ] **Step 4: 构建 App**

```powershell
dotnet build src/HanabePhotoManager.App/HanabePhotoManager.App.csproj -c Release /warnaserror
```

Expected: 无 XAML 编译错误和新增警告。

- [ ] **Step 5: 提交 XAML 批次**

```powershell
git add -- src/HanabePhotoManager.App docs/maintainability/src-wpf-contract-after.txt docs/agent-change-log.md
git commit -m "docs: annotate wpf views and theme resources"
```

### Task 8: 编写维护指南与用户说明

**Files:**
- Create: `docs/maintainer-guide.zh-CN.md`
- Create: `docs/user-guide.zh-CN.md`
- Modify: `README.md`
- Modify: `docs/agent-change-log.md`

**Interfaces:**
- Consumes: 完成后的真实目录、服务、设置、功能和发布流程。
- Produces: 维护入口索引与最终用户操作说明。

- [ ] **Step 1: 编写维护指南**

必须包含：三层依赖图、功能到源码/测试定位表、常见修改配方、Binding 与主题保护、异步取消、缓存和文件安全、构建测试发布、故障定位清单。

- [ ] **Step 2: 编写用户说明**

必须包含：前置环境、安装与首次启动、照片库、导入、多文件夹、图库、查看器、人物、地图、工具、设置、支持格式、数据与备份、常见故障。

- [ ] **Step 3: 从 README 增加文档入口**

添加“开发维护指南”和“用户使用说明”链接，不重写现有项目介绍。

- [ ] **Step 4: 校验所有本地链接并提交**

```powershell
git diff --check -- README.md docs/maintainer-guide.zh-CN.md docs/user-guide.zh-CN.md
git add -- README.md docs/maintainer-guide.zh-CN.md docs/user-guide.zh-CN.md docs/agent-change-log.md
git commit -m "docs: add maintainer and user guides"
```

### Task 9: 全量契约比对、发布与运行回归

**Files:**
- Create: `docs/maintainability/src-public-api-after.txt`
- Create: `docs/maintainability/final-verification.md`
- Modify: `AGENT_HANDOFF.md`
- Modify: `docs/current-status.md` only if implementation facts changed
- Modify: `docs/known-issues.md` only for newly verified unresolved issues
- Modify: `docs/agent-change-log.md`

**Interfaces:**
- Consumes: Tasks 1–8 的全部结果。
- Produces: 可审计的等价性证据和最终交接。

- [ ] **Step 1: 生成最终公共 API 清单并比对**

使用 Task 1 相同命令输出到 `src-public-api-after.txt`；忽略行号后比较声明。只允许因 partial 移动导致的路径/行号变化，不允许签名消失或改变。

- [ ] **Step 2: 执行最终自动验证**

```powershell
dotnet build HanabePhotoManager.sln -c Release /warnaserror
dotnet test HanabePhotoManager.sln -c Release --no-build
dotnet publish src/HanabePhotoManager.App/HanabePhotoManager.App.csproj -c Release -r win-x64
```

Expected: 所有命令退出码为 `0`。

- [ ] **Step 3: 对发布输出执行运行回归**

逐项检查启动/退出、导航、Light/Dark、导入与取消、进度与摘要、图库滚动/展开/缩放/选择、双击查看器、Inspector、人物、地图、设置保存和外接设备提示；记录可复现证据，不把未执行项目写成通过。

- [ ] **Step 4: 记录验证结果与已知问题**

`final-verification.md` 分开记录自动测试、人工检查、性能观察、未覆盖项和已知问题。P0/P1 未解决时不得宣布完成。

- [ ] **Step 5: 最终提交**

```powershell
git add -- docs/maintainability AGENT_HANDOFF.md docs/current-status.md docs/known-issues.md docs/agent-change-log.md
git commit -m "docs: record src refactor verification"
```

## Self-Review Result

- Spec coverage: 注释、等价精简、行为保护、两份指南、构建/测试/发布和运行 QA 均有对应任务。
- Placeholder scan: 计划不包含 TBD、TODO 或未定义的“稍后实现”；新增 partial 文件仅在现有职责可被完整移动时创建，并受契约比对约束。
- Type consistency: 计划不新增跨层公共接口；所有现有签名以 Task 1 基线为准，避免在计划中虚构类型。
- Scope control: 生产代码只修改 `src/`；`tests/` 只运行不编辑，`installer/` 完全不处理。
