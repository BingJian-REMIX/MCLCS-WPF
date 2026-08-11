using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MCLCS.Core.Localization;
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
    /// <summary>
    /// 页面切换动画开关。设置页修改后需即时生效（此前只在窗口构造时读一次，必须重启才生效）。
    /// </summary>
    public static bool AnimationsEnabled { get; set; } = true;

    // 索引贴可视部件
    private readonly Dictionary<MainTabKind, TabParts> _tabs = new();
    // 侧边栏可视部件
    private readonly Dictionary<string, SidebarParts> _sidebarItems = new();
    private readonly SidebarState _sidebarState = new();

    private DispatcherTimer? _expandTimer;
    private DispatcherTimer? _collapseTimer;
    private TrayIconService? _tray;

    public MainWindow()
    {
        InitializeComponent();

        _pages[MainTabKind.Game] = new GameView();
        _pages[MainTabKind.Download] = new DownloadPageView();
        _pages[MainTabKind.Toolbox] = new ToolboxView();
        _pages[MainTabKind.Settings] = new SettingsView();

        // bug #14：标题栏下载入口与弹窗共享下载页 VM 的同一份队列。
        // 不能在 XAML 里用 x:Static —— InitializeComponent 阶段 Current 尚为 null。
        if (DownloadPageViewModel.Current is { } dlVm)
        {
            DownloadPopupBtn.DataContext = dlVm;
            DownloadQueuePopup.DataContext = dlVm;
        }

        BuildTabs();
        ApplyTabLayout(MainTabKind.Game);
        SetTabTheme(MainTabKind.Game);

        _sidebarState.SwitchOwner(MainTabKind.Game);
        BuildSidebar(MainTabKind.Game);

        // 语言切换时刷新主标签与侧边栏标题
        LocaleManager.LocaleChanged += _ => Dispatcher.Invoke(() =>
        {
            BuildTabs();
            ApplyTabLayout(_currentKind);
            SetTabTheme(_currentKind);
            BuildSidebar(_sidebarState.Owner);
            if (!string.IsNullOrEmpty(_sidebarState.SelectedId))
                UpdateSidebarSelection();
        });

        PageHost.Content = _pages[MainTabKind.Game];
        _currentKind = MainTabKind.Game;

        AnimationsEnabled = ProfileStore.Load(GameConstants.DefaultGameRoot).AnimationsEnabled;

        // bug #21（游戏目录切换）：页面在构造期一次性缓存，换目录后版本 / 存档列表仍是旧数据，
        // 故重建除设置页外的三个页面。设置页正是事件源，重建它会销毁当前正在显示的界面。
        MCLCS.App.Services.LauncherService.GameRootChanged += () => Dispatcher.BeginInvoke(() =>
        {
            try
            {
                _pages[MainTabKind.Game] = new GameView();
                _pages[MainTabKind.Download] = new DownloadPageView();
                _pages[MainTabKind.Toolbox] = new ToolboxView();
                if (_currentKind != MainTabKind.Settings)
                    PageHost.Content = _pages[_currentKind];
            }
            catch { /* 重建失败不影响当前会话 */ }
        });

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

        // bug #26：最小化到托盘。托盘图标常驻，提供「打开主界面 / 退出」；
        // 当 MinimizeToTray 开启时，最小化即隐藏主窗口到系统托盘（见 MainWindow_StateChanged）。
        _tray = new TrayIconService(this, RestoreFromTray, () => Application.Current.Shutdown());
        StateChanged += MainWindow_StateChanged;
        Closing += (_, _) => _tray?.Dispose();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized &&
            ProfileStore.Load(GameConstants.DefaultGameRoot).MinimizeToTray)
        {
            // 最小化到托盘：隐藏主窗口（任务栏按钮随之消失，仅留托盘图标）。
            Visibility = Visibility.Hidden;
        }
    }

    /// <summary>从托盘恢复主窗口（双击托盘图标 / 右键「打开主界面」）。</summary>
    private void RestoreFromTray()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Visibility = Visibility.Visible;
        ShowInTaskbar = true;
        Activate();
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

    // ===== 索引贴构建（纯文字 + 四色贴，无图标） =====

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
            var title = new TextBlock
            {
                Text = LocaleManager.T(def.Title),
                Foreground = Brushes.White,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
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

            // bug #8：索引贴位于标题栏内，MouseLeftButtonDown 会冒泡触发 TitleBar 的 DragMove()，
            // 拖动模态循环吞掉后续 MouseLeftButtonUp，导致 SelectTab 永不执行（窗口未最大化时尤其明显）。
            // 这里在按下阶段截断冒泡，保证抬起事件能正常派发到索引贴。
            grid.MouseLeftButtonDown += (_, e) => e.Handled = true;
            grid.MouseLeftButtonUp += (_, _) => SelectTab(def.Kind);

            TabPanel.Children.Add(grid);
            Panel.SetZIndex(grid, def.ZIndex);

            _tabs[def.Kind] = new TabParts(grid, bg, title, underline);
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
            var icon = new PngIcon { Token = it.Icon, Size = 18 };
            var title = new TextBlock
            {
                Text = LocaleManager.T(it.Title),
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
            if (active) p.Row.Background = (Brush)FindResource("ControlHoverBackground");
        }
    }

    private void SelectSidebarItem(string id)
    {
        _sidebarState.Select(id);
        UpdateSidebarSelection();
        RouteSidebar(id);
    }

    /// <summary>把全局侧边栏副标签的点击路由到对应主视图（规格 1.4：侧边栏点击切换内容区）。</summary>
    private void RouteSidebar(string id)
    {
        switch (_currentKind)
        {
            case MainTabKind.Download:
                (_pages[MainTabKind.Download] as DownloadPageView)?.ShowSubTab(id);
                break;
            case MainTabKind.Toolbox:
                (_pages[MainTabKind.Toolbox] as ToolboxView)?.ShowPanel(id);
                break;
            case MainTabKind.Settings:
                (_pages[MainTabKind.Settings] as SettingsView)?.ShowSidebarItem(id);
                break;
        }
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

        // 进入各主视图时同步加载当前选中的副标签内容（规格 1.4 / 2.2）
        RouteSidebar(_sidebarState.SelectedId);

        if (AnimationsEnabled) PlayPageTransition();
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
        if (e.ChangedButton != MouseButton.Left) return;

        // bug #12：标题栏此前无条件 DragMove()，点击全局搜索框时事件冒泡至此，
        // 拖拽立刻抢走鼠标捕获，搜索框永远无法获得焦点 —— 表现为"搜索栏失效"。
        // 因此命中可交互控件（输入框 / 按钮 / 下拉框等）时不触发窗口拖动。
        if (IsInteractiveElement(e.OriginalSource as DependencyObject))
            return;

        try { DragMove(); }
        catch (InvalidOperationException) { /* 拖动期间窗口状态变化，忽略 */ }
    }

    /// <summary>判断命中元素是否位于可交互控件（TextBox / 按钮 / ComboBox 等）内部。</summary>
    private static bool IsInteractiveElement(DependencyObject? src)
    {
        while (src is not null)
        {
            if (src is System.Windows.Controls.Primitives.TextBoxBase
                or System.Windows.Controls.Primitives.ButtonBase
                or System.Windows.Controls.ComboBox
                or System.Windows.Controls.PasswordBox
                or System.Windows.Controls.Primitives.Thumb)
                return true;

            src = src is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(src)
                : LogicalTreeHelper.GetParent(src);
        }
        return false;
    }

    /// <summary>展开 / 收起标题栏下载队列弹窗（bug #14）。</summary>
    private void DownloadPopupBtn_Click(object sender, RoutedEventArgs e)
    {
        // 页面可能在窗口构造后才首次实例化，这里兜底再绑一次
        if (DownloadQueuePopup.DataContext is null && DownloadPageViewModel.Current is { } vm)
        {
            DownloadPopupBtn.DataContext = vm;
            DownloadQueuePopup.DataContext = vm;
        }
        DownloadQueuePopup.IsOpen = !DownloadQueuePopup.IsOpen;
    }

    private void BtnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>设置主窗口背景图片（bug #20：此前仅持久化路径、从未真正应用）。path 为空或文件不存在时隐藏。</summary>
    public void SetBackgroundImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            BackgroundImage.Source = null;
            BackgroundImage.Visibility = Visibility.Collapsed;
            return;
        }
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            BackgroundImage.Source = bmp;
            BackgroundImage.Visibility = Visibility.Visible;
        }
        catch
        {
            BackgroundImage.Source = null;
            BackgroundImage.Visibility = Visibility.Collapsed;
        }
    }

    // ===== 工具 =====

    private static SolidColorBrush Brush(string key) =>
        (SolidColorBrush)Application.Current.FindResource(key);

    private static void AnimateWidth(FrameworkElement el, double to) =>
        el.BeginAnimation(FrameworkElement.WidthProperty,
            new DoubleAnimation(to, TimeSpan.FromMilliseconds(MainTabs.TransitionMs)));

    private static void AnimateMargin(FrameworkElement el, Thickness to) =>
        el.BeginAnimation(FrameworkElement.MarginProperty,
            new ThicknessAnimation(to, TimeSpan.FromMilliseconds(MainTabs.TransitionMs)));

    /// <summary>全局搜索：回车后若命中设置关键词则跳转到对应设置子项，否则跳下载页并预填搜索词（bug #23）。</summary>
    private void GlobalSearch_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var text = GlobalSearchBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        // 命中设置子项关键词 → 跳转设置对应分类
        var settingsTarget = MatchSettingsSubItem(text);
        if (settingsTarget is not null && _pages[MainTabKind.Settings] is SettingsView sv)
        {
            NavigateTo(MainTabKind.Settings);
            sv.ShowSidebarItem(settingsTarget);
            return;
        }

        // 默认：跳转到下载页并设置搜索词
        NavigateTo(MainTabKind.Download);
        if (_pages[MainTabKind.Download] is DownloadPageView dpv)
        {
            dpv.SetSearchKeyword(text);
        }
    }

    /// <summary>将搜索词匹配到设置子项 id（general/launch/download/recommend/account/ai/appearance/about）。无匹配返回 null。</summary>
    private static string? MatchSettingsSubItem(string raw)
    {
        var t = raw.ToLowerInvariant();
        // 先匹配最具体的词，避免被通用词误命中
        if (t.Contains("主题") || t.Contains("外观") || t.Contains("背景") || t.Contains("字体") || t.Contains("颜色") ||
            t.Contains("theme") || t.Contains("appearance") || t.Contains("background") || t.Contains("font"))
            return "appearance";
        if (t.Contains("账号") || t.Contains("账户") || t.Contains("登录") || t.Contains("微软") ||
            t.Contains("account") || t.Contains("login") || t.Contains("microsoft"))
            return "account";
        if (t.Contains("启动") || t.Contains("内存") || t.Contains("java") || t.Contains("游戏路径") || t.Contains("路径") ||
            t.Contains("launch") || t.Contains("memory") || t.Contains("ram"))
            return "launch";
        if (t.Contains("下载") || t.Contains("源") || t.Contains("镜像") || t.Contains("并发") ||
            t.Contains("download") || t.Contains("mirror") || t.Contains("source"))
            return "download";
        if (t.Contains("推荐") || t.Contains("recommend"))
            return "recommend";
        if (t.Contains("ai") || t.Contains("助手") || t.Contains("ollama") || t.Contains("assistant"))
            return "ai";
        if (t.Contains("语言") || t.Contains("自启") || t.Contains("托盘") || t.Contains("通用") ||
            t.Contains("language") || t.Contains("general") || t.Contains("autostart"))
            return "general";
        if (t.Contains("关于") || t.Contains("更新") || t.Contains("版本") ||
            t.Contains("about") || t.Contains("update") || t.Contains("version"))
            return "about";
        return null;
    }

    private sealed record TabParts(Grid Root, Border Bg, TextBlock Title, Rectangle Underline);

    private sealed record SidebarParts(Grid Row, Rectangle Indicator, TextBlock Title, FrameworkElement Icon);
}
