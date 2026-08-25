# Hanabe Photo Manager 源码维护指南

## 总方针

维护时遵循一条主线：先确认行为归属，再做最小修改，最后用自动化测试和实际界面验证结果。不要为了减少文件数量而合并职责，也不要为了“看起来更干净”而移动绑定、设置键或文件操作顺序。

```text
用户操作 / XAML
        ↓
ViewModel（状态、命令、流程编排）
        ↓
Core（可独立测试的规则）
        ↓
Infrastructure（磁盘、数据库、外部系统实现）
```

依赖方向固定为 `App → Core`、`App → Infrastructure`、`Infrastructure → Core`。Core 不应出现 WPF、SQLite、系统对话框或 Windows Shell 类型。

## 从哪里开始看

1. `AGENT_HANDOFF.md`：当前版本、分支和已知问题。
2. `docs/current-status.md`：各功能真实完成状态。
3. `docs/architecture.md`：项目分层和依赖方向。
4. `docs/design-system.md`：所有 UI、颜色、圆角、动效的唯一设计依据。
5. `docs/features/photo-library.md`：图库筛选、分组、缩略图和查看器行为。
6. 目标类对应的测试；修改前先看测试表达了哪些既有契约。

## `src/` 目录职责

| 目录 | 应放内容 | 不应放内容 |
|---|---|---|
| `HanabePhotoManager.Core` | 分类、排序、导入规划、布局等纯规则 | WPF、磁盘实现、数据库 |
| `HanabePhotoManager.Infrastructure` | 文件传输、校验、持久化、索引实现 | 页面状态、控件、主题 |
| `HanabePhotoManager.App` | WPF 页面、ViewModel、桌面集成、应用服务 | 可独立复用的领域规则 |

App 内部优先按“页面/ViewModel → 协作服务 → Core/Infrastructure”阅读。`MainWindowViewModel` 使用 partial 文件承载既有主流程；新增独立功能时优先创建聚焦的 ViewModel 或服务，避免继续扩大主类。

## 注释规范

- 公开和内部类型：说明它负责什么、明确不负责什么。
- 公开方法：说明输入、输出、副作用、取消和失败语义。
- 复杂逻辑：解释“为什么必须这样做”，例如原子发布、最后请求生效、UI 线程切换、缓存失效条件。
- 不复述代码字面含义，不给简单赋值和明显分支增加噪声。
- 行为被修改时同步更新注释；过期注释比没有注释更危险。

## 常见修改路线

### 修改图库

先看 `docs/features/photo-library.md`。保持日期/文件夹分组、自然滚动、展开收起、缩略图缓存、双击查看器和 Inspector 选择状态彼此独立。不要把 XML 当作独立媒体卡片；它只能作为对应视频的元数据标记。

### 修改导入

先确认来源分析、分类规划、重复决策、临时文件写入、哈希复验、最终发布和日志恢复的顺序。任何“加速”都不能删掉目标复验、失败回滚或修后目录保护。

### 修改 UI 或主题

只使用 `docs/design-system.md` 规定的语义资源。不要在页面内硬编码主题色；不要改已有 `x:Key`、Binding、Command 或事件名，除非同时修改调用方并增加回归测试。代码后置只保留视觉树、输入设备、窗口、拖放和系统对话框适配。

## 安全精简原则

可以做：删除重复局部计算、提取无副作用函数、展开难读的压缩单行代码、复用已有服务、使用等价的标准库 API。

需要谨慎：拆分大型 ViewModel、改变异步并发、缓存键、文件枚举顺序、设置序列化字段、XAML 资源加载顺序。

禁止无证据修改：导入结果、文件删除/覆盖策略、公开 API、Binding/Command、设置键和主题资源键。

## 验证清单

```powershell
dotnet restore HanabePhotoManager.sln
dotnet build HanabePhotoManager.sln -c Release /warnaserror
dotnet test HanabePhotoManager.sln -c Release --no-build
```

涉及界面时还要手动检查：启动/关闭、目标页面往返、浅色/深色切换、键盘操作、空状态、取消和失败恢复。涉及发布内容时再执行 `docs/release.md` 的 publish 流程。

完成后追加 `docs/agent-change-log.md`，写清修改范围、保留的契约和验证数字。
