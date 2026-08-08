using System.Linq;
using System.Windows.Controls;
using MCLCS.App.ViewModels;

namespace MCLCS.App.Views;

public partial class ToolboxView : UserControl
{
    private ToolboxViewModel Vm => (ToolboxViewModel)DataContext;

    public ToolboxView()
    {
        InitializeComponent();
    }

    /// <summary>由 MainWindow 全局侧边栏路由调用，切换到指定面板（规格 1.4）。</summary>
    public void ShowPanel(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        // 开发工具在全局侧边栏拆成 4 项（moddev/packmaker/nbt/command），统一归入「开发工具」面板
        var lookup = id switch
        {
            "moddev" or "packmaker" or "command" => "dev",
            _ => id
        };

        var panel = Vm.PanelItems.FirstOrDefault(p => p.Id == lookup);
        if (panel is not null)
        {
            Vm.SelectedPanel = panel;
            return;
        }

        // 全局侧边栏有、但工具箱内部列表未收录的面板（崩溃分析）
        var extra = lookup switch
        {
            "crash" => (UserControl)new CrashAnalysisView(),
            _ => null
        };
        if (extra is not null) Vm.SelectedView = extra;
    }
}
