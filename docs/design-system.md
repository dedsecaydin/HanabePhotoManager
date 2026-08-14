# Hanabe Photo Manager Design System

版本：1.1　更新：2026-08-14　状态：项目唯一 UI 规范来源（M3 大改方向见 `docs/M3_DESIGN_FINAL.md`，排版用变体 001）

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

`App.xaml` 默认加载 `Themes/Themes/Dynamic.Light.xaml`（默认：动态色彩 · 浅色）。主题入口依次合并 Colors、Brushes、Tokens、Typography、Motion 和 Controls。6 套主题 = 3 配色（Dynamic 动态色彩 / Forest 森林绿 / Violet 紫罗兰）× 2 明暗（Light/Dark），入口为 `Themes/Themes/{Scheme}.{Mode}.xaml`，组件不判断主题。

原始颜色只能出现在 `Themes/Colors/Colors.<Scheme>.<Mode>.xaml`（6 套 M3 tonal 色值）。语义 Brush 位于 `Brushes.Light/Dark.xaml`（含 M3 语义 Brush：`Brush.Primary` / `Brush.Surface.Container*` / `Brush.OnSurfaceVariant` 等）。`ThemeManager` 即时切换主题并在本机保存偏好（`ui-theme.txt` 存 `"{Scheme}.{Mode}"`）。

## Tokens

- Color：Background（Canvas/Subtle/Chrome）、Surface（Default/Subtle/Elevated/Interactive/Selected/Disabled/Overlay）、Border、Text、Accent、Status（含 Subtle）与 Viewer 语义族；Overlay Scrim 独立表达遮罩层。
- Spacing：0、2、4、6、8、12、16、20、24、32、40、48；页面 32，卡片 20，gutter 16/24。复合 Thickness 必须由共享 Spacing Token 提供。
- Radius：Small 8、Control 12、Card 12、Dialog 16、Container 28、Full 999（M3：容器 28 / 卡片 12-16 / chip 8-12 / pill 999）。
- Typography：`Segoe UI Variable, Microsoft YaHei UI`；Caption 11/16、BodySmall 12/18、Body 13/20、Label 13/18、TitleSmall 16/22、Title 20/28、Display 28/36（字号/行高）；字重仅使用 Regular、Medium、SemiBold、Bold Token。
- Sizing：控件 36/40，Navigation Rail 88，内容最大宽度 1440，阅读区 960，图标 12/16/18/20/24。
- Material：Shell Chrome 可使用低对比半透明材质；普通内容区保持清晰、稳定和高可读性。
- Shadow：普通内容无阴影或仅有极轻分隔；Floating、Popup 与 Dialog 使用分层阴影；重点展示区域可使用单一强调阴影。
- Highlight / Glow：高光描边用于材质边缘；发光只用于 Focus、Selected 或特殊状态，且不得成为常规边框。
- Motion：Fast 150ms、Normal 180ms、Slow 220ms；仅使用 Standard 或 Emphasized easing，且只允许颜色、透明度、边框与小幅位移。

## Material 3 Desktop Adaptation Rules

Material 3 在 HanabePhoto 中是桌面设计体系，不是移动端模板。Shell Chrome（顶部栏、侧边栏、次级摘要）可用共享的轻材质 Brush 和细分隔线建立连续工作区；内容区、图库和表单必须使用稳定、清晰的 Surface 与 Border 层级，优先保证图像和高密度信息可读。

- 保持桌面信息密度：常规控件高度使用 36/40 Token，避免移动端大按钮、巨大圆角和 FAB。
- 交互反馈使用语义色、边框和轻量透明度变化；不使用大缩放 Hover、弹簧/回弹、玻璃拟态卡片堆叠、彩色渐变或常规发光边框。
- Focus 永远可见，使用 `Brush.Border.Focus` 或 `Shadow.FocusGlow`；Selected 与 Focus 不能只依赖颜色差异。
- 普通内容 Surface 不使用阴影；仅 Floating、Popup、Dialog 使用分层 Shadow Token。PhotoViewer 保持独立深色 Canvas，其工具栏与信息栏仍复用共享组件。
- Light 与 Dark 必须导出完全相同、类型兼容的 Color/Brush/Token/Style 键；每套主题分别维持正文、次要文字、焦点和状态色的可读对比度。

## Component Library

- Button：Primary、Secondary、Ghost、Danger、Icon、Toolbar、Disclosure、Fab（56×56 圆形 primary）。
- Input：TextBox、PasswordBox、ComboBox、CheckBox、RadioButton、Slider。
- Card：Default、Subtle、Interactive、Selected。
- Dialog：Window、Surface、Title、Body。
- Sidebar：Container、GroupLabel。
- Navigation：Item、Segment.Item、Tab.Item。
- Toolbar：Container。
- List：Default、ListItem.Default。
- Menu：Context、Item、Separator.Menu。
- Status：Panel、Title、Description。
- Inspector：Container、Header、SectionLabel、Panel（320px surface-container-low 大圆角面板）。
- Layout：PageSurface、PageTitle、SectionTitle。

所有交互组件必须覆盖 Normal、Hover、Pressed、Focus、Disabled；输入组件还需覆盖 ReadOnly 和 ValidationError。

## Layout and States

主界面使用 88px Navigation Rail 与弹性工作区（M3 排版变体 001：Rail 88 / Topbar surface-container-low / Workspace surface-container-lowest / Inspector 320 / Statusbar 44 / FAB 右下角）。Shell 顶部区域与主内容区共享连续 Shell 背景与统一对齐体系。页面使用 16/24px gutter，标题、说明、表单和列表遵循同一左边线。首页统计使用轻量摘要区；缩略图、设备项、文件夹项等独立语义单元可以保留小 Card。PhotoViewer 是唯一固定深色画布，工具栏与信息栏仍使用共享组件。

## Controlled Visual Effects

- 允许使用半透明背景、分层阴影、高光描边，以及选中态或焦点态发光。
- 所有效果必须来自统一 Design Tokens、语义 Brush、Effect 或共享组件；页面不得写死颜色、渐变、模糊、阴影、发光、圆角或间距。
- 普通内容区以清晰、可读和统一为优先，不重复叠加材质和效果。
- Shell Chrome 只使用轻材质与低对比高光；Popup、Dialog 和 Floating 使用中等阴影。
- 背景模糊只在技术实现稳定、性能可控且不降低文字对比度时使用；不满足条件时使用 Token 化半透明材质替代。
- 禁止彩色渐变；如确有环境层级需求，必须先在本规范和共享语义 Brush 中定义，并经过 Light/Dark 可读性验证。
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
