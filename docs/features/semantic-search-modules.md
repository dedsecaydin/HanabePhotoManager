# Semantic Search — 模块划分（框架工程师分块方案）

> 依据 `.ai/framework-engineer-role.md`。实施必须按此文件规划分块落地，严禁把多个模块堆进一个文件。

## 模块划分

| 模块 | 职责 | 文件路径 | 依赖 |
|------|------|----------|------|
| 搜索模型 | 查询/结果/索引状态/索引条目 数据模型 | `src/HanabePhotoManager.Core/Search/SemanticSearchModels.cs` | Core 内部 |
| 搜索接口 | ISemanticSearchService（EnsureIndex/Search/Status） | `src/HanabePhotoManager.Core/Search/ISemanticSearchService.cs` | 模型 |
| 索引存储接口 | ISemanticIndexStore（Upsert/GetAll/Count/RemoveMissing） | `src/HanabePhotoManager.Core/Search/ISemanticIndexStore.cs` | 模型 |
| CLIP 分词器 | BPE tokenizer（中英文） | `src/HanabePhotoManager.Infrastructure/Search/ClipTokenizer.cs` | 资源 |
| 图像预处理 | ImageSharp resize 224 + normalize | `src/HanabePhotoManager.Infrastructure/Search/ClipImagePreprocessor.cs` | ImageSharp |
| 推理服务 | ONNX 图像/文本编码 + 余弦排序 | `src/HanabePhotoManager.Infrastructure/Search/ClipSemanticSearchService.cs` | 分词器/预处理/存储 |
| 向量存储 | SQLite semantic_index 表读写 | `src/HanabePhotoManager.Infrastructure/Search/SqliteSemanticIndexStore.cs` | Sqlite |
| 模型目录 | 模型路径解析/缺失提示 | `src/HanabePhotoManager.Infrastructure/Search/ModelCatalog.cs` | — |
| 搜索 VM | 防抖查询/索引状态/进度 | `src/HanabePhotoManager.App/Search/SemanticSearchViewModel.cs` | 服务接口 |
| 搜索结果项 VM | 单条结果（缩略图/得分/导航） | `src/HanabePhotoManager.App/Search/SearchResultItemViewModel.cs` | VM |
| 搜索视图 | 搜索框+结果网格+进度条 XAML | `src/HanabePhotoManager.App/Search/SemanticSearchView.xaml` | VM |
| 视图代码 | 事件/导航 code-behind | `src/HanabePhotoManager.App/Search/SemanticSearchView.xaml.cs` | 视图 |

## 接口契约

```csharp
namespace HanabePhotoManager.Core.Search;
public interface ISemanticSearchService {
    Task EnsureIndexAsync(string libraryRoot, IProgress<SemanticIndexStatus>? progress, CancellationToken ct);
    Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(string query, int limit, CancellationToken ct);
    SemanticIndexStatus GetIndexStatus();
}
public interface ISemanticIndexStore {
    Task UpsertAsync(IReadOnlyList<SemanticIndexEntry> entries, CancellationToken ct);
    Task<IReadOnlyList<SemanticIndexEntry>> GetAllAsync(CancellationToken ct);
    Task<int> CountAsync(CancellationToken ct);
    Task RemoveMissingAsync(IEnumerable<string> existingPaths, CancellationToken ct);
}
```

## 实施顺序（分块）

1. 块 1：Core 模型 + 两个接口（SemanticSearchModels / ISemanticSearchService / ISemanticIndexStore）
2. 块 2：Infrastructure ClipTokenizer + ClipImagePreprocessor（可测）
3. 块 3：Infrastructure SqliteSemanticIndexStore + ModelCatalog
4. 块 4：Infrastructure ClipSemanticSearchService（串起 2+3）
5. 块 5：App SemanticSearchViewModel + SearchResultItemViewModel
6. 块 6：App SemanticSearchView.xaml + code-behind（接入 MainWindow 顶部搜索框）
7. 块 7：测试（Core 全测 + Infra 桩测）+ 文档 + commit/push

每块完成即可构建；最终 0 警告 0 错误 + 全测试绿。
