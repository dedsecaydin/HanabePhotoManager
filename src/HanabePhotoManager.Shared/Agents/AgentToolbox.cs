using System.Text.Json;
using System.Text.Json.Nodes;

namespace HanabePhotoManager.App.Agents;

/// <summary>一个可供 AI 调用的功能：名字、说明、参数 JSON Schema，以及是否需要用户确认。</summary>
public sealed record AgentTool(
    string Name,
    string Description,
    JsonObject InputSchema,
    bool RequiresConfirmation,
    Func<JsonObject, CancellationToken, Task<object?>> Execute,
    Func<JsonObject, string>? DescribeAction = null);

/// <summary>一次工具调用的结果；Text 是给 AI 读的 JSON 文本。</summary>
public sealed record AgentToolResult(bool IsError, string Text);

/// <summary>工具调用被用户拒绝或参数不合法时抛出，作为错误结果返回给 AI，而不是让程序崩溃。</summary>
public sealed class AgentToolException(string message) : Exception(message);

/// <summary>
/// 统一的工具箱：搜索栏 AI 和外部 agent（MCP）都从这里调用。
/// · 所有工具在界面线程执行（它们会读写界面状态）；
/// · RequiresConfirmation 的工具先调用 <see cref="Confirm"/>，用户拒绝则不执行。
/// </summary>
public sealed class AgentToolbox
{
    private readonly Dictionary<string, AgentTool> _tools;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public AgentToolbox(IEnumerable<AgentTool> tools)
    {
        _tools = tools.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<AgentTool> Tools => _tools.Values;

    /// <summary>确认回调：参数是给用户看的操作说明和调用方（“搜索栏 AI” / “外部 agent”），返回是否允许。</summary>
    public Func<string, string, Task<bool>> Confirm { get; set; } = (_, _) => Task.FromResult(false);

    /// <summary>把任务切到界面线程执行；测试里没有 Dispatcher 时直接执行。</summary>
    public Func<Func<Task<object?>>, Task<object?>> RunOnUi { get; set; } = work => work();

    /// <summary>每次调用后通知（用于搜索栏显示“已打开图库”之类的动作记录）。</summary>
    public event EventHandler<AgentToolInvocation>? Invoked;

    public async Task<AgentToolResult> InvokeAsync(string name, JsonObject? arguments, string caller, CancellationToken cancellationToken)
    {
        arguments ??= [];
        AgentToolResult result;
        if (!_tools.TryGetValue(name, out var tool))
        {
            result = new AgentToolResult(true, Serialize(new { error = $"没有名为 {name} 的工具" }));
        }
        else
        {
            try
            {
                if (tool.RequiresConfirmation)
                {
                    var description = tool.DescribeAction?.Invoke(arguments) ?? $"{tool.Description}\n参数：{arguments.ToJsonString()}";
                    if (!await Confirm(description, caller).ConfigureAwait(true))
                        throw new AgentToolException("用户拒绝了这个操作，没有做任何修改。");
                }

                var value = await RunOnUi(() => tool.Execute(arguments, cancellationToken)).ConfigureAwait(true);
                result = new AgentToolResult(false, Serialize(value ?? new { ok = true }));
            }
            catch (AgentToolException ex) { result = new AgentToolResult(true, Serialize(new { error = ex.Message })); }
            catch (OperationCanceledException) { result = new AgentToolResult(true, Serialize(new { error = "已取消" })); }
            catch (Exception ex) { result = new AgentToolResult(true, Serialize(new { error = "执行失败：" + ex.Message })); }
        }

        Invoked?.Invoke(this, new AgentToolInvocation(name, caller, result));
        return result;
    }

    public static string Serialize(object value) => JsonSerializer.Serialize(value, value.GetType(), JsonOptions);

    // ---------- 参数读取小工具 ----------

    public static string RequireString(JsonObject args, string name)
    {
        var value = OptionalString(args, name);
        if (string.IsNullOrWhiteSpace(value)) throw new AgentToolException($"缺少参数 {name}");
        return value;
    }

    public static string? OptionalString(JsonObject args, string name) =>
        args.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text.Trim()
            : null;

    public static int OptionalInt(JsonObject args, string name, int fallback, int min, int max)
    {
        if (args.TryGetPropertyValue(name, out var node) && node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var number)) return Math.Clamp(number, min, max);
            if (value.TryGetValue<double>(out var real)) return Math.Clamp((int)real, min, max);
            if (value.TryGetValue<string>(out var text) && int.TryParse(text, out number)) return Math.Clamp(number, min, max);
        }
        return fallback;
    }

    public static IReadOnlyList<string> OptionalStringArray(JsonObject args, string name) =>
        args.TryGetPropertyValue(name, out var node) && node is JsonArray array
            ? array.Select(item => item is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : string.Empty)
                .Where(text => text.Length > 0).ToArray()
            : [];

    /// <summary>构造参数 Schema：Schema(("query","string","搜索描述",true), ...)。</summary>
    public static JsonObject Schema(params (string Name, string Type, string Description, bool Required)[] properties)
    {
        var props = new JsonObject();
        var required = new JsonArray();
        foreach (var (propertyName, type, description, isRequired) in properties)
        {
            var property = new JsonObject { ["type"] = type, ["description"] = description };
            if (type == "array") property["items"] = new JsonObject { ["type"] = "string" };
            props[propertyName] = property;
            if (isRequired) required.Add(propertyName);
        }
        return new JsonObject { ["type"] = "object", ["properties"] = props, ["required"] = required };
    }
}

/// <summary>一次工具调用的记录。</summary>
public sealed record AgentToolInvocation(string Tool, string Caller, AgentToolResult Result);
