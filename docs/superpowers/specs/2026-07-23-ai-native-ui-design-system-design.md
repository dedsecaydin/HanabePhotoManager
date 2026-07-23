# AI-native Desktop UI Design System 设计规格

日期：2026-07-23  
状态：待用户复核  
依据：`docs/ui-audit.md`

## 目标与边界

把 Hanabe Photo Manager 统一为现代、克制、专业的 AI-native Windows 桌面应用。默认浅色并支持深色切换；强调高信息密度、清晰层级、键盘优先和稳定的原生桌面质感。

本次只允许调整 XAML UI、布局、资源字典、视觉组件组织及必要的主题状态持久化胶水。不得改变领域逻辑、ViewModel 行为、现有数据绑定语义、命令、API、服务或数据模型。事件处理器和绑定名称必须保留。视觉重构发现的业务问题另行记录，不顺带修改。

## 选定方案

采用方案 A：设计令牌驱动的渐进式统一。

先建立 Design Token 与共享组件层，再由小到大迁移页面。每完成一个模块必须执行构建；有相应测试的模块同时执行目标测试。禁止一次性替换全部 XAML，禁止通过页面级新样式复制全局组件。

## 视觉方向

- 浅色主题：中性灰背景、白色或轻灰表面、近黑文字、细灰边框。
- 深色主题：石墨灰背景与表面，不使用蓝黑底色或大面积纯黑。
- 强调色：单一低饱和冷灰蓝，仅用于主操作、焦点、选中和链接。
- 状态色：成功、警告、危险、信息只表达状态，不承担装饰。
- 不使用玻璃拟态、新拟态、炫彩渐变、发光描边和大面积重阴影。
- 普通卡片依靠表面色与 1px 边框分层；浮层和 Dialog 才使用轻阴影。
- Hover、Focus、Pressed 只改变表面、边框、前景或轻微位移；不做缩放式悬停。

## 架构

资源按职责拆分，并由 `App.xaml` 按稳定顺序合并：

```text
Themes/
  Tokens.Core.xaml          # 非主题 Token：间距、圆角、字号、时长、尺寸
  Tokens.Light.xaml         # 浅色语义色和阴影
  Tokens.Dark.xaml          # 深色同名语义色和阴影
  Typography.xaml
  Controls.Buttons.xaml
  Controls.Inputs.xaml
  Controls.Cards.xaml
  Controls.Navigation.xaml
  Controls.Lists.xaml
  Controls.Dialogs.xaml
  Controls.Status.xaml
```

主题字典必须暴露完全相同的键。组件只引用语义 Brush，不引用原始颜色。页面只引用 Token 和共享 Style；业务特有布局可以保留页面级样式，但必须 `BasedOn` 共享 Style 且不得写视觉常量。

主题切换由一个最小的 UI 基础设施服务更换合并字典，并通过现有应用设置机制持久化 `System/Light/Dark` 偏好。初始默认值为 Light；若未来增加 System，逻辑不影响页面。切换入口位于主侧栏底部，并提供可发现的快捷键和 ToolTip。

## Token 规范

### Color Tokens

采用“语义优先”命名：

- Background：Canvas、Subtle、Overlay。
- Surface：Default、Subtle、Elevated、Interactive、Selected。
- Border：Subtle、Default、Strong、Focus。
- Text：Primary、Secondary、Tertiary、Disabled、Inverse、Link。
- Accent：Default、Hover、Pressed、Subtle、Foreground。
- Status：Info、Success、Warning、Danger，各自提供 Default/Subtle/Foreground。

原始色值只能出现在 `Tokens.Light.xaml` 和 `Tokens.Dark.xaml`。透明状态必须使用独立语义 Brush，不能由页面自行拼 ARGB。

### Spacing Tokens

基础尺度：0、2、4、6、8、12、16、20、24、32、40、48。页面、卡片、表单、列表和工具栏只能使用这些尺度或它们组成的 Thickness Token。

### Radius Tokens

0、6、8、12、16。Button/Input 为 8，Card 为 12，Dialog 为 16；胶囊只用于状态标签和明确的分段控件。

### Typography Tokens

普通 UI 字体栈统一为 `Segoe UI Variable, Microsoft YaHei UI`，不混用其他 UI 字体。中文由 Microsoft YaHei UI 回退，英文与数字优先 Segoe UI Variable。等宽内容必须通过单独的 `Typography.FontFamily.Mono` Token 使用。

字号层级：Caption 11、BodySmall 12、Body 13、Label 13、TitleSmall 16、Title 20、Display 28。字重仅 Regular 和 SemiBold；正文行高约为字号的 1.45 倍。

### Shadow Tokens

None、Floating、Dialog 三档。Card 默认 None；ComboBox Popup、菜单使用 Floating；Dialog 使用 Dialog。深色主题降低阴影依赖并增强边框对比。

### Animation Tokens

Fast 150ms、Normal 180ms、Slow 220ms；只用于颜色、透明度、边框和小幅位置过渡。尊重系统减少动画设置。加载动画不得阻塞交互。

### Icon Size Tokens

12、16、18、20、24。工具栏 16、导航 18、空状态 24。图标采用统一的简洁线性 Path 几何，默认 1.5px 视觉线宽，不混用 Emoji 作为功能图标。

## 组件系统

