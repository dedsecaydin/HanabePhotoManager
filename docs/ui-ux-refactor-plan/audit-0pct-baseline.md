# HanabePhoto UI/UX 重构 — 0% Baseline / Current Version Audit

> 日期：2026-08-11 | 审计者：Hermes (Coordinator)
> 依据：docs/HERMES_MASTER_GUIDE.md（已解压至 .hermes-guide/，待正式合并进 docs/）

## 1. 审计范围（读了什么）

| 项 | 状态 |
|---|---|
| HERMES_MASTER_GUIDE.md | ✅ 前 500 行精读（原则：Coordinator 角色 / Current State > History / 先审计后改 / MD3×Codex×Lightroom / Fast×Subtle×Precise×Interruptible / 每 10% 汇报 / 50%+90% ChatGPT Review） |
| AGENTS.md | ✅ 规范入口（Token First、文档路由、改动纪律） |
| AGENT_HANDOFF.md | ✅ 分支 codex/photo-treemap-browser、KI-01~14、完成/部分清单 |
| docs/current-status.md | ✅ 全功能实时状态（Stable/Partial/Planned） |
| docs/known-issues.md | ✅ KI-01~14 详情 |
| docs/design-system.md | ✅ **项目 UI 唯一权威**（Token First、Radius 6/8/12/16、Motion 150/180/220ms、Sidebar 232、禁 Card 套 Card、MD3 方向） |
| Git 状态 / 最近提交 | ✅ 分支、8 条近期提交、工作区 |
| 代码结构 | ✅ App 模块树、MainWindow.xaml 1862 行、MainWindowViewModel.cs 7255 行、Browsing/Navigation/ViewModels |
| 测试 | ✅ **893/893 全通过**（Core 370 / Infra 164 / App 359，Debug，2026-08-11） |
| 运行验证 | ✅ Release 构建 0 警告 0 错误；应用启动正常（Alpha 主窗口）；主页/图库/自定义相册已修复 |

## 2. 当前版本基线

- **分支**：`codex/photo-treemap-browser`
- **版本**：0.2.0-alpha.3（AGENTS.md 记录；实际 git 已进 0.2.0-alpha.3+ 多轮）
- **最新提交**：`1ea66d1`（自定义相册 + 图库浏览排版 + XAML 资源修复——今日刚提交）
- **技术栈**：.NET 8 / C# 12 / WPF / CommunityToolkit.Mvvm / xUnit
- **构建**：Release 0 警告 / 0 错误
- **测试**：893/893 通过
- **工作区未跟踪**：`.hermes-guide/`（本任务新增）、`ms.html/sf.html`（临时文件，勿提交）、3 份旧 spec md（import-features/semantic-search）

## 3. Feature Inventory（当前真实功能清单）

### 3.1 App Shell / Navigation
- ✓ 左侧 Sidebar 232px：主页/照片图库/人物查找/导入照片/图片小工具/地图照片/投稿项目/欣赏项目/网盘/自定义相册/深色模式
- ✓ Navigation 数据驱动（NavigationItemViewModel / NavigationOrderPolicy / NavigationDisplayMode）
- ✓ 深色/浅色主题切换（ThemeManager + Light.xaml/Dark.xaml 同名资源键 + 持久化）

### 3.2 照片图库（Photo Library / Browse）
- ✓ 浏览条件：日期日历（单选）、人物、业务分类（RAW生图/JPG生图/修后/视频/action视频/素材）、修图状态、文件类型 chips（RAW/JPG/PNG/Video，PSD 排除）、评分、搜索（文件名+语义）
- ✓ 显示：网格视图（缩放、方形瓦片、渐进缩略图）、时间线、列表、**空间树图（Treemap，默认）**
- ✓ Treemap：外部分类 Squarified、Justified Gallery 内部布局、面包屑、子目录导航、Space+拖拽平移、Ctrl+滚轮缩放（0.5x-30x）、全景语义缩放
- ✓ 缩略图管线：视口驱动加载（150ms debounce）、优先级队列、自愈恢复、异步尺寸读取
- ✓ 项计数（子树感知 CurrentViewItemCount）

### 3.3 自定义相册（Albums，今日新增已提交）
- ✓ 添加任意文件夹（去重）、浏览照片网格、重命名显示名、移除引用（不删磁盘）、JSON 原子持久化
- ✓ 分层实现 + 4 层测试

### 3.4 其他功能页
- ✓ 人物查找（FaceSearch，YuNet+SFace）、地图照片（WebView2）、投稿/欣赏项目、网盘（Cloud）、图片小工具（压缩/水印/等）、导入（多选/进度/取消/SHA-256 去重决策）、语义搜索（Chinese-CLIP 集成在浏览条件内）
- ✓ PhotoViewer 看图（深色画布、键盘导航、EXIF）

## 4. Current Context Package（供 ChatGPT Desktop Review 用）

