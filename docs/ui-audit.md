# Hanabe Photo Manager UI Audit

审计日期：2026-07-23  
审计范围：`src/HanabePhotoManager.App/**/*.xaml`，排除 `.artifacts`、`bin`、`obj` 和发布产物。

## 结论

当前界面已经具备一套初步的深色资源，但没有形成可持续复用的 Design System。12 个 XAML 文件共 2,681 行，存在 271 个不同的硬编码颜色值（489 次出现）、146 种间距写法（527 次出现）、20 种圆角值（101 次出现）以及 6 处独立阴影。`MainWindow.xaml` 单文件 1,725 行、包含 295 处颜色和 37 个局部样式，是设计漂移的主要来源。

现有风格以深蓝、青色、半透明表面和渐变为主，与目标的中性色、轻阴影、无玻璃拟态 AI-native Desktop 风格不一致。后续应先建立语义化 Token 和共享组件，再按模块迁移；禁止继续增加页面级隐式样式或视觉常量。

## 页面列表

| 页面 | 类型 | 行数 | 硬编码颜色 | 主要职责 | 审计判断 |
|---|---:|---:|---:|---|---|
| `App.xaml` | 全局资源 | 497 | 74 | 全局颜色与控件模板 | Token、组件和主题混在同一文件 |
| `MainWindow.xaml` | Window | 1,725 | 295 | 主 Shell、导入、图库、设置及多个工作区 | 体积过大，局部样式和重复视觉最多 |
| `PhotoViewerWindow.xaml` | Window | 55 | 13 | 沉浸式照片查看 | 可保留深色画布，控件需统一 |
| `DeleteConfirmationWindow.xaml` | Window | 77 | 23 | 删除确认 | 应迁移到统一危险确认 Dialog |
| `RemarkPromptWindow.xaml` | Window | 14 | 9 | 备注输入 | 应迁移到统一表单 Dialog |
| `Cloud/CloudPage.xaml` | UserControl | 18 | 2 | 云端工作区 | 结构简单，可早期迁移验证 |
| `Compression/CompressionPage.xaml` | UserControl | 54 | 8 | 图片压缩 | 含列表、进度和工具操作区 |
| `Watermark/WatermarkPage.xaml` | UserControl | 31 | 26 | 批量水印 | 局部 Button/Panel 重复实现明显 |
| `Map/MapPage.xaml` | UserControl | 94 | 17 | 地图照片与定位 | 含地图画布、Tab、列表、表单 |
| `Contest/ContestOpenPage.xaml` | UserControl | 43 | 6 | 开放赛模式 | 与已评比赛页面结构重复 |
| `Contest/ContestJudgedPage.xaml` | UserControl | 46 | 8 | 评审赛模式 | 与开放赛页面结构重复 |
| `Contest/ContestPickerWindow.xaml` | Window | 27 | 8 | 比赛选择 | 应使用统一选择 Dialog/List |

## 重复项

### 组件

- Button：约 126 个实例，同时存在全局模板、主窗口局部模板、水印页隐式模板和多个页面内联变体。
- Card/Panel：约 136 个 Border 实例，使用 `GlassPanel`、`SoftCard`、`SidebarCard`、`Panel` 及大量内联 Border 组合。
- Input：TextBox、PasswordBox、ComboBox 各有全局样式和页面级覆盖，Focus、错误态、禁用态不统一。
- Dialog：4 个独立 Window 使用不同标题栏、背景、圆角、阴影和按钮排布。
- Navigation/Sidebar：导航样式集中在超大的 `MainWindow.xaml`，没有独立的共享资源边界。
- Toolbar：照片查看、图库筛选、地图和批处理页各自使用 StackPanel/WrapPanel 拼装。
- List：列表行、选择态、悬停态、空态分别内联实现；缺乏统一 ListItem 容器。
- Status：Loading、Empty、Error 多为零散 TextBlock/ProgressBar，缺少一致的状态面板。

### 颜色

