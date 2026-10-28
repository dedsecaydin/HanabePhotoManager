# 切图实施计划

目标：新增二分、六分、九宫格可定位裁切与高对比工具入口。
架构：分割服务负责像素边界和安全导出，ViewModel 管理异步状态，Window 只负责预览拖动与文件选择。复用 ImageSharp、CommunityToolkit 与共享主题。
约束：保留所有现有改动；输出在 D 盘，禁止 OneDrive；不改变现有工具业务。

- [x] 新增 ImageSplitTests，验证非整除覆盖、裁切边界、像素定位、重名与取消。
- [x] 新增 Splitting/ImageSplitService.cs，实现验证、定向解码和独立目录导出。
- [x] 新增 Splitting/ImageSplitViewModel.cs 和 ImageSplitWindow.xaml/.cs，绑定模式、选区、格式与进度，预览支持拖动。
- [x] CompressionPage 新增入口，复用现有窗口激活模式；共享 Button.ActionTile 增强边界和背景。
- [x] Release 构建、App 测试、运行界面检查，更新状态和变更记录。

