# HanabePhoto Hermes Master Guide

> 文件建议路径：`docs/HERMES_MASTER_GUIDE.md`
>
> 状态：HanabePhoto 跨 Agent 总控规范  
> 适用对象：Hermes、ChatGPT Desktop、Codex、其他 Coding Agent  
> 核心原则：**当前仓库与实际运行状态永远高于历史对话和 Agent 记忆。**

---

# 1. Hermes 的角色

Hermes 是 HanabePhoto 的：

- 总协调 Agent（Coordinator）
- 当前上下文管理者（Context Coordinator）
- UI/UX 重构协调者
- Bug Hunting / QA 协调者
- 多 Agent 任务分发者
- 阶段验收与进度汇报者

Hermes 不应把用户的一句话原样转发给 ChatGPT 或 Coding Agent。

Hermes 必须先理解当前项目，再构造完整上下文，然后分发任务。

核心工作链：

```text
Current Repository
        ↓
Current Runtime
        ↓
Current Docs / Tests / Recent Changes
        ↓
Feature Inventory
        ↓
Current Context Package
        ↓
ChatGPT Desktop Design / Review
        ↓
Codex / Coding Agent Implementation
        ↓
Build + Run + Test
        ↓
Screenshot / Runtime QA
        ↓
Bug Hunting + Regression Check
        ↓
ChatGPT Review
        ↓
Fix
        ↓
Documentation / Handoff
```

---

# 2. 最高优先级：Current State > History

HanabePhoto 持续由用户、Hermes、ChatGPT Desktop、Codex 和其他 Agent 并行迭代。

任何 Agent 都不得假设自己的历史上下文是完整的。

事实优先级：

```text
用户最新明确要求
        ↓
当前实际运行程序
        ↓
当前仓库代码
        ↓
当前 Git 工作区 / 当前 Branch
        ↓
当前测试结果
        ↓
当前项目文档
        ↓
最近修改记录 / Commit
        ↓
AGENT_HANDOFF.md
        ↓
Agent 历史上下文
        ↓
旧截图 / 旧设计稿 / 旧需求
```

必须遵循：

> Current State > Historical Context  
> Repository > Memory  
> Runtime Behavior > Assumption  
> Verify > Assume  
> Preserve Existing Features > Redesign Convenience  
> Incremental Change > Rewrite

ChatGPT Desktop 即使记得过去的 HanabePhoto，也只能把历史当参考。

---

# 3. 当前项目规范必须先被发现，不能硬编码旧版本

每次任务开始时，Hermes 必须重新读取当前项目。

至少检查：

- `AGENTS.md`
- `AGENT_HANDOFF.md`
- `README*`
- `docs/`
- `.ai/`
- `.codex/skills/`
- Solution / csproj
- 当前 Design System
- 当前 Theme / ResourceDictionary
- 当前 MainWindow / App Shell
- 当前 View / ViewModel
- 当前 Service / Infrastructure
- 当前 Tests
- 当前版本记录
- 最近修改

如果文件路径已经变化，以当前仓库为准。

不要为了符合旧总纲强行创建旧页面、旧服务或旧目录。

---

# 4. 当前已知 UI 基线：必须兼容，不得无脑推翻

当前 HanabePhoto 已经采用 Token 驱动的 WPF UI 设计方向。

Hermes 应先检查当前实现是否仍与这些规则一致，再决定如何演进：

- Light / Dark 使用同名语义资源键
- 页面不直接写原始颜色
- 页面优先消费共享 Token / Component
- UI 字体以当前项目 Design System 为准
- 共享 Motion Token 优先复用
- 普通内容区域尽量扁平
- 视觉服务于照片管理效率
- UI 不应改变 ViewModel / Command / Binding / API / 数据流
- 新增 UI 改动必须能够逐阶段 Build / Test

**如果当前仓库已经修改这些规则，以当前仓库文档和实际实现为准。**

不得因为本文件中的历史描述覆盖更新后的真实设计系统。

---

# 5. 主题策略

目标视觉方向可以优先以 Dark Theme 做高质量审查，因为专业影像工作流在深色环境下更容易评估内容。

但是：

**不得擅自改变当前默认主题行为。**

如果当前版本默认 Light：

- 保留默认 Light
- 保留 Dark
- 两套主题必须保持相同资源契约
- 用户主题偏好必须继续持久化

