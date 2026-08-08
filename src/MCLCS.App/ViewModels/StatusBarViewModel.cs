using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using MCLCS.Core.Launcher;
using MCLCS.Core.Localization;
using MCLCS.Core.MultiInstance;
using MCLCS.Core.Mvvm;
using MCLCS.Core.Toolbox;
using MCLCS.Core.Utils;
using MCLCS.App.Services;

namespace MCLCS.App.ViewModels;

/// <summary>
/// 全局状态栏视图模型（单例）。各业务页在下载/网络/启动过程中向此处写入进度，
/// 主窗口状态栏只读绑定到这里。下载进度由 <see cref="StatusBarViewModel"/> 维护，
/// 网络状态按需探测。
/// </summary>
public class StatusBarViewModel : ObservableObject
{
    /// <summary>全局单例。</summary>
    public static StatusBarViewModel Current { get; } = new();

    private string _javaVersionText = "检测中…";
    private int _installedCount;
    private double _downloadProgress;      // 0-100
    private string _downloadText = "空闲";
    private string _networkStatusText = "未探测";
    private bool _isNetworkOk;
    private int _runningInstances;
    private string _lastLog = "";

    public string JavaVersionText
    {
        get => _javaVersionText;
        set => SetField(ref _javaVersionText, value);
    }

    public int InstalledCount
    {
        get => _installedCount;
        set => SetField(ref _installedCount, value);
    }

    /// <summary>下载进度（0-100）。</summary>
    public double DownloadProgress
    {
        get => _downloadProgress;
        set => SetField(ref _downloadProgress, value);
    }

    public string DownloadText
    {
        get => _downloadText;
        set => SetField(ref _downloadText, value);
    }

    public string NetworkStatusText
    {
        get => _networkStatusText;
        set => SetField(ref _networkStatusText, value);
    }

    public bool IsNetworkOk
    {
        get => _isNetworkOk;
        set => SetField(ref _isNetworkOk, value);
    }

    public int RunningInstances
    {
        get => _runningInstances;
        set => SetField(ref _runningInstances, value);
    }

    public string LastLog
    {
        get => _lastLog;
        set => SetField(ref _lastLog, value);
    }

    public ICommand RefreshCommand { get; }

    // ---- 本地化属性（用于 StringFormat 替换） ----
    public string InstalledCountText => LocaleManager.Tf("status.installed", InstalledCount);
    public string RunningInstancesText => LocaleManager.Tf("status.running", RunningInstances);

    public StatusBarViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        LocaleManager.LocaleChanged += _ =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(InstalledCountText));
                OnPropertyChanged(nameof(RunningInstancesText));
            });
        };
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            InstalledCount = LauncherService.Instance.ListInstalledVersions().Count;
            RunningInstances = InstanceTracker.ActiveCount();

            var java = await JavaDetector.FindBestAsync(GameConstants.MinimumJavaMajorVersion);
            JavaVersionText = java is not null
                ? $"Java {java.MajorVersion}"
                : LocaleManager.Tf("status.no_java", GameConstants.MinimumJavaMajorVersion);
        }
        catch
        {
            JavaVersionText = "检测失败";
        }

        _ = ProbeNetworkAsync();
    }

    private async Task ProbeNetworkAsync()
    {
        try
        {
            var results = await NetworkDiagnostics.DiagnoseAsync(null, new HttpClient());
            var ok = results.Count(r => r.Reachable);
            IsNetworkOk = ok > 0;
            NetworkStatusText = $"{ok}/{results.Count} 可达";
        }
        catch
        {
            IsNetworkOk = false;
            NetworkStatusText = "探测失败";
        }
    }
}
