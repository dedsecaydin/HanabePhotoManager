using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using HanabePhotoManager.App.Agents;
using HanabePhotoManager.App.ViewModels;
using Xunit;

namespace HanabePhotoManager.App.Tests;

/// <summary>AI / Agent 接口：工具确认、MCP 协议（含真实 HTTP、令牌、跨站拦截）、模型工具调用循环、密钥加密。</summary>
public sealed class AgentInterfaceTests
{
    private static AgentToolbox CreateToolbox(List<string> executed) => new(
    [
        new AgentTool("echo", "回显", AgentToolbox.Schema(("text", "string", "内容", true)), false,
            (args, _) => { executed.Add("echo"); return Task.FromResult<object?>(new { text = AgentToolbox.RequireString(args, "text") }); }),
        new AgentTool("rename", "改名", AgentToolbox.Schema(("name", "string", "名字", true)), true,
            (_, _) => { executed.Add("rename"); return Task.FromResult<object?>(new { ok = true }); },
            args => $"改名为 {AgentToolbox.OptionalString(args, "name")}"),
    ]);

    [Fact]
    public async Task Toolbox_ConfirmationRequiredTools_DoNotRunWhenDenied()
    {
        var executed = new List<string>();
        var toolbox = CreateToolbox(executed);
        string? asked = null;
        toolbox.Confirm = (description, caller) => { asked = $"{caller}:{description}"; return Task.FromResult(false); };

        var denied = await toolbox.InvokeAsync("rename", new JsonObject { ["name"] = "新娘" }, "测试", default);

        denied.IsError.Should().BeTrue();
        denied.Text.Should().Contain("拒绝");
        executed.Should().BeEmpty();
        asked.Should().Be("测试:改名为 新娘");

        toolbox.Confirm = (_, _) => Task.FromResult(true);
        (await toolbox.InvokeAsync("rename", new JsonObject { ["name"] = "新娘" }, "测试", default)).IsError.Should().BeFalse();
        executed.Should().Equal("rename");
    }

    [Fact]
    public async Task Toolbox_MissingArgumentsAndUnknownTools_BecomeErrorResults()
    {
        var toolbox = CreateToolbox([]);
        (await toolbox.InvokeAsync("echo", [], "测试", default)).Text.Should().Contain("缺少参数 text");
        (await toolbox.InvokeAsync("nope", null, "测试", default)).IsError.Should().BeTrue();
    }