只有用户明确要求时，才允许修改默认主题策略。

---

# 6. HanabePhoto 产品定位

HanabePhoto 是：

**Windows 桌面专业照片 / 视频管理工具。**

不是：

- Android App 放大版
- 普通相册
- 网页 Dashboard
- Windows 设置页面
- Terminal Emulator
- 纯 ncurses TUI

最终设计方向：

> **Material Design 3 × Codex Desktop × Lightroom**

同时具备：

> **Terminal-inspired Professional Desktop Tool**

的工具感。

---

# 7. 中文设计关键词

- Material Design 3 Desktop
- Dense Desktop UI
- Professional Productivity
- Photo Management
- Codex Desktop Aesthetic
- Developer Tool Aesthetic
- Terminal Inspired
- Minimal
- Flat
- Low Saturation
- Unified App Shell
- High Information Density
- Content First
- Image First
- Keyboard First
- Contextual UI
- Fast Visual Feedback
- Subtle Motion
- Modern Native Desktop

整体观感：

**现代、克制、专业、紧凑、统一、连续、高效。**

---

# 8. Material Design 3 的使用方式

Material Design 3 是设计体系，不是 Android 模板。

重点借鉴：

- Color System
- Typography
- Shape
- Elevation
- Motion
- Component States
- Design Tokens
- Interaction Feedback

必须进行 Desktop Adaptation：

> Reduce mobile characteristics.  
> Increase desktop information density.

不要机械复制 Google App。

---

# 9. Unified App Shell

整体目标：

```text
┌──────────────────────────────────────────────────────────┐
│ Compact Top App Bar                                      │
├────────────┬───────────────────────────────┬─────────────┤
│ Navigation │                               │ Inspector   │
│ / Sidebar  │       Main Workspace          │ / Details   │
│            │                               │             │
├────────────┴───────────────────────────────┴─────────────┤
│ Optional Compact Status / Background Tasks               │
└──────────────────────────────────────────────────────────┘
```

Shell、顶部、Sidebar、内容区应形成连续工作空间。

不要把页面做成：

> Card + Card + Card + Card Dashboard

---

# 10. 当前功能高于设计示例

任何页面、菜单、导航、筛选、按钮列表都只能作为“设计参考”。

例如总纲里出现：

- RAW
- JPG
- MP4
- 人物
- 地图
- 已修
- 空间树

并不代表必须新增这些功能。

Hermes 必须先检查当前版本：

- 已经存在 → 复用并改视觉
- 名称改变 → 使用当前名称
- 已经删除 → 不恢复
- 尚未实现 → 除非用户要求，否则不新增
- 已由其他 Agent 新增 → 纳入当前 Feature Inventory

---

# 11. 修改前必须建立 Feature Inventory

任何重要 UI / UX 修改前，扫描当前相关模块，生成 Feature Inventory。

示例：

```text
PHOTO PAGE FEATURE INVENTORY

Navigation
✓ Date navigation
✓ Folder navigation

Filters
✓ RAW
✓ JPG
✓ MP4

Gallery
✓ Virtualization
✓ Viewport-priority loading
✓ Progressive thumbnail loading

Interaction
✓ Multi-select
✓ Ctrl-select
✓ Shift-select
✓ Spacebar pan
✓ Ctrl + Wheel zoom
```

这只是格式示例。

**真实清单必须来自当前代码和运行结果。**

修改后必须再次检查。

除非用户明确要求删除，否则当前稳定功能不得消失。

---

# 12. 功能保护顺序

```text
Preserve Function
      ↓
Improve Presentation
      ↓
Improve Interaction
      ↓
Improve Animation
      ↓
Polish
```

禁止：

```text
Redesign UI
      ↓
Rewrite Stable Business Logic
      ↓
Regression
```

---

# 13. 不得重复制造第二套系统

如果项目已经有：

- Thumbnail Loader
- Selection Service
- Filter System
- Theme Manager
- Cache
- Background Task Service

优先：

- Reuse
- Extend
- Refactor
- Consolidate

不要新增：

- NewThumbnailLoader2
- SelectionManagerV2
- NewFilterService
- ThemeManagerNew

除非现有架构确实无法满足需求，并且有明确迁移与回归方案。

---

# 14. UI 视觉规则

优先：

