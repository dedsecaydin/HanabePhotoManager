# 绯夜蔷薇 · Hanabe Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增 Hanabe 浅色/深色主题、可关闭的角色助手，并让助手只读展示现有任务进度。

**Architecture:** 主题继续由 `ThemeManager` 替换成对资源字典；角色偏好进入 `AppSettings`。新增纯表现层 `HanabeAssistantState` 将 `MainWindowViewModel` 已有的 `IsBusy`、`ProgressLabel`、`ProgressValue`、`EstimatedTimeRemaining` 映射为助手展示，不改变任务执行与取消逻辑。

**Tech Stack:** .NET 8、C# 12、WPF/XAML、CommunityToolkit.Mvvm、xUnit、FluentAssertions

## Global Constraints

- 所有项目文件仅保存在 `D:\HanabePhoto`，不得写入 OneDrive。
- 原始颜色只出现在 `Themes/Colors/Colors.Hanabe.Light.xaml` 与 `Colors.Hanabe.Dark.xaml`。
- Hanabe Light/Dark 导出与现有配色相同的资源键和类型。
- 角色关闭后原有状态栏、进度条、通知和任务取消操作继续工作。
- 不修改导入、复制、移动、扫描或 AI 识别的业务流程。
- 不在照片查看器或照片缩略图上覆盖角色水印。

---

### Task 1: Hanabe theme resource pair

**Files:**
- Create: `src/HanabePhotoManager.App/Themes/Colors/Colors.Hanabe.Light.xaml`
- Create: `src/HanabePhotoManager.App/Themes/Colors/Colors.Hanabe.Dark.xaml`
- Create: `src/HanabePhotoManager.App/Themes/Themes/Hanabe.Light.xaml`
- Create: `src/HanabePhotoManager.App/Themes/Themes/Hanabe.Dark.xaml`
- Modify: `tests/HanabePhotoManager.App.Tests/DesignSystemResourceTests.cs`

**Interfaces:**
- Consumes: existing raw-color key contract from `Colors.Violet.Light.xaml` and `Colors.Violet.Dark.xaml`.
- Produces: pack URIs `/HanabePhotoManager.App;component/Themes/Themes/Hanabe.{Light|Dark}.xaml`.

- [ ] Add failing resource-contract assertions with `schemes = new[] { "Dynamic", "Forest", "Violet", "Classic", "Hanabe" }`.
- [ ] Run `dotnet test tests/HanabePhotoManager.App.Tests/HanabePhotoManager.App.Tests.csproj -c Release --filter DesignSystemResourceTests`; expect failure because Hanabe dictionaries do not exist.
- [ ] Copy the exact key structure of Violet raw dictionaries, replace only color values with the approved ivory/crimson/blackberry palette, and compose both theme entry dictionaries with existing Brushes/Tokens/Controls.
- [ ] Re-run the filtered test; expect pass.
- [ ] Commit theme resources and contract test.

### Task 2: ThemeManager and settings theme selector

**Files:**
- Modify: `src/HanabePhotoManager.App/Services/ThemeManager.cs`
- Modify: `src/HanabePhotoManager.App/SettingsCenterPage.xaml`
- Modify: `src/HanabePhotoManager.App/SettingsCenterPage.xaml.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/ThemeManagerTests.cs`

**Interfaces:**
- Consumes: Hanabe theme pack URIs from Task 1.
- Produces: `AppColorScheme.Hanabe` and parsing of `"hanabe"`.

- [ ] Add `[InlineData("hanabe", AppColorScheme.Hanabe)]` to the recognition and fallback test sets and add XAML/source assertions for the selector label `绯夜蔷薇 · Hanabe`.
- [ ] Run ThemeManager tests; expect enum/parser failures.
- [ ] Add `Hanabe` to `AppColorScheme`, parse it explicitly, add the selector item, and map the selection in code-behind.
- [ ] Re-run ThemeManager and design-system tests; expect pass.
- [ ] Commit theme selection support.

### Task 3: Assistant state model and persisted visibility