    [Fact]
    public async Task Mcp_InitializeListAndCall_FollowJsonRpc()
    {
        var server = new LocalMcpServer(CreateToolbox([]), 0, "secret", "1.0.0");

        var init = await server.HandleMessageAsync(Parse("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18"}}"""), default);
        init!["result"]!["protocolVersion"]!.GetValue<string>().Should().Be("2025-06-18");
        init["result"]!["capabilities"]!["tools"].Should().NotBeNull();

        (await server.HandleMessageAsync(Parse("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""), default)).Should().BeNull();

        var list = await server.HandleMessageAsync(Parse("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}"""), default);
        var tools = list!["result"]!["tools"]!.AsArray();
        tools.Select(tool => tool!["name"]!.GetValue<string>()).Should().Equal("echo", "rename");
        tools[1]!["description"]!.GetValue<string>().Should().Contain("需要用户在软件里确认");

        var call = await server.HandleMessageAsync(Parse("""{"jsonrpc":"2.0","id":"x","method":"tools/call","params":{"name":"echo","arguments":{"text":"你好"}}}"""), default);
        call!["id"]!.GetValue<string>().Should().Be("x");
        call["result"]!["isError"]!.GetValue<bool>().Should().BeFalse();
        call["result"]!["content"]![0]!["text"]!.GetValue<string>().Should().Contain("你好");

        var unknown = await server.HandleMessageAsync(Parse("""{"jsonrpc":"2.0","id":3,"method":"resources/list"}"""), default);
        unknown!["error"]!["code"]!.GetValue<int>().Should().Be(-32601);
    }

    [Fact]
    public async Task Mcp_OverHttp_RequiresTokenAndRejectsCrossSiteOrigins()
    {
        var port = FreePort();
        using var server = new LocalMcpServer(CreateToolbox([]), port, "secret-token", "1.0.0");
        server.Start();
        using var http = new HttpClient();
        var url = $"http://localhost:{port}/mcp";
        const string body = """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""";

        var anonymous = await http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var wrong = Request(url, body, "nope");
        (await http.SendAsync(wrong)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var crossSite = Request(url, body, "secret-token");
        crossSite.Headers.Add("Origin", "https://evil.example");
        (await http.SendAsync(crossSite)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var ok = Request(url, body, "secret-token");
        var response = await http.SendAsync(ok);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"echo\"");

        using var notification = Request(url, """{"jsonrpc":"2.0","method":"notifications/initialized"}""", "secret-token");
        (await http.SendAsync(notification)).StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Agent_ExecutesToolCallsUntilModelAnswers()
    {
        var executed = new List<string>();
        var handler = new ScriptedModel(
            """{"choices":[{"message":{"role":"assistant","content":null,"tool_calls":[{"id":"c1","type":"function","function":{"name":"echo","arguments":"{\"text\":\"新娘\"}"}}]}}]}""",
            """{"choices":[{"message":{"role":"assistant","content":"已经帮你找好了。"}}]}""");
        var agent = new OpenAiCompatibleAgent(CreateToolbox(executed), new HttpClient(handler));

        var reply = await agent.AskAsync(new AgentModelSettings("https://example.test/v1", "m", "k"), "找新娘", "系统", default);

        reply.Text.Should().Be("已经帮你找好了。");
        reply.Actions.Should().ContainSingle(action => action.Tool == "echo" && !action.Result.IsError);
        executed.Should().Equal("echo");
        handler.Requests.Should().HaveCount(2);
        handler.Urls.Should().AllBe("https://example.test/v1/chat/completions");
        handler.Requests[0].Should().Contain("\"tools\"").And.Contain("\"name\":\"echo\"");
        handler.Requests[1].Should().Contain("\"role\":\"tool\"").And.Contain("\"tool_call_id\":\"c1\"");
        handler.Authorization.Should().Be("Bearer k");
    }

    [Fact]
    public async Task Agent_ExplainsMissingConfiguration()
    {
        var agent = new OpenAiCompatibleAgent(CreateToolbox([]), new HttpClient(new ScriptedModel()));
        var act = () => agent.AskAsync(new AgentModelSettings("", "", ""), "hi", "s", default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*设置模型接口*");
        OpenAiCompatibleAgent.BuildUrl("https://api.deepseek.com/v1/").Should().Be("https://api.deepseek.com/v1/chat/completions");
        OpenAiCompatibleAgent.BuildUrl("http://localhost:11434/v1/chat/completions").Should().Be("http://localhost:11434/v1/chat/completions");
    }

    [Fact]
    public void SecretProtector_RoundTripsAndDoesNotStorePlainText()
    {
        var protectedText = SecretProtector.Protect("sk-test-123");
        protectedText.Should().NotContain("sk-test");
        SecretProtector.Unprotect(protectedText).Should().Be("sk-test-123");
        SecretProtector.Unprotect("not-base64!!").Should().BeEmpty();
        SecretProtector.Protect("").Should().BeEmpty();
    }

    [Fact]
    public async Task ViewModelTools_ExposeLibraryActionsAndValidateDates()
    {
        var viewModel = new MainWindowViewModel();
        var names = viewModel.AgentToolbox.Tools.Select(tool => tool.Name).ToArray();
        names.Should().Contain(["get_library_overview", "search_photos", "open_page", "open_shoot_day", "list_people", "rename_person", "set_shoot_day_remark", "prepare_import"]);
        viewModel.AgentToolbox.Tools.Where(tool => tool.RequiresConfirmation).Select(tool => tool.Name)
            .Should().BeEquivalentTo("rename_person", "set_shoot_day_remark");

        (await viewModel.AgentToolbox.InvokeAsync("open_page", new JsonObject { ["page"] = "people" }, "测试", default)).IsError.Should().BeFalse();
        viewModel.CurrentPage.Should().Be("FaceSearch");

        var missingDay = await viewModel.AgentToolbox.InvokeAsync("open_shoot_day", new JsonObject { ["date"] = "11.04" }, "测试", default);
        missingDay.IsError.Should().BeTrue();
        missingDay.Text.Should().Contain("list_shoot_days");
    }

    private static JsonObject Parse(string json) => (JsonObject)JsonNode.Parse(json)!;

    private static HttpRequestMessage Request(string url, string body, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Authorization", "Bearer " + token);
        return request;
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class ScriptedModel(params string[] responses) : HttpMessageHandler
    {
        private int _index;
        public List<string> Requests { get; } = [];
        public List<string> Urls { get; } = [];
        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            Urls.Add(request.RequestUri!.ToString());
            Authorization = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses[Math.Min(_index++, responses.Length - 1)], Encoding.UTF8, "application/json"),
            };
        }
    }
}
