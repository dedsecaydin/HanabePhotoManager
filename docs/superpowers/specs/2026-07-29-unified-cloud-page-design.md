# 统一网盘页面与深色网页设计

## 目标

把左侧“百度网盘”和“夸克网盘”两个入口合并为一个“网盘”入口，在页面顶部用横向按钮切换服务；保留两套 WebView2 的登录、地址与浏览历史。应用处于深色模式时，嵌入网页区域也同步变暗，切回浅色后能完整撤销，不污染媒体、二维码或验证码。

## 现状与实现约束

- `MainWindow.xaml` 当前同时常驻 `BaiduCloudPageHost` 与 `QuarkCloudPageHost`，两者分别绑定 `IsBaiduCloudPage`、`IsQuarkCloudPage`。
- `CloudPage` 使用 `InitialUrl` 的 host 生成独立用户数据目录：
  - 百度：`%LOCALAPPDATA%\HanabePhotoManager\WebView2\pan_baidu_com`
  - 夸克：`%LOCALAPPDATA%\HanabePhotoManager\WebView2\pan_quark_cn`
- `CloudPage.IsActive` 已实现首次按需初始化、非活动实例 `TrySuspendAsync()`、重新选中时 `Resume()`，因此合并页面不应重建 WebView2。
- 当前主题由静态 `ThemeManager.Apply` 更换资源字典；它没有主题变更事件，也不会通知已初始化的 WebView2。
- 项目引用 `Microsoft.Web.WebView2 1.0.4078.44`。本地包 API 已确认提供：
  - `CoreWebView2.Profile.PreferredColorScheme`
  - `CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync`
  - `CoreWebView2.RemoveScriptToExecuteOnDocumentCreated`
  - `CoreWebView2.CallDevToolsProtocolMethodAsync`
- `PreferredColorScheme` 会设置 `prefers-color-scheme`，并影响 WebView2 的菜单、提示与对话框；它不能保证不支持深色媒体查询的第三方网页自行变暗，所以仍需要安全的页面样式兜底。

## 页面结构

- 左侧导航只保留键 `Cloud`，显示名称“网盘”。
- 页面标题为“网盘”，副标题说明可以在百度网盘与夸克网盘之间切换。
- 标题下方提供“百度网盘”“夸克网盘”两个横向分段按钮，使用现有主题化分段样式，不再重复显示“网盘”功能名称。
- 按钮支持鼠标、`Tab` 聚焦和左右方向键切换，选中态由 `SelectedCloudProvider` 决定。
- 内容区复用两个现有 `CloudPage` 实例；切换时只改变选中状态与可见性，不销毁实例、不修改地址。
- 切换使用 180ms 交叉淡入：
  1. 新页面先设为可见、透明度 0；
  2. 新页面开始激活并淡入；
  3. 动画完成后隐藏并挂起旧页面。
- 系统关闭客户端动画时跳过淡入，直接切换。
- 快速连续切换时取消上一轮动画，以最后一次选择为准；不能让两个 WebView2 最终都保持 Active。

## 状态模型

新增：

```csharp
public enum CloudProviderChoice
{
    Baidu,
    Quark
}
```

`MainWindowViewModel` 保存并暴露：

- `SelectedCloudProvider`
- `IsCloudPage`
- `IsBaiduCloudSelected`
- `IsQuarkCloudSelected`
- `SelectCloudProviderCommand`
- `ShowCloudCommand`

`CurrentPage` 的新标准值为 `Cloud`。`PageTitle` 固定为“网盘”，`PageSubtitle` 不随子项重复功能名称。

选择某一服务的顺序是：先更新 `SelectedCloudProvider`，再进入 `Cloud`。这样从旧命令、新手指南或其他快捷入口进入时，首帧就是正确服务，不会先闪出另一个网页。

## 兼容旧导航命令

下列公开命令暂时保留，不直接删除：

- `ShowBaiduCloudCommand`：设置 `SelectedCloudProvider=Baidu`，再执行 `ShowCloudCommand`。
- `ShowQuarkCloudCommand`：设置 `SelectedCloudProvider=Quark`，再执行 `ShowCloudCommand`。

兼容命令执行后：

- `CurrentPage` 必须为 `Cloud`；
- `IsCloudPage=true`；
- 对应的 `Is*CloudSelected=true`；
- 触发 `CurrentPage`、`IsCloudPage`、选择状态、标题和副标题通知。

旧的 `IsBaiduCloudPage`、`IsQuarkCloudPage` 若外部测试或绑定仍依赖，可在一个过渡版本中保留为：

