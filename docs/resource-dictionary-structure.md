# Resource Dictionary Structure

日期：2026-07-23

## 设计原则

1. Function First：视觉服务于照片管理效率，不增加无业务意义的装饰。
2. Token First：页面和组件只使用语义 Token，不直接消费颜色、字号、圆角、阴影、动画或尺寸常量。
3. Single Source of Truth：同一组件只有一个基础模板，变体必须 `BasedOn` 基础样式。
4. Progressive Migration：严格按模块迁移，每个模块可独立编译、验证和回退。
5. Stable Semantics：Light/Dark 使用同名语义键，组件不感知具体主题。
6. Native Desktop：保证键盘、焦点、窗口缩放、滚动、菜单和快捷键符合 Windows 桌面习惯。
7. Controlled Density：信息密集但不拥挤，统一页面宽度、栅格、留白和行高。
8. Accessibility：所有交互状态可辨识，颜色不作为唯一信息载体。
9. Maintainability：资源按职责拆分，页面不得创建全局视觉体系的替代实现。
10. Business Preservation：不改变 ViewModel、命令、绑定、API、服务和数据模型。

## 最终目录结构

```text
src/HanabePhotoManager.App/
  Themes/
    Tokens/
      Spacing.xaml
      Radius.xaml
      Sizing.xaml
      Shadows.xaml
      Icons.xaml
    Colors/
      Colors.Light.xaml
      Colors.Dark.xaml
      Brushes.Light.xaml
      Brushes.Dark.xaml
    Typography/
      FontFamilies.xaml
      TypeScale.xaml
    Motion/
      Durations.xaml
      Easings.xaml
    Controls/
      Buttons.xaml
      Inputs.xaml
      Selection.xaml
      Cards.xaml
      Dialogs.xaml
      Sidebar.xaml
      Navigation.xaml
      Toolbars.xaml
      Lists.xaml
      Menus.xaml
      ScrollBars.xaml
      Status.xaml
      Layout.xaml
    Themes/
      Light.xaml
      Dark.xaml
```

## 职责

### Tokens

- `Spacing.xaml`：Spacing.Double 与常用 Thickness，仅含 0、2、4、6、8、12、16、20、24、32、40、48。
- `Radius.xaml`：0、6、8、12、16 的 CornerRadius。
- `Sizing.xaml`：控件高度、侧栏宽度、内容最大宽度、栅格 gutter 和 Icon Size。
- `Shadows.xaml`：Floating、Dialog、Emphasis 与 FocusGlow 的分层 Effect；普通内容不使用阴影，若主题需不同阴影则只在主题入口覆盖对应键。
- `Icons.xaml`：统一线性 Geometry，仅包含图形数据和尺寸引用，不包含页面命令。

### Colors

- `Colors.Light/Dark.xaml`：原始 Color 值。只有这两个文件允许出现颜色字面量。
- `Brushes.Light/Dark.xaml`：把 Color 映射为 Background、Surface、Border、Text、Accent、Status 等语义 Brush。
- 两个主题必须暴露完全相同的资源键。

### Typography

- `FontFamilies.xaml`：UI 字体仅为 `Segoe UI Variable, Microsoft YaHei UI`；Mono 单独定义。
- `TypeScale.xaml`：Caption、BodySmall、Body、Label、TitleSmall、Title、Display 及其行高与字重。

### Motion

- `Durations.xaml`：150ms、180ms、220ms。
- `Easings.xaml`：Standard、Emphasized；禁止页面自行创建 easing。

### Controls

每个文件只定义一个组件族。组件只引用 Token 与语义 Brush；控件文件之间尽量不交叉引用，必要时只能引用更基础的组件。

### Themes

- `Light.xaml` 合并 Light Colors/Brushes、共享 Tokens、Typography、Motion 与 Controls。
- `Dark.xaml` 以相同顺序合并 Dark Colors/Brushes 与所有共享资源。
- `App.xaml` 只合并当前主题入口，不再承载大段 ControlTemplate。

## 合并顺序

```text
Colors → Brushes → Tokens → Typography → Motion → Controls → Theme overrides
```

资源只能向左依赖：Controls 可依赖前面的资源；Tokens、Colors 不得反向依赖 Controls。页面只依赖 Theme 暴露的键。

## 命名规则

- Token：`Spacing.16`、`Radius.Card`、`Size.Control.Default`、`Motion.Duration.Normal`。
- Brush：`Brush.Text.Primary`、`Brush.Surface.Default`、`Brush.Border.Focus`。
- Style：`Button.Primary`、`Card.Interactive`、`Navigation.Item`。
- Geometry：`Icon.Home`、`Icon.Settings`、`Icon.Search`。
- 禁止使用颜色名、产品名或视觉实现命名，如 Blue500、Win11Button、GlassPanel。

## App.xaml 目标形态

```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <ResourceDictionary Source="Themes/Themes/Light.xaml" />
    </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

主题切换仅替换主题入口字典；页面无需重新加载或判断主题。

## 禁止事项

- 禁止在页面或组件中写十六进制颜色、命名字体、任意圆角、任意阴影、任意动画时长和任意图标尺寸。
- 禁止新增页面级隐式 Button、TextBox、ComboBox、Card、List 或 Dialog 样式。
- 禁止复制 ControlTemplate；新变体必须继承或扩展现有组件。
- 禁止使用玻璃拟态、新拟态、彩色渐变、发光、重阴影和大幅缩放 Hover。
- 禁止使用 Emoji、彩色字符或混合图标库作为功能图标。
- 禁止用颜色作为 Loading、Empty、Error、Success 的唯一表达。
- 禁止隐藏键盘焦点；移除系统 FocusVisualStyle 时必须提供 Token 化焦点环。
- 禁止改变 Binding、Command、事件处理器、ViewModel、API 或业务数据流。
- 禁止一次性重写全部 XAML；必须按 Foundation → Button → Input → Card → Dialog → Sidebar → Navigation → MainWindow → PhotoViewer → Cleanup 执行。
- 禁止在某阶段构建失败时继续迁移下一模块。
- 禁止在最终实现前把设计文档描述成已完成事实；`docs/design-system.md` 必须以最终代码为准生成。

## 资源审计规则

每个阶段验证：新文件能被 WPF 编译；主题键在 Light/Dark 中对称；页面未新增视觉字面量；共享 Style 无重复键；现有绑定字符串保持不变。Cleanup 阶段对全部 XAML 执行自动扫描，并记录 PhotoViewer 画布等批准例外。
