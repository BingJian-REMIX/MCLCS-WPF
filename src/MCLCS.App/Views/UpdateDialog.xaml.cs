using System.Diagnostics;
using System.IO;
using System.Windows;
using MCLCS.App.Services;
using MCLCS.Core.Update;
using MCLCS.Core.Utils;

namespace MCLCS.App.Views;

/// <summary>
/// 更新可用时的模态弹窗：展示新版本号与更新日志（来自 CNB Pages latest.json 的 changelog 字段），
/// 提供「下载更新」（调用启动器内置下载器拉取 cnb 发布直链，解压后原地替换并接力启动新版本）与「稍后」按钮。
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
            ? "（无法获取更新日志，请点击下方「下载更新」在发布页查看详情）"
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

    /// <summary>点击「下载更新」：用启动器内置下载器拉取 cnb 发布直链，解压后原地替换并接力启动新版本。</summary>
    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        var url = _result.DownloadUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            TryOpenBrowser(GameConstants.CnbRepoUrl + "/-/releases");
            Close();
            return;
        }

        DownloadButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;
        StatusText.Text = "正在通过内置下载器获取更新包…";

        var version = _result.LatestVersion ?? GameConstants.LauncherVersion;
        var updRoot = Path.Combine(Path.GetTempPath(), "MCLCS", "updates");
        Directory.CreateDirectory(updRoot);
        var zipPath = Path.Combine(updRoot, $"MCLCS-v{version}-win-x64.zip");
        var extractDir = Path.Combine(updRoot, version);

        var progress = new Progress<double>(p =>
        {
            ProgressBar.Value = p;
            StatusText.Text = $"下载中… {Math.Round(p * 100)}%";
        });

        try
        {
            await LauncherService.Instance.DownloadFileAsync(url, zipPath, progress);
            StatusText.Text = "下载完成，正在解压…";
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
            Unzip.ExtractToDirectory(zipPath, extractDir);

            // 定位新版本主程序，由它接管并完成原地替换（避免覆盖正在运行的自身）
            var exe = Directory.GetFiles(extractDir, "MCLCS.App.exe", SearchOption.AllDirectories).FirstOrDefault()
                      ?? Directory.GetFiles(extractDir, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (exe is null) throw new FileNotFoundException("更新包中未找到启动器主程序。");

            var installDir = Path.GetDirectoryName(
                Environment.ProcessPath ?? AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!;
            StatusText.Text = "正在应用更新…";
            // 以 --apply-update 启动新副本并退出当前进程，释放被锁定的旧 exe
            Process.Start(new ProcessStartInfo(exe, $"--apply-update \"{installDir}\"") { UseShellExecute = true });
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"更新失败：{ex.Message}";
            TryOpenBrowser(url);
            DownloadButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
        }
    }

    private static void TryOpenBrowser(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* 打开浏览器失败不影响弹窗 */ }
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();
}
