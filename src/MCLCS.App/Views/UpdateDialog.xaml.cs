using System.Diagnostics;
using System.Windows;
using MCLCS.Core.Update;

namespace MCLCS.App.Views;

/// <summary>
/// 更新可用时的模态弹窗：展示新版本号与更新日志（来自 annotated git tag 消息），
/// 提供「前往下载」（打开 CNB 发布页）与「稍后」按钮。
/// 半透明遮罩覆盖整个主窗口，卡片居中（规格 1.4：弹窗模态居中、半透明遮罩、主操作按钮右置）。
/// </summary>
public partial class UpdateDialog : Window
{
    private readonly UpdateCheckResult _result;

    public UpdateDialog(UpdateCheckResult result)
    {
        InitializeComponent();
        _result = result;

        TitleText.Text = $"发现新版本 v{result.LatestVersion}";
        SubtitleText.Text = $"当前 {result.CurrentVersion} → 最新 {result.LatestVersion}" +
                            (result.Mandatory ? "（建议立即更新）" : "");

        ChangelogBox.Text = string.IsNullOrWhiteSpace(result.Notes)
            ? "（无法离线获取更新日志，请点击下方「前往下载」在发布页查看详情）"
            : result.Notes;

        // 让遮罩铺满 Owner 窗口，卡片在其上居中。
        Loaded += (_, _) =>
        {
            if (Owner is Window o && o.IsLoaded)
            {
                Left = o.Left;
                Top = o.Top;
                Width = o.ActualWidth;
                Height = o.ActualHeight;
            }
        };
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_result.DownloadUrl))
        {
            try
            {
                Process.Start(new ProcessStartInfo(_result.DownloadUrl) { UseShellExecute = true });
            }
            catch
            {
                // 打开浏览器失败不影响弹窗关闭
            }
        }
        Close();
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();
}
