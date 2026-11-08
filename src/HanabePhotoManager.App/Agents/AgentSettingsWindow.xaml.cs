using System.Windows;
using HanabePhotoManager.App.ViewModels;

namespace HanabePhotoManager.App.Agents;

/// <summary>“AI 与 Agent 接口”设置：搜索栏模型接口 + 本机 MCP 接口。</summary>
public partial class AgentSettingsWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public AgentSettingsWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        ApiKeyBox.Password = viewModel.AgentApiKey;
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e) => _viewModel.AgentApiKey = ApiKeyBox.Password;

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string preset }) return;
        var parts = preset.Split('|');
        _viewModel.AgentBaseUrl = parts[0];
        if (parts.Length > 1) _viewModel.AgentModelName = parts[1];
    }

    private void CopyToken_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_viewModel.AgentServerToken)) _viewModel.RegenerateAgentServerTokenCommand.Execute(null);
        TryCopy(_viewModel.AgentServerToken);
    }

    private void CopySnippets_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_viewModel.AgentServerToken)) _viewModel.RegenerateAgentServerTokenCommand.Execute(null);
        TryCopy(_viewModel.AgentClientSnippets);
    }

    private static void TryCopy(string text)
    {
        try { System.Windows.Clipboard.SetText(text); }
        catch (System.Runtime.InteropServices.ExternalException) { /* 剪贴板被占用时忽略，用户可手动选中复制 */ }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
