using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HanabePhotoManager.App.Agents;
using HanabePhotoManager.App.Services;
using static HanabePhotoManager.App.Agents.AgentToolbox;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>
/// 给 AI 用的工具（搜索栏 AI 与外部 agent 共用）。
/// 查询 / 跳转类直接执行；会改磁盘文件或已保存数据的（改日期文件夹备注、给人物改名）需要用户确认。
/// 导入只能“准备”（分析来源、打开导入页），真正复制仍要用户在导入页点确认。
/// </summary>
public sealed partial class MainWindowViewModel
{
    private AgentToolbox? _agentToolbox;

    public AgentToolbox AgentToolbox
    {
        get
        {
            if (_agentToolbox is null)
            {
                _agentToolbox = new AgentToolbox(CreateAgentTools());
                ConfigureAgentToolbox(_agentToolbox);
            }
            return _agentToolbox;
        }
    }

    private static readonly Regex DateArgument = new(@"^\s*(?:(?<y>\d{4})\s*[-./年]\s*)?(?<m>\d{1,2})\s*[-./月]\s*(?<d>\d{1,2})\s*日?\s*$", RegexOptions.Compiled);

    private IEnumerable<AgentTool> CreateAgentTools()
    {
        yield return new AgentTool(
            "get_library_overview",
            "获取照片库概况：照片库位置、照片/视频数量、拍摄日数量、人物数、自定义相册数、磁盘剩余空间、交付进度、是否有未完成的导入、当前所在页面。",
            Schema(),
            false,
            (_, _) => Task.FromResult<object?>(new
            {
                library_root = LibraryRoot,
                media_count = PreviewFiles.Count,
                shoot_day_count = DiscoveredDateCount,
                people_count = PeopleAlbums.Albums.Count,
                named_people_count = PeopleAlbums.NamedAlbumCount,
                custom_album_count = CustomAlbums.Albums.Count,
                storage = LibraryStorageText,
                delivery = HomeDeliverySummary,
                pending_import = HasPendingImportResume ? PendingImportResumeSummary : null,
                current_page = CurrentPage,
            }));

        yield return new AgentTool(
            "list_shoot_days",
            "列出最近的拍摄日（日期文件夹），含文件夹名、备注、原片数、“修后”成片数，按日期从新到旧。",
            Schema(("limit", "integer", "最多返回几天，默认 15", false)),
            false,
            (args, _) =>
            {
                var limit = OptionalInt(args, "limit", 15, 1, 100);
                var days = DatedNodesNewestFirst().Take(limit).Select(node =>
                {
                    var (originals, retouched) = CountDayFiles(node);
                    return new
                    {
                        date = FormatAgentDate(node.Date!.Value),
                        folder = Path.GetFileName(node.FullPath),
                        path = node.FullPath,
                        files = originals + retouched,
                        retouched,
                    };
                }).ToArray();
                return Task.FromResult<object?>(new { count = days.Length, days });
            });

        yield return new AgentTool(
            "search_photos",
            "在照片库里搜索照片并在“照片图库”页显示结果。query 可以是文件名，也可以是画面描述（如“白色婚纱 户外”），描述会用本机语义索引按相关度排序。返回最相关的文件路径。",
            Schema(("query", "string", "文件名或画面描述", true), ("limit", "integer", "返回几条路径，默认 20", false)),
            false,
            async (args, token) =>
            {
                var query = RequireString(args, "query");
                var limit = OptionalInt(args, "limit", 20, 1, 50);
                if (!HasLibraryRoot) throw new AgentToolException("还没有选择照片库。");
                var semantic = !LooksLikeFileOrPath(query);
                await SearchFromWorkspaceAsync(query).ConfigureAwait(true);
                if (!semantic)
                {
                    var files = FilteredPreviewFiles.Take(limit).Select(file => file.FullPath).ToArray();
                    return new { mode = "文件名", shown_in_gallery = true, total = FilteredPreviewFiles.Count(), paths = files };
                }

                // 语义搜索是防抖 + 后台计算：等它算完（最多 25 秒，首次可能要建立索引）。
                var deadline = DateTime.UtcNow.AddSeconds(25);
                await Task.Delay(450, token).ConfigureAwait(true);
                while (SemanticSearch.IsBusy && DateTime.UtcNow < deadline)
                    await Task.Delay(200, token).ConfigureAwait(true);
                return new
                {
                    mode = "画面描述",
                    shown_in_gallery = true,
                    status = SemanticSearch.StatusText,
                    still_indexing = SemanticSearch.IsBusy,
                    paths = SemanticSearch.RankedResultPaths.Take(limit).ToArray(),
                };
            });

        yield return new AgentTool(
            "open_page",
            "切换到某个功能页：home 主页、import 导入、gallery 照片图库、albums 自定义相册、people 人物查找、map 地图照片、tools 图片小工具、date_folders 日期文件夹、settings 设置。",
            Schema(("page", "string", "home / import / gallery / albums / people / map / tools / date_folders / settings", true)),
            false,
            (args, _) =>
            {
                var page = RequireString(args, "page").ToLowerInvariant();
                var command = page switch
                {
                    "home" => ShowHomeCommand,
                    "import" => ShowImportCommand,
                    "gallery" or "preview" or "library" => ShowPreviewCommand,
                    "albums" => ShowCustomAlbumsCommand,
                    "people" or "faces" => ShowFaceSearchCommand,
                    "map" => ShowMapPhotosCommand,
                    "tools" => ShowCompressionCommand,
                    "date_folders" => ShowDateFoldersCommand,
                    "settings" => ShowSettingsCommand,
                    _ => throw new AgentToolException($"没有叫 {page} 的页面"),
                };
                command.Execute(null);
                return Task.FromResult<object?>(new { ok = true, current_page = CurrentPage });
            });

        yield return new AgentTool(
            "open_shoot_day",
            "在照片图库中打开某一天的照片。date 可写 2026-11-04、11.04、11月4日。",
            Schema(("date", "string", "拍摄日期", true)),
            false,
            async (args, _) =>
            {
                var node = FindDateNode(RequireString(args, "date"));
                CurrentPage = "Preview";
                await SelectDateAsync(node).ConfigureAwait(true);
                var (originals, retouched) = CountDayFiles(node);
                return new { ok = true, opened = Path.GetFileName(node.FullPath), files = originals + retouched, retouched };
            });

        yield return new AgentTool(
            "list_people",
            "列出人物相册（本机人脸识别的结果），按照片数从多到少。可按名字过滤。",
            Schema(("query", "string", "名字里包含的文字，可省略", false), ("limit", "integer", "最多返回几个，默认 20", false)),
            false,
            (args, _) =>
            {
                var query = OptionalString(args, "query") ?? string.Empty;
                var limit = OptionalInt(args, "limit", 20, 1, 200);
                var people = PeopleAlbums.Albums
                    .Where(album => query.Length == 0 || album.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                    .OrderByDescending(album => album.PhotoCount)
                    .Take(limit)
                    .Select(album => new { name = album.Name, photos = album.PhotoCount, named = album.IsNamed })
                    .ToArray();
                return Task.FromResult<object?>(new { total = PeopleAlbums.Albums.Count, people });
            });

        yield return new AgentTool(
            "open_person",
            "打开“人物查找”页并显示某个人物的全部照片。",
            Schema(("name", "string", "人物名字，例如“新娘”或“人物 12”", true)),
            false,
            (args, _) =>
            {
                var album = FindPerson(RequireString(args, "name"));
                ShowFaceSearchCommand.Execute(null);
                PeopleAlbums.SelectedAlbum = album;
                return Task.FromResult<object?>(new { ok = true, name = album.Name, photos = album.PhotoCount });
            });

        yield return new AgentTool(
            "rename_person",
            "给人物相册改名（例如把“人物 12”改成“新娘”）。只改人物资料，不动照片文件；执行前会请用户确认。",
            Schema(("name", "string", "现在的名字", true), ("new_name", "string", "新名字", true)),
            true,
            async (args, _) =>
            {
                var album = FindPerson(RequireString(args, "name"));
                var newName = RequireString(args, "new_name");
                if (newName.Length > 40) throw new AgentToolException("名字太长了（最多 40 字）。");
                var oldName = album.Name;
                album.Name = newName;
                await album.SaveNameCommand.ExecuteAsync(null).ConfigureAwait(true);
                return new { ok = true, renamed = $"{oldName} → {newName}" };
            },
            args => $"把人物「{OptionalString(args, "name")}」改名为「{OptionalString(args, "new_name")}」。\n只改人物资料，不动照片文件。");

        yield return new AgentTool(
            "set_shoot_day_remark",
            "修改日期文件夹的备注，例如把“11.04”改成“11.04_婚礼”。会重命名磁盘上的文件夹（“修后”等子文件夹随之一起移动，不改动任何照片），执行前会请用户确认。",
            Schema(("date", "string", "拍摄日期", true), ("remark", "string", "新备注，留空表示去掉备注", true)),
            true,
            async (args, _) =>
            {
                var node = FindDateNode(RequireString(args, "date"));
                var remark = OptionalString(args, "remark") ?? string.Empty;
                var result = LibraryDateFolderService.RenameRemark(node.FullPath, remark);
                if (result.Status is not (DateFolderRenameStatus.Success or DateFolderRenameStatus.NoChange))
                    throw new AgentToolException($"没能改名：{result.Status} {result.ErrorMessage}");
                if (RefreshLibraryCommand.CanExecute(null)) await RefreshLibraryCommand.ExecuteAsync(null).ConfigureAwait(true);
                return new { ok = true, folder = Path.GetFileName(result.EffectivePath) };
            },
            args =>
            {
                var date = OptionalString(args, "date") ?? "?";
                var folder = TryFindDateNode(date) is { } node ? Path.GetFileName(node.FullPath) : date;
                var remark = OptionalString(args, "remark");
                return $"重命名日期文件夹「{folder}」→ 备注「{(string.IsNullOrEmpty(remark) ? "（无）" : remark)}」。\n文件夹会在磁盘上改名，里面的照片不会被修改。";
            });

        yield return new AgentTool(
            "prepare_import",
            "准备导入：分析一个或多个来源文件夹 / 存储卡（只读，不复制任何文件），然后打开导入页让用户检查并确认。真正的复制必须由用户在导入页点击确认。",
            Schema(("source_folders", "array", "来源文件夹完整路径，例如 H:\\DCIM", true)),
            false,
            async (args, _) =>
            {
                var folders = OptionalStringArray(args, "source_folders").Where(Directory.Exists).ToArray();
                if (folders.Length == 0) throw new AgentToolException("这些来源文件夹都不存在或无法访问。");
                if (IsBusy) throw new AgentToolException("软件正在执行其它任务，稍后再试。");
                ShowImportCommand.Execute(null);
                await PrepareMultipleImportSourcesAsync(folders).ConfigureAwait(true);
                return new { ok = true, analyzed = ImportItems.Count, summary = ImportReportHeadline, next = "请用户在导入页确认日期后开始导入" };
            });

        yield return new AgentTool(
            "get_import_status",
            "查看导入状态：是否正在导入、进度、最近一次导入的结果、是否有可以继续的未完成导入。",
            Schema(),
            false,
            (_, _) => Task.FromResult<object?>(new
            {
                running = IsImportRunning,
                progress = IsBusy ? $"{ProgressLabel}（{ProgressValue:0}%）" : null,
                last_result = ImportReportHeadline,
                success = ImportSuccessCount,
                skipped = ImportSkippedCount,
                failed = ImportFailedCount,
                pending_resume = HasPendingImportResume ? PendingImportResumeSummary : null,
            }));

        yield return new AgentTool(
            "list_devices",
            "列出当前连接的存储卡、U 盘、相机与照片库设备。",
            Schema(),
            false,
            (_, _) => Task.FromResult<object?>(new
            {
                groups = DeviceGroups.Select(group => new
                {
                    group = group.Name,
                    devices = group.Devices.Select(device => new { name = device.Name, path = device.Path, connected = device.IsConnected, detail = device.Detail }).ToArray(),
                }).ToArray(),
            }));

        yield return new AgentTool(
            "add_schedule",
            "在主页“日程安排”里加一条拍摄计划，例如“11.12 漫展外拍”“明天 棚拍”。",
            Schema(("text", "string", "日期 + 内容，日期可省略（默认今天）", true)),
            false,
            (args, _) =>
            {
                NewScheduleText = RequireString(args, "text");
                AddScheduleCommand.Execute(null);
                return Task.FromResult<object?>(new { ok = true, summary = ScheduleSummary });
            });
    }

    private IEnumerable<LibraryDateNode> DatedNodesNewestFirst() => FlattenDateNodes(LibraryDates)
        .Where(node => node.Date is not null)
        .OrderByDescending(node => node.Date!.Value.Year)
        .ThenByDescending(node => node.Date!.Value.Month)
        .ThenByDescending(node => node.Date!.Value.Day);

    /// <summary>按已读取的文件清单统计某天的原片 / 修后数量（目录树统计还没算出来时也准确）。</summary>
    private (int Originals, int Retouched) CountDayFiles(LibraryDateNode node)
    {
        var retouchedFolder = ConcreteCategoryFolders.TryGetValue(Core.Imports.MediaCategory.Edited, out var name) ? name : "修后";
        var prefix = Path.TrimEndingDirectorySeparator(node.FullPath) + Path.DirectorySeparatorChar;
        var retouchedPrefix = prefix + retouchedFolder + Path.DirectorySeparatorChar;
        int originals = 0, retouched = 0;
        foreach (var file in PreviewFiles)
        {
            if (!file.FullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (file.FullPath.StartsWith(retouchedPrefix, StringComparison.OrdinalIgnoreCase)) retouched++;
            else originals++;
        }
        return originals + retouched == 0 ? (Math.Max(0, node.TotalFiles - node.RetouchedFiles), node.RetouchedFiles) : (originals, retouched);
    }

    private static string FormatAgentDate(Core.Imports.LibraryDate date) => $"{date.Year:0000}-{date.Month:00}-{date.Day:00}";

    private LibraryDateNode? TryFindDateNode(string text)
    {
        var match = DateArgument.Match(text);
        if (!match.Success) return null;
        var month = int.Parse(match.Groups["m"].Value);
        var day = int.Parse(match.Groups["d"].Value);
        int? year = match.Groups["y"].Success ? int.Parse(match.Groups["y"].Value) : null;
        return DatedNodesNewestFirst().FirstOrDefault(node =>
            node.Date!.Value.Month == month && node.Date.Value.Day == day && (year is null || node.Date.Value.Year == year));
    }

    private LibraryDateNode FindDateNode(string text) =>
        TryFindDateNode(text) ?? throw new AgentToolException($"照片库里没有 {text} 这一天的文件夹。可以先用 list_shoot_days 看看有哪些日期。");

    private PersonAlbumItemViewModel FindPerson(string name)
    {
        var exact = PeopleAlbums.Albums.FirstOrDefault(album => string.Equals(album.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (exact is not null) return exact;
        var partial = PeopleAlbums.Albums.Where(album => album.Name.Contains(name, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        return partial.Length == 1 ? partial[0]
            : partial.Length == 0 ? throw new AgentToolException($"没有找到叫「{name}」的人物。")
            : throw new AgentToolException($"有 {partial.Length} 个人物名字包含「{name}」：{string.Join("、", partial.Take(8).Select(album => album.Name))}，请说得更具体。");
    }
}
