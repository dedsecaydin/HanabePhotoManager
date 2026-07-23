# Development Workflow

> **Purpose:** Define the authoritative process for implementing and maintaining this project.  
> **Scope:** Features, fixes, refactoring, dependency work, and documentation.  
> **Audience:** Human and AI contributors from intake through handoff.  
> **References:** [architecture.md](architecture.md), [components.md](components.md), [testing.md](testing.md)

## Table of Contents

- [Required Flow](#required-flow)
- [Requirement Analysis](#requirement-analysis)
- [Architecture Analysis](#architecture-analysis)
- [Reuse Check](#reuse-check)
- [Implementation](#implementation)
- [Build and Test](#build-and-test)
- [Documentation](#documentation)
- [Completion Criteria](#completion-criteria)

## Required Flow

`Requirement analysis → Architecture analysis → Component reuse check → Implementation → Build → Test → Documentation`

Do not continue after a failing mandatory step. Investigation may move backward when evidence disproves an assumption.

## Requirement Analysis

State the user-visible outcome, inputs, outputs, error states, persistence, cancellation needs, non-goals, and affected workflow. Separate the requested change from opportunistic cleanup. For UI work, link to [design-system.md](design-system.md) instead of repeating its rules.

## Architecture Analysis

Read current production code and matching tests. Use [architecture.md](architecture.md) to select the owning layer and identify contracts, persisted formats, external systems, ViewModels, and composition points. Document changes to dependencies, provider boundaries, data formats, or resource-loading order before implementation. Historical specs explain intent but do not override current code or standards.

## Reuse Check

Search source, tests, `Themes/Controls`, and [component-inventory.md](component-inventory.md) by responsibility. Record whether the work reuses, extends, or creates a component/service and apply [components.md](components.md).

## Implementation

Make the smallest coherent change. Modify the owning layer first, followed by adapters, presentation, and composition. Keep unrelated formatting and refactoring out. Add tests with the implementation and preserve cancellation, progress, and persisted-state compatibility in workflows that already support them.

Follow [coding-style.md](coding-style.md). UI implementation follows [design-system.md](design-system.md) without page-local substitutes for shared resources.

## Build and Test

Run focused tests during iteration. Then run the Release build, required automated tests, and applicable smoke checks selected by [testing.md](testing.md). Warnings and required test failures block completion.

## Documentation

Update only the authority that owns the changed fact:

- Architecture or data flow → `architecture.md`.
- Component governance → `components.md`.
- Implementation convention → `coding-style.md`.
- Development process → this file.
- Verification requirement → `testing.md`.
- Release procedure → `release.md`.
- UI design rule → `design-system.md` only.
- AI procedure → `.ai/`, linking to the owning standard.

Update inventories only when their recorded snapshot changes. Never promote temporary task status, local workarounds, migration history, or session notes into long-term standards.

## Completion Criteria

- Requirement and non-goals are clear.
- Ownership, dependency direction, and reuse decision are correct.
- Implementation and tests are focused.
- Required Build, Test, and Smoke Test pass.
- No credentials or local runtime data are introduced.
- The single owning document is updated; other documents link rather than copy.