- 中性低饱和色
- Tonal surface
- 轻分隔
- 高信息密度
- 连续工作区
- 内容优先
- 图片优先
- 紧凑 Desktop spacing
- 清晰 Typography hierarchy

避免：

- 巨大 Card Dashboard
- Card inside Card
- 粗边框
- 夸张大圆角
- 大面积高饱和色块
- 重阴影
- 强玻璃拟态
- 霓虹发光
- 强渐变
- 巨大 Padding
- Mobile Android layout
- Giant FAB

如果当前 Design System 已有 Radius / Spacing Token：

**必须使用当前 Token，不得凭空写新的视觉常量。**

---

# 15. Typography

正常 UI：

使用当前 Design System 的 UI Font Token。

技术信息可使用 Mono Token，例如：

- 文件路径
- EXIF 数值
- 文件大小
- Resolution
- FPS
- RAW/JPG/MP4 状态
- 后台任务
- Progress
- Technical Metadata

不要把整个应用做成等宽字体。

---

# 16. Terminal Inspired 的正确理解

Terminal 气质来自：

- 快速反馈
- 精确对齐
- 高信息密度
- 状态清晰
- Monospace metadata
- Command Palette
- Keyboard shortcut
- Background Task
- Structured progress

示例：

```text
INDEXING

D:\Photos\2026\08

██████████████░░░░ 74%

1,482 / 2,003 assets

RAW   842
JPG   566
MP4    74
```

目标：

> Terminal Inspired GUI

不是：

> Pure Terminal UI

---

# 17. 动画总原则

动画设计公式：

> **Fast × Subtle × Precise × Interruptible**

关键词：

- Material Motion
- Desktop Motion
- Codex-like Transition
- Developer Tool Motion
- Subtle Fade
- Short Slide
- Cross Fade
- Shared Axis
- Soft Scale
- Immediate Feedback
- Interruptible Animation

动画应该让界面：

**很快，但不是突然跳变。**

---

# 18. 必须优先复用当前 Motion Token

如果当前项目已经存在类似：

- Fast
- Normal
- Slow

等共享 Motion Token：

不得重新建立平行体系。

目标动画映射到当前 Token。

例如当前常见基线可以映射为：

```text
Fast   → 微交互 / Hover / Press
Normal → Menu / Selection / Navigation transition
Slow   → Inspector / Sidebar / Large overlay
```

如果实际仓库 Token 已变化，以当前实现为准。

页面不得各自创建独立 duration / easing。

---

# 19. 动画速度目标

推荐体验范围：

- Hover / Press / Selection：80–150ms
- Menu / Tooltip / Chip：100–180ms
- Sidebar / Inspector：160–220ms
- Workspace switch：180–260ms
- Dialog / Overlay：180–240ms

但如果当前 Design System 已固定 150/180/220ms：

**优先复用现有 150 / 180 / 220ms。**

不要为了“更精确”破坏共享 Token。

正常动画尽量不超过 300ms。

---

# 20. 动画禁止项

禁止：

- Bouncy
- Elastic
- Overshoot
- Excessive Spring
- Huge Slide
- Long Fade
- Large Zoom
- Decorative Motion
- Parallax Abuse
- Mobile-style full-page transition

动画不是表演。

---

# 21. Navigation 切换

同级页面切换：

推荐：

```text
Cross Fade
+
Very Small Translate
```

目标位移约：

4–8px。

目标时长：

约 Normal Motion Token。

不要整页从屏幕一侧飞入。

---

# 22. 上下级内容

例如：

Gallery → Photo Detail

可以使用：

- Shared Axis
- Content Transform
- Small Depth Transition

位移保持克制，例如：

8–16px。

---

# 23. Sidebar / Inspector

打开和关闭必须：

- 快
- 可中断
- 可反向
- 不阻塞输入

例如：

Open → Close → Open

不能等上一段动画播完再响应。

Panel 容器和内部内容可以轻微错开，但不要形成复杂 Sequence。

---

# 24. Hover / Selection

照片 Hover：

优先：

- Subtle overlay
- Action fade-in

Scale 如果存在，应非常轻。

Selection：

必须立即确认。

优先：

- Tonal overlay
- Small check indicator
- Focus ring
- Small state transition

图片仍是视觉主体。

---

# 25. Thumbnail / Gallery 动画

未缓存：

Placeholder / Skeleton → Thumbnail Cross Fade。

已缓存：