**Files:**
- Create: `src/HanabePhotoManager.App/ViewModels/HanabeAssistantState.cs`
- Modify: `src/HanabePhotoManager.App/Services/AppSettingsStore.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Create: `tests/HanabePhotoManager.App.Tests/HanabeAssistantStateTests.cs`
- Modify: `tests/HanabePhotoManager.App.Tests/AppSettingsStoreTests.cs`

**Interfaces:**
- Produces: `HanabeAssistantMode { Hidden, Idle, Active, Completed, Failed, Cancelled }` and immutable `HanabeAssistantState.Create(bool isVisible, bool isBusy, string progressLabel, double progressValue, string estimatedTimeRemaining)`.
- Produces ViewModel properties: `ShowHanabeAssistant`, `HanabeAssistant`, and `ToggleHanabeAssistantCommand`.

- [ ] Write parameterized tests for Hidden, Idle, Active and completed/error/cancelled label mapping; add settings round-trip test for `ShowHanabeAssistant` defaulting true.
- [ ] Run the two test classes; expect missing type/property failures.
- [ ] Implement the deterministic state record and persisted `AppSettings.ShowHanabeAssistant`; expose and notify the derived state whenever existing progress fields change.
- [ ] Re-run tests; expect pass.
- [ ] Commit assistant state and preference.

### Task 4: Character assets and WPF assistant view

**Files:**
- Create: `src/HanabePhotoManager.App/Assets/Hanabe/hanabe-assistant.png`
- Create: `src/HanabePhotoManager.App/Controls/HanabeAssistantControl.xaml`
- Create: `src/HanabePhotoManager.App/Controls/HanabeAssistantControl.xaml.cs`
- Modify: `src/HanabePhotoManager.App/MainWindow.xaml`
- Modify: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Interfaces:**
- Consumes: `HanabeAssistantState` and `ToggleHanabeAssistantCommand` from Task 3.
- Produces: a bottom-right assistant overlay that binds only to derived presentation properties.

- [ ] Add XAML guard tests asserting the control, dynamic semantic resources, automation name, progress binding, and absence of literal color values.
- [ ] Run ControlThemeTests; expect missing control failure.
- [ ] Crop one user-provided expression into a transparent packaged PNG; implement compact/expanded templates and place the control above the status region without changing existing bindings or commands.
- [ ] Re-run ControlThemeTests and compare MainWindow binding/command/event inventories.
- [ ] Commit the assistant view and asset.

### Task 5: Settings character card and toggle

**Files:**
- Modify: `src/HanabePhotoManager.App/SettingsCenterPage.xaml`
- Modify: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

**Interfaces:**
- Consumes: `ShowHanabeAssistant` two-way property and Hanabe packaged asset.
- Produces: theme profile card and accessible assistant switch.

- [ ] Add failing assertions for `AutomationProperties.Name="显示 Hanabe 小助手"`, two-way binding, role copy, and theme name.
- [ ] Run filtered tests; expect failure.
- [ ] Add the tokenized profile row and toggle to the existing appearance settings group; hide no unrelated controls.
- [ ] Re-run tests; expect pass.
- [ ] Commit settings UI.

### Task 6: Documentation and full verification

**Files:**
- Modify: `docs/design-system.md`
- Modify: `docs/current-status.md`
- Modify: `docs/agent-change-log.md`

**Interfaces:**
- Consumes: completed theme and assistant feature.
- Produces: current documentation and verification evidence.

- [ ] Update the resource architecture from 4 schemes/8 themes to 5 schemes/10 themes and document assistant semantics.
- [ ] Run `dotnet build HanabePhotoManager.sln -c Release /warnaserror`; expect 0 warnings and 0 errors.
- [ ] Run `dotnet test HanabePhotoManager.sln -c Release --no-build`; expect all tests pass.
- [ ] Run `dotnet publish src/HanabePhotoManager.App/HanabePhotoManager.App.csproj -c Release -r win-x64`; expect successful publish.
- [ ] Generate fresh Light/Dark screenshots if the repository screenshot path is available; otherwise record manual visual QA as not reached.
- [ ] Append exact automated/manual evidence to `docs/agent-change-log.md` and commit documentation.