- `IsBaiduCloudPage => IsCloudPage && IsBaiduCloudSelected`
- `IsQuarkCloudPage => IsCloudPage && IsQuarkCloudSelected`

新 XAML 只绑定标准的 `IsCloudPage` 与选择状态，避免继续扩散旧概念。

## 导航顺序迁移

读取旧 `NavigationOrder` 时先迁移，再交给通用规范化策略：

1. 找到 `BaiduCloud` 与 `QuarkCloud` 中最靠前的索引；
2. 在该索引插入一个 `Cloud`；
3. 删除所有 `BaiduCloud`、`QuarkCloud` 重复项；
4. 若旧顺序没有两个旧键，则按新默认顺序补入 `Cloud`；
5. 保存时只写 `Cloud`。

示例：

- `Home, QuarkCloud, Preview, BaiduCloud` → `Home, Cloud, Preview`
- `Home, Cloud, BaiduCloud, Cloud` → `Home, Cloud`

默认导航顺序也只包含一个 `Cloud`。这样保留用户原本的大致位置，同时不会重置其自定义排序。

## 会话与资源生命周期

- 百度和夸克继续使用当前由 `InitialUrl` host 生成的独立 WebView2 用户数据目录；不改目录名、不搬迁、不清 Cookie。
- 统一页面仍持有两个 `CloudPage` 实例，因此现有登录态、当前地址、前进后退历史都保留。
- 首次选择某服务时才初始化它的 WebView2。
- 只有当前选中的服务 `IsActive=true`；另一个实例在动画结束后 `TrySuspendAsync()`。
- 切换回来时调用 `Resume()`，不导航首页；只有用户点击“首页”才回到 `InitialUrl`。
- 主窗口关闭时分别 `Dispose()` 两个实例，行为与当前一致。
- WebView2 初始化失败只影响对应服务，另一个服务仍可正常切换使用。

## 深色模式传播

### 主题通知

给 `ThemeManager` 增加只读主题状态与变更通知，例如：

```csharp
public static event EventHandler<AppTheme>? ThemeChanged;
```

`Apply` 在资源字典替换并更新 `Current` 后触发事件。`CloudPage` 在加载时订阅、释放时退订，并通过 `IsDarkTheme` 依赖属性或内部方法接收当前主题。

设置页直接调用 `ThemeManager.Apply` 与主窗口按钮调用 `ThemeManager.Toggle` 都必须走同一通知链。不能只在主窗口点击事件里调用网盘更新，否则设置页切换主题时网页不会同步。

### 第一层：WebView2 原生首选色

WebView2 初始化完成后，以及每次主题变化时，分别设置：

```csharp
CoreWebView2.Profile.PreferredColorScheme =
    isDark
        ? CoreWebView2PreferredColorScheme.Dark
        : CoreWebView2PreferredColorScheme.Light;
```

这一层负责：

- 向网页暴露正确的 `prefers-color-scheme`；
- 同步 WebView2 菜单、提示、选择器等原生 UI；
- 让百度/夸克将来若增加原生暗色支持时自动生效。

不能使用 `Auto` 代替应用主题，因为用户可能让 Hanabe 使用深色、Windows 仍使用浅色。

### 第二层：可撤销的安全样式兜底

对于不响应 `prefers-color-scheme` 的页面，`CloudPage` 注入一个带固定 id 的 style，例如 `hanabe-cloud-dark-style`，仅调整常规页面表面与文字：

- `html`、`body` 和常见纯色容器降低背景亮度；
- 常规文字、边框、输入框使用可读的深色配色；
- 设置 `color-scheme: dark`；
- 不对整个页面使用 `filter: invert()`。

明确排除以下元素及其子树：

- `img`
- `picture`
- `video`
- `canvas`
- `svg`
- `iframe`
- `[role="img"]`
- 与验证码、二维码、安全校验有关的元素（通过稳定属性、`aria-label`、class/id 关键词白名单识别）

原因：整页反色再二次反色虽看似省事，但会破坏 CSS 背景图、透明 PNG、二维码、canvas 渲染和部分嵌套 iframe，无法满足“不误反色”。

脚本策略：

1. 在初始化后用 `AddScriptToExecuteOnDocumentCreatedAsync` 注册一次主题桥接脚本，保存返回的 script id；
2. 脚本读取主机写入的当前主题标记，创建或更新固定 id 的 style；
3. 每次主题变化对当前文档调用 `ExecuteScriptAsync`，立即应用或移除 style；
4. 对后续完整导航，文档创建脚本自动复用当前主题；
5. 切回浅色时删除该 style，并恢复由脚本写入的主机属性，不保留内联颜色；
6. `Dispose` 时调用 `RemoveScriptToExecuteOnDocumentCreated(scriptId)` 并解除主题事件。

