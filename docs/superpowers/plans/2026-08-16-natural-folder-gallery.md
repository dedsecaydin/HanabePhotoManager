# 自然文件夹图库 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立单一自然滚动的文件夹分组图库，并完成设置、XML 视频标记、多选和导入高级选项视觉修正。

**Architecture:** 用扁平 `GalleryWallItems` 数据流承载分组头与媒体卡片，以一个虚拟化面板负责布局和滚动；分组标题格式、XML 伴随关系和截帧排除在 ViewModel 发布前确定。设置通过现有 `AppSettingsStore` 持久化，查看器继续复用现有双击路径。

**Tech Stack:** .NET 8、C# 12、WPF、CommunityToolkit.Mvvm、xUnit、FluentAssertions

## Global Constraints

- 不覆盖工作区中与本任务无关的改动。
- XML 和视频截帧 JPG 不得成为独立图库项目。
- 双击无边框照片/视频查看器必须保留。
- 仅使用现有语义颜色、圆角、间距和排版 Token。
- Release 构建 0 警告 0 错误，全量测试通过并发布到 `D:\hanabe-publish-v2`。

---

### Task 1: 分组标题设置与持久化

**Files:**
- Modify: `src/HanabePhotoManager.App/Services/AppSettingsStore.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/HanabePhotoManager.App/SettingsCenterPage.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/AppSettingsStoreTests.cs`

**Interfaces:**
- Produces: `GalleryGroupTitleMode`，值为 `ParsedDate`、`FolderName`、`ParsedDateAndFolderName`。

- [ ] 写失败测试：默认值为 `ParsedDate`，三个值可持久化往返。
- [ ] 运行定向测试并确认因属性不存在而失败。
- [ ] 实现设置字段、VM 属性和“照片库与导入”下拉框。
- [ ] 运行定向测试并确认通过。

### Task 2: 自然分组数据流

**Files:**
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/HanabePhotoManager.App/Controls/VirtualizingWrapPanel.cs`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/PreviewPerformanceTests.cs`

**Interfaces:**
- Consumes: `GalleryGroupTitleMode`。
- Produces: 扁平顺序为 `IWallSectionHeader, PreviewFileViewModel...` 的 `PreviewWallItems`。

- [ ] 写失败测试：无筛选时所有分组均出现，标题后紧跟该组媒体，且没有折叠状态。
- [ ] 运行测试确认旧展开模型导致失败。
- [ ] 删除图库 UI 对展开命令、展开状态和旧滚动补偿的依赖，建立单一自然滚动布局。
- [ ] 运行布局与性能定向测试确认通过。

### Task 3: XML、视频封面和查看器保护

**Files:**
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `src/HanabePhotoManager.Infrastructure/Files/LibraryContentScanner.cs`
- Test: `tests/HanabePhotoManager.App.Tests/PreviewPerformanceTests.cs`
- Test: `tests/HanabePhotoManager.Infrastructure.Tests/Files/LibraryContentScannerTests.cs`

**Interfaces:**
- Produces: `PreviewFileViewModel.HasXmlSidecar`；图库集合排除 `.xml` 和视频截帧派生文件。

- [ ] 写失败测试：XML 不出现在媒体集合、同名视频获得 XML 标记、截帧 JPG 不成为独立项目。
- [ ] 运行测试确认失败原因对应旧扫描行为。
- [ ] 实现伴随文件关联和派生封面排除，卡片显示 XML 徽标。
- [ ] 检查并测试双击仍调用现有无边框查看器且参数是原视频路径。

### Task 4: 多选与导航视觉

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Controls/Navigation.xaml`
- Modify: `src/HanabePhotoManager.App/Themes/Controls/Sidebar.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`
- Test: `tests/HanabePhotoManager.App.Tests/NavigationMotionTests.cs`

**Interfaces:**
- Consumes: 现有 `IsMultiSelectEnabled` 与 `PreviewFileViewModel.IsSelected`。

- [ ] 写失败 XAML 契约测试：多选入口使用 Switch、选中卡片为深色背景、一级导航无内部指示、二级菜单圆角。
- [ ] 运行测试确认旧视觉契约失败。
- [ ] 修改共享样式和页面模板，不更改命令及绑定。
- [ ] 运行导航和主题定向测试确认通过。

### Task 5: 导入高级选项重排

**Files:**
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Interfaces:**
- Consumes: 当前导入高级选项全部绑定、命令和事件。

- [ ] 记录高级选项区的 Binding、Command 和事件清单并写结构失败测试。
- [ ] 将其重排为紧凑设置行，保留清单中的全部行为属性。
- [ ] 比较修改前后行为清单并运行定向测试。

### Task 6: 全量验证、发布和实机 QA

**Files:**
- Modify: `docs/agent-change-log.md`

- [ ] 运行 `dotnet build HanabePhotoManager.sln -c Release /warnaserror`，要求 0 警告 0 错误。
- [ ] 运行 `dotnet test HanabePhotoManager.sln -c Release --no-build`，要求全部通过。
- [ ] 发布并覆盖 `D:\hanabe-publish-v2`。
- [ ] 使用 Computer Use 检查自然跨分组滚动、缩放、多选、XML 徽标、双击查看器、标题设置、设置导航和导入高级选项。
- [ ] 仅暂存本任务代码块并创建 Git 提交。