立即显示。

不能为了播放动画延迟图片。

Gallery 动画不得破坏：

- Virtualization
- Viewport Priority
- Scroll performance
- Thumbnail queue
- Memory behavior

性能永远优先于动画。

---

# 26. Command Palette / Context Menu

Command Palette：

- 轻微 background dim
- opacity
- 4–8px translate
- 非夸张 scale

Context Menu：

- opacity
- 极轻微 0.98 → 1 scale

目标风格：

Codex / Raycast / Linear 类现代桌面工具感。

---

# 27. Background Task

IMPORT / INDEX / SCAN / AI / THUMBNAIL / COMPRESSION 等：

优先：

- Status area
- Background task indicator
- Compact progress panel
- Snackbar

避免不必要的阻塞式 Dialog。

状态变化尽量保持同一位置：

```text
Import
→ Importing 42%
→ Completed
```

而不是反复创建/销毁大组件。

---

# 28. 动画性能

优先：

- Opacity
- Transform

谨慎：

- Width
- Height
- Margin
- 大范围 Layout Animation

Gallery 尤其避免大规模 Layout 重算。

如果动画影响：

- FPS
- UI Thread
- 输入延迟
- Virtualization
- Thumbnail Loading

立即降级动画。

---

# 29. Reduced Motion

如果当前技术实现支持或后续新增：

Reduced Motion 下：

减少：

- Translate
- Scale
- Container transform

保留必要的短 Fade 和状态反馈。

不得因为 Reduced Motion 导致状态难以辨认。

---

# 30. Hermes 调用 ChatGPT Desktop 前必须构造 Context Package

每次调用 ChatGPT Desktop 前生成：

```text
HanabePhoto Current Context Package

Version:
Branch:
Latest Commit / Recent Change:

Task:
本次目标

Current Implementation:
当前实现

Current Runtime Behavior:
实际运行状态

Existing Features:
已有功能

Recent Changes:
最近其他 Agent 修改

Known Bugs:
已知问题

Must Preserve:
绝对不能破坏

Files Involved:
相关文件

Screenshots:
当前界面截图

Design System:
当前设计规范

Motion System:
当前动画 Token / Easing

Expected Result:
预期结果

Regression Risks:
潜在风险
```

---

# 31. 给 ChatGPT Desktop 的固定前置说明（中文）

每次 UI / UX / 动画 / 架构 Review 前附带：

> 你可能拥有过去关于 HanabePhoto 的上下文，但这些上下文可能已经过时。  
> HanabePhoto 正在持续开发，很多功能可能已经由其他 Agent 在你上次参与后完成。  
> 不得把历史对话作为当前版本事实来源。  
> 当前仓库、当前运行行为、当前截图、当前测试、当前项目文档和 Hermes 提供的 Current Context Package 才是权威信息。  
> 修改前必须先理解当前实现。  
> 不得因为某项功能不存在于你的历史上下文中，就删除、覆盖或者重新实现当前已有功能。  
> 优先复用当前系统。  
> UI 重构不得破坏业务逻辑。  

---

# 32. Fixed ChatGPT Desktop Preamble (English)

> You may have previous context about HanabePhoto, but that context may be outdated.  
> Do not treat previous conversations as the source of truth.  
> HanabePhoto is under active development and many features may have been implemented by other agents since your last interaction.  
> Treat the current repository, current runtime behavior, current screenshots, current tests, current documentation, and the Current Context Package provided by Hermes as authoritative.  
> Before proposing changes, inspect and understand the current implementation.  
> Never remove, replace, or reimplement an existing feature merely because it does not exist in your previous context.  
> Prefer reuse and incremental changes.  
> UI refactoring must preserve current business behavior unless the user explicitly requests a behavior change.

---

# 33. ChatGPT Desktop 的角色

ChatGPT Desktop 主要作为：

**UI/UX Design Director + Reviewer**

负责：

- UI Review
- UX Review
- Material Design 3 Review
- Motion Review
- Density Review
- Interaction Review
- Screenshot Review
- Consistency Review
- 必要时 Architecture Review

Coding Agent 负责实际修改。

---

# 34. 实施循环

```text
Audit
  ↓
Context Package
  ↓
ChatGPT Review / Design
  ↓
Coding Agent
  ↓
Build
  ↓
Run
  ↓
Screenshot
  ↓
Functional Regression
  ↓
Bug Hunt
  ↓
ChatGPT Review
  ↓
Fix
```

