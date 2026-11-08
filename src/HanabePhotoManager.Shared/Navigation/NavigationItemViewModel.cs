using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HanabePhotoManager.App.Navigation;

/// <summary>一级导航项的稳定键、标题、图标、可用状态，以及是否显示在左侧功能栏。</summary>
public sealed class NavigationItemViewModel : ObservableObject
{
    private bool _isVisible = true;

    public NavigationItemViewModel(
        string key,
        string label,
        string iconResourceKey,
        IRelayCommand command,
        int order)
    {
        Key = key;
        Label = label;
        IconResourceKey = iconResourceKey;
        Command = command;
        Order = order;
    }

    public string Key { get; }

    public string Label { get; }

    public string IconResourceKey { get; }

    public IRelayCommand Command { get; }

    public int Order { get; internal set; }

    /// <summary>用户可在侧栏右键菜单里隐藏不常用的功能页；隐藏后顺序保留，重新显示时回到原位置。</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    /// <summary>主页是所有功能的入口，不允许从侧栏隐藏。</summary>
    public bool CanHide => !string.Equals(Key, "Home", StringComparison.Ordinal);
}