- Button：Primary、Secondary、Ghost、Danger、Icon、Toolbar。全部覆盖 Normal/Hover/Pressed/Focus/Disabled。
- Input：TextBox、PasswordBox、ComboBox。统一 36/40px 高度，支持 Focus、ReadOnly、Disabled、ValidationError。
- Card：Default、Subtle、Interactive、Selected；只有 Interactive 响应 Hover。
- Dialog：统一标题、正文、操作栏、关闭行为、Esc、默认按钮和 Focus trap。
- Sidebar/Navigation：232px 基准宽度，选中态为中性表面加细强调标记，支持键盘导航。
- Toolbar：40px 高度基线，动作按语义分组，主操作不超过一个。
- List：统一 36/44/52px 密度档，默认 44；共享 Hover、Selected、Focus 和虚拟化设置。
- Status：Loading、Empty、Error 使用统一 StatusPanel；状态包含简洁图标、标题、说明和可选操作。

优先使用 Style 和 ControlTemplate。只有当一个组合组件具有稳定语义、在三个以上页面复用且单靠 Style 无法表达时，才新增 UserControl/CustomControl。

## 布局系统

- 主 Shell：侧栏 + 内容工作区；侧栏固定基准宽度 232px。
- Page：外边距 24px（紧凑窗口）或 32px（标准窗口），内容最大宽度 1440px。
- 阅读/表单内容最大宽度 960px；超宽屏居中，不无限拉伸。
- Grid：12 列概念栅格，常规 gutter 24px，紧凑 gutter 16px。WPF 页面可使用等价 Grid 列组合，无需引入外部栅格库。
- Alignment：标题、描述、表单标签和列表内容遵守同一左边线；操作区右对齐；同组控件垂直中心对齐。
- Density：默认舒适高密度。列表行 44px、标准控件 36px、工具栏控件 32/36px、卡片内边距 20px。
- 响应策略：窄窗口优先收缩辅助列、让工具栏换行或溢出，禁止遮挡主要操作。

## 页面策略

- MainWindow：保留现有视图和绑定，逐分区迁移 Shell、导航、工具栏、内容卡片、图库列表和设置区；不在同一提交中重写整文件。
- PhotoViewer：照片画布固定使用沉浸式深色 Token；浮动工具栏和信息面板仍跟随组件规范。
- Dialog：删除确认、备注、比赛选择先迁移，用于验证公共 Dialog。
- 功能页：Cloud、Compression、Watermark、Contest、Map 依次迁移；共享 PageHeader、Toolbar、Card、List 和 StatusPanel。

## 状态与可访问性

- Loading：优先骨架或小型进度指示；已知进度使用 ProgressBar，并显示文字状态。
- Empty：说明为什么为空，并提供一个最相关操作。
- Error：清楚描述失败和恢复方式，危险色仅用于图标、边框或关键文字。
- Focus：所有可交互元素必须有高对比焦点环，不能以 `FocusVisualStyle=null` 后不提供替代。
- Keyboard：保留 Tab 顺序、Enter/Esc 语义和现有快捷键；新增主题切换快捷键不得覆盖业务快捷键。
- Contrast：正文和关键控件满足 WCAG AA 的对比目标；Disabled 仍须可辨识。

## 渐进式实施与验证

1. 基线：运行现有构建与测试，记录失败，不修改业务。
2. Foundation：Token、Light/Dark 字典、主题服务、字体与基础控件；构建并运行控件级测试。
3. Dialog：迁移 3 个 Dialog/Picker；构建、测试、手工检查键盘行为。
4. Small pages：Cloud 与 Contest；每个模块完成后构建。
5. Tool pages：Compression、Watermark、Map；逐个构建并运行对应测试。
6. Main Shell：按导航、顶栏、工作区、列表、设置分段迁移；每段构建。
7. Viewer：迁移工具栏和信息面板，验证沉浸画布。
8. Cleanup：扫描硬编码视觉值和重复样式；仅保留有记录的例外。
9. Documentation：根据最终实际实现生成 `docs/design-system.md`，替代旧的 `design-system/hanabe-photo-manager/MASTER.md` 成为唯一规范来源。

每个阶段至少执行 `dotnet build HanabePhotoManager.sln -c Release --artifacts-path .artifacts/ui-refactor-verification`。涉及既有测试覆盖时执行对应测试；最终执行完整 `dotnet test` 和发布构建。若运行副本锁定默认输出，始终使用隔离 artifacts 路径。

## 验收标准

- 默认浅色，深色可即时切换并持久化；所有页面在两种主题下可读。
- 页面与共享组件不再新增硬编码颜色、字体、圆角、阴影、动画、图标尺寸和非标准间距。
- Button、Input、Card、Dialog、Sidebar、Navigation、Toolbar、List 和状态面板均来自共享系统。
- 页面最大宽度、留白、栅格、对齐和密度在所有模块一致。
- Hover、Focus、Pressed、Disabled、Loading、Empty、Error 状态完整且一致。
- 原有命令、绑定、API、ViewModel 和业务行为不变。
- 每个迁移阶段均有新鲜构建证据，最终完整测试和发布构建通过，或明确记录与 UI 改造无关的既有失败。
- `docs/design-system.md` 与最终代码一致，并被标记为唯一 UI 规范来源。

## 已知约束

当前源码副本不含 `.git` 元数据，因此无法在此副本中提交规格或按模块创建提交。若需要提交历史，实施前需提供原始 Git 工作树或允许在当前目录初始化新仓库。

