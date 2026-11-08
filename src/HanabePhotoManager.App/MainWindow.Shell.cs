using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HanabePhotoManager.App.Imports;
using HanabePhotoManager.App.Navigation;

namespace HanabePhotoManager.App;

/// <summary>窗口外壳：左侧功能栏的收起 / 页面显示菜单，以及导入步骤条的点击跳转。</summary>
public partial class MainWindow
{
    /// <summary>右键左侧功能栏：勾选要显示的功能页、切换窄侧栏、收起侧栏。菜单每次打开时按当前状态重建，样式与全局右键菜单一致。</summary>
    private void Sidebar_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement { ContextMenu: { } menu })
        {
            return;
        }

        menu.Style = (Style)FindResource("Menu.Context");
        var itemStyle = (Style)FindResource("Menu.Item");
        var headerStyle = (Style)FindResource("Menu.SectionHeader");
        var separatorStyle = (Style)FindResource("Separator.Menu");

        menu.Items.Clear();
        menu.Items.Add(new MenuItem { Header = "侧栏显示的功能页", Style = headerStyle });
        foreach (var item in _viewModel.NavigationItems)
        {
            var entry = new MenuItem
            {
                Header = item.Label,
                Style = itemStyle,
                IsCheckable = item.CanHide,
                IsChecked = item.IsVisible,
                StaysOpenOnClick = true,
                // 主页不能隐藏：不画勾选框，右侧注明“固定”，避免一个灰掉的勾看起来像出错。
                InputGestureText = item.CanHide ? string.Empty : "固定",
                ToolTip = item.CanHide ? null : "主页是所有功能的入口，始终显示",
                Tag = item,
            };
            if (item.CanHide)
            {
                entry.Click += (_, _) =>
                {
                    if (entry.Tag is NavigationItemViewModel target)
                    {
                        _viewModel.SetNavigationPageVisible(target.Key, entry.IsChecked);
                        entry.IsChecked = target.IsVisible;
                    }
                };
            }
            else
            {
                entry.IsHitTestVisible = false;
            }
            menu.Items.Add(entry);
        }

        if (_viewModel.HiddenNavigationKeys.Count > 0)
        {
            var showAll = new MenuItem { Header = $"全部显示（已隐藏 {_viewModel.HiddenNavigationKeys.Count} 个）", Style = itemStyle };
            showAll.Click += (_, _) => _viewModel.ShowAllNavigationPages();
            menu.Items.Add(showAll);
        }

        menu.Items.Add(new Separator { Style = separatorStyle });
        menu.Items.Add(new MenuItem { Header = "侧栏样式", Style = headerStyle });

        var compact = new MenuItem
        {
            Header = "只显示图标（窄侧栏）",
            Style = itemStyle,
            IsCheckable = true,
            IsChecked = _viewModel.NavigationDisplayMode == NavigationDisplayMode.Icon,
        };
        compact.Click += (_, _) => _viewModel.NavigationDisplayMode = compact.IsChecked
            ? NavigationDisplayMode.Icon
            : NavigationDisplayMode.IconAndText;
        menu.Items.Add(compact);

        var collapse = new MenuItem { Header = "收起侧栏，内容铺满窗口", Style = itemStyle, InputGestureText = "Ctrl+B" };
        collapse.Click += (_, _) => _viewModel.IsSidebarCollapsed = true;
        menu.Items.Add(collapse);
    }

    /// <summary>导入步骤条：点击“选择来源”返回、点击“确认日期”进入下一步、点击“整理场次”打开场次窗口。</summary>
    private void ImportWizardStep_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ImportWizardStepItem step || !step.IsClickable)
        {
            return;
        }

        if (step.Number == 4)
        {
            if (_viewModel.CanGoToImportWizardStep(4))
            {
                OpenSessions_Click(new FrameworkElement { Tag = "ImportLast" }, e);
            }
        }
        else
        {
            _viewModel.GoToImportWizardStep(step.Number);
        }

        e.Handled = true;
    }

    // ---------- 搜索栏 AI ----------

    private async void AgentAsk_Click(object sender, RoutedEventArgs e) => await AskAgentFromSearchBoxAsync();

    /// <summary>把搜索栏里的话交给 AI；没有内容时只打开对话面板。</summary>
    private async Task AskAgentFromSearchBoxAsync()
    {
        var text = WorkspaceSearchBox.Text.Trim();
        _viewModel.IsAgentPanelOpen = true;
        if (text.Length == 0) return;
        WorkspaceSearchBox.Clear();
        await _viewModel.AskAgentAsync(text);
        WorkspaceSearchBox.Focus();
    }

    private void AgentSettings_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsAgentPanelOpen = false;
        new Agents.AgentSettingsWindow(_viewModel) { Owner = this }.ShowDialog();
    }

    /// <summary>拍摄频率板块被拉高 / 压矮时，条形最大高度跟着变（留出日期文字和悬停气泡的位置）。</summary>
    private void ShootingBars_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.HeightChanged && e.NewSize.Height > 0)
            _viewModel.ShootingBarMaxHeight = e.NewSize.Height - 16 - 30;
    }

    private async void HomeStorageRefresh_Click(object sender, RoutedEventArgs e) =>
        await _viewModel.RefreshLibraryStorageAsync();

    private static readonly TimeSpan SidebarMotionDuration = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// 侧栏宽度变化（收起 / 展开 / 切换窄侧栏）：外层裁剪框宽度平滑过渡，页面内容随之平滑让位；
    /// 面板本身同时淡出并向左滑出（展开时反向）。启动恢复或系统关闭动画时直接到位。
    /// </summary>
    private void AnimateSidebar()
    {
        if (SidebarClip is null || SidebarSurface is null) return;
        var target = _viewModel.SidebarColumnWidth;
        var collapsing = target <= 0;
        var slide = SidebarSurface.RenderTransform as System.Windows.Media.TranslateTransform;
        if (slide is null)
        {
            slide = new System.Windows.Media.TranslateTransform();
            SidebarSurface.RenderTransform = slide;
        }

        if (!IsLoaded || !Controls.Motion.IsEnabled)
        {
            SidebarClip.BeginAnimation(WidthProperty, null);
            SidebarSurface.BeginAnimation(OpacityProperty, null);
            slide.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
            SidebarClip.Width = target;
            SidebarSurface.Opacity = collapsing ? 0 : 1;
            slide.X = 0;
            SidebarSurface.Visibility = collapsing ? Visibility.Hidden : Visibility.Visible;
            return;
        }

        SidebarSurface.Visibility = Visibility.Visible;
        var width = new System.Windows.Media.Animation.DoubleAnimation(SidebarClip.ActualWidth, target, SidebarMotionDuration)
        {
            EasingFunction = Controls.Motion.EaseInOut,
        };
        width.Completed += (_, _) =>
        {
            if (_viewModel.SidebarColumnWidth != target) return;
            SidebarClip.BeginAnimation(WidthProperty, null);
            SidebarClip.Width = target;
            if (collapsing) SidebarSurface.Visibility = Visibility.Hidden;
        };
        SidebarClip.BeginAnimation(WidthProperty, width, System.Windows.Media.Animation.HandoffBehavior.SnapshotAndReplace);

        if (collapsing || SidebarSurface.Opacity < 1 || slide.X != 0)
        {
            slide.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
                new System.Windows.Media.Animation.DoubleAnimation(collapsing ? -24 : 0, SidebarMotionDuration) { EasingFunction = Controls.Motion.EaseOut },
                System.Windows.Media.Animation.HandoffBehavior.SnapshotAndReplace);
            SidebarSurface.BeginAnimation(OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(collapsing ? 0 : 1,
                    collapsing ? Controls.Motion.Normal : SidebarMotionDuration) { EasingFunction = Controls.Motion.EaseOut },
                System.Windows.Media.Animation.HandoffBehavior.SnapshotAndReplace);
        }
    }

    /// <summary>Ctrl+B：收起 / 展开左侧功能栏。返回 true 表示已处理。</summary>
    private bool TryHandleShellShortcut(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.B && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _viewModel.ToggleSidebarCommand.Execute(null);
            return true;
        }

        return false;
    }
}