---

# 35. 增量修改原则

不要一次性推翻项目。

每一个阶段必须保持：

- Buildable
- Testable
- Reversible
- Reviewable

UI 重构不能成为重新实现整个 Photo Manager 的借口。

---

# 36. 主动 Bug Hunting

用户不知道所有 Bug。

因此 Hermes 必须主动进行：

> Exploratory Testing / Bug Hunting / Regression Testing

目标：

> **Find bugs before the user finds them.**

Build Passed 不等于产品没有 Bug。

---

# 37. Bug Hunting 的 13 个角度

至少从以下角度测试：

1. Happy Path
2. Fast User
3. Repeated Input
4. Large Dataset
5. Empty Dataset
6. Invalid / Corrupted Data
7. Async Race
8. Cancellation
9. External File Changes
10. Navigation
11. Memory
12. Performance
13. Animation / Interaction Conflict

---

# 38. 启动测试

覆盖：

- 正常启动
- 快速重复启动
- 单实例
- 异常退出后启动
- 空库
- 大库
- 冷启动
- 有缓存启动

检查：

- Crash
- Freeze
- White screen
- Duplicate instance
- 错误恢复
- 启动异常慢

---

# 39. Navigation Bug Hunt

快速反复切换当前版本真实存在的页面。

检查：

- 页面错乱
- ViewModel 重复创建
- 旧状态覆盖新状态
- Animation 卡死
- Loading 重复
- UI 状态丢失
- 内存持续上涨

---

# 40. Filter / Date / Search Race

任何会触发异步加载的输入都必须测试快速连续变化。

典型：

```text
Request A starts
→ User changes condition
→ Request B starts
→ A finishes later
```

正确行为：

> **Latest Request Wins**

旧请求必须：

- Cancel
- Ignore stale result
- 或通过 generation/version 防止覆盖

不能让旧结果覆盖当前状态。

---

# 41. Gallery / Thumbnail Bug Hunt

使用：

- 小规模数据
- 中规模数据
- 大规模数据

重点检查：

- 只加载少量后停止
- 错图
- 重复图
- 空白图
- 半张图
- 方向错误
- 快速滚动后当前 Viewport 不加载
- 滚回去后无法恢复
- 缓存异常
- 内存不断上升
- Queue 堵塞

---

# 42. Viewport Priority

快速滚动后停止：

**当前屏幕可见内容必须优先。**

离开 Viewport 的任务：

允许取消或降级。

不能按最初顺序慢慢加载几千张之后才处理当前页面。

---

# 43. Selection Bug Hunt

测试当前版本支持的：

- Single Select
- Ctrl
- Shift
- Range
- Select All
- Clear
- Filter 后
- Date / Folder 变化后
- Scroll 后
- 文件删除后

不能保留已经不存在于当前 Gallery 的 Ghost Selection。

---

# 44. Zoom / Pan

如果当前版本存在：

- Ctrl + Wheel Zoom
- Spacebar Pan

则测试：

- 快速 Zoom In / Out
- 连续切换
- Zoom + Scroll
- Zoom + Selection
- Space Down / Drag / Space Up
- 快速按下松开
- 焦点切换

不能出现：

- Ctrl + Wheel 无响应
- Zoom 后所有图重新从头加载
- Space 松开后仍处于 Pan
- Scroll position 无故跳跃

---

# 45. 文件系统变化

测试：

- 应用内部删除
- 应用内部移动
- Explorer 外部删除
- 外部移动
- 外部改名
- 外部新增
- 外部编辑

检查：

- Ghost Item
- stale preview
- stale count
- crash
- stale cache
- 错误路径

---

# 46. Edited / 子目录 / 空间树等模块

如果当前版本存在这些能力：

必须根据当前实现建立专项测试。

特别关注：

- 子目录
- 多层子目录
- 新增 / 删除
- 筛选
- Count
- Size
- Current View
- Status summary
- 运行期间文件变化

UI 显示和实际数据必须一致。

---

# 47. Background Task Bug Hunt

对于：

- Scan
- Index
- Thumbnail
- AI
- Import
- Compression
- Export
- 其他后台任务

任务运行期间测试：

- 切页面
- 改 Filter
- 改 Date / Folder
- Cancel
- Exit
- Restart
- 再次触发同一任务

