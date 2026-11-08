using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.Mac.Services;
using HanabePhotoManager.App.Services;
using HanabePhotoManager.App.Platform;

namespace HanabePhotoManager.Mac.ViewModels;

/// <summary>主窗口：侧栏导航、主题、照片库位置，以及各页面的视图模型。</summary>
public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private string _page = "Home";
    [ObservableProperty] private string _startupStatus = string.Empty;
    private readonly StartupPreferenceService? _startup;
    private bool _startupEnabled;

    public MainViewModel(MacSettings settings)
    {
        Settings = settings;
        if (OperatingSystem.IsMacOS())
        {
            _startup = new StartupPreferenceService(PlatformServices.Startup, new AppSettingsStore());
            try { _startupEnabled = _startup.IsEnabled(); }
            catch (Exception ex) { StartupStatus = "读取登录启动状态失败：" + ex.Message; }
        }
        Library = new LibraryViewModel(this);
        Import = new ImportViewModel(this);
    }

    public MacSettings Settings { get; }
    public ImportViewModel Import { get; }
    public LibraryViewModel Library { get; }

    /// <summary>由窗口注入：系统文件夹选择框、确认框。</summary>
    public Func<string, bool, Task<IReadOnlyList<string>>> FolderPicker { get; set; } = (_, _) => Task.FromResult<IReadOnlyList<string>>([]);
    public Func<string, string, Task<bool>> Confirm { get; set; } = (_, _) => Task.FromResult(false);

    public Task<IReadOnlyList<string>> PickFoldersAsync(string title, bool allowMultiple) => FolderPicker(title, allowMultiple);
    public Task<bool> ConfirmAsync(string title, string message) => Confirm(title, message);

    public bool IsHome => Page == "Home";
    public bool IsImport => Page == "Import";
    public bool IsLibrary => Page == "Library";
    public bool IsSettings => Page == "Settings";

    public string Greeting => DateTime.Now.Hour switch { < 11 => "早上好", < 14 => "中午好", < 18 => "下午好", _ => "晚上好" };
    public string LibraryRootText => string.IsNullOrWhiteSpace(Settings.LibraryRoot) ? "还没有选择照片库" : Settings.LibraryRoot;
    public string PlatformText => OperatingSystem.IsMacOS() ? "macOS 版" : "跨平台版（Windows 调试运行）";
    public bool IsDarkTheme => Settings.Theme != "Light";
    public string ThemeButtonText => IsDarkTheme ? "切换浅色" : "切换深色";
    public bool IsStartupAvailable => _startup is not null;
    public string StartupButtonText => _startupEnabled ? "关闭登录启动" : "启用登录启动";

    partial void OnPageChanged(string value)
    {
        foreach (var name in new[] { nameof(IsHome), nameof(IsImport), nameof(IsLibrary), nameof(IsSettings) }) OnPropertyChanged(name);
        if (value == "Import") Import.RefreshDevicesCommand.Execute(null);
    }

    [RelayCommand] private void Navigate(string page) => Page = page;

    [RelayCommand]
    private async Task ToggleStartup()
    {
        if (_startup is null) return;
        try
        {
            await _startup.SetEnabledAsync(!_startupEnabled);
            _startupEnabled = _startup.IsEnabled();
            StartupStatus = _startupEnabled ? "登录时自动启动已启用。" : "登录时自动启动已关闭。";
        }
        catch (Exception ex)
        {
            try { _startupEnabled = _startup.IsEnabled(); } catch { }
            StartupStatus = "无法更改登录启动：" + ex.Message;
        }
        OnPropertyChanged(nameof(StartupButtonText));
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        Settings.Theme = IsDarkTheme ? "Light" : "Dark";
        if (Application.Current is { } app) app.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        SaveSettings(settings => settings.MacTheme = Settings.Theme);
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(ThemeButtonText));
    }

    [RelayCommand]
    private async Task ChooseLibraryRoot()
    {
        var folders = await PickFoldersAsync("选择照片库根目录（例如 2026 年的照片文件夹）", allowMultiple: false);
        if (folders.Count == 0) return;
        Settings.LibraryRoot = folders[0];
        SaveSettings(settings => settings.LibraryRoot = Settings.LibraryRoot);
        OnPropertyChanged(nameof(LibraryRootText));
        Library.Refresh();
        Import.RefreshDevicesCommand.Execute(null);
    }

    public void SaveSettings(Action<AppSettings> change)
    {
        try { MacSettingsStore.Update(change); }
        catch (IOException) { }
    }
}
