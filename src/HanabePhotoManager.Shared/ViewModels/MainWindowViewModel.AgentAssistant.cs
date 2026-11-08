using System.Collections.ObjectModel;
using System.Reflection;
using System.Security.Cryptography;
using HanabePhotoManager.App.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Agents;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App.ViewModels;

/// <summary>搜索栏 AI 对话里的一条消息。</summary>
public sealed partial class AgentMessageViewModel : ObservableObject
{
    public AgentMessageViewModel(bool isUser, string text, IReadOnlyList<string>? actions = null)
    {
        IsUser = isUser;
        _text = text;
        Actions = actions ?? [];
    }

    public bool IsUser { get; }
    [ObservableProperty] private string _text;
    public IReadOnlyList<string> Actions { get; }
    public bool HasActions => Actions.Count > 0;
}

/// <summary>搜索栏 AI 助手 + 本机 MCP 接口的状态与设置。</summary>
public sealed partial class MainWindowViewModel
{
    private OpenAiCompatibleAgent? _agent;
    private CancellationTokenSource? _agentCancellation;
    private LocalMcpServer? _mcpServer;
    private string _agentBaseUrl = string.Empty;
    private string _agentModelName = string.Empty;
    private string _agentApiKey = string.Empty;
    private bool _agentServerEnabled;
    private int _agentServerPort = 47820;
    private string _agentServerToken = string.Empty;
    private string _agentServerStatus = "未开启";
    private bool _isAgentBusy;
    private bool _isAgentPanelOpen;

    public ObservableCollection<AgentMessageViewModel> AgentConversation { get; } = [];

    public bool IsAgentBusy
    {
        get => _isAgentBusy;
        private set { if (SetProperty(ref _isAgentBusy, value)) OnPropertyChanged(nameof(AgentStatusText)); }
    }

    public bool IsAgentPanelOpen { get => _isAgentPanelOpen; set => SetProperty(ref _isAgentPanelOpen, value); }

    public bool IsAgentModelConfigured => CurrentAgentModel.IsConfigured;

    public string AgentStatusText => IsAgentBusy ? "思考中…" : IsAgentModelConfigured ? $"{AgentModelName} · 可以连续追问" : "还没有设置模型";

    public string AgentBaseUrl { get => _agentBaseUrl; set { if (SetProperty(ref _agentBaseUrl, value?.Trim() ?? string.Empty)) AgentSettingsChanged(); } }
    public string AgentModelName { get => _agentModelName; set { if (SetProperty(ref _agentModelName, value?.Trim() ?? string.Empty)) AgentSettingsChanged(); } }
    public string AgentApiKey { get => _agentApiKey; set { if (SetProperty(ref _agentApiKey, value?.Trim() ?? string.Empty)) AgentSettingsChanged(); } }

    private AgentModelSettings CurrentAgentModel => new(AgentBaseUrl, AgentModelName, AgentApiKey);

    private void AgentSettingsChanged()
    {
        OnPropertyChanged(nameof(IsAgentModelConfigured));
        OnPropertyChanged(nameof(AgentStatusText));
        _ = SaveSettingsAsync();
    }

    // ---------- 搜索栏 AI ----------

