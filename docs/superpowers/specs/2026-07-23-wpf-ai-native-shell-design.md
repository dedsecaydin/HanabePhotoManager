# Hanabe Photo Manager AI-native App Shell 设计

日期：2026-07-23  
状态：待用户书面确认  
范围：项目专用 UI Skill、MainWindow App Shell、Sidebar、顶部区域、首页布局及直接相关共享视觉资源

## 目标

将当前由外层工作区 Card 与首页多组大 Card 叠加形成的“卡片拼贴式后台页面”，改造成连续、克制、有层级的 AI-native 桌面应用壳层。Sidebar、顶部区域和主内容区共享同一视觉背景体系；信息层级主要依靠排版、留白、背景层次、透明度和轻分隔建立。

## 非目标与硬边界

- 不新增业务功能。
- 不修改 ViewModel、Command、Binding、API、数据结构、事件处理器和业务流程。
- 不修改设置中心或其它功能页面的内部信息架构。
- 不进行与 Shell、Sidebar、顶部或首页无关的重构。
- 不覆盖旧截图；Visual QA 使用独立目录。

## 方案

采用已批准的方案 A：连续 Shell 背景、Sidebar 轻微材质层、顶部轻分隔、首页分区化。

### App Shell

- 移除工作区最外层 `Card.Default` 的大卡片表达，改为连续工作平面。
- Sidebar 保持 232px 既有宽度，使用低对比半透明材质背景与轻分隔线，不再通过独立 Card 和明显外边距与内容区断开。
- 顶部区域作为工作区内部的稳定标题/操作带，使用统一背景与底部分隔，不包装成独立大圆角盒子。
- 内容区继续承载所有现有页面和绑定，只改变壳层背景、边距、对齐和视觉层级。

### 首页

- 统计区域从三个独立大 Card 改为同一摘要区中的三列轻量指标，以列间轻分隔、标题和数字层级组织。
- 最近照片区域取消外层大 Card，保留照片缩略图的小卡片语义。
- 设备与文件夹区域取消包裹整个分区的大 Card；保留设备项、文件夹项等具有独立业务语义的小型交互项。
- 各分区使用统一左边线、稳定垂直节奏、Section 标题和必要 Separator，避免无层级的大面积空白。

## 视觉效果与 Token

- 更新 `docs/design-system.md`，把玻璃、渐变、阴影和发光的全面禁止改为受控、分层使用。
- 原始颜色仍只进入 Light/Dark 原始颜色字典；语义 Brush 在对应 Brush 字典中映射；页面不得写颜色字面量。
- 新增或扩展共享 Shell/材质/高光/焦点语义资源时，Light/Dark 必须具有同名同类型资源键。
- 普通内容区无阴影或仅使用极轻分隔；Sidebar 与顶部只使用轻材质层和低对比高光。
- Popup/Dialog 保留中等阴影；重点摘要或选中态可使用单一、低强度强调层；发光仅用于焦点或选中状态。
- 渐变仅限 Shell 背景或重点区域的低饱和环境渐变，不重复铺满组件。
- WPF 背景模糊若现有技术路径无法在不改代码的条件下稳定支持，则以 Token 化半透明材质表达替代，不新增代码行为。

## 项目专用 Skill

创建 `wpf-ai-native-shell`，默认放置在项目 `.codex/skills/wpf-ai-native-shell/`，内容直接引用本项目的规范路径、MainWindow、主题资源结构、验证命令和 Visual QA 清单。Skill 自动匹配 Hanabe Photo Manager 的 MainWindow、App Shell、Sidebar、顶部、首页、一体化桌面视觉、设置布局、DPI、截断、主题和 Visual QA 任务；显式调用方式为 `$wpf-ai-native-shell`。

Skill 的执行门槛：先验证项目根目录与指定文件，读取项目规范，审计绑定/命令/事件与资源复用，输出最小计划，实施后执行 Release Build、全量 Test、win-x64 Publish 和 Visual QA。

## 实施边界

预计只修改：

- `.codex/skills/wpf-ai-native-shell/SKILL.md` 及必要的 Skill UI 元数据。
- `docs/design-system.md` 与共享资源清单类文档（仅在公共资源契约变化时）。
- `src/HanabePhotoManager.App/MainWindow.xaml`。
- `Themes/Colors`、`Themes/Tokens`、`Themes/Controls`、主题入口中与 Shell 直接相关的资源。

任何现有 Binding、Command、事件处理器属性值必须原样保留。若实现需要修改范围外文件或业务层，立即停止并报告。

## 验证

依次执行：

1. Skill `quick_validate.py`。
2. `dotnet build HanabePhotoManager.sln -c Release /warnaserror`。
3. `dotnet test HanabePhotoManager.sln -c Release --no-build`。
4. `dotnet publish src/HanabePhotoManager.App/HanabePhotoManager.App.csproj -c Release -r win-x64`（若项目已有正式发布脚本，以项目脚本为准）。
5. 启动发布产物并进行 Visual QA。

Visual QA 在独立目录保存首页、导入、照片图库、人物查找、地图照片、批量压缩、批量水印、设置八张新截图，并检查 Light/Dark、小窗口、中文长文本、常见 DPI、Hover/Pressed/Focus/Disabled、Loading/Empty/Error、遮挡、溢出、截断和错位。

## 完成标准

- Sidebar、顶部和内容区形成连续 App Shell。
- 首页不再由多个外层大 Card 拼贴，但缩略图、设备项和文件夹项仍保留清晰语义。
- Light/Dark 使用统一资源契约，配色保持柔和、低饱和、中性、克制。
- 视觉效果集中在壳层氛围、浮层和状态强调，不影响文字对比度或性能。
- Build、Test、Publish 均有新鲜命令输出证据；无法自动化的 Visual QA 项明确标注人工检查状态。

## 环境限制

`D:\APP` 当前不含 `.git`，因此本轮只写设计文档，不创建或伪造提交记录。
