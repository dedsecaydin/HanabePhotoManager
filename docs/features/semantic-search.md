# 语义搜索（Chinese-CLIP）

语义搜索完全在本机执行：ONNX Runtime CPU 生成图像和文本向量，SQLite 保存向量索引。照片和查询不会上传。

## 模型目录

将已导出的 Chinese-CLIP 文件放入：

`%LOCALAPPDATA%\HanabePhotoManager\models\ChineseCLIP\`

目录必须包含：

- `image_encoder.onnx`：输入 `[1,3,224,224]`，输出图像 embedding。
- `text_encoder.onnx`：输入 `input_ids`、`attention_mask`（可选 `token_type_ids`），输出文本 embedding。
- `vocab.txt`：Chinese-CLIP 的 BERT 词表，必须包含 `[PAD]`、`[UNK]`、`[CLS]`、`[SEP]`。

模型文件不得放入仓库，也不得提交。模型缺失时，应用会保持现有图库、Treemap 等功能可用，并显示模型未就绪提示。

## 使用

1. 打开左侧“照片图库”，展开“浏览条件”。
2. 在“语义描述”中输入“海边日落”“红色连衣裙”“重庆夜景”等自然语言；首次搜索会在后台自动建立或更新本机索引。
3. 索引期间可查看进度并取消。索引就绪后，查询经过 300ms 防抖，现有照片墙直接显示最多 50 张相关照片。
4. 日期、业务分类、修图状态、文件类型、评分、智能分类和人物筛选会继续作用于语义候选集；结果保持语义相关度顺序。
5. 点击结果继续使用照片图库既有的查看器与导航；清空描述即可恢复普通浏览。

语义搜索不再占用独立侧边栏页面。模型加载、图像编码和文本查询均通过既有 `ClipSemanticSearchService` 执行，应用层只负责后台调度与浏览筛选组合。

索引仅处理 JPG、JPEG、PNG、BMP、WebP、TIFF；视频和 RAW 保持原有流程，不参与本期索引。

## 排障

- “模型未就绪”：检查上方目录、文件名和词表特殊 token。
- “无法加载 ONNX”：确认图像/文本导出使用同一 Chinese-CLIP checkpoint，且输出是 float embedding。
- 结果不准确：删除 `%LOCALAPPDATA%\HanabePhotoManager\semantic-index.db` 后重新索引；照片原文件不会被删除。
