# Duplicate Merge Scope Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans task-by-task with TDD.

**Goal:** Ask which duplicate algorithms should feed the review window before a library duplicate scan starts.

**Architecture:** A pure enum/policy owns scope semantics; a small WPF dialog captures the choice; the existing audit method conditionally invokes exact and visual scanners and keeps destructive actions in the existing review window.

**Tech Stack:** .NET 8, C# 12, WPF, xUnit, FluentAssertions.

## Global Constraints

- No automatic file deletion, overwrite, move, or merge.
- Default scope preserves the existing exact-plus-visual behavior.
- Work and publish only on D drive.

### Task 1: Scope policy

- [ ] Add failing policy tests for exact-only, visual-only, and both.
- [ ] Implement `DuplicateMergeScope` and `DuplicateMergeScopePolicy`.
- [ ] Run focused tests and commit.

### Task 2: Selection dialog and scan routing

- [ ] Add failing XAML/source contract tests for three radio choices, default both, cancel, and conditional scanner calls.
- [ ] Implement `DuplicateMergeScopeWindow` using shared dialog/button resources.
- [ ] Prompt before scanning and conditionally call exact/visual scanners; visual exclusions use exact paths only when exact scanning ran.
- [ ] Run focused App/Infrastructure tests and commit.

### Task 3: Verify and publish

- [ ] Update current status, handoff, and append-only change log.
- [ ] Run Release `/warnaserror` build and full tests.
- [ ] Merge locally, stop only the exact published process, overwrite `D:\hanabe-publish-v2`, launch it, and verify the process path.
