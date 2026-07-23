# Hanabe Photo Manager Component Inventory

日期：2026-07-23  
范围：`src/HanabePhotoManager.App/**/*.xaml`  
依据：12 个 XAML、2,681 行、52 个 Style 声明。

## 现状统计

| 组件/元素 | 数量 | 现状 |
|---|---:|---|
| TextBlock | 287 | 字号、前景色和字重大量内联 |
| Border | 136 | 同时承担 Card、Panel、Toolbar、Dialog、ListItem |
| Button | 126 | 全局、MainWindow、Dialog、Watermark 多套模板 |
| StackPanel | 109 | 常用于临时布局与 Toolbar，间距不统一 |
| Grid | 74 | 页面布局缺乏统一 gutter 与最大宽度 |
| Style | 52 | 32 个命名 Style、20 个隐式 Style，分布边界混乱 |
| MenuItem | 19 | 主要由 MainWindow 的 Win11 菜单样式控制 |
| ComboBox | 18 | 全局模板与预览页专用模板重复 |
| ScrollViewer | 18 | 滚动策略和内边距不一致 |
| TextBox | 15 | 全局样式被页面属性覆盖 |
| ItemsControl | 15 | 卡片、网格和状态呈现缺少统一容器 |
| Slider | 12 | 旧 Glass Token 命名与目标风格冲突 |
| Separator | 10 | 颜色、长度与边距分散 |
| CheckBox | 9 | 仅有基础隐式样式，状态不完整 |
| ListBox | 5 | 缺少共享 ItemContainerStyle 与密度规范 |
| ProgressBar | 4 | 模板可复用，需改为中性主题 Token |
| Window | 5 | 主窗口、查看器、3 个 Dialog/Picker 外观不一致 |

## 现有 Style 去向

### `App.xaml`

| 现有 Style | 处理 | 最终归属/替代 |
|---|---|---|
| 隐式 TextBlock | 保留并重建 | `Typography.xaml` 的默认正文 |
| 隐式 Button | 保留并重建 | `Controls/Buttons.xaml` → `Button.Secondary` 基线 |
| `ButtonAccent` | 重命名合并 | `Button.Primary` |
| `ButtonDanger` | 重命名合并 | `Button.Danger` |
| `ButtonGhost` | 重命名合并 | `Button.Ghost` |
| `ButtonSuccess` | 删除 | 成功语义不应形成独立按钮体系；使用 Primary 或 Secondary + 状态文案 |
| `GlassPanel` | 删除并合并 | `Card.Default` 或 `Surface.Page` |
| `SoftCard` | 合并 | `Card.Default` |
| `SidebarCard` | 合并 | `Card.Subtle` / `Navigation.Item` |
| 隐式 TextBox | 保留并重建 | `Controls/Inputs.xaml` |
| 隐式 PasswordBox | 保留并重建 | `Controls/Inputs.xaml` |
| 隐式 ComboBox/ComboBoxItem | 保留并重建 | `Controls/Inputs.xaml` |
| 隐式 CheckBox | 保留并重建 | `Controls/Inputs.xaml` |
| 隐式 Slider | 保留并重建 | `Controls/Inputs.xaml` |
| 隐式 ProgressBar | 保留并重建 | `Controls/Status.xaml` |
| 隐式 ScrollBar + `GlassScrollThumb` | 保留能力、删除旧命名 | `Controls/Scrollbars.xaml` |
| 隐式 TreeView/TreeViewItem/Expander | 保留并统一 | `Controls/Lists.xaml` |

### `MainWindow.xaml`

| 现有 Style | 处理 | 最终归属/替代 |
|---|---|---|
| 重复的隐式 TextBlock/Button/TextBox/CheckBox/TreeView/Expander | 删除 | 使用全局组件样式 |
| 重复 `GlassPanel` / `SoftCard` | 删除 | `Card.Default` / `Surface.Page` |
| `PageSurface` | 保留语义、迁移 | `Layout.PageSurface` |
| `Win11NavButton` | 重命名合并 | `Navigation.Item`；图标换为线性 Path |
| `Win11PillButton` | 合并 | `Button.Toolbar` 或 `Segment.Item` |
| `Win11ContextMenu` | 重命名保留 | `Menu.Context` |
| `Win11MenuItem` | 重命名保留 | `Menu.Item` |
| `Win11Separator` | 重命名保留 | `Separator.Menu` |
| `DeviceCardButton` | 保留业务语义、基于公共样式 | `Card.InteractiveButton` |
| `PreviewSegmentButton` | 合并 | `Segment.Item` |
| `PreviewSortComboBox` | 删除 | 全局 ComboBox + `Input.Compact` |
| `PreviewSortItem` | 删除 | 全局 ComboBoxItem |
| `PreviewDateHeaderButton` | 保留语义、基于 Ghost | `Button.Disclosure` |
| `PreviewItemContainer` | 保留布局职责 | `Gallery.ItemContainer`，只定义布局，不含颜色 |

