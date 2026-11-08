using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HanabePhotoManager.App.Agents;

/// <summary>
/// 本机 MCP 服务器（Streamable HTTP 传输，JSON-RPC 2.0），让 Claude Code、Codex、Cursor 等外部 agent 调用软件功能。
/// 安全边界：
/// · 只监听 localhost（其它电脑访问不到）；
/// · 每个请求必须带 Authorization: Bearer &lt;令牌&gt;；
/// · 浏览器发来的跨站请求（Origin 不是本机）一律拒绝，防止网页借你的浏览器偷偷调用；
/// · 需要确认的工具照样会在软件里弹窗，由你决定。
/// </summary>
public sealed class LocalMcpServer : IDisposable
{
    public const string ProtocolVersion = "2025-03-26";
    private readonly AgentToolbox _toolbox;
    private readonly string _token;
    private readonly string _version;
    private HttpListener? _listener;
    private CancellationTokenSource? _cancellation;

    public LocalMcpServer(AgentToolbox toolbox, int port, string token, string version)
    {
        _toolbox = toolbox;
        Port = port;
        _token = token;
        _version = version;
    }

    public int Port { get; }
    public string Endpoint => $"http://localhost:{Port}/mcp";
    public bool IsRunning => _listener?.IsListening == true;

    /// <summary>启动监听；端口被占用等失败时抛出，调用方显示给用户。</summary>
    public void Start()
    {
        if (IsRunning) return;
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{Port}/");
        listener.Start();
        _listener = listener;
        _cancellation = new CancellationTokenSource();
        _ = Task.Run(() => AcceptLoopAsync(listener, _cancellation.Token));
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        try { _listener?.Stop(); _listener?.Close(); } catch (ObjectDisposedException) { }
        _listener = null;
    }

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync().ConfigureAwait(false); }
            catch (HttpListenerException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (InvalidOperationException) { return; }
            _ = Task.Run(() => HandleAsync(context, token), token);
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken token)
    {
        var request = context.Request;
        var response = context.Response;
        try
        {
            var origin = request.Headers["Origin"];
            if (!string.IsNullOrEmpty(origin) && !IsLocalOrigin(origin))
            {
                await WriteAsync(response, 403, new JsonObject { ["error"] = "只接受本机发起的请求" }).ConfigureAwait(false);
                return;
            }

            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;
            if (path == "/health")
            {
                await WriteAsync(response, 200, new JsonObject { ["ok"] = true, ["name"] = "hanabe-photo-manager", ["version"] = _version }).ConfigureAwait(false);
                return;
            }

            if (!IsAuthorized(request.Headers["Authorization"]))
            {
                response.AddHeader("WWW-Authenticate", "Bearer");
                await WriteAsync(response, 401, new JsonObject { ["error"] = "缺少或令牌不正确：请在 Hanabe 的“AI 与 Agent 接口”设置里复制令牌" }).ConfigureAwait(false);
                return;
            }

            if (path != "/mcp")
            {
                await WriteAsync(response, 404, new JsonObject { ["error"] = "未知地址，请使用 /mcp" }).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "DELETE") { response.StatusCode = 200; response.Close(); return; }
            if (request.HttpMethod != "POST")
            {
                // 本服务器不主动推送消息，不提供 SSE 流。
                response.StatusCode = 405;
                response.AddHeader("Allow", "POST");
                response.Close();
                return;
            }

            string body;
            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                body = await reader.ReadToEndAsync(token).ConfigureAwait(false);

            JsonNode? parsed;
            try { parsed = JsonNode.Parse(body); }
            catch (JsonException)
            {
                await WriteAsync(response, 400, Error(null, -32700, "JSON 解析失败")).ConfigureAwait(false);
                return;
            }

            if (parsed is JsonArray batch)
            {
                var results = new JsonArray();
                foreach (var item in batch)
                    if (item is JsonObject message && await HandleMessageAsync(message, token).ConfigureAwait(false) is { } reply)
                        results.Add(reply);
                if (results.Count == 0) { response.StatusCode = 202; response.Close(); }
                else await WriteAsync(response, 200, results).ConfigureAwait(false);
                return;
            }

            if (parsed is not JsonObject single)
            {
                await WriteAsync(response, 400, Error(null, -32600, "无效请求")).ConfigureAwait(false);
                return;
            }

            var answer = await HandleMessageAsync(single, token).ConfigureAwait(false);
            if (answer is null) { response.StatusCode = 202; response.Close(); }
            else await WriteAsync(response, 200, answer).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            try { await WriteAsync(response, 500, Error(null, -32603, ex.Message)).ConfigureAwait(false); }
            catch { /* 连接已断开 */ }
        }
    }

    /// <summary>处理一条 JSON-RPC 消息；通知（没有 id）返回 null。公开给测试使用。</summary>
    public async Task<JsonObject?> HandleMessageAsync(JsonObject message, CancellationToken token)
    {
        var id = message["id"]?.DeepClone();
        var method = message["method"]?.GetValue<string>() ?? string.Empty;
        if (id is null) return null; // notifications/initialized 等通知不需要回复

        switch (method)
        {
            case "initialize":
                var requested = message["params"]?["protocolVersion"]?.GetValue<string>();
                return Result(id, new JsonObject
                {
                    ["protocolVersion"] = string.IsNullOrWhiteSpace(requested) ? ProtocolVersion : requested,
                    ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
                    ["serverInfo"] = new JsonObject { ["name"] = "hanabe-photo-manager", ["title"] = "Hanabe 照片管理器", ["version"] = _version },
                    ["instructions"] = AgentInstructions.ForExternalAgents,
                });
            case "ping":
                return Result(id, new JsonObject());
            case "tools/list":
                var tools = new JsonArray();
                foreach (var tool in _toolbox.Tools.OrderBy(tool => tool.Name, StringComparer.Ordinal))
                {
                    tools.Add(new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description + (tool.RequiresConfirmation ? "（需要用户在软件里确认）" : string.Empty),
                        ["inputSchema"] = tool.InputSchema.DeepClone(),
                        ["annotations"] = new JsonObject
                        {
                            ["readOnlyHint"] = !tool.RequiresConfirmation && !tool.Name.StartsWith("prepare_", StringComparison.Ordinal) && tool.Name != "add_schedule",
                            ["destructiveHint"] = false,
                        },
                    });
                }
                return Result(id, new JsonObject { ["tools"] = tools });
            case "tools/call":
                var name = message["params"]?["name"]?.GetValue<string>() ?? string.Empty;
                var arguments = message["params"]?["arguments"] as JsonObject;
                var outcome = await _toolbox.InvokeAsync(name, (JsonObject?)arguments?.DeepClone(), "外部 agent", token).ConfigureAwait(false);
                return Result(id, new JsonObject
                {
                    ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = outcome.Text } },
                    ["isError"] = outcome.IsError,
                });
            default:
                return Error(id, -32601, $"不支持的方法：{method}");
        }
    }

    private bool IsAuthorized(string? header)
    {
        if (string.IsNullOrEmpty(_token) || header is null || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        var supplied = Encoding.UTF8.GetBytes(header[7..].Trim());
        var expected = Encoding.UTF8.GetBytes(_token);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    internal static bool IsLocalOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
        (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));

    private static JsonObject Result(JsonNode id, JsonObject result) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };

    private static JsonObject Error(JsonNode? id, int code, string text) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = text } };

    private static async Task WriteAsync(HttpListenerResponse response, int status, JsonNode payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload.ToJsonString(AgentToolbox.JsonOptions));
        response.StatusCode = status;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }
}

/// <summary>给 AI 的使用说明，搜索栏 AI 与外部 agent 共用。</summary>
public static class AgentInstructions
{
    public const string Common =
        "你在操作 Hanabe 照片管理器：一个给摄影师用的本机照片库（婚礼、漫展、外拍），按“月/日期_备注/分类”整理，分类有 RAW生图、JPG生图、修后、视频、action视频、素材。" +
        "“修后”文件夹是已修好的成片，任何时候都不能删除或覆盖。" +
        "需要信息时先调用工具，不要编造路径、数量或人名。会改文件的工具（rename_person、set_shoot_day_remark）会在软件里请用户确认，被拒绝时如实告诉用户。" +
        "导入只能用 prepare_import 准备，真正复制要用户在导入页确认。";

    public const string ForExternalAgents = Common + "回答用户时用中文。";

    public const string ForSearchBar = Common +
        "你是软件顶部搜索栏里的助手：用户的话可能是要找照片、打开某一天、看人物、查空间或导入状态。" +
        "能直接做的就做（例如调用 search_photos 或 open_shoot_day 把结果显示在界面上），然后用一两句中文简短说明做了什么、看到了什么，不要长篇大论。";
}
