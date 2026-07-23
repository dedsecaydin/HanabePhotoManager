# AI Onboarding

> **Purpose:** Give a new AI a timed, procedural path from repository entry to safe implementation.  
> **Scope:** The first five minutes of any new development session.  
> **Audience:** AI coding assistants with no prior repository context.  
> **References:** [architecture.md](../docs/architecture.md), [workflow.md](../docs/workflow.md)

## Table of Contents

- [First Two Minutes](#first-two-minutes)
- [Minutes Three to Five](#minutes-three-to-five)
- [Ready-to-Work Check](#ready-to-work-check)

## First Two Minutes

1. Read the root `AGENTS.md` entry point.
2. Read [architecture.md](../docs/architecture.md) and scan [architecture-map.md](architecture-map.md).
3. Identify the task's owning project and matching test project.
4. If any XAML, Style, Theme, or UI state may change, read [design-system.md](../docs/design-system.md).

## Minutes Three to Five

1. Read the relevant section of [workflow.md](../docs/workflow.md) and the verification row in [testing.md](../docs/testing.md).
2. Inspect the exact production files and matching tests; search by responsibility before proposing new types.
3. Check [components.md](../docs/components.md) and [component-inventory.md](../docs/component-inventory.md) for reusable UI/services.
4. Check working-tree state when Git metadata exists. This exported snapshot may not include `.git`; do not infer history when it is absent.
5. Write down the outcome, non-goals, owning layer, reuse decision, verification plan, and authoritative document affected.

## Ready-to-Work Check

Start implementation only when you can answer:

- Which project owns the change and why?
- What existing implementation and tests are closest?
- Is this reuse, extension, or creation?
- Which build/test/smoke checks are mandatory?
- Which single document owns any changed long-term rule?
- What user data, credentials, and unrelated work must remain untouched?

Use [feature-template.md](feature-template.md) for a feature-sized change and [debug-guide.md](debug-guide.md) for diagnosis. These are execution aids; the linked `docs/` standards remain authoritative.
