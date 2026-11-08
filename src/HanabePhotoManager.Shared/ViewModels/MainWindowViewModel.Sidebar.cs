using CommunityToolkit.Mvvm.Input;
using HanabePhotoManager.App.Navigation;

namespace HanabePhotoManager.App.ViewModels
{
    /// <summary>
    /// 左侧功能栏：整体收起 / 展开（内容铺满窗口），以及按页面选择显示哪些功能。
    /// 宽度随“图标 / 文字 / 图标和文字”显示模式变化：只显示图标时自动收窄成图标轨道。
    /// </summary>
    public sealed partial class MainWindowViewModel
    {
        /// <summary>完整侧栏列宽（与 Sizing.xaml 的 Size.Sidebar.Width 一致）。</summary>
        public const double ExpandedSidebarWidth = 228;

        /// <summary>只显示图标时的侧栏列宽（与 Sizing.xaml 的 Size.Rail.Width 一致）。</summary>
        public const double CompactSidebarWidth = 88;

        private bool _isSidebarCollapsed;
        private readonly HashSet<string> _hiddenNavigationKeys = new(StringComparer.Ordinal);
        private IRelayCommand? _toggleSidebarCommand;

        /// <summary>true 时左侧功能栏完全收起，页面内容占满整个窗口宽度。</summary>
        public bool IsSidebarCollapsed
        {
            get => _isSidebarCollapsed;
            set
            {
                if (SetProperty(ref _isSidebarCollapsed, value))
                {
                    OnPropertyChanged(nameof(IsSidebarVisible));
                    OnPropertyChanged(nameof(SidebarColumnWidth));
                    OnPropertyChanged(nameof(SidebarToggleToolTip));
                    _ = SaveSettingsAsync();
                }
            }
        }

        public bool IsSidebarVisible => !IsSidebarCollapsed;

        /// <summary>只显示图标时，侧栏里的标题、文字标签和底部卡片一并隐藏。</summary>
        public bool IsSidebarCompact => NavigationDisplayMode == NavigationDisplayMode.Icon;

        public bool IsSidebarFull => !IsSidebarCompact;

        public double SidebarColumnWidth => IsSidebarCollapsed
            ? 0
            : IsSidebarCompact ? CompactSidebarWidth : ExpandedSidebarWidth;

        /// <summary>侧栏面板本身的宽度（不含 12px 左外边距）；收起时保持不变，由外层裁剪滑出。</summary>
        public double SidebarContentWidth => (IsSidebarCompact ? CompactSidebarWidth : ExpandedSidebarWidth) - 12;

        public string SidebarToggleToolTip => IsSidebarCollapsed ? "展开左侧功能栏（Ctrl+B）" : "收起左侧功能栏，内容铺满窗口（Ctrl+B）";

        public IRelayCommand ToggleSidebarCommand => _toggleSidebarCommand ??= new RelayCommand(() => IsSidebarCollapsed = !IsSidebarCollapsed);

        public IReadOnlyCollection<string> HiddenNavigationKeys => _hiddenNavigationKeys;

        /// <summary>设置某个功能页是否显示在左侧功能栏。主页不能隐藏；隐藏当前所在页面时回到主页。</summary>
        public void SetNavigationPageVisible(string key, bool visible)
        {
            var item = NavigationItems.FirstOrDefault(entry => string.Equals(entry.Key, key, StringComparison.Ordinal));
            if (item is null || (!visible && !item.CanHide)) return;
            var changed = visible ? _hiddenNavigationKeys.Remove(key) : _hiddenNavigationKeys.Add(key);
            item.IsVisible = visible;
            if (!changed) return;
            if (!visible && string.Equals(CurrentPage, key, StringComparison.Ordinal) && ShowHomeCommand.CanExecute(null))
                ShowHomeCommand.Execute(null);
            StatusMessage = visible ? $"已在左侧功能栏显示“{item.Label}”。" : $"已从左侧功能栏隐藏“{item.Label}”，右键功能栏可重新显示。";
            _ = SaveSettingsAsync();
        }

        /// <summary>重新显示所有被隐藏的功能页。</summary>
        public void ShowAllNavigationPages()
        {
            foreach (var key in _hiddenNavigationKeys.ToArray()) SetNavigationPageVisible(key, true);
        }

        private void RestoreSidebarSettings(bool collapsed, IEnumerable<string>? hiddenKeys)
        {
            _isSidebarCollapsed = collapsed;
            _hiddenNavigationKeys.Clear();
            foreach (var key in hiddenKeys ?? [])
            {
                if (!string.IsNullOrWhiteSpace(key) && !string.Equals(key, "Home", StringComparison.Ordinal))
                    _hiddenNavigationKeys.Add(key);
            }
            OnPropertyChanged(nameof(IsSidebarCollapsed));
            OnPropertyChanged(nameof(IsSidebarVisible));
            OnPropertyChanged(nameof(SidebarColumnWidth));
            OnPropertyChanged(nameof(SidebarToggleToolTip));
        }
    }
}
