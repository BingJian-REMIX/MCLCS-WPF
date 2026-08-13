using System.Diagnostics;
using System.Windows;
using MCLCS.Core.Update;
using MCLCS.Core.Utils;

namespace MCLCS.App.Views;

/// <summary>
/// 更新可用时的模态弹窗：展示新版本号与更新日志（来自 GitHub 镜像 latest.json 的 changelog 字段），
/// 提供「下载更新」（优先调用 winget 安装 MCLCS-v{版本}-win-x64.zip，失败回退浏览器打开发布页）与「稍后」按钮。
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

        ChangelogBox.Text = string.IsNullOrWhiteSpace(result.Changelog)
            ? "（无法获取更新日志，请点击下方「前往下载」在发布页查看详情）"
            : result.Changelog;

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
        // 优先用 winget 拉取安装包（MCLCS-v{版本}-win-x64.zip）；winget 不可用或失败时回退到浏览器打开发布页。
        if (!TryWingetUpdate() && !string.IsNullOrWhiteSpace(_result.DownloadUrl))
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

    /// <summary>尝试通过 winget 安装/更新到最新版本；成功启动 winget 进程返回 true，否则返回 false。</summary>
    private bool TryWingetUpdate()
    {
        if (string.IsNullOrWhiteSpace(_result.LatestVersion))
            return false;
        try
        {
            var args = $"install {GameConstants.WingetPackageId} " +
                       $"--version {_result.LatestVersion} " +
                       "--accept-package-agreements --accept-source-agreements --scope user";
            Process.Start(new ProcessStartInfo("winget", args) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();
}
