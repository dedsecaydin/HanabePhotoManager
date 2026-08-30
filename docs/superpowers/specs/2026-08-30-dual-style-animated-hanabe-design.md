# 双画风动态 Hanabe 助手设计

## 目标

为最小化任务助手提供可切换的 Q 版动态与 8-bit 像素动态形象。动画由结构化任务状态驱动，主窗口恢复时隐藏并暂停，不依赖网络。

## 用户设置

新增 `HanabeAssistantVisualStyle`，可选：

- `ChibiAnimated`：红黑洛丽塔 Q 版 GIF，默认值。
- `PixelAnimated`：同一角色的 8-bit 像素 GIF。
- `Static`：现有静态 Q 版 PNG。
- `Off`：与现有 `ShowHanabeAssistant = false` 等价；迁移时保留旧开关兼容。

设置页以单选下拉框呈现，修改后即时保存；动态样式只影响视觉，不改变任务、导入或扫描行为。

## 状态模型

新增公开给 UI 绑定的 `HanabeAssistantState`：

- `Idle`：没有活动任务。
- `Scanning`：库预读取、日期读取、媒体扫描、来源分析与人物识别。
- `Checking`：重复检查、SHA-256、续传核对与备注校验。
- `Importing`：复制、移动、恢复导入及传输验证。
- `Completed`：任务正常完成。
- `Error`：任务失败、中断或取消。

ViewModel 在任务生命周期的结构化入口设置状态，不通过匹配中文 `ProgressLabel` 推断。`Completed` 与 `Error` 保持 3 秒后回到 `Idle`；新任务可立即打断过渡状态。

## 动画资源

每套动态画风包含以下文件：

```text
Assets/Hanabe/Animated/Chibi/idle.gif
Assets/Hanabe/Animated/Chibi/scanning.gif
Assets/Hanabe/Animated/Chibi/checking.gif
Assets/Hanabe/Animated/Chibi/importing.gif
Assets/Hanabe/Animated/Chibi/completed.gif
Assets/Hanabe/Animated/Chibi/error.gif

Assets/Hanabe/Animated/Pixel/idle.gif
Assets/Hanabe/Animated/Pixel/scanning.gif
Assets/Hanabe/Animated/Pixel/checking.gif
Assets/Hanabe/Animated/Pixel/importing.gif
Assets/Hanabe/Animated/Pixel/completed.gif
Assets/Hanabe/Animated/Pixel/error.gif
```

- 角色必须保持黑红双马尾、大蝴蝶结、哥特裙和相机识别特征。
- GIF 使用透明背景、正方形画布、有限色彩，避免大面积闪烁。
- 循环动画控制在 8–16 帧；完成/失败可以非循环并由状态计时器结束。
- 所有资源作为 WPF Resource 嵌入；任意资源加载失败时回退 `hanabe-assistant.png`。

## WPF 动画控件

新增 `AnimatedGifImage` 控件：

- 使用 `GifBitmapDecoder` 读取嵌入资源的帧和帧延迟。
- 使用单个 `DispatcherTimer` 顺序显示帧，不增加第三方包。
- 不可见、窗口隐藏或应用退出时停止计时器并释放帧引用。
- `Source` 变化时取消旧动画并从新动画第一帧开始。
- 尊重 Windows 客户端动画设置；关闭动画时显示第一帧。
- 像素风使用 `BitmapScalingMode.NearestNeighbor`，Q 版使用高质量缩放。

## 悬浮窗行为

- 主窗口处于 `Minimized` 且助手未关闭时显示；Normal/Maximized 时隐藏。
- 角色区域扩大为主要视觉，进度文字和进度条保持紧凑可读。
- 动画状态、进度、预计剩余时间继续绑定同一 `MainWindowViewModel`。
- 悬浮窗保持可拖动、置顶、不占任务栏；恢复主窗口后动画立即暂停。

## 测试与验收

- 单元测试覆盖任务类型到助手状态映射、完成/错误回落策略、样式设置迁移与持久化。
- 控件测试覆盖 GIF 帧解析、帧延迟下限、Source 切换、隐藏暂停和损坏资源回退。
- XAML 契约测试覆盖设置项、动态资源路径、像素缩放和静态回退。
- Release `/warnaserror` 构建、全量测试和自包含发布必须通过。
- 运行验收覆盖两套画风的 6 个状态、最小化显示、恢复隐藏、快速任务切换以及禁用动画。
