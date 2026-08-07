using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MCLCS.App.ViewModels;

namespace MCLCS.App.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel Vm => (SettingsViewModel)DataContext;

    public SettingsView()
    {
        InitializeComponent();
        CategoryList.SelectedIndex = 0;
        ShowCategory("General");
        // 回填 API Key 密文（PasswordBox 不参与 TwoWay 绑定）
        Loaded += (_, _) =>
        {
            if (Vm is not null)
            {
                AiKeyPw.Password = Vm.AiApiKey;
            }
        };
    }

    private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryList.SelectedItem is ListBoxItem item)
            ShowCategory(item.Tag as string ?? "General");
    }

    /// <summary>按分类 tag 切换可见的设置面板（General/Launch/...）。</summary>
    public void ShowCategory(string tag)
    {
        GridGeneral.Visibility = tag == "General" ? Visibility.Visible : Visibility.Collapsed;
        GridLaunch.Visibility = tag == "Launch" ? Visibility.Visible : Visibility.Collapsed;
        GridDownload.Visibility = tag == "Download" ? Visibility.Visible : Visibility.Collapsed;
        GridRecommend.Visibility = tag == "Recommend" ? Visibility.Visible : Visibility.Collapsed;
        GridAccounts.Visibility = tag == "Accounts" ? Visibility.Visible : Visibility.Collapsed;
        GridAi.Visibility = tag == "Ai" ? Visibility.Visible : Visibility.Collapsed;
        GridAppearance.Visibility = tag == "Appearance" ? Visibility.Visible : Visibility.Collapsed;
        GridAbout.Visibility = tag == "About" ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>由 MainWindow 全局侧边栏路由调用（id 对应 <see cref="SidebarModel.Settings"/>）。</summary>
    public void ShowSidebarItem(string id) => ShowCategory(id switch
    {
        "general" => "General",
        "launch" => "Launch",
        "download" => "Download",
        "recommend" => "Recommend",
        "account" => "Accounts",
        "ai" => "Ai",
        "appearance" => "Appearance",
        "about" => "About",
        _ => "General"
    });

    private void AddAuthlib_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.AddAuthlibAccount(vm.AuthlibServerUrl, vm.AuthlibEmail, AuthlibPw.Password);
    }

    // AI 助手：本地模型切换（未下载时按规格弹确认窗，取消则回退）
    private async void LocalModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LocalModelCombo.SelectedValue is string name && Vm is not null)
            await Vm.TrySelectLocalModelAsync(name);
    }

    // AI 助手：API Key 密文实时同步到配置
    private void AiKeyPw_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (Vm is not null) Vm.AiApiKey = AiKeyPw.Password;
    }

    private void OpenUrl_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is System.Windows.Documents.Hyperlink link && link.NavigateUri is not null)
                Process.Start(new ProcessStartInfo(link.NavigateUri.ToString()) { UseShellExecute = true });
        }
        catch { /* ignore */ }
    }
}
