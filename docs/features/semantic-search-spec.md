# Semantic Search (CLIP) — Implementation Spec

> 目标：在 Hanabe Photo Manager 中实现**描述性语义搜索**——用户用自然语言描述查询照片（如「海边日落」「红色连衣裙」「2025 年重庆夜景」），返回按语义相关度排序的照片。范围仅限语义搜索；本次**不做**人脸识别、OCR、物体分类（后续迭代）。

## 1. 铁律（必须遵守）

1. **0 警告 0 错误**：Release 构建必须 0 warnings / 0 errors。
2. **现有测试全过**：Core 365 + Infrastructure 160 + App 336（约）全绿，不能破坏任何现有功能（treemap、导入、日期过滤、修后保护等）。
3. 遵循项目现有 Clean Architecture：依赖方向 Core ← Infrastructure ← App，App 只通过接口用 Core/Infrastructure。
4. 先读 `AGENTS.md`、`AGENT_HANDOFF.md`、`docs/architecture.md`、`docs/testing.md`、`docs/design-system.md` 再动手。
5. 修改代码后追加到 `docs/agent-change-log.md`。
6. 不把模型文件（数百 MB）提交进 git；新增路径进 `.gitignore`。
7. 新代码要有测试（Core 层逻辑必须全覆盖；Infrastructure 用桩/mock 避免依赖真实模型文件）。

## 2. 技术选型

- **ONNX Runtime**：`Microsoft.ML.OnnxRuntime` NuGet（CPU 版）。GPU 加速（DirectML）留作可选扩展，不阻塞主路径。
- **图像预处理**：复用现有 `SixLabors.ImageSharp`。
- **向量存储**：复用现有 `Microsoft.Data.Sqlite`（已引用）。新表 `semantic_index`。
- **模型（二选一，先验证可用性再定）**：
  - 首选 **Chinese-CLIP**（`OFA-Sys/chinese-clip-vit-base-patch32`）——中文语义搜索效果最好。ONNX 导出若 HF 无现成，用 Python（transformers + onnx）转一次，转换脚本放 `tools/`。
  - 备选 **SigLIP**（`google/siglip-base-patch16-224` 或其 ONNX 社区导出）——多语言、现成 ONNX 多。
  - 下载源：`hf-mirror.com`（国内镜像，HF 直连超时）。模型文件放 `%LOCALAPPDATA%\HanabePhotoManager\models\`（不是项目目录）。
- **Tokenizer**：CLIP BPE tokenizer（内嵌 vocab.json + merges.txt 资源，或 ONNX 内联）。中文查询必须支持。

## 3. 架构与文件规划

### Core（新增，纯接口与模型）
- `src/HanabePhotoManager.Core/Search/SemanticSearchModels.cs` — `SemanticSearchQuery`、`SemanticSearchResult`（FileKey + Score）、`SemanticIndexStatus`（总文件数/已索引数/运行中）、`SemanticIndexEntry`
- `src/HanabePhotoManager.Core/Search/ISemanticSearchService.cs` —
  ```csharp
  Task EnsureIndexAsync(string libraryRoot, IProgress<SemanticIndexStatus>? progress, CancellationToken ct);
  Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(string query, int limit, CancellationToken ct);
  SemanticIndexStatus GetIndexStatus();
  ```
- `src/HanabePhotoManager.Core/Search/ISemanticIndexStore.cs` — 向量持久化（Upsert/GetAll/Count/RemoveMissing）

### Infrastructure（新增，实现）
- `src/HanabePhotoManager.Infrastructure/Search/ClipSemanticSearchService.cs` — ONNX Runtime 推理：图像编码（224x224 resize + ImageNet normalize → ViT → 512 维向量）、文本编码（tokenize → 文本 transformer → 512 维向量）、余弦相似度排序
- `src/HanabePhotoManager.Infrastructure/Search/ClipTokenizer.cs` — BPE tokenizer
- `src/HanabePhotoManager.Infrastructure/Search/ClipImagePreprocessor.cs` — ImageSharp 预处理
- `src/HanabePhotoManager.Infrastructure/Search/SqliteSemanticIndexStore.cs` — SQLite 向量表（embedding 存 BLOB：4 字节小端 float 数组）
- `src/HanabePhotoManager.Infrastructure/Search/ModelCatalog.cs` — 模型路径解析 + 缺失提示（首次搜索时给出「模型未就绪」引导：下载命令/手动放置说明）
- 新增 NuGet：`Microsoft.ML.OnnxRuntime`（Core 不加依赖）

### App（新增 UI）
- 搜索入口：MainWindow 顶部搜索框（或工具栏），占位符「语义搜索：试试「海边日落」…」
- `SemanticSearchViewModel` — 防抖（300ms）查询、结果列表、状态（未索引/索引中 x%/完成）、首次索引自动触发 + 手动「重新索引」
- 结果展示：复用现有照片网格风格（缩略图 + 相关度排序），点击结果导航到对应照片/目录
- 索引进度：状态栏或搜索框下方细进度条；索引在后台 Task 跑，节流（每张间 5-20ms，避免卡 UI），可取消

## 4. 数据流

索引：
1. 扫描媒体库根目录（复用现有扫描逻辑/分类器，只索引图片：RAW/JPG/PNG 优先，视频跳过或后续）
2. 每张图：ImageSharp 解码缩略图 → 预处理 → CLIP 图像编码 → 512 向量
3. Upsert 进 SQLite（path 为主键）；已存在且未变更的跳过（用文件 mtime/尺寸判断）
4. 完成后状态「已索引 N 张」

查询：
1. 文本 → tokenizer → CLIP 文本编码 → 512 向量
2. 从 SQLite 读全部向量（或分批），内存中余弦相似度排序
3. 返回 Top N（默认 50）结果

## 5. UI 文案（中文，简洁）

- 未索引：「首次使用需建立语义索引（约 N 张照片，后台进行，可随时用）」
- 索引中：「正在索引 x/N（yy%）…」
- 模型缺失：「语义搜索模型未就绪：下载命令或手动放置说明见 docs/features/semantic-search.md」
- 无结果：「没找到相关照片，换个描述试试」

## 6. 测试要求

- Core：tokenizer（中英文输入）、余弦相似度排序（已知向量）、索引 store 的 Upsert/RemoveMissing（SQLite 内存库）
- Infrastructure：ClipImagePreprocessor 尺寸/归一化；ClipSemanticSearchService 用桩模型或接口 mock
- 全量回归：`dotnet build -c Release` 0 警告 0 错误 + `dotnet test` 全绿
- 验证：构建产物可以启动、现有页面不崩（treemap 打开正常）

## 7. 交付

1. 代码实现 + 测试 + `docs/features/semantic-search.md`（模型获取/下载/常见问题）
2. 转换/下载脚本放 `tools/`（如 `tools/export_clip_onnx.py`、`tools/download_models.ps1`）
3. 追加 `docs/agent-change-log.md`
4. git commit + push 到当前分支
5. 报告：改动文件清单、模型验证结果（选定的模型 + 下载链接）、测试结果摘要
