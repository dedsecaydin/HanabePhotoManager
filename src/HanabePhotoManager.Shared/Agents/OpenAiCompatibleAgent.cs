using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HanabePhotoManager.App.Agents;

/// <summary>模型接口配置：OpenAI 兼容的 /chat/completions（OpenAI、DeepSeek、通义、Claude 兼容接口、本机 Ollama 等）。</summary>
public sealed record AgentModelSettings(string BaseUrl, string Model, string ApiKey)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Model);
}

/// <summary>助手一轮回复：最终文字 + 期间调用过的工具。</summary>
public sealed record AgentReply(string Text, IReadOnlyList<AgentToolInvocation> Actions);

/// <summary>
/// 最小的“工具调用”循环：把对话和工具清单发给模型 → 模型要调用工具就执行并把结果发回 → 直到模型给出文字回答。
/// 每次最多 8 轮，防止模型陷入循环；对话历史保留在实例里，可以连续追问。
/// </summary>
public sealed class OpenAiCompatibleAgent
{
    private const int MaxRounds = 8;
    private const int MaxToolResultChars = 6000;
    private readonly HttpClient _http;
    private readonly AgentToolbox _toolbox;
    private readonly List<JsonObject> _history = [];

    public OpenAiCompatibleAgent(AgentToolbox toolbox, HttpClient? http = null)
    {
        _toolbox = toolbox;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
    }

    public void Reset() => _history.Clear();

    public async Task<AgentReply> AskAsync(AgentModelSettings settings, string userText, string systemPrompt, CancellationToken token)
    {
        if (!settings.IsConfigured) throw new InvalidOperationException("还没有设置模型接口：点搜索栏旁的 ✦ → 设置，填写接口地址和模型名。");
        if (_history.Count == 0) _history.Add(new JsonObject { ["role"] = "system", ["content"] = systemPrompt });
        _history.Add(new JsonObject { ["role"] = "user", ["content"] = userText });

        var actions = new List<AgentToolInvocation>();
        for (var round = 0; round < MaxRounds; round++)
        {
            var message = await SendAsync(settings, token).ConfigureAwait(true);
            _history.Add(message);
            var calls = message["tool_calls"] as JsonArray;
            if (calls is null || calls.Count == 0)
                return new AgentReply(message["content"]?.GetValue<string>()?.Trim() ?? "（模型没有返回文字）", actions);

            foreach (var call in calls.OfType<JsonObject>())
            {
                var callId = call["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N");
                var name = call["function"]?["name"]?.GetValue<string>() ?? string.Empty;
                JsonObject? arguments = null;
                try { arguments = JsonNode.Parse(call["function"]?["arguments"]?.GetValue<string>() ?? "{}") as JsonObject; }
                catch (JsonException) { }
                var result = await _toolbox.InvokeAsync(name, arguments, "搜索栏 AI", token).ConfigureAwait(true);
                actions.Add(new AgentToolInvocation(name, "搜索栏 AI", result));
                var text = result.Text.Length > MaxToolResultChars ? result.Text[..MaxToolResultChars] + "…（已截断）" : result.Text;
                _history.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = callId, ["content"] = text });
            }
        }

        return new AgentReply("这个请求步骤太多，我先停在这里。可以把要求说得更具体一些再试。", actions);
    }

    private async Task<JsonObject> SendAsync(AgentModelSettings settings, CancellationToken token)
    {
        var tools = new JsonArray();
        foreach (var tool in _toolbox.Tools)
        {
            tools.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = tool.InputSchema.DeepClone(),
                },
            });
        }

        var body = new JsonObject
        {
            ["model"] = settings.Model.Trim(),
            ["messages"] = new JsonArray(_history.Select(item => (JsonNode)item.DeepClone()).ToArray()),
            ["tools"] = tools,
            ["temperature"] = 0.2,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(settings.BaseUrl))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey.Trim());

        using var response = await _http.SendAsync(request, token).ConfigureAwait(true);
        var text = await response.Content.ReadAsStringAsync(token).ConfigureAwait(true);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"模型接口返回 {(int)response.StatusCode}：{Shorten(ExtractError(text))}");

        var message = JsonNode.Parse(text)?["choices"]?[0]?["message"] as JsonObject
            ?? throw new InvalidOperationException("模型接口的返回格式不对（找不到 choices[0].message），请确认它兼容 OpenAI 的 /chat/completions。");
        var clean = new JsonObject { ["role"] = "assistant", ["content"] = message["content"]?.DeepClone() };
        if (message["tool_calls"] is JsonArray calls && calls.Count > 0) clean["tool_calls"] = calls.DeepClone();
        return clean;
    }

    /// <summary>“https://api.deepseek.com/v1” 或已带 /chat/completions 的完整地址都可以。</summary>
    public static string BuildUrl(string baseUrl)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        return trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + "/chat/completions";
    }

    private static string ExtractError(string body)
    {
        try { return JsonNode.Parse(body)?["error"]?["message"]?.GetValue<string>() ?? body; }
        catch (JsonException) { return body; }
    }

    private static string Shorten(string text) => text.Length > 240 ? text[..240] + "…" : text;
}
