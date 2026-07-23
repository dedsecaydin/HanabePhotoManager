# Hanabe Photo Manager Design System

版本：1.0　更新：2026-07-23　状态：项目唯一 UI 规范来源

## Design Principles

1. Function First：视觉服务于照片管理效率。
2. Token First：页面不直接声明颜色、字体、圆角、阴影和动效。
3. Consistency：同一语义只使用一个共享组件。
4. Native Desktop：键盘、焦点、缩放、菜单和滚动符合 Windows 习惯。
5. Controlled Density：保持高信息密度与稳定的留白、栅格和对齐。
6. Progressive Change：任何 UI 改动必须可独立构建与验证。
7. Business Preservation：UI 不改变 ViewModel、命令、绑定、API 和数据流。
8. Integrated Shell：Sidebar、顶部与主内容区属于同一个连续的 AI-native Desktop App Shell；大结构依靠统一背景、留白、排版和轻分隔建立层级。

## Resource Architecture

`App.xaml` 默认加载 `Themes/Themes/Light.xaml`。主题入口依次合并 Colors、Brushes、Tokens、Typography、Motion 和 Controls。深色模式加载同名资源键的 `Dark.xaml`，组件不判断主题。

原始颜色只能出现在 `Themes/Colors/Colors.Light.xaml` 与 `Colors.Dark.xaml`。语义 Brush 位于对应 Brushes 文件。`ThemeManager` 即时切换主题并在本机保存偏好。

## Tokens

- Color：Background、Surface、Border、Text、Accent、Status 与 Viewer 语义族。
- Spacing：0、2、4、6、8、12、16、20、24、32、40、48；页面 32，卡片 20，gutter 16/24。
- Radius：Small 6、Control 8、Card 12、Dialog 16。
- Typography：`Segoe UI Variable, Microsoft YaHei UI`；11、12、13、16、20、28；仅 Regular/SemiBold。
- Sizing：控件 36/40，Sidebar 232，内容最大宽度 1440，阅读区 960，图标 12/16/18/20/24。
- Material：Shell Chrome 可使用低对比半透明材质；普通内容区保持清晰、稳定和高可读性。
- Gradient：仅允许低饱和环境渐变和重点展示渐变，必须由共享语义 Brush 提供。
- Shadow：普通内容无阴影或仅有极轻分隔；Floating、Popup 与 Dialog 使用分层阴影；重点展示区域可使用单一强调阴影。
- Highlight / Glow：高光描边用于材质边缘；发光只用于 Focus、Selected 或特殊状态，且不得成为常规边框。
- Motion：Fast 150ms、Normal 180ms、Slow 220ms，只允许颜色、透明度、边框与小幅位移。

## Component Library

- Button：Primary、Secondary、Ghost、Danger、Icon、Toolbar、Disclosure。
- Input：TextBox、PasswordBox、ComboBox、CheckBox、RadioButton、Slider。
- Card：Default、Subtle、Interactive、Selected。
- Dialog：Window、Surface、Title、Body。
- Sidebar：Container、GroupLabel。
- Navigation：Item、Segment.Item、Tab.Item。
- Toolbar：Container。
- List：Default、ListItem.Default。
- Menu：Context、Item、Separator.Menu。
- Status：Panel、Title、Description。
- Layout：PageSurface、PageTitle、SectionTitle。

所有交互组件必须覆盖 Normal、Hover、Pressed、Focus、Disabled；输入组件还需覆盖 ReadOnly 和 ValidationError。

## Layout and States

主界面使用 232px Sidebar 与弹性工作区。Sidebar、顶部区域和主内容区共享连续 Shell 背景与统一对齐体系，不得分别包装成互相割裂的大 Card。页面使用 16/24px gutter，标题、说明、表单和列表遵循同一左边线。首页统计使用轻量摘要区；缩略图、设备项、文件夹项等独立语义单元可以保留小 Card。PhotoViewer 是唯一固定深色画布，工具栏与信息栏仍使用共享组件。

## Controlled Visual Effects

- 允许使用半透明背景、背景模糊、柔和渐变、分层阴影、高光描边，以及选中态或焦点态发光。
- 所有效果必须来自统一 Design Tokens、语义 Brush、Effect 或共享组件；页面不得写死颜色、渐变、模糊、阴影、发光、圆角或间距。
- 普通内容区以清晰、可读和统一为优先，不重复叠加材质和效果。
- Shell Chrome 只使用轻材质与低对比高光；重点区域只允许少量环境渐变或强调阴影；Popup、Dialog 和 Floating 使用中等阴影。
- 背景模糊只在技术实现稳定、性能可控且不降低文字对比度时使用；不满足条件时使用 Token 化半透明材质替代。
- 渐变必须柔和、低饱和并与主题协调；禁止廉价炫彩渐变。
- 发光仅表达 Focus、Selected 或特殊状态，不得作为所有组件的常规描边。
- Light / Dark 必须暴露同名、同类型资源，并分别验证对比度、层级和可读性。
- 不得因视觉效果破坏 App Shell 连续性、信息层级、键盘焦点、DPI 表现或运行性能。

Loading 使用进度与文字；Empty 说明原因并提供主操作；Error 使用 Danger 语义色并提供恢复方式。颜色不得成为唯一信息载体。

## Forbidden Rules

- 禁止在页面或组件中写原始颜色或混用 UI 字体。
- 禁止新增页面级隐式共享组件样式或复制 ControlTemplate。
- 禁止无 Token 的玻璃拟态、渐变、模糊、阴影、高光与发光；禁止大面积重复材质、廉价彩色渐变、无状态意义的发光、重阴影和 Emoji 功能图标。
- 禁止隐藏键盘焦点而不提供替代焦点环。
- 禁止为了 UI 修改 ViewModel、API、命令、绑定或业务数据流。
- 禁止在构建失败时继续下一模块。

## Contribution Checklist

确认 Light/Dark 均可读；只使用 Token；优先复用组件；键盘与焦点可用；Loading/Empty/Error 完整；Release Build 与测试通过；新增公共资源时同步更新本文档。