### 页面局部 Style

| 位置 | Style | 处理 |
|---|---|---|
| DeleteConfirmationWindow | 本地隐式 Button | 删除，使用 `Button.Secondary` 与 `Button.Danger` |
| MapPage | `MapModeTabItem` | 合并为 `Segment.Item` / `Tab.Item` |
| WatermarkPage | 本地隐式 Button | 删除，使用全局 Button |
| WatermarkPage | `Panel` | 删除，使用 `Card.Default` |

## 最终 Component Library

### Foundation Components

- `Typography.*`：Display、Title、TitleSmall、Body、BodySmall、Label、Caption、Mono。
- `Surface.Page`：页面背景与最大宽度容器。
- `Separator.Default` / `Separator.Menu`。
- `Icon.*`：12、16、18、20、24 尺寸与统一 Path 前景。

### Actions

- `Button.Primary`：页面唯一主操作。
- `Button.Secondary`：常规操作和默认隐式 Button。
- `Button.Ghost`：低强调操作。
- `Button.Danger`：破坏性确认。
- `Button.Icon`：方形图标按钮。
- `Button.Toolbar`：紧凑工具栏按钮。
- `Button.Disclosure`：展开/收起标题。

### Inputs

- TextBox、PasswordBox、ComboBox、ComboBoxItem 的默认与 Compact 变体。
- CheckBox、RadioButton、Slider 的统一模板。
- `FormField.Label`、`FormField.Hint`、`FormField.Error`。
- 所有输入统一 Normal、Hover、Focus、ReadOnly、Disabled、ValidationError。

### Surfaces

- `Card.Default`、`Card.Subtle`、`Card.Interactive`、`Card.Selected`。
- `StatusBadge.Default/Info/Success/Warning/Danger`。
- `Popup.Surface` 与 `Tooltip.Surface`。

### Navigation

- `Sidebar.Container`、`Sidebar.Header`、`Sidebar.Footer`。
- Shell 导航使用低对比半透明材质、轻分隔与仅键盘焦点可见的 Token 化 FocusGlow。
- `Navigation.Item`、`Navigation.GroupLabel`、`Navigation.SelectionIndicator`。
- `Segment.Container`、`Segment.Item`。
- `Tab.Item`。

### Collections

- `List.Default`、`List.Compact`。
- `ListItem.Default`、`ListItem.Compact`、`ListItem.Selected`。
- TreeView、TreeViewItem、ItemsControl 容器与虚拟化默认值。
- `Gallery.ItemContainer`：照片网格专用布局组件。

### Feedback and Overlays

- `ProgressBar.Default`、`ProgressBar.Compact`。
- `StatusPanel.Loading`、`StatusPanel.Empty`、`StatusPanel.Error`。
- `Dialog.Window`、`Dialog.Title`、`Dialog.Body`、`Dialog.Actions`。
- `Menu.Context`、`Menu.Item`、`Separator.Menu`。
- `ScrollBar.Default`。

### Layout

- `Page.Container`、`Page.Header`、`Page.Actions`、`Section.Header`。
- `Toolbar.Default`、`Toolbar.Group`。
- `Grid.Gutter.Compact/Default` 通过 Thickness Token 使用。
- `Layout.Shell`、`Layout.Workspace`、`Layout.TopBar`：连续 App Shell 的背景、工作区和顶部轻分隔。
- `Layout.HomeSummary`、`Layout.HomeSection`：首页轻量摘要与无大 Card 的分区结构。

## 保留、删除与合并规则

### 保留

- WPF 原生控件类型及现有绑定、命令、事件处理器。
- Gallery、DeviceCard 等具有明确业务语义的布局容器。
- PhotoViewer 深色画布这一受控主题例外。
- ContextMenu、TreeView、Slider、ScrollBar 等必要的定制模板。

### 删除

- 所有 `Glass*`、`Win11*` 视觉命名。
- 页面内重复的隐式 Button、TextBlock、TextBox、Card Style。
- `ButtonSuccess` 与无明确语义的彩色按钮。
- Emoji 功能图标和重复的局部 ComboBox 模板。
- 仅为某个颜色、圆角或阴影存在的 Style。

### 合并

- `GlassPanel`、`SoftCard`、`SidebarCard`、`Panel` → Card/Surface 系列。
- `ButtonAccent`、各页面主操作 → `Button.Primary`。
- `Win11PillButton`、Preview Segment、Map Tab → Segment/Toolbar 系列。
- 各页面列表行 → ListItem 系列。
- 3 个独立弹窗外壳 → Dialog 系列。

## 新增组件门槛

只有同时满足以下条件才允许新增公共组件：具有稳定语义；至少三个页面复用；现有 Style/ControlTemplate 无法表达；包含完整状态；使用既有 Token。否则使用布局组合或扩展现有组件。
