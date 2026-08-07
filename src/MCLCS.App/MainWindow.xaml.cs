using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MCLCS.Core.Profiles;
using MCLCS.Core.UI;
using MCLCS.Core.Utils;
using MCLCS.App.Services;
using MCLCS.App.Themes;
using MCLCS.App.ViewModels;
using MCLCS.App.Views;
using System.Windows.Shapes;

namespace MCLCS.App;

public partial class MainWindow : Window
{
    // 主标签 → 页面
    private readonly Dictionary<MainTabKind, FrameworkElement> _pages = new();
    private MainTabKind _currentKind = (MainTabKind)(-1);
    private bool _animations;

    // 索引贴可视部件
    private readonly Dictionary<MainTabKind, TabParts> _tabs = new();
    // 侧边栏可视部件
    private readonly Dictionary<string, SidebarParts> _sidebarItems = new();
    private readonly SidebarState _sidebarState = new();

    private DispatcherTimer? _expandTimer;
    private DispatcherTimer? _collapseTimer;

    public MainWindow()
    {
        InitializeComponent();

        _pages[MainTabKind.Game] = new GameView();
        _pages[MainTabKind.Download] = new DownloadPageView();
        _pages[MainTabKind.Toolbox] = new ToolboxView();
        _pages[MainTabKind.Settings] = new SettingsView();

        BuildTabs();
        ApplyTabLayout(MainTabKind.Game);
        SetTabTheme(MainTabKind.Game);

        _sidebarState.SwitchOwner(MainTabKind.Game);
        BuildSidebar(MainTabKind.Game);

        PageHost.Content = _pages[MainTabKind.Game];
        _currentKind = MainTabKind.Game;

        _animations = ProfileStore.Load(GameConstants.DefaultGameRoot).AnimationsEnabled;

        // 音乐播放器解码宿主（MediaElement 实现 IMediaPlayer）
        var player = new MediaElementPlayer(MusicMedia);
        MusicPlayerViewModel.Instance.Host = player;
        player.Ended += () => MusicPlayerViewModel.Instance.OnTrackEnded();
        MusicPlayerViewModel.Instance.SetVolumeFromHost(); // 推送初始音量到 MediaElement
        ((System.Windows.Controls.Primitives.Popup)MusicListPopup).Closed += (_, _) => MusicPlayerViewModel.Instance.Expanded = false;

        LauncherService.Instance.Logged += line =>
        {
            if (!string.IsNullOrWhiteSpace(line))
                StatusBarViewModel.Current.LastLog = line;
        };
        Closed += (_, _) => _ = StatusBarViewModel.Current.RefreshAsync();

        // §2.3-16 焦点回归时检测手动丢入的新文件（首次 Activated 只建基线，不弹通知）
        Activated += MainWindow_Activated;
    }

    private DateTime _lastFileWatch = DateTime.MinValue;

    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        // 防抖：同一秒内不重复扫描
        var now = DateTime.Now;
        if ((now - _lastFileWatch).TotalSeconds < 1) return;
        _lastFileWatch = now;
        _ = LaunchCoordinator.CheckFileChangesAsync();

