using CommunityToolkit.Mvvm.Input;

namespace HanabePhotoManager.App.Navigation;

public sealed class NavigationItemViewModel
{
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
}
