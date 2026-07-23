# AI Development Infrastructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a project-specific, cross-linked AI development documentation system with one authoritative source for every long-term rule.

**Architecture:** Keep `AGENTS.md` as a short router, `docs/` as the sole home of long-term project rules, and `.ai/` as procedural guidance that links back to those rules. Preserve existing UI and audit documents without duplicating the design system.

**Tech Stack:** Markdown, .NET 8, C# 12, WPF, XAML, CommunityToolkit.Mvvm, xUnit, PowerShell.

## Global Constraints

- Do not change product code, behavior, or UI.
- `docs/design-system.md` remains the sole UI design-system authority.
- Every normative rule has exactly one owning document.
- Every new document begins with Purpose, Scope, Audience, and References metadata.
- Long documents use a table of contents and consistent terminology.
- `AGENT_HANDOFF.md` remains only as a compatibility pointer.
- The current exported workspace has no `.git` directory; do not claim commits or history checks.

---

### Task 1: Entrypoints and authority map

**Files:**
- Create: `AGENTS.md`
- Modify: `AGENT_HANDOFF.md`

- [ ] Write the concise project introduction, AI principles, reading order, workflow overview, and document index in `AGENTS.md`.
- [ ] Replace `AGENT_HANDOFF.md` with a compatibility notice that links to `AGENTS.md`.
- [ ] Verify neither file contains detailed coding, UI, test, or release rules.

### Task 2: Long-term project standards

**Files:**
- Create: `docs/architecture.md`
- Create: `docs/components.md`
- Create: `docs/coding-style.md`
- Create: `docs/workflow.md`
- Create: `docs/testing.md`
- Create: `docs/release.md`

- [ ] Derive architecture and dependency facts from the solution, project references, App composition, ViewModels, services, themes, and tests.
- [ ] Give each rule category one owner and link to `docs/design-system.md` for all UI design decisions.
- [ ] Record current exceptions as facts while defining safe boundaries for new work.
- [ ] Add project-specific commands and verification criteria only to their owning workflow documents.

### Task 3: AI operating handbook

**Files:**
- Create: `.ai/onboarding.md`
- Create: `.ai/architecture-map.md`
- Create: `.ai/feature-template.md`
- Create: `.ai/common-tasks.md`
- Create: `.ai/debug-guide.md`

- [ ] Write a two-minute orientation and five-minute startup path.
- [ ] Map directories, dependencies, and common change locations without redefining architecture rules.
- [ ] Add procedural playbooks for pages, dialogs, themes, toolbars, and business features.
- [ ] Add diagnostic decision paths for Theme, Binding, ResourceDictionary, MVVM, WebView2, and locked build output.
- [ ] Link every normative statement back to its owning `docs/` file.

### Task 4: Documentation review and project verification

**Files:**
- Review: all Markdown files referenced by `AGENTS.md`
- Verify: `HanabePhotoManager.sln`

- [ ] Check required metadata, heading format, terminology, TOCs, relative links, and reciprocal navigation.
- [ ] Scan for duplicate rules, conflicting ownership, stale references, circular links, unreferenced new documents, and orphaned norms.
- [ ] Run `dotnet build HanabePhotoManager.sln -c Release --artifacts-path .artifacts/ai-infrastructure-review` and expect exit code 0.
- [ ] Run `dotnet test HanabePhotoManager.sln -c Release --no-build --artifacts-path .artifacts/ai-infrastructure-review` and expect exit code 0 with all tests passing.
- [ ] Do not publish because this change only affects documentation; validate that `docs/release.md` references `tools/Publish-Clean.ps1` accurately.

## Self-Review

- Spec coverage: all approved files, metadata, authority boundaries, compatibility handling, and review criteria map to Tasks 1–4.
- Placeholder scan: no incomplete implementation instructions are present.
- Consistency: paths and terminology match the current exported project structure.