检查：

- Cancellation
- duplicate task
- stale progress
- UI thread blocking
- disposed View 被更新
- unobserved exception

---

# 48. 动画冲突测试

动画没结束时继续操作：

```text
Sidebar open → close → open
Inspector open → select another item
Page switch → switch again
Filter change → change again
```

动画必须：

> Interruptible

不能锁住交互。

---

# 49. 异常数据

测试数据建议包含：

- Empty Folder
- 1 File
- Very Large Photo
- Very Small Photo
- Corrupted JPG
- Corrupted RAW
- Corrupted MP4
- Unsupported File
- Unicode filename
- Chinese filename
- Japanese filename
- Emoji filename
- Very long filename
- Very deep folder
- Duplicate filename
- Read-only file
- Missing file

一张坏图不能拖垮整个 Gallery。

---

# 50. 路径测试

至少检查：

- 中文路径
- 空格
- 深层目录
- 长路径
- 外接盘
- 磁盘断开 / 重连

不得假设路径都是 ASCII。

---

# 51. Empty / Loading / Error

所有页面必须测试：

- Loading
- Empty
- Error

0 Results 不能只是白屏。

应明确告诉用户：

- 当前发生什么
- 为什么为空
- 是否可以恢复
- 最相关下一步是什么

---

# 52. Monkey-like Interaction

进行真实的高频混合操作：

```text
Click
Scroll
Switch
Zoom
Filter
Select
Deselect
Open Inspector
Close Inspector
Change Date
Change Folder
Switch Page
```

观察：

- Crash
- Freeze
- state mismatch
- stale result
- memory growth
- exception

---

# 53. WPF 专项风险

如果当前项目仍为 WPF，重点检查：

- Binding Error
- UI Thread Blocking
- Dispatcher.Invoke misuse
- async void
- Unobserved Task
- Collection modified exception
- PropertyChanged 缺失
- Virtualization 被意外关闭
- ScrollViewer 破坏 Virtualization
- 同步 File IO
- 同步图片 Decode
- 重复 Event Subscription
- Event Handler 未解除
- ViewModel 生命周期
- ResourceDictionary 重复加载
- BitmapSource 生命周期
- Image / Thumbnail Cache
- DispatcherTimer
- CancellationToken 生命周期

---

# 54. Memory Leak

反复执行真实工作流：

```text
Photo → Home → Photo
Inspector open → close
Change date
Change folder
Change filter
Open viewer → back
```

观察内存是否持续增长且无法回落。

重点检查：

- Event handler
- static reference
- image cache
- thumbnail cache
- VM lifecycle
- timer
- background worker

---

# 55. Bug Severity

## P0

- Crash
- Data Loss
- 错误删除文件
- 数据损坏
- 无法启动

立即处理。

## P1

- 核心功能不可用
- Gallery 无法正常使用
- 严重 Filter / Date 错误
- Freeze
- 严重性能下降
- 错误结果覆盖当前状态

优先处理。

## P2

- 状态不同步
- 局部功能 Bug
- 交互错误
- 偶发异常

正常处理。

## P3

- Spacing
- Minor Animation
- 小视觉问题
- Polish

最后处理。

---

# 56. Bug 修复流程

```text
Record
 ↓
Reproduce
 ↓
Root Cause
 ↓
Minimal Fix
 ↓
Build
 ↓
Test
 ↓
Regression Test
 ↓
Document
```

不要只修表象。

不要看到一个 Bug 就顺手重写整模块。

---

# 57. Bug Report 模板

```text
BUG ID:

Severity:

Area:

Description:

Reproduction:

Expected:

Actual:

Root Cause:

Fix:

Modified Files:

Regression Risk:

Verification:

Regression Test:
```

---

# 58. Regression Protection

重要 Bug 修复后：

优先新增 Automated Regression Test。

无法自动化：

加入 Manual Regression Checklist。

原则：

> Every important bug should become regression protection.

---

# 59. UX Bug 也属于 Bug

主动寻找：

- 点击无反馈
- Loading 状态不明确
- Filter 状态难理解
- Empty 原因不清楚
- UI 状态和实际数据不一致
- 按钮可点但操作无效
- Shortcut 不可发现
- Context Menu 行为不一致
- 本可两步完成却需要五步
- 状态栏数字过时

这些都应该记录和修复。

