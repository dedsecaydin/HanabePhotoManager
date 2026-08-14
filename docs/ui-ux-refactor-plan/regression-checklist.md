# HanabePhoto UI/UX 重构 — Regression Checklist（基线）

> 建立：2026-08-11（0% 审计后）
> 用途：每次 10% 阶段修改后必跑。任何一项回归 = 阶段不通过。

## A. 构建 / 测试（每次必跑）

- [ ] `dotnet build HanabePhotoManager.sln -c Release` → 0 警告 0 错误
- [ ] `dotnet test HanabePhotoManager.sln -c Debug` → 全绿（当前基线 893：Core 370 / Infra 164 / App 359）

## B. 今日已修回归项（不得复发）

- [ ] 启动无"发生了未处理的错误"弹窗
- [ ] 主页切换正常（此前 CornerRadius `0,0,{StaticResource}` 崩溃点）
- [ ] 自定义相册页打开正常（BoolToVis 本地资源）
- [ ] 看图弹窗（PhotoViewerWindow）打开正常
- [ ] 微信发送页（WeChatSenderView）打开正常

## C. 核心功能回归（10%-100% 阶段抽查）

### 图库浏览
- [ ] 照片图库进入（默认空间树图）
- [ ] 网格视图 / 时间线 / 列表切换
- [ ] 日期日历单选切换（KI-09 27→25 空结果不得复发）
- [ ] 文件类型筛选 chips（RAW/JPG/MP4/Video）
- [ ] 业务分类筛选（RAW生图/JPG生图/修后/视频/action视频/素材）
- [ ] "已修"筛选不崩溃（KI-08）
- [ ] 搜索框（文件名 + 语义描述）
- [ ] Ctrl+滚轮缩放、Space+拖拽平移
- [ ] 项计数（右下 CurrentViewItemCount 子树感知）

### 空间树图
- [ ] 分类 Squarified 布局渲染
- [ ] 子目录进入/返回 + 面包屑
- [ ] 缩略图持续加载（KI-01 不复发）
- [ ] 大库滚动（6217+）不卡 UI 线程（KI-05/06/07）

### 其他
- [ ] 导入：多选 / 进度 / 取消 / SHA-256 去重决策
- [ ] 自定义相册：添加 / 浏览 / 重命名 / 移除引用
- [ ] 语义搜索：首查索引进度 / 候选排序 / 与其他筛选组合
- [ ] 深色/浅色主题切换 + 用户偏好持久化
- [ ] 看图：键盘导航 / EXIF 面板 / 关闭

## D. UI 违禁项检查（master guide §14 对照）

- [ ] 无新增 Card 套 Card
- [ ] 无粗边框 / 巨大圆角矩形
- [ ] 无 Android 手机式布局 / 巨大按钮
- [ ] 无强渐变 / 霓虹 Glow / 重玻璃拟态
- [ ] 无夸张动画（只允许 150/180/220ms 颜色/透明度/边框/微位移）
- [ ] 页面不写死原始颜色（只用 Token）
- [ ] 键盘焦点可见

## E. 已知问题跟踪（KI 状态快照）

| ID | 问题 | 状态 | 备注 |
|---|---|---|---|
| KI-01 | 缩略图只加载第一批 | Fix attempted | 10%-20% 复现验证 |
| KI-03 | Justified 像固定网格 | In progress | UI 阶段重点关注 |
| KI-04 | 瓦片大白边 | Partial | |
| KI-05/06 | 大库截断/底部细条 | Partial | |
| KI-08 | 已修崩溃 | Fix attempted | 回归项 C |
| KI-09/10/11 | 日期/修后归属 | Fix attempted | 回归项 C |
| KI-13 | PSD 排除 | Partial | |
| KI-14 | Root overview | Blocked | 待重设计 |
