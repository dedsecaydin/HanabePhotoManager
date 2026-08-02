Hanabe Photo Manager — AI 开发指南
目的：提供 AI 辅助开发的统一入口。
范围：代码库概览、工作原理、阅读顺序和文档路由。
受众：Codex、ChatGPT、Claude Code、Cursor 和人类贡献者。
参考资料：docs/architecture.md、docs/workflow.md、docs/design-system.md
项目
Hanabe Photo Manager 是一款基于 Windows 的照片管理桌面应用程序，使用 .NET 8、C# 12、WPF、XAML 和 CommunityToolkit.Mvvm 构建。该解决方案将域策略（核心）、外部系统实现（基础架构）和 WPF 应用程序（App）分离，并配有相应的 xUnit 测试项目。
该应用程序涵盖媒体导入和组织、元数据和评级、本地图像分析、人物和地图视图、压缩和水印工作流程以及与提供商无关的云基础架构。
AI 工作原则
先检查后编辑。首先阅读相关的生产代码、测试和权威文档。
保留用户工作成果。切勿重置、覆盖或删除无关的更改。
遵循现有边界。依赖项必须始终指向核心。
先重用后创建。在添加抽象之前，检查组件和服务清单。
保持 UI 规则集中化。docs/design-system.md 是唯一的 UI 设计系统权威文档。
进行最小的、连贯的更改；不要捆绑无关的清理工作。
按比例验证。使用 docs/testing.md 进行构建和测试。
当长期规则或架构边界发生变化时，更新所属文档。
切勿提交凭据、令牌、Cookie、个人路径或生成的运行时数据。
区分已验证的当前行为与提案和历史设计记录。
阅读顺序
首次贡献时，请按以下顺序阅读：
本文件。
架构和快速架构图。
工作流程和测试。
负责执行计划变更的标准：组件、编码风格或版本。
对于任何 UI 工作，请在修改 XAML 之前阅读 design-system.md。
阅读相关的源文件和测试；完整的启动清单请参见 .ai/onboarding.md。
开发流程
需求分析 → 架构分析 → 复用检查 → 实现 → 构建 → 测试 → 文档编写
当所需的构建或测试失败时，请停止。在继续之前，请诊断失败原因。详细流程请参见 workflow.md；验证选择请参见 testing.md。
文档索引
长期标准 (docs/)
文档权威性
architecture.md 项目层级、职责、依赖关系、MVVM、资源架构和数据流
design-system.md UI 设计、令牌、视觉组件、布局和交互状态的唯一权威性
components.md 组件治理、重用、扩展、创建和命名决策
coding-style.md C#、WPF、XAML、ResourceDictionary、样式和主题实现规范
workflow.md 功能和维护工作流程
testing.md 构建、测试、冒烟测试和发布决策矩阵
release.md 正式发布和回归测试流程
现有的组件清单、资源字典结构和 UI 审计是快照或专家参考，并非长期规则来源。
AI 手册 (.ai/)
文档使用指南
onboarding.md 仓库入门五分钟指南
architecture-map.md 快速目录和依赖项查找指南
feature-template.md 标准功能分析和交付记录
common-tasks.md 项目特定任务手册
debug-guide.md 常见故障的诊断路径
Hanabe Photo Manager — AI Development Guide
Purpose: Provide the single entry point for AI-assisted development.
Scope: Repository orientation, working principles, reading order, and document routing.
Audience: Codex, ChatGPT, Claude Code, Cursor, and human contributors.
References: docs/architecture.md, docs/workflow.md, docs/design-system.md

Project
Hanabe Photo Manager is a Windows photo-management desktop application built with .NET 8, C# 12, WPF, XAML, and CommunityToolkit.Mvvm. The solution separates domain policies (Core), external-system implementations (Infrastructure), and the WPF application (App), with matching xUnit test projects.

The application covers media import and organization, metadata and ratings, local image analysis, people and map views, compression and watermark workflows, and provider-neutral cloud foundations.

AI Working Principles
Inspect before editing. Read the relevant production code, tests, and authoritative document first.
Preserve user work. Never reset, overwrite, or delete unrelated changes.
Follow existing boundaries. Dependencies must continue to point toward Core.
Reuse before creating. Check the component and service inventories before adding abstractions.
Keep UI rules centralized. docs/design-system.md is the only UI design-system authority.
Make the smallest coherent change; do not bundle unrelated cleanup.
Verify proportionally. Build and test using docs/testing.md.
Update the owning document when a long-term rule or architecture boundary changes.
Never commit credentials, tokens, cookies, personal paths, or generated runtime data.
Distinguish verified current behavior from proposals and historical design records.
Reading Order
For a first contribution, read in this order:

This file.
Architecture and the quick architecture map.
Workflow and testing.
The standard that owns the planned change: components, coding style, or release.
For any UI work, read design-system.md before XAML changes.
Read the relevant source files and tests; use .ai/onboarding.md for the complete startup checklist.
Development Flow
Requirement analysis → Architecture analysis → Reuse check → Implementation → Build → Test → Documentation

Stop when a required build or test fails. Diagnose the failure before continuing. The detailed process belongs to workflow.md; validation selection belongs to testing.md.

Documentation Index
Long-term standards (docs/)
Document	Authority
architecture.md	Project layers, responsibilities, dependency direction, MVVM, resource architecture, and data flow
design-system.md	Sole authority for UI design, tokens, visual components, layout, and interaction states
components.md	Component governance, reuse, extension, creation, and naming decisions
coding-style.md	C#, WPF, XAML, ResourceDictionary, Style, and Theme implementation conventions
workflow.md	Feature and maintenance workflow
testing.md	Build, test, smoke-test, and publish decision matrix
release.md	Formal release and regression procedure
Existing component inventory, resource dictionary structure, and UI audit are snapshots or specialist references, not long-term rule sources.

AI handbook (.ai/)
Document	Use
onboarding.md	First five minutes in the repository
architecture-map.md	Fast directory and dependency lookup
feature-template.md	Standard feature analysis and delivery record
common-tasks.md	Project-specific task playbooks
debug-guide.md	Diagnostic paths for common failures
