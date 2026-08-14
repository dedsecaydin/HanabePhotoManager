# HanabePhoto Hermes / Multi-Agent Mandatory Rules

> 建议插入 `AGENTS.md` 靠前位置。  
> 本节只负责指向总纲，不替代项目中已有的工程规则。

## Mandatory Master Guide

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

## Source of Truth

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

不得把 ChatGPT、Hermes、Codex 的历史上下文当作当前版本事实来源。

如果历史上下文和当前仓库冲突：

**当前仓库与实际运行结果优先。**

## Preserve Existing Behavior

UI / UX / Motion 重构不得擅自：

- 删除当前已有功能
- 修改现有 Command / Binding 语义
- 改变 ViewModel 行为
- 复制第二套业务 Service
- 重写稳定 Thumbnail / Selection / Filter / Cache 系统
- 修改数据格式或持久化行为

如任务确实需要行为变更：

必须明确记录原因、影响范围、回归风险和验证结果。

## Required Pre-Change Audit

大型修改前必须：

1. 读取当前相关代码和文档。
2. 建立当前 Feature Inventory。
3. 记录 Must Preserve 项。
4. 进行 baseline Build / Test。
5. 如果是 UI 任务，尽可能记录当前 Runtime / Screenshot。
6. 构造 Current Context Package 后再调用 ChatGPT Desktop。

## UI Direction

目标方向：

> Material Design 3 × Codex Desktop × Lightroom

Motion：

> Fast × Subtle × Precise × Interruptible

但是：

**必须复用当前项目现有 Design Token、Motion Token 和共享组件。**

不得为了新风格建立平行 Design System。

## Active Bug Hunting

不要等待用户提供完整 Bug 列表。

重要改动后必须主动检查：

- Async race
- Cancellation
- Thumbnail / Virtualization
- Viewport priority
- Selection state
- Navigation state
- External file changes
- Memory
- UI thread blocking
- Animation conflict
- Loading / Empty / Error
- WPF Binding / Event / Dispatcher / lifecycle issues

## Progress

如果任务属于 Hermes 总控 UI/UX 重构计划：

每完成并验证 10%：

向用户汇报一次。

P0 阻止进度提升。

当前阶段关键 P1 未解决时：

不得宣布该阶段完成。

## Documentation

重要修改后更新：

- `AGENT_HANDOFF.md`
- 当前相关 `docs/`
- 必要的 Feature / Regression / Design 文档

总原则：

> Inspect first.  
> Preserve working behavior.  
> Modify incrementally.  
> Verify runtime behavior.  
> Current HanabePhoto is the source of truth.
