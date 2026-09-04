using System.Windows;
using System.Windows.Controls;
using MCLCS.App.ViewModels;

namespace MCLCS.App.Views;

/// <summary>
/// 版本设置面板（对齐 MCLCS-Linux 的 VersionSettingsDialog）。
/// 宿主窗口负责创建：见 <see cref="VersionSettingsWindow"/>。
/// </summary>
public partial class VersionSettingsView : UserControl
{
    public VersionSettingsViewModel VM { get; }

    /// <summary>返回上一页（大页导航关闭，或重新打开版本列表）；未设置时回退到关闭宿主窗口。</summary>
    public Action? OnBack { get; set; }

    public VersionSettingsView(string gameRoot, string versionId, string versionType, Action? onBack = null)
    {
        OnBack = onBack;
        VM = new VersionSettingsViewModel(gameRoot, versionId, versionType);
        DataContext = VM;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            CloseButton.Click += (_, _) => (OnBack ?? (() => Window.GetWindow(this)?.Close()))();
        };
    }

    private void BackButton_Click(object sender, System.Windows.RoutedEventArgs e) =>
        (OnBack ?? (() => Window.GetWindow(this)?.Close()))();
}
