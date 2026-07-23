# Hanabe Photo Manager AI Development Infrastructure Design

日期：2026-07-23  
状态：已批准并实施

## 目标

为当前 Hanabe Photo Manager 仓库建立可供 Codex、ChatGPT、Claude Code、Cursor 等工具共同使用的长期开发规范。新的 AI 应能在两分钟内识别项目边界、技术栈和必读资料，并在五分钟内依据统一流程开始安全开发。

本次工作只建设开发基础设施，不改变产品功能、业务逻辑或 UI。

## 当前项目依据

规范以当前导出快照为依据：

- `HanabePhotoManager.sln` 包含 Core、Infrastructure、App 三个生产项目和对应的三套测试项目。
- App 是 .NET 8 Windows WPF 应用，使用 CommunityToolkit.Mvvm，并引用 Core 与 Infrastructure。
- Core 保存不依赖 WPF 或具体存储实现的领域模型、策略和接口。
- Infrastructure 实现文件导入、持久化、云服务、SQLite 与受保护会话等外部能力。
- App 包含 WPF 页面、窗口、ViewModel、应用服务和主题资源。
- `docs/design-system.md` 已声明为 UI 唯一规范来源。
- 主题资源按 Colors、Brushes、Tokens、Typography、Motion、Controls 和 Themes 分层。
- `tests/` 已覆盖 Core、Infrastructure 和 App；发布脚本位于 `tools/Publish-Clean.ps1`。
- 当前目录是无 `.git` 元数据的导出快照，无法依据提交历史补充规范或提交本设计。

## 文档分层

### 统一入口

根目录 `AGENTS.md` 是所有 AI 和开发者的统一入口。它只介绍项目、规定 AI 工作原则、给出必读顺序、概述开发流程并索引权威文档，不承载详细规则。

`AGENT_HANDOFF.md` 保留为兼容入口，只说明长期规范已迁移，并链接到 `AGENTS.md`。

### 项目长期规范

`docs/` 中的新增文档面向所有开发者：

- `architecture.md`：解决方案分层、模块职责、MVVM、主题资源架构、目录职责和数据流。
- `components.md`：组件库治理、新增与扩展判定、命名、复用检查和禁止重复组件。
- `coding-style.md`：C#、WPF、XAML、命名、ResourceDictionary、Style 与 Theme 的代码约束。
- `workflow.md`：从需求分析到文档更新的日常开发流程和变更边界。
- `testing.md`：按变更类型选择 Build、Test、Smoke Test 的验证矩阵。
- `release.md`：版本核对、正式 Publish、产物检查和回归流程。

已有专项文档保持其原职责：

- `design-system.md`：唯一 UI 视觉与交互规范，不在其他文件复述。
- `component-inventory.md`：组件与样式的现状清单，不作为新增规则来源。
- `resource-dictionary-structure.md`：资源字典结构的专项说明；长期约束由 `architecture.md` 和 `coding-style.md` 引用而不复制细节。
- `ui-audit.md`：特定时间点的审计记录，不作为长期规范。
- `功能简介.md`：用户可见能力概览，不作为架构规范。
- `docs/superpowers/specs/` 与 `plans/`：历史设计和实施记录，不作为当前规范入口。

### AI 工作手册

`.ai/` 只说明 AI 如何执行工作，不重新定义项目规则：

- `onboarding.md`：首次接手的五分钟阅读和检查顺序。
- `architecture-map.md`：目录到职责、依赖方向和对应权威文档的快速映射。
- `feature-template.md`：新增功能的标准分析与交付记录模板。
- `common-tasks.md`：新增页面、Dialog、Theme、Toolbar 和业务功能的项目专属步骤。
- `debug-guide.md`：Theme、Binding、ResourceDictionary、MVVM、构建输出锁定等常见问题的定位路径。

AI 手册中的每项规则均链接到 `docs/` 的权威章节；允许包含操作顺序和检查命令，但不复制规范正文。

## 唯一来源矩阵

| 信息 | 唯一来源 |
|---|---|
| AI 入口与必读顺序 | `AGENTS.md` |
| 解决方案、分层、数据流 | `docs/architecture.md` |
| UI 视觉、Token、交互状态 | `docs/design-system.md` |
| 组件治理 | `docs/components.md` |
| 代码和资源写法 | `docs/coding-style.md` |
| 功能开发流程 | `docs/workflow.md` |
| 构建、测试、Smoke Test | `docs/testing.md` |
| 发布与回归 | `docs/release.md` |
| AI 首次接手步骤 | `.ai/onboarding.md` |
| AI 常见任务执行手册 | `.ai/common-tasks.md` |
| AI 调试决策路径 | `.ai/debug-guide.md` |
| UI/组件现状快照 | 现有 audit 和 inventory 文档 |

## 规则编写原则

- 只记录能从当前代码、项目文件、现有文档或脚本验证的事实。
- 区分当前架构与期望约束，不把尚未实现的设计描述成现状。
- 使用链接代替跨文件复制；命令只在对应流程的权威文档完整出现。
- 对已有例外如 App 层服务、窗口 code-behind 和大型 ViewModel 如实说明，并规定新增代码的目标边界，不伪称项目已经完全纯化。
- 不将阶段性任务、临时修复、历史迁移或特定代理会话写入长期规范。
- 不携带密钥、令牌、Cookie、OAuth 凭据或本地路径状态。

## 验证方案

完成文档后执行以下检查：

1. 检查所有要求的文件存在，且 `AGENTS.md` 与 `AGENT_HANDOFF.md` 保持精简。
2. 扫描相对 Markdown 链接，确认目标存在。
3. 搜索 `TBD`、`TODO`、占位文本、失效文件名和旧入口引用。
4. 对照唯一来源矩阵检查跨文档重复和冲突。
5. 运行 Release Build 和完整测试；若 WPF 进程锁定常规输出，则使用独立 artifacts 路径。
6. 文档改动不执行 Publish；只验证发布脚本和发布规范与当前项目一致。

## 不在范围内

- 不重构现有 C#、XAML、主题资源或测试代码。
- 不修复 `design-system.md` 或其他历史文档的编码与内容，除非链接校验需要修正路径。
- 不删除历史 specs、plans、audit 或 inventory。
- 不发布正式安装包，也不修改版本号。
