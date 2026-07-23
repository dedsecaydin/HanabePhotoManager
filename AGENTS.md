# Hanabe Photo Manager — AI Development Guide

> **Purpose:** Provide the single entry point for AI-assisted development.  
> **Scope:** Repository orientation, working principles, reading order, and document routing.  
> **Audience:** Codex, ChatGPT, Claude Code, Cursor, and human contributors.  
> **References:** [`docs/architecture.md`](docs/architecture.md), [`docs/workflow.md`](docs/workflow.md), [`docs/design-system.md`](docs/design-system.md)

## Project

Hanabe Photo Manager is a Windows photo-management desktop application built with .NET 8, C# 12, WPF, XAML, and CommunityToolkit.Mvvm. The solution separates domain policies (`Core`), external-system implementations (`Infrastructure`), and the WPF application (`App`), with matching xUnit test projects.

The application covers media import and organization, metadata and ratings, local image analysis, people and map views, compression and watermark workflows, and provider-neutral cloud foundations.

## AI Working Principles

1. Inspect before editing. Read the relevant production code, tests, and authoritative document first.
2. Preserve user work. Never reset, overwrite, or delete unrelated changes.
3. Follow existing boundaries. Dependencies must continue to point toward Core.
4. Reuse before creating. Check the component and service inventories before adding abstractions.
5. Keep UI rules centralized. [`docs/design-system.md`](docs/design-system.md) is the only UI design-system authority.
6. Make the smallest coherent change; do not bundle unrelated cleanup.
7. Verify proportionally. Build and test using [`docs/testing.md`](docs/testing.md).
8. Update the owning document when a long-term rule or architecture boundary changes.
9. Never commit credentials, tokens, cookies, personal paths, or generated runtime data.
10. Distinguish verified current behavior from proposals and historical design records.

## Reading Order

For a first contribution, read in this order:

1. This file.
2. [Architecture](docs/architecture.md) and the quick [architecture map](.ai/architecture-map.md).
3. [Workflow](docs/workflow.md) and [testing](docs/testing.md).
4. The standard that owns the planned change: [components](docs/components.md), [coding style](docs/coding-style.md), or [release](docs/release.md).
5. For any UI work, read [design-system.md](docs/design-system.md) before XAML changes.
6. Read the relevant source files and tests; use [.ai/onboarding.md](.ai/onboarding.md) for the complete startup checklist.

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
