# Hanabe Photo Manager — AI Development Guide

> **Purpose:** Provide the single entry point for AI-assisted development.  
> **Scope:** Repository orientation, working principles, reading order, and document routing.  
> **Audience:** Codex, ChatGPT, Claude Code, Cursor, WorkBuddy, and human contributors.  
> **Current Version:** `0.2.0-alpha.3` (2026-08-06)  
> **Project Path:** `D:\HanabePhoto`  
> **Tech Stack:** .NET 8 / C# 12 / WPF / CommunityToolkit.Mvvm / xUnit  
> **References:** [`AGENT_HANDOFF.md`](AGENT_HANDOFF.md), [`docs/architecture.md`](docs/architecture.md), [`docs/workflow.md`](docs/workflow.md), [`docs/design-system.md`](docs/design-system.md)

## HanabePhoto Hermes / Multi-Agent Mandatory Rules

> 本节约束 Hermes 与所有 Agent 的跨 Agent 协作。

### Mandatory Master Guide

任何涉及以下内容的任务开始前：

- HanabePhoto UI / UX
- Material Design / Design System
- App Shell / Navigation / Sidebar / Toolbar
- 动画 / Motion
- Gallery / Thumbnail / Inspector
- 页面视觉重构
- Bug Hunting / QA / Regression
- Hermes → ChatGPT Desktop → Codex 的跨 Agent 协作
- 当前版本功能保护
- 进度阶段与汇报

执行 Agent **必须先读取：**

`docs/HERMES_MASTER_GUIDE.md`

然后再读取与当前任务相关的现有项目文档。

### Source of Truth

始终遵循：

```text
Current Repository / Runtime
        >
Current Tests / Docs
        >
Agent Handoff
        >
Historical Chat / Agent Memory
```

## Project

Hanabe Photo Manager is a Windows photo-management desktop application built with .NET 8, C# 12, WPF, XAML, and CommunityToolkit.Mvvm. The solution separates domain policies (`Core`), external-system implementations (`Infrastructure`), and the WPF application (`App`), with matching xUnit test projects.

The application covers media import and organization, metadata and ratings, local image analysis, people and map views, compression and watermark workflows, and provider-neutral cloud foundations.

## AI Working Principles

1. Inspect before editing. Read the relevant production code, tests, and authoritative document first.
2. **Read [`AGENT_HANDOFF.md`](AGENT_HANDOFF.md) first** for current task status and known issues.
3. **Read [`docs/current-status.md`](docs/current-status.md)** for the real-time state of all features.
4. Preserve user work. Never reset, overwrite, or delete unrelated changes.
5. Follow existing boundaries. Dependencies must continue to point toward Core.
6. Reuse before creating. Check the component and service inventories before adding abstractions.
7. Keep UI rules centralized. [`docs/design-system.md`](docs/design-system.md) is the only UI design-system authority.
8. Make the smallest coherent change; do not bundle unrelated cleanup.
9. Verify proportionally. Build and test using [`docs/testing.md`](docs/testing.md).
10. **For photo library work** → [`docs/features/photo-library.md`](docs/features/photo-library.md)
11. **For treemap work** → [`docs/architecture/photo-treemap.md`](docs/architecture/photo-treemap.md)
12. **Before modifying code** → check [`docs/known-issues.md`](docs/known-issues.md)
13. **After modifying code** → append to [`docs/agent-change-log.md`](docs/agent-change-log.md)
14. Do not describe planned work as completed. Do not claim a bug is fixed without verified reproduction.
15. Do not change business logic, bindings, or data models without related evidence.
16. Never commit credentials, tokens, cookies, personal paths, or generated runtime data.

## Reading Order

For a first contribution, read in this order:

1. This file.
2. [`AGENT_HANDOFF.md`](AGENT_HANDOFF.md) — current task status, branch, recent changes.
3. [`docs/current-status.md`](docs/current-status.md) — feature-by-feature implementation state.
4. [Architecture](docs/architecture.md) and the quick [architecture map](.ai/architecture-map.md).
5. [Workflow](docs/workflow.md) and [testing](docs/testing.md).
6. The standard that owns the planned change: [components](docs/components.md), [coding style](docs/coding-style.md), or [release](docs/release.md).
7. For any UI work, read [design-system.md](docs/design-system.md) before XAML changes.
8. Read the relevant source files and tests; use [.ai/onboarding.md](.ai/onboarding.md) for the complete startup checklist.

### Feature-specific documents

| Area | Document |
|---|---|
| Photo library (browse, filters, grid) | [`docs/features/photo-library.md`](docs/features/photo-library.md) |
| Treemap architecture & layout | [`docs/architecture/photo-treemap.md`](docs/architecture/photo-treemap.md) |
| Known issues & bugs | [`docs/known-issues.md`](docs/known-issues.md) |
| Agent change history | [`docs/agent-change-log.md`](docs/agent-change-log.md) |
| Version changelog | [`CHANGELOG.md`](CHANGELOG.md) |

## Development Flow

`Requirement analysis → Architecture analysis → Reuse check → Implementation → Build → Test → Documentation`

Stop when a required build or test fails. Diagnose the failure before continuing. The detailed process belongs to [workflow.md](docs/workflow.md); validation selection belongs to [testing.md](docs/testing.md).

## Documentation Index

### Long-term standards (`docs/`)

| Document | Authority |
|---|---|
| [architecture.md](docs/architecture.md) | Project layers, responsibilities, dependency direction, MVVM, resource architecture, and data flow |
| [design-system.md](docs/design-system.md) | Sole authority for UI design, tokens, visual components, layout, and interaction states |
| [components.md](docs/components.md) | Component governance, reuse, extension, creation, and naming decisions |
| [coding-style.md](docs/coding-style.md) | C#, WPF, XAML, ResourceDictionary, Style, and Theme implementation conventions |
| [workflow.md](docs/workflow.md) | Feature and maintenance workflow |
| [testing.md](docs/testing.md) | Build, test, smoke-test, and publish decision matrix |
| [release.md](docs/release.md) | Formal release and regression procedure |

### Current-state documents

| Document | Use |
|---|---|
| [AGENT_HANDOFF.md](AGENT_HANDOFF.md) | Mandatory first read — current branch, completed/partial/known issues |
| [docs/current-status.md](docs/current-status.md) | Feature-by-feature implementation state with Stable/Partial/Planned labels |
| [docs/known-issues.md](docs/known-issues.md) | Bug tracking with reproduction steps and resolution status |
| [docs/agent-change-log.md](docs/agent-change-log.md) | Append-only record of every agent modification |
| [CHANGELOG.md](CHANGELOG.md) | Human-readable version changelog |

Existing [component inventory](docs/component-inventory.md), [resource dictionary structure](docs/resource-dictionary-structure.md), and [UI audit](docs/ui-audit.md) are snapshots or specialist references, not long-term rule sources.

### AI handbook (`.ai/`)

| Document | Use |
|---|---|
| [onboarding.md](.ai/onboarding.md) | First five minutes in the repository |
| [architecture-map.md](.ai/architecture-map.md) | Fast directory and dependency lookup |
| [feature-template.md](.ai/feature-template.md) | Standard feature analysis and delivery record |
| [common-tasks.md](.ai/common-tasks.md) | Project-specific task playbooks |
| [debug-guide.md](.ai/debug-guide.md) | Diagnostic paths for common failures |

Historical files under `docs/superpowers/` explain past decisions but never override the standards above.