共发现 271 个不同的十六进制值。高频值包括透明白、`#E2E8F0`、`#94A3B8`、`#F8FAFC`、`#0C1117` 等；同一语义常由多个近似颜色表达。透明度被编码进 ARGB，导致浅色/深色主题无法可靠映射。

### 字体

至少存在 5 套字体栈，混用了 MiSans、HarmonyOS Sans SC、DengXian、Segoe UI、Segoe UI Variable Text/Display、Microsoft YaHei UI；另有 Cascadia Code/Consolas 用于等宽信息。全局正文、弹窗和局部组件的字体策略不一致。

目标策略：普通 UI 统一为 `Segoe UI Variable, Microsoft YaHei UI`；仅确有代码、路径或技术数值对齐需求时使用统一的等宽 Token。

### 圆角

共 20 种：2、3、4、8、9、10、11、12、13、14、15、16、17、18、19、20、22、24、30 及不对称值。按钮、卡片和 Dialog 缺乏稳定的层级关系。

### 阴影

共 6 处 DropShadowEffect，BlurRadius 包括 7、8、24、30、34；部分阴影偏重并与半透明表面叠加。应收敛为 None、Floating、Dialog 三档，普通卡片默认无阴影。

### 间距与密度

共 146 种 Margin/Padding 写法。4、5、6、7、8、10、12、14、16、18、20、22、26 等数值混杂；相似卡片和工具栏密度不同。页面最大宽度、页面边距、两栏/三栏 gutter 也没有全局约束。

## 可抽象的公共组件与样式

WPF 首阶段采用 ResourceDictionary + Style/ControlTemplate，不新增不必要的自定义控件：

- `AppButton`：Primary、Secondary、Ghost、Danger、Icon、Toolbar 六种用途。
- `AppInput`：TextBox、PasswordBox、ComboBox 的统一高度、边框、Focus 和 Validation。
- `AppCard`：Default、Subtle、Interactive、Selected 四种状态。
- `AppDialog`：统一窗口表面、标题、内容、操作区和默认/取消键行为。
- `AppSidebar` / `NavigationItem`：统一侧栏宽度、图标、标签、选中态和底部工具区。
- `AppToolbar`：统一 40px 控件高度、组间距、分隔符和溢出规则。
- `AppList` / `ListItem`：统一行高、选中、悬停、键盘焦点和虚拟化约束。
- `PageHeader`、`SectionHeader`、`FormField`、`StatusPanel`、`EmptyState`、`LoadingState`、`ErrorState`。

## 建议的 Design Tokens

- Color：`Color.Background.*`、`Color.Surface.*`、`Color.Border.*`、`Color.Text.*`、`Color.Accent.*`、`Color.Status.*`，每个主题提供同名 Brush。
- Spacing：0、2、4、6、8、12、16、20、24、32、40、48；页面使用 24/32，卡片使用 16/20/24。
- Radius：0、6、8、12、16；Button/Input 8，Card 12，Dialog 16。
- Typography：Caption 11、BodySmall 12、Body 13、Label 13、TitleSmall 16、Title 20、Display 28；只使用 Regular、SemiBold。
- Shadow：None、Floating、Dialog；浅色和深色主题分别定义颜色与透明度。
- Animation：Fast 150ms、Normal 180ms、Slow 220ms；统一 Standard 与 Emphasized easing。
- Icon Size：12、16、18、20、24；导航默认 18，工具栏默认 16。
- Layout：PagePadding 24/32、ContentMaxWidth 1440、ReadableMaxWidth 960、SidebarWidth 232、GridGutter 16/24、ControlHeight 36/40。

## 优先级与迁移建议

1. 建立 Token、主题切换和共享基础组件，不改页面结构。
2. 迁移 Dialog 与小页面，验证浅色/深色、Focus、Loading/Empty/Error。
3. 迁移 Cloud、Compression、Watermark、Contest、Map。
4. 分区迁移 MainWindow；每个分区单独编译验证。
5. 最后迁移 PhotoViewer 的工具栏，保留沉浸画布特例。
6. 删除已无引用的旧样式，生成最终 `docs/design-system.md`。