        // 自动同步游戏内添加/删除的服务器（servers.dat）
        if (_pages.TryGetValue(MainTabKind.Game, out var gamePage) && gamePage is GameView gv)
        {
            if (gv.DataContext is GameViewModel gvm)
                gvm.RefreshServers();
        }
    }

    // ===== 索引贴构建 =====

    /// <summary>主标签 → 矢量图标 token（见 <see cref="Icons"/>）。</summary>
    private static readonly Dictionary<MainTabKind, string> TabIconToken = new()
    {
        [MainTabKind.Game] = "game",
        [MainTabKind.Download] = "download",
        [MainTabKind.Toolbox] = "toolbox",
        [MainTabKind.Settings] = "settings"
    };

    private void BuildTabs()
    {
        TabPanel.Children.Clear();
        _tabs.Clear();

        foreach (var def in MainTabs.All)
        {
            var grid = new Grid
            {
                Height = MainTabs.TabHeight,
                Width = def.AlwaysExpanded ? MainTabs.ExpandedWidth : MainTabs.CollapsedWidth,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Tag = def
            };

            var bg = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent
            };

            var inner = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 14, 0)
            };
            var icon = Icons.Make(TabIconToken.GetValueOrDefault(def.Kind, "info"), 16);
            var title = new TextBlock
            {
                Text = def.Title,
                Foreground = Brushes.White,
                FontSize = 13,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            inner.Children.Add(icon);
            inner.Children.Add(title);

            var underline = new Rectangle
            {
                Height = MainTabs.UnderlineHeight,
                VerticalAlignment = VerticalAlignment.Bottom,
                Fill = Brushes.White,
                Visibility = Visibility.Collapsed
            };

            grid.Children.Add(bg);
            grid.Children.Add(inner);
            grid.Children.Add(underline);

            grid.MouseLeftButtonUp += (_, _) => SelectTab(def.Kind);

            TabPanel.Children.Add(grid);
            Panel.SetZIndex(grid, def.ZIndex);

            _tabs[def.Kind] = new TabParts(grid, bg, icon, title, underline);
        }
    }

    /// <summary>按当前选中态刷新所有索引贴的宽度 / 配色 / 细线 / 重叠。</summary>
    private void ApplyTabLayout(MainTabKind selected)
    {
        var list = MainTabs.All;
        for (var i = 0; i < list.Count; i++)
        {
            var def = list[i];
            var p = _tabs[def.Kind];
            var isSel = def.Kind == selected;
            var expanded = isSel || def.AlwaysExpanded;
            var w = expanded ? MainTabs.ExpandedWidth : MainTabs.CollapsedWidth;

            AnimateWidth(p.Root, w);

            p.Bg.Background = isSel
                ? Brush($"Tab{def.Kind}ActiveBrush")
                : Brush($"Tab{def.Kind}Brush");

            p.Title.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;

            p.Underline.Visibility = isSel ? Visibility.Visible : Visibility.Collapsed;
            if (isSel)
                p.Underline.Fill = Brush($"Tab{def.Kind}UnderlineBrush");

            // 重叠：左侧邻居展开则 10px，否则 20px
            var left = i == 0 ? 0 :
                (NeighborExpanded(i, selected) ? -MainTabs.ExpandedOverlap : -MainTabs.CollapsedOverlap);
            AnimateMargin(p.Root, new Thickness(left, 0, 0, 0));
        }
    }

    private static bool NeighborExpanded(int index, MainTabKind selected) =>
        index > 0 && (MainTabs.All[index - 1].Kind == selected || MainTabs.All[index - 1].AlwaysExpanded);

    // ===== 主题跟着主标签走 =====

    private void SetTabTheme(MainTabKind kind)
    {
        var solid = Brush($"Tab{kind}Brush");
        // 覆写 App 级资源，所有 DynamicResource 引用即时刷新
        Application.Current.Resources["TitleBarBrush"] = solid;
        Application.Current.Resources["SidebarIndicatorBrush"] = solid;
    }

    // ===== 侧边栏 =====

    private void BuildSidebar(MainTabKind kind)
    {
        SidebarItemsPanel.Children.Clear();
        _sidebarItems.Clear();

        if (!Sidebar.Has(kind))
        {
            SidebarRoot.Visibility = Visibility.Collapsed;
            return;
        }

        SidebarRoot.Visibility = Visibility.Visible;

        foreach (var it in Sidebar.For(kind))
        {
            var row = new Grid
            {
                Height = 40,
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = Cursors.Hand,
                Tag = it.Id,
                Background = Brushes.Transparent
            };

            var indicator = new Rectangle
            {
                Width = SidebarState.IndicatorWidth,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                Fill = (Brush)Application.Current.Resources["SidebarIndicatorBrush"],
                Visibility = Visibility.Collapsed
            };

            var inner = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 12, 0)
            };
            var icon = Icons.Make(it.Icon, 18, (Brush)FindResource("SecondaryForeground"));
            var title = new TextBlock
            {
                Text = it.Title,
                Foreground = (Brush)FindResource("SecondaryForeground"),
                FontSize = 13,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = _sidebarState.Expanded ? Visibility.Visible : Visibility.Collapsed
            };
            inner.Children.Add(icon);
            inner.Children.Add(title);

            row.Children.Add(indicator);
            row.Children.Add(inner);

            row.MouseLeftButtonUp += (_, _) => SelectSidebarItem(it.Id);
            row.MouseEnter += (_, _) => row.Background = (Brush)FindResource("ControlHoverBackground");
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;

            SidebarItemsPanel.Children.Add(row);
            _sidebarItems[it.Id] = new SidebarParts(row, indicator, title, icon);
        }

        UpdateSidebarSelection();
    }

    private void UpdateSidebarSelection()
    {
        var sel = _sidebarState.SelectedId;
        foreach (var (id, p) in _sidebarItems)
        {
            var active = id == sel;
            p.Indicator.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            var fg = active ? Brushes.White : (Brush)FindResource("SecondaryForeground");
            p.Title.Foreground = fg;
            p.Icon.Stroke = fg;
            if (active) p.Row.Background = (Brush)FindResource("ControlHoverBackground");
        }
    }

    private void SelectSidebarItem(string id)
    {
        _sidebarState.Select(id);
        UpdateSidebarSelection();
        // 路由到当前主视图（下载页按副标签切换卡片内容；规格 2.2）
        if (_currentKind == MainTabKind.Download)
            (_pages[MainTabKind.Download] as DownloadPageView)?.ShowSubTab(id);
    }

    private void Sidebar_MouseEnter(object sender, MouseEventArgs e)
    {
        _collapseTimer?.Stop();
        if (_sidebarState.Expanded)
        {
            AnimateSidebar(_sidebarState.Width, _sidebarState.Expanded);
            return;
        }
        _expandTimer?.Stop();
        _expandTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SidebarState.HoverExpandDelayMs) };
        _expandTimer.Tick += (_, _) =>
        {
            _expandTimer?.Stop();
            _sidebarState.HoverEnter();
            AnimateSidebar(_sidebarState.Width, _sidebarState.Expanded);
        };
        _expandTimer.Start();
    }

    private void Sidebar_MouseLeave(object sender, MouseEventArgs e)
    {
        _expandTimer?.Stop();
        _collapseTimer?.Stop();
        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SidebarState.HoverCollapseDelayMs) };
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer?.Stop();
            _sidebarState.HoverLeave();
            AnimateSidebar(_sidebarState.Width, _sidebarState.Expanded);
        };
        _collapseTimer.Start();
    }

    private void AnimateSidebar(double width, bool expanded)
    {
        SidebarRoot.BeginAnimation(FrameworkElement.WidthProperty,
            new DoubleAnimation(width, TimeSpan.FromMilliseconds(SidebarState.TransitionMs)));
        foreach (var p in _sidebarItems.Values)
            p.Title.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
    }

    // ===== 导航 =====

    private void SelectTab(MainTabKind kind)
    {
        if (kind == _currentKind) return;
        NavigateTo(kind);
    }

    private void NavigateTo(MainTabKind kind)
    {
        if (!_pages.TryGetValue(kind, out var page) || page is null) return;

        PageHost.Content = page;
        _currentKind = kind;

        SetTabTheme(kind);
        ApplyTabLayout(kind);

        _sidebarState.SwitchOwner(kind);
        BuildSidebar(kind);
        AnimateSidebar(Sidebar.Has(kind) ? _sidebarState.Width : 0, _sidebarState.Expanded);

        // 进入下载页时加载初始副标签内容（规格 2.2）
        if (kind == MainTabKind.Download)
            (_pages[MainTabKind.Download] as DownloadPageView)?.ShowSubTab(_sidebarState.SelectedId);

        if (_animations) PlayPageTransition();
    }

    private void PlayPageTransition()
    {
        var duration = TimeSpan.FromMilliseconds(200);
        var fade = new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var slide = new DoubleAnimation(18, 0, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        PageBorder.BeginAnimation(UIElement.OpacityProperty, fade);
        PageTransform.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    // ===== 窗口控制 =====

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void BtnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    // ===== 工具 =====

    private static SolidColorBrush Brush(string key) =>
        (SolidColorBrush)Application.Current.FindResource(key);

    private static void AnimateWidth(FrameworkElement el, double to) =>
        el.BeginAnimation(FrameworkElement.WidthProperty,
            new DoubleAnimation(to, TimeSpan.FromMilliseconds(MainTabs.TransitionMs)));

    private static void AnimateMargin(FrameworkElement el, Thickness to) =>
        el.BeginAnimation(FrameworkElement.MarginProperty,
            new ThicknessAnimation(to, TimeSpan.FromMilliseconds(MainTabs.TransitionMs)));

    /// <summary>全局搜索：回车后跳转到下载页，预填搜索关键词。</summary>
    private void GlobalSearch_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var text = GlobalSearchBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        // 跳转到下载页并设置搜索词
        NavigateTo(MainTabKind.Download);
        if (_pages[MainTabKind.Download] is DownloadPageView dpv)
        {
            dpv.SetSearchKeyword(text);
        }
    }

    private sealed record TabParts(Grid Root, Border Bg, System.Windows.Shapes.Path Icon, TextBlock Title, Rectangle Underline);

    private sealed record SidebarParts(Grid Row, Rectangle Indicator, TextBlock Title, System.Windows.Shapes.Path Icon);
}