若页面使用封闭 Shadow DOM 或跨域 iframe，样式无法安全深入时保持其原样；不得以全局反色换取“看起来全黑”。顶层 WebView2 外框、加载态和错误态始终使用应用的深色资源，因此不会出现整块白色空白。

### DevTools 自动深色的取舍

当前包支持 `CallDevToolsProtocolMethodAsync`，但 `Emulation.setAutoDarkModeOverride` 属于 Chromium DevTools 协议能力，随运行时变化且会自动改色媒体内容。它不作为正式实现路径，只允许在开发诊断中验证，不进入生产默认行为。

### 异常与并发

- 主题可能在 WebView2 尚未初始化、正在导航、已挂起或正在释放时变化；保存最新主题状态，初始化/恢复后补应用。
- 主题应用使用一个串行异步门或版本号；较旧任务完成时不得覆盖较新的主题。
- `ExecuteScriptAsync`、脚本移除或 Profile 设置失败只记录诊断，不让网盘页面崩溃。
- 两个 WebView2 即使其中一个挂起，也各自记录最新主题；恢复时先应用主题再显示。

## 新手指南

保留百度与夸克两个介绍步骤，但都导航到 `Cloud`：

- 百度步骤：先设置 `SelectedCloudProvider=Baidu`，再进入统一页面，箭头指向“百度网盘”按钮。
- 夸克步骤：切换 `SelectedCloudProvider=Quark`，箭头改指向“夸克网盘”按钮。

步骤数量可保持不变。介绍文本说明登录会话分别保存，不暗示两个网站共享账号或 Cookie。

## 测试

### 导航与迁移

- 默认导航只存在一个键为 `Cloud`、名称为“网盘”的入口。
- 各种旧顺序迁移后只有一个 `Cloud`，且位置采用两个旧入口的最早位置。
- 重复迁移具有幂等性。
- `ShowBaiduCloudCommand`、`ShowQuarkCloudCommand` 进入统一页面并选中正确服务。
- `PageTitle` 为“网盘”，不再返回两个旧标题。

### 页面切换与会话

- 横向分段按钮及键盘导航存在，选中态正确。
- 180ms 淡入存在；关闭客户端动画时不执行动画。
- 快速连续切换后只留下最后选择的服务 Active。
- 两个 `InitialUrl` 和用户数据目录规则保持不变。
- 已初始化实例切出时挂起，切回时恢复且不自动导航首页。
- 切换服务不会 Dispose 或重新创建 WebView2。

### 深色模式

- `ThemeManager.Apply` 在主题实际变化时发出一次通知；重复应用同一主题不得引发竞态。
- WebView2 初始化前切换主题，初始化后使用最新的 `PreferredColorScheme`。
- 深色时两个 Profile 都为 `Dark`，浅色时都为 `Light`。
- 深色注入脚本只注册一次，不随每次导航累积。
- 当前页面深色切浅色后固定 id 的 style 被移除。
- 后续导航仍会应用当前主题。
- 兜底 CSS 不包含页面级 `filter: invert`，并明确排除 `img`、`video`、`canvas`、`svg`、`iframe` 与验证码/二维码区域。
- 某一 WebView2 尚未初始化、已挂起或初始化失败时切换主题不会抛出未处理异常。

### 回归

- 浏览器后退、前进、刷新、首页与错误重试行为不变。
- 百度与夸克登录态保持。
- 新手指南两个网盘步骤进入统一页面并选中对应按钮。
- 浅色/深色主题、导航、云页面与设置全量测试通过。

## 验收标准

- 左侧只显示一个“网盘”。
- 页面顶部可平滑横向切换百度/夸克，切换回来仍停留在原地址并保有登录态。
- 深色模式下 WebView2 原生 UI 和常规网页表面同步变暗，页面外框无白块。
- 照片、视频、canvas、SVG、二维码和验证码颜色不被全局反转。
- 切回浅色无需刷新即可撤销主机注入的深色样式。
- 旧导航设置、旧命令与新手指南不会失效。

## 非目标

- 不新增网盘 API，不改变登录方式。
- 不合并两个网站的 Cookie、缓存或 WebView2 用户数据目录。
- 不破解网站样式、验证码或登录流程。
- 不保证跨域 iframe、封闭 Shadow DOM 内部完全深色；优先保证内容正确与安全验证可用。
- 不使用 Docker、外部服务或未经授权的自动登录手段。
