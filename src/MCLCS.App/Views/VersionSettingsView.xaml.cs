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

    public VersionSettingsView(string gameRoot, string versionId, string versionType)
    {
        VM = new VersionSettingsViewModel(gameRoot, versionId, versionType);
        DataContext = VM;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            CloseButton.Click += (_, _) => Window.GetWindow(this)?.Close();
        };
    }
}