    /// <summary>把搜索栏里的话交给 AI：AI 会调用工具（搜索、打开日期、看人物……）并简短回答。</summary>
    public async Task AskAgentAsync(string text)
    {
        text = text?.Trim() ?? string.Empty;
        if (text.Length == 0 || IsAgentBusy) return;
        IsAgentPanelOpen = true;
        AgentConversation.Add(new AgentMessageViewModel(true, text));
        if (!IsAgentModelConfigured)
        {
            AgentConversation.Add(new AgentMessageViewModel(false,
                "还没有设置模型接口。点右上角「设置」，填写 OpenAI 兼容的接口地址、模型名和 API Key（本机 Ollama 不需要 Key）。"));
            return;
        }

        _agent ??= new OpenAiCompatibleAgent(AgentToolbox);
        _agentCancellation?.Dispose();
        _agentCancellation = new CancellationTokenSource();
        var pending = new AgentMessageViewModel(false, "思考中…");
        AgentConversation.Add(pending);
        IsAgentBusy = true;
        try
        {
            var reply = await _agent.AskAsync(CurrentAgentModel, text, AgentInstructions.ForSearchBar, _agentCancellation.Token).ConfigureAwait(true);
            AgentConversation.Remove(pending);
            AgentConversation.Add(new AgentMessageViewModel(false, reply.Text, reply.Actions.Select(DescribeAgentAction).ToArray()));
        }
        catch (OperationCanceledException)
        {
            pending.Text = "已停止。";
        }
        catch (Exception ex)
        {
            pending.Text = "出错了：" + ex.Message;
        }
        finally
        {
            IsAgentBusy = false;
        }
    }

    [RelayCommand]
    private void CancelAgent() => _agentCancellation?.Cancel();

    [RelayCommand]
    private void NewAgentConversation()
    {
        _agentCancellation?.Cancel();
        _agent?.Reset();
        AgentConversation.Clear();
    }

    private static string DescribeAgentAction(AgentToolInvocation invocation)
    {
        var label = invocation.Tool switch
        {
            "search_photos" => "搜索照片",
            "open_page" => "切换页面",
            "open_shoot_day" => "打开拍摄日",
            "list_shoot_days" => "查看拍摄日",
            "get_library_overview" => "查看照片库概况",
            "list_people" => "查看人物",
            "open_person" => "打开人物",
            "rename_person" => "人物改名",
            "set_shoot_day_remark" => "修改文件夹备注",
            "prepare_import" => "准备导入",
            "get_import_status" => "查看导入状态",
            "list_devices" => "查看设备",
            "add_schedule" => "添加日程",
            _ => invocation.Tool,
        };
        return invocation.Result.IsError ? $"{label}（未完成）" : label;
    }

    // ---------- 本机 MCP 接口 ----------

    public bool AgentServerEnabled
    {
        get => _agentServerEnabled;
        set
        {
            if (!SetProperty(ref _agentServerEnabled, value)) return;
            ApplyAgentServer();
            _ = SaveSettingsAsync();
        }
    }

    public int AgentServerPort
    {
        get => _agentServerPort;
        set
        {
            if (!SetProperty(ref _agentServerPort, Math.Clamp(value, 1024, 65535))) return;
            OnPropertyChanged(nameof(AgentServerEndpoint));
            OnPropertyChanged(nameof(AgentClientSnippets));
            if (AgentServerEnabled) ApplyAgentServer();
            _ = SaveSettingsAsync();
        }
    }

    public string AgentServerToken => _agentServerToken;
    public string AgentServerEndpoint => $"http://localhost:{AgentServerPort}/mcp";
    public string AgentServerStatus { get => _agentServerStatus; private set => SetProperty(ref _agentServerStatus, value); }

    /// <summary>给常见 agent 客户端的接入写法（复制即可用）。</summary>
    public string AgentClientSnippets =>
        $"Claude Code：\n  claude mcp add --transport http hanabe {AgentServerEndpoint} --header \"Authorization: Bearer {AgentServerToken}\"\n\n" +
        $"Codex（~/.codex/config.toml）：\n  [mcp_servers.hanabe]\n  url = \"{AgentServerEndpoint}\"\n  http_headers = {{ Authorization = \"Bearer {AgentServerToken}\" }}\n\n" +
        $"只支持本地命令的客户端（如 Claude Desktop）：\n  {{ \"mcpServers\": {{ \"hanabe\": {{ \"command\": \"npx\", \"args\": [\"mcp-remote\", \"{AgentServerEndpoint}\", \"--header\", \"Authorization: Bearer {AgentServerToken}\"] }} }} }}";

