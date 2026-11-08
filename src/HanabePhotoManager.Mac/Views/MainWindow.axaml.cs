using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using HanabePhotoManager.Mac.ViewModels;

namespace HanabePhotoManager.Mac.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        if (OperatingSystem.IsMacOS())
        {
            // macOS：内容延伸到标题栏，红绿灯按钮浮在侧栏左上角，和原生应用一致。
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.PreferSystemChrome;
            ExtendClientAreaTitleBarHeightHint = 28;
            SidebarHeader.Margin = new Thickness(6, 26, 6, 22);
        }

        DropZone.AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None);
        DropZone.AddHandler(DragDrop.DropEvent, OnDrop);
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not MainViewModel vm) return;
            vm.FolderPicker = PickFoldersAsync;
            vm.Confirm = ConfirmAsync;
        };
        // 调试用：HANABE_MAC_AUTOANALYZE=来源文件夹 时启动后直接进入导入并分析（自动化验收用，普通使用不会设置）。
        Opened += async (_, _) =>
        {
            var auto = Environment.GetEnvironmentVariable("HANABE_MAC_AUTOANALYZE");
            if (DataContext is MainViewModel vm && !string.IsNullOrWhiteSpace(auto) && Directory.Exists(auto))
            {
                vm.Page = "Import";
                await vm.Import.AnalyzeAsync([auto]);
            }
        };
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var paths = e.Data.GetFiles()?
            .Select(item => item.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            .Cast<string>()
            .ToArray() ?? [];
        if (paths.Length > 0) await vm.Import.AnalyzeAsync(paths);
    }

    private async Task<IReadOnlyList<string>> PickFoldersAsync(string title, bool allowMultiple)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = allowMultiple });
        return folders.Select(folder => folder.TryGetLocalPath()).Where(path => !string.IsNullOrWhiteSpace(path)).Cast<string>().ToArray();
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var result = false;
        var dialog = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = (Avalonia.Media.IBrush?)Application.Current?.FindResource(Application.Current.ActualThemeVariant, "Hb.Bg"),
        };
        var yes = new Button { Content = "确定", Classes = { "primary" } };
        var no = new Button { Content = "取消" };
        yes.Click += (_, _) => { result = true; dialog.Close(); };
        no.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = title, FontSize = 18, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { no, yes } },
            },
        };
        await dialog.ShowDialog(this);
        return result;
    }
}