- 当前 UI 是 **Token 驱动 WPF**：Colors.Light/Dark → Brushes → Tokens（Spacing/Radius/Typography/Sizing/Material/Gradient/Shadow/Highlight/Motion）→ Controls（Button/Input/Card/Dialog/Sidebar/Navigation/Toolbar/List/Menu/Status/Layout）
- Motion Token：Fast 150ms / Normal 180ms / Slow 220ms，仅颜色/透明度/边框/小幅位移
- Radius：Small 6 / Control 8 / Card 12 / Dialog 16
- 现有 Shell：232px Sidebar + 弹性工作区，连续背景
- MainWindow.xaml 1862 行（单文件偏大，但拆分为"共享资源收敛+页面最小替换"策略，不强制大拆）
- 已知视觉方向已与 master guide 对齐（MD3 Desktop、低饱和、扁平、高密度）

## 5. Must Preserve 功能清单（红线）

| 领域 | 必须保留 |
|---|---|
| 业务 | 分类定义（RAW生图/JPG生图/修后/视频/action视频/素材）、文件扫描基础设施、构建/测试命令、Directory.Build.props（WarningsAsErrors）、global.json |
| 数据流 | ViewModel / Command / Binding / API / 数据流一律不动（design-system 明文禁止） |
| 功能页 | 全部 11 个导航页 + PhotoViewer + 导入/去重/语义搜索/自定义相册 |
| 交互 | 键盘焦点、Ctrl+滚轮缩放、Space 平移、多选、快捷筛选、日期切换 |
| 主题 | Light 默认 + Dark，同名资源键契约，用户偏好持久化 |

## 6. 当前已知问题（KI 汇总 + 今日修复）

| ID | 问题 | 状态 |
|---|---|---|
| KI-01 | Treemap 只加载第一批缩略图后停止 | Fix attempted，未验证 |
| KI-03 | Justified Gallery 仍像固定网格 | In progress |
| KI-04 | 瓦片内大白边 | Partial |
| KI-05/06 | 6217+ 项只显示前十几个 / 底部裁成细条 | Partial |
| KI-08 | 点"已修"可能崩溃 | Fix attempted |
| KI-09/10/11 | 日期切换空结果 / 修后归属 / 修后递归 | Fix attempted |
| KI-13 | PSD 排除未完全生效 | Partial |
| KI-14 | Root overview fit-all | Blocked（已回退，待重设计） |
| ✅ 今日 | CustomAlbums 启动崩溃（BoolToVis）、CornerRadius StaticResource 17 处（含 0,0,{StaticResource} 混合写法） | **已修复已提交** |

## 7. 初始 Bug Hunting 发现（代码审计层面）

> 尚未逐项复现（0% 阶段只做静态/基线审计）；以下为高嫌疑点，列入 10%-20% 阶段复现清单

1. **KI-01 缩略图停止加载**：`MainWindowViewModel.cs` 中 `BrowseDisplayMode` setter + `SelfHealTreemapThumbnailsAsync` 的 cancel/restart 竞态——高优先级复现
2. **异步 Race Condition（KI-09 日期切换）**：`SelectDateAsync` 旧任务晚完成覆盖新结果——需验证 Latest-Request-Wins
3. **MainWindowViewModel.cs 7255 行单文件**：事件订阅/内存泄漏风险点分散——审计 `RebuildRetouchTrackingAsync` / 缩略图任务取消
4. **缩略图快速滚动**：150ms debounce + 生成安全队列——快速滚动下是否丢帧/重复加载需实测
5. **Justified Gallery 布局**：AspectRatio 默认 1.0 直到缩略图加载，无 re-layout 触发——KI-03/KI-04 根因
6. **MP4/损坏图片**：`ImageDimensionReader` 只读 JPEG/PNG 头；损坏文件是否抛异常中断批次——需验证
7. **Unicode/中文路径**：导入/自定义相册/扫描的中文路径覆盖——需测试
8. **外部文件删除/移动**：浏览中文件消失 → 缩略图/选中状态处理——未覆盖
9. **WPF Binding Error**：语义搜索 ProgressValue 只读属性 TwoWay 绑定（历史已修 b614e06）——检查是否还有类似绑定错误
10. **自定义相册**：大目录扫描 UI 线程阻塞风险（CustomAlbumPhotoScanner 是否 Task.Run）——新功能回归

## 8. 0% 结论

- ✅ 基线健康：构建 0 警告 0 错误、测试 893/893、应用可运行、今日 3 类崩溃已修
- ✅ 文档体系完整：design-system.md 已是 MD3 方向，与 master guide 无冲突
- ⚠️ MainWindow.xaml 1862 行 + MainWindowViewModel.cs 7255 行 = 重构主战场（渐进式，不一次性大拆）
- ⚠️ 10 项初始 Bug 嫌疑待复现（10%-30% 阶段排期）

## 9. 下一阶段（10%）

1. 把 HERMES_MASTER_GUIDE.md + AGENTS_HERMES_INSERT 正式落项目（docs/ + AGENTS.md 合并）
2. 建立 Regression Checklist 基线（今天已修的 3 项 + KI-01/KI-09 复现验证）
3. 首次 10% 交付：**App Shell 连续性审计**（Sidebar/顶部/工作区是否连续——按 master guide §9 对照截图）
4. 准备 ChatGPT Desktop 初次 Context Package（50% 节点前先给设计方向预览）