    [RelayCommand]
    private void RegenerateAgentServerToken()
    {
        _agentServerToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        OnPropertyChanged(nameof(AgentServerToken));
        OnPropertyChanged(nameof(AgentClientSnippets));
        if (AgentServerEnabled) ApplyAgentServer();
        _ = SaveSettingsAsync();
    }

    private void ApplyAgentServer()
    {
        _mcpServer?.Dispose();
        _mcpServer = null;
        if (!AgentServerEnabled)
        {
            AgentServerStatus = "未开启";
            return;
        }

        if (string.IsNullOrEmpty(_agentServerToken))
        {
            _agentServerToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            OnPropertyChanged(nameof(AgentServerToken));
            OnPropertyChanged(nameof(AgentClientSnippets));
            _ = SaveSettingsAsync();
        }
        try
        {
            var version = PlatformServices.AppVersion;
            _mcpServer = new LocalMcpServer(AgentToolbox, AgentServerPort, _agentServerToken, version);
            _mcpServer.Start();
            AgentServerStatus = $"运行中 · {AgentServerEndpoint}";
        }
        catch (Exception ex)
        {
            _mcpServer?.Dispose();
            _mcpServer = null;
            AgentServerStatus = $"启动失败：{ex.Message}（端口 {AgentServerPort} 可能被占用，换一个端口试试）";
        }
    }

    public void StopAgentServer()
    {
        _mcpServer?.Dispose();
        _mcpServer = null;
    }

    private void RestoreAgentSettings(AppSettings settings)
    {
        _agentBaseUrl = settings.AgentModelBaseUrl ?? string.Empty;
        _agentModelName = settings.AgentModelName ?? string.Empty;
        _agentApiKey = PlatformServices.Secrets.Unprotect(settings.AgentApiKeyProtected, "api-key");
        _agentServerPort = settings.AgentServerPort is >= 1024 and <= 65535 ? settings.AgentServerPort : 47820;
        _agentServerToken = PlatformServices.Secrets.Unprotect(settings.AgentServerTokenProtected, "server-token");
        _agentServerEnabled = settings.AgentServerEnabled;
        foreach (var name in new[] { nameof(AgentBaseUrl), nameof(AgentModelName), nameof(AgentApiKey), nameof(AgentServerPort),
                     nameof(AgentServerToken), nameof(AgentServerEnabled), nameof(AgentServerEndpoint), nameof(AgentClientSnippets),
                     nameof(IsAgentModelConfigured), nameof(AgentStatusText) })
            OnPropertyChanged(name);
        if (_agentServerEnabled) ApplyAgentServer();
    }

    private void StoreAgentSettings(AppSettings settings)
    {
        settings.AgentModelBaseUrl = AgentBaseUrl;
        settings.AgentModelName = AgentModelName;
        settings.AgentApiKeyProtected = PlatformServices.Secrets.Protect(AgentApiKey, "api-key");
        settings.AgentServerEnabled = AgentServerEnabled;
        settings.AgentServerPort = AgentServerPort;
        settings.AgentServerTokenProtected = PlatformServices.Secrets.Protect(_agentServerToken, "server-token");
    }

    /// <summary>工具在界面线程执行；需要确认的操作弹出确认框。窗口可替换 Confirm 以使用自定义样式。</summary>
    private void ConfigureAgentToolbox(AgentToolbox toolbox)
    {
        toolbox.RunOnUi = work => PlatformServices.Dispatcher.InvokeAsync(work).Unwrap();
        toolbox.Confirm = async (description, caller) =>
        {
            var answer = false;
            await PlatformServices.Dispatcher.InvokeAsync(async () =>
            {
                answer = await PlatformServices.Dialogs.ConfirmAsync("Hanabe · AI 操作确认", $"{caller} 请求执行：\n\n{description}\n\n允许吗？");
                StatusMessage = answer ? $"已允许 {caller} 的操作。" : $"已拒绝 {caller} 的操作。";
            });
            return answer;
        };
    }
}