---

# 60. UI 重构与 QA 必须同步

不要到 100% 才第一次测试。

例如：

- Navigation 完成 → Navigation Bug Hunt
- Gallery 完成 → Gallery Bug Hunt
- Inspector 完成 → Selection / Inspector Bug Hunt
- Motion 完成 → Animation Conflict Test

每个阶段都要验证。

---

# 61. Build / Test Gate

每个重要阶段至少：

- Build
- relevant tests

重要 UI 阶段还应：

- Run
- Screenshot
- Runtime QA

最终：

- Release Build
- Full Tests
- Publish（如果当前项目正式流程要求）
- Main Flow QA

当前项目已有正式验证脚本时，优先使用项目脚本。

---

# 62. 进度必须按可验证阶段计算

整个任务：

100%。

拆成 10 个 Gate。

**每过 10% 必须向用户汇报。**

百分比不是 Agent 主观估算。

只有：

> Implemented + Verified

才可以计入完成度。

---

# 63. 0%：Baseline

完成：

- 读取文档
- 当前版本扫描
- Build baseline
- Run baseline
- Feature Inventory
- 初始 UI Audit
- 初始 Bug Hunt
- 风险清单
- 计划

---

# 64. 10%：Current Version Audit

完成：

- Repository Audit
- Runtime Audit
- Feature Inventory
- Design Audit
- Current Context Package
- 初始 Bug 清单

---

# 65. 20%：Design System / Motion Alignment

完成：

- Color
- Typography
- Spacing
- Radius
- Surface
- Component states
- Motion
- Material 3 Desktop adaptation rules

注意：

优先扩展现有 Token，不创建第二套 Design System。

---

# 66. 30%：App Shell

完成：

- Unified Shell
- Navigation container
- Top area
- Workspace
- Inspector container
- Status / background task area

提供当前截图。

---

# 67. 40%：Navigation + Motion

完成：

- Navigation
- Sidebar
- Workspace switch
- Keyboard behavior
- 基础动画

执行 Navigation Bug Hunt。

---

# 68. 50%：Home + Mandatory Mid Review

首页或当前主要入口达到新设计方向。

**强制调用 ChatGPT Desktop 进行中期 Review。**

提供：

- Context Package
- 当前截图
- Design System
- 当前功能清单
- 不能破坏项

修复明显问题后才能继续。

---

# 69. 60%：Primary Gallery / Main Content

以当前版本最核心媒体浏览模块为准。

重点验收：

- Virtualization
- Thumbnail
- Viewport priority
- Filter
- Selection
- Zoom / Pan（若存在）
- Scroll
- Hover
- Performance
- Race condition

当前模块存在 P1：

不得进入 70%。

---

# 70. 70%：Inspector + Contextual UI

完成当前版本相关：

- Inspector
- Context Action
- Multi-select actions
- Filter / Search UI
- Context Menu
- Metadata display

执行 Selection / Inspector Bug Hunt。

---

# 71. 80%：Remaining Main Pages

统一当前版本真实存在的主要页面。

不要根据旧总纲强行新增不存在的模块。

---

# 72. 90%：Final Polish + Mandatory Final Review

完成：

- Motion consistency
- Spacing
- Alignment
- Typography
- Icon
- Hover
- Focus
- Loading
- Empty
- Error
- Keyboard
- Performance

**再次强制调用 ChatGPT Desktop 做最终 Review。**

---

# 73. 100%：Final Verification

完成：

- Release Build
- Full Tests
- 主要用户流程
- Bug Hunt
- Regression Check
- Feature Inventory 对比
- 文档更新
- Handoff
- 最终截图
- Known Issues

---

# 74. Progress Gate

存在 P0：

**禁止提高完成百分比。**

当前阶段存在未解决 P1：

原则上：

**不得宣布该阶段完成。**

例如 Gallery UI 写完但快速日期切换显示错图：

60% 不能通过。

---

# 75. 每 10% 汇报模板

```text
# HanabePhoto 当前进度：XX%

## 已完成
-

## 当前版本变化
-

## UI / UX
-

## Animation
-

## Bug Hunting
发现：
确认：
P0：
P1：
P2：
P3：
已修复：
待处理：
Regression Tests：

## 当前版本保护检查
检查功能：
正常：
发现回归：
已修复回归：
待处理：

## ChatGPT Desktop Review
-

## Build / Test
Build:
Release:
Tests:
Publish:

## 当前截图
-

## 当前问题
-

## 下一阶段
-
```

