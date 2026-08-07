using System.Collections.ObjectModel;
using System.Windows.Controls;
using MCLCS.Core.Mvvm;
using MCLCS.App.Views;

namespace MCLCS.App.ViewModels;

/// <summary>工具箱侧边栏的一个面板条目。</summary>
public class ToolboxPanelItem : ObservableObject
{
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
/// 工具箱页视图模型：17 个面板的侧边栏导航（规格 2.3，第二梯队侧边栏改造）。
/// </summary>
public class ToolboxViewModel : ObservableObject
{
    public ObservableCollection<ToolboxPanelItem> PanelItems { get; } = new();

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

    public ToolboxViewModel()
    {
        var items = new (string Icon, string Title, Func<UserControl> Factory)[]
        {
            ("\U0001F4CB", "日志管理",     () => new LogView()),
            ("\U0001F4BE", "存档管理",     () => new SavesView()),
            ("\U0001F4F7", "截图管理",     () => new ScreenshotView()),
            ("\u26A1",     "性能/实例",    () => new PerfView()),
            ("\U0001F310", "网络诊断",     () => new NetworkDiagView()),
            ("\U0001F517", "快捷方式",     () => new ShortcutView()),
            ("\U0001F9F9", "冗余清理",     () => new RedundantCleanView()),
            ("\U0001F4E6", "整合包",       () => new ModpackView()),
            ("\U0001F6E1", "备份管理器",   () => new BackupView()),
            ("\U0001F527", "NBT 编辑器",   () => new NbtView()),
            ("\u26A0",     "数据包冲突检测", () => new DataPackView()),
            ("\U0001F5C4", "资源包缓存",   () => new ServerPackView()),
            ("\U0001F50D", "文件变更检测", () => new FileWatchView()),
            ("\U0001F4CA", "年度报告",     () => new AnnualReportView()),
            ("\U0001F916", "AI 助手",      () => new AiAssistView()),
            ("\U0001F3B5", "音乐播放器",   () => new MusicPlayerView()),
            ("\U0001F5A5", "挂机工作流",   () => new AfkWorkflowView()),
            ("\U0001F6E0", "开发工具",     () => new DevToolsView()),
            ("\U0001F3A8", "皮肤编辑器",   () => new SkinEditorView()),
            ("\u2600",     "光影配置",     () => new ShaderTokenView()),
            ("\U0001F3C6", "成就展示",     () => new AchievementView()),
        };

        foreach (var (icon, title, factory) in items)
            PanelItems.Add(new ToolboxPanelItem { Icon = icon, Title = title, Factory = factory });

        // 默认选中第一项
        SelectedPanel = PanelItems.FirstOrDefault();
    }
}
