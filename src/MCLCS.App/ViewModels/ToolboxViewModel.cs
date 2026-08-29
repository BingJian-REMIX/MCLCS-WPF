using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using MCLCS.Core.Mvvm;
using MCLCS.App.Views;

namespace MCLCS.App.ViewModels;

/// <summary>工具箱侧边栏的一个面板条目。</summary>
public class ToolboxPanelItem : ObservableObject
{
    /// <summary>与全局侧边栏 <see cref="SidebarModel"/> 的 Toolbox 项 Id 对齐，用于路由。</summary>
    public string Id { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Title { get; init; } = "";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    /// <summary>懒加载的视图（选中时才创建）。</summary>
    internal Func<UserControl> Factory { get; init; } = () => new UserControl();

    /// <summary>
    /// 子面板自身是否带搜索框。带搜索的面板（命令速查、下载中心、下载页等）不重复显示全局搜索框，
    /// 避免 bug.txt 反馈的「每个工具箱页上方都多一个输入框」（#18 副作用修正）。
    /// </summary>
    public bool NeedsSearch { get; init; } = true;

    private UserControl? _view;
    public UserControl View
    {
        get
        {
            if (_view is null)
            {
                _view = Factory();
            }
            return _view;
        }
    }
}

/// <summary>
/// 工具箱页视图模型：22 个面板的侧边栏导航（规格 2.3，第二梯队侧边栏改造）。
/// <para>
/// <c>versionlist</c> 为对齐 MCLCS-Linux 侧栏新增的入口（"管理已安装的游戏版本，选择并一键启动"），
/// 此前 <c>VersionListView</c> 已存在但没有任何入口可达。
/// </para>
/// </summary>
public class ToolboxViewModel : ObservableObject
{
    /// <summary>全部面板（不受搜索过滤影响），用于重建过滤后的 <see cref="PanelItems"/>。</summary>
    private readonly List<ToolboxPanelItem> _allPanels = new();

    public ObservableCollection<ToolboxPanelItem> PanelItems { get; } = new();

    private string _searchKeyword = "";
    /// <summary>bug #18：工具箱搜索关键词（子串匹配，支持中文）。置空则显示全部面板。</summary>
    public string SearchKeyword
    {
        get => _searchKeyword;
        set
        {
            if (SetField(ref _searchKeyword, value)) ApplyFilter();
        }
    }

    private ToolboxPanelItem? _selectedPanel;
    public ToolboxPanelItem? SelectedPanel
    {
        get => _selectedPanel;
        set
        {
            if (!SetField(ref _selectedPanel, value)) return;
            if (_selectedPanel is not null)
            {
                _selectedPanel.IsSelected = true;
                SelectedView = _selectedPanel.View;
            }

            // 取消之前的选中态
            foreach (var item in PanelItems)
                if (item != _selectedPanel) item.IsSelected = false;
        }
    }

    private UserControl? _selectedView;
    public UserControl? SelectedView
    {
        get => _selectedView;
        set => SetField(ref _selectedView, value);
    }

    private void ApplyFilter()
    {
        var kw = _searchKeyword.Trim();
        PanelItems.Clear();
        IEnumerable<ToolboxPanelItem> src = _allPanels;
        if (kw.Length > 0)
            src = src.Where(p =>
                p.Title.Contains(kw, System.StringComparison.CurrentCultureIgnoreCase) ||
                p.Id.Contains(kw, System.StringComparison.CurrentCultureIgnoreCase));
        foreach (var p in src) PanelItems.Add(p);
        if (PanelItems.Count > 0 && !PanelItems.Contains(SelectedPanel))
            SelectedPanel = PanelItems[0];
    }

    /// <summary>
    /// bug #17：供全局搜索调用——返回与关键词匹配的第一个面板 Id（标题或 Id 子串，忽略大小写）；无匹配返回 null。
    /// 仅做匹配，不修改当前选中项，由调用方决定是否跳转。
    /// </summary>
    public string? MatchPanelId(string keyword)
    {
        var kw = (keyword ?? "").Trim();
        if (kw.Length == 0) return null;
        var hit = _allPanels.FirstOrDefault(p =>
            p.Title.Contains(kw, System.StringComparison.CurrentCultureIgnoreCase) ||
            p.Id.Contains(kw, System.StringComparison.CurrentCultureIgnoreCase));
        return hit?.Id;
    }

    public ToolboxViewModel()
    {
        var items = new (string Id, string Icon, string Title, Func<UserControl> Factory, bool NeedsSearch)[]
        {
            ("log",        "\U0001F4CB", "日志管理",     () => new LogView(),          true),
            ("versionlist","\U0001F5C3", "版本列表",     () => new VersionListView(),   true),
            ("saves",      "\U0001F4BE", "存档管理",     () => new SavesView(),         true),
            ("screenshot", "\U0001F4F7", "截图管理",     () => new ScreenshotView(),    true),
            ("perf",       "\u26A1",     "性能/实例",    () => new PerfView(),          true),
            ("network",    "\U0001F310", "网络诊断",     () => new NetworkDiagView(),   true),
            ("shortcut",   "\U0001F517", "快捷方式",     () => new ShortcutView(),      true),
            ("clean",      "\U0001F9F9", "冗余清理",     () => new RedundantCleanView(),true),
            ("modpackio",  "\U0001F4E6", "整合包",       () => new ModpackView(),       true),
            ("backup",     "\U0001F6E1", "备份管理器",   () => new BackupView(),        true),
            ("nbt",        "\U0001F527", "NBT 编辑器",   () => new NbtView(),           true),
            ("datapack",   "\u26A0",     "数据包冲突检测", () => new DataPackView(),     true),
            ("serverpack", "\U0001F5C4", "资源包缓存",   () => new ServerPackView(),    true),
            ("filewatch",  "\U0001F50D", "文件变更检测", () => new FileWatchView(),     true),
            ("annual",     "\U0001F4CA", "年度报告",     () => new AnnualReportView(),  true),
            ("aichat",     "\U0001F916", "AI 助手",      () => new AiAssistView(),      true),
            ("music",      "\U0001F3B5", "音乐播放器",   () => new MusicPlayerView(),   true),
            ("afk",        "\U0001F5A5", "挂机工作流",   () => new AfkWorkflowView(),   true),
            ("dev",        "\U0001F6E0", "开发工具",     () => new DevToolsView(),      false),
            ("skin",       "\U0001F3A8", "皮肤编辑器",   () => new SkinEditorView(),    true),
            ("shadertoken","\u2600",     "光影配置",     () => new ShaderTokenView(),   true),
            ("achievement","\U0001F3C6", "成就展示",     () => new AchievementView(),    true),
            ("command",    "\U0001F4DD", "命令助手",     () => new CommandView(),       false),
            ("moddev",     "\U0001F9F1", "Mod 开发",     () => new ModDevView(),        true),
            ("map",        "\U0001F5FA", "地图安装",     () => new MapView(),           true),
        };

        foreach (var (id, icon, title, factory, needsSearch) in items)
            _allPanels.Add(new ToolboxPanelItem { Id = id, Icon = icon, Title = title, Factory = factory, NeedsSearch = needsSearch });

        // 默认选中第一项
        ApplyFilter();
        SelectedPanel = PanelItems.FirstOrDefault();
    }
}