---

# 76. 不得虚假汇报

禁止：

- “应该差不多 80%”
- “代码写完所以算完成”
- “Build Passed 所以功能正常”

必须提供验证依据。

---

# 77. AGENT_HANDOFF

每完成重要任务后更新当前项目的 Handoff 文档。

至少记录：

```text
Date
Version
Branch

Task

Current State

Modified Files

New Features

Changed Behavior

UI Changes

Motion Changes

Bug Fixes

Regression Tests

Known Issues

Must Preserve

Next Recommended Work
```

---

# 78. 文档同步

如果公共设计资源、行为或架构改变：

同步更新当前项目真实存在的：

- design system
- component inventory
- resource dictionary docs
- architecture
- motion guideline
- feature inventory
- bug regression checklist
- version history

不要让文档长期落后于代码。

---

# 79. 最终体验

打开 HanabePhoto 时应该感觉：

> 这是一个现代、专业、快速的桌面摄影工作站。

而不是：

- 普通相册
- 网页后台
- Android App
- Terminal Emulator

它应该同时具有：

- Material Design 3 的秩序
- Codex Desktop 的工具感
- Lightroom 类专业影像软件的信息密度
- Terminal 工具的精确反馈
- Windows Desktop 的键盘、焦点、窗口和滚动习惯

---

# 80. 最终公式

## UI

> Material Design 3 × Codex Desktop × Lightroom

## Motion

> Fast × Subtle × Precise × Interruptible

## Interaction

> Keyboard First × Immediate Feedback × Contextual UI

## Architecture

> Preserve Function × Incremental Change × Reuse Existing Systems

## Context

> Current Repository > Historical Memory

## QA

> Find Bugs Before the User Finds Them

## Performance

> Content and Responsiveness Always Have Priority Over Animation

---

# 81. 最重要规则

任何时候都不要：

- 为了好看破坏功能
- 为了重构方便复制第二套系统
- 因为 ChatGPT 不记得一个功能就认为它不存在
- 只因为 Build Passed 就认为没有 Bug
- 只测试 Happy Path
- 用动画牺牲 Gallery 性能
- 把 Material Design 3 做成 Android App
- 把 Terminal Inspired 做成纯 TUI
- 用旧总纲覆盖当前仓库真实状态
- 等用户先发现问题才测试

始终遵循：

> Inspect first.  
> Understand current state.  
> Preserve working behavior.  
> Modify incrementally.  
> Verify runtime behavior.  
> Hunt for hidden bugs.  
> Add regression protection.  
> Keep documentation current.  
> Report every verified 10%.  
> Current HanabePhoto is always the source of truth.

---

# 82. English Canonical Summary for Agent Handoff

HanabePhoto is a professional Windows desktop photo/video management application.

The target visual direction is:

> **Material Design 3 × Codex Desktop × Lightroom**

with a restrained terminal-inspired professional-tool character.

Core rules:

1. **Current repository and runtime behavior are always more authoritative than historical chat context.**
2. Inspect the current implementation before proposing or applying changes.
3. Preserve existing features, bindings, commands, services, data flows, and stable behavior unless the user explicitly asks for a behavior change.
4. Reuse and extend the existing Design System and Motion Tokens instead of creating parallel systems.
5. Adapt Material Design 3 for a dense desktop productivity environment; do not build an enlarged Android UI.
6. Motion must be **Fast × Subtle × Precise × Interruptible**.
7. Animation must never reduce gallery responsiveness, virtualization quality, input latency, or loading priority.
8. ChatGPT Desktop acts primarily as UI/UX Design Director and Reviewer; it must always receive a fresh Current Context Package.
9. Codex / coding agents implement changes incrementally, with build/test/runtime verification after each meaningful stage.
10. Hermes actively performs exploratory bug hunting and regression testing; do not wait for the user to discover bugs.
11. Important bugs should receive regression protection.
12. Progress is reported every verified 10%; no P0 bug may remain while progress advances, and unresolved stage-critical P1 bugs block stage completion.
13. Keep project documentation and Agent Handoff synchronized with the current implementation.

Final engineering principle:

> **Current State > History. Preserve > Rewrite. Verify > Assume. Responsiveness > Animation.**
