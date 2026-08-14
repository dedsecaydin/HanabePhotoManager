# Import Features (Multi-select + Progress + Dedupe) — Implementation Spec

> 目标：修复并增强 Hanabe Photo Manager 的**照片导入**功能，三项：
> 1. **多文件同时导入**（回归修复——用户反馈之前支持多选批量导入，当前版本退化成单文件导入）
> 2. **导入进度条**（多文件导入时有可见进度）
> 3. **防止重复文件导入**（自动检测重复并给出决策）

## 1. 铁律（必须遵守）

1. **0 警告 0 错误**：Release 构建必须 0 warnings / 0 errors。
2. **现有测试全绿**：Core 365 + Infrastructure 160 + App 336（约）全绿，不能破坏现有功能（treemap、语义搜索新代码若有、日期过滤、修后保护等）。
3. 遵循现有 Clean Architecture：依赖方向 Core ← Infrastructure ← App。
4. 先读 `AGENTS.md`、`AGENT_HANDOFF.md`、`docs/architecture.md`、`docs/features/photo-library.md`、`docs/testing.md`、`docs/design-system.md`。
5. 修改代码后追加到 `docs/agent-change-log.md`。
6. 新代码要有测试。

## 2. 现状调研（动手前必须完成）

1. 阅读现有导入实现：`ImportPlanBuilder.cs`、`MediaGroupBuilder.cs`、`ImportPlan` 相关模型、App 层导入对话框/命令（搜 `Import` 关键字）。
2. **用 git log/history 找"多文件导入"回归点**：用户明确说"之前有，现在没有了"。找到之前支持多选导入的提交/代码路径，确认是被谁改坏的（可能是某次重构、某次拖放改动、或对话框 AllowMultiple 被改）。
3. 检查现有去重逻辑：AGENT_HANDOFF 提到 "Import exact-duplicate decision | ✅ SHA-256 after size prefilter; explicit skip/import/Explorer decision with side-by-side thumbnails"——确认此逻辑当前是否工作，用户反馈"防重复导入"需求，说明现状可能不满足（可能只对精确重复有效，或入口不便）。

## 3. 需求细节

### 3.1 多文件同时导入（回归修复）
- 文件选择对话框必须支持 **Ctrl/Shift 多选**（OpenFileDialog `Multiselect = true`；若用自定义选择器同样支持多选）。
- 拖放导入支持**多文件**（若现有拖放只接受单文件，一并修复）。
- 导入流程按文件列表逐个/并行处理，每个文件走现有导入管线（分类、重命名、拷贝/移动、写元数据）。
- 全部文件完成后汇总结果（成功 N、跳过 M、失败 K）。

### 3.2 导入进度条
- 多文件导入时显示进度：`正在导入 x/N (yy%)`，带进度条（ProgressBar 或等价控件）。
- 进度按已完成文件数推进；每文件内部若有长操作（拷贝大 RAW/视频）也可细分。
- 可取消（Cancel 按钮/CancellationToken）。
- 单文件导入时进度条可隐藏或不显示，避免打扰。
- 导入完成显示结果摘要（成功/跳过/失败数量 + 失败原因列表可展开）。

### 3.3 防重复导入
- 保持并增强现有 SHA-256 去重：大小预过滤 → SHA-256 全比对 → 命中重复时给出明确决策。
- 决策 UI（批处理模式）：本次多选导入中检测到重复，统一提供「全部跳过（推荐）/全部仍导入/逐个选择」选项，避免每个文件都弹窗打断。
- 重复判定范围：目标库内已有文件（同路径或同名但内容哈希一致均算重复）；「修后」目录只读保护不受影响。
- 导入后元数据不重复追加（同一文件重复导入不会产生两条记录）。

## 4. 测试要求

- Core：去重判定（大小预过滤 + SHA-256 逻辑）、ImportPlanBuilder 对多文件输入的计划生成、进度报告逻辑。
- App：多选导入命令参数、进度 VM 状态机（空闲/运行中/完成/取消）、重复决策 VM。
- 全量回归：`dotnet build -c Release` 0 警告 0 错误 + `dotnet test` 全绿。
- 验证：应用可启动，导入对话框可多选，导入 3+ 文件有进度条，重复文件二次导入会提示跳过。

## 5. 交付

1. 代码实现 + 测试。
2. 追加 `docs/agent-change-log.md`（记录回归根因和修复）。
3. git commit + push 到当前分支。
4. 报告：回归根因（为什么多文件导入失效了）、改动文件清单、测试结果摘要。
