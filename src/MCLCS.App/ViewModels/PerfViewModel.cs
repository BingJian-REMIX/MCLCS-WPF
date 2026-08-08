using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using MCLCS.Core.MultiInstance;
using MCLCS.Core.Mvvm;
using MCLCS.Core.Statistics;
using MCLCS.App.Services;

namespace MCLCS.App.ViewModels;

/// <summary>性能/实例监控面板：展示正在运行的游戏实例与游玩统计。</summary>
public class PerfViewModel : ObservableObject
{
    private ObservableCollection<RunningInstance> _instances = new();
    private PlayStats _stats = new();
    private string _statusMessage = "";

    public ObservableCollection<RunningInstance> Instances
    {
        get => _instances;
        set => SetField(ref _instances, value);
    }

    public PlayStats Stats
    {
        get => _stats;
        set => SetField(ref _stats, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public int ProcessorCount => Environment.ProcessorCount;
    public string MemoryUsageText => $"{(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024):N0} MB 可用";
    public string CpuUsageText
    {
        get
        {
            try
            {
                using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                cpuCounter.NextValue();
                System.Threading.Thread.Sleep(100);
                return $"{cpuCounter.NextValue():F0}%";
            }
            catch { return "—"; }
        }
    }

    public ICommand RefreshCommand { get; }

    public PerfViewModel()
    {
        RefreshCommand = new RelayCommand(_ => Refresh());
        Refresh();
    }

    public void Refresh()
    {
        Instances = new ObservableCollection<RunningInstance>(InstanceTracker.ListActive());
        Stats = PlaytimeTracker.Load(LauncherService.Instance.GameRoot);
        StatusMessage = Instances.Count > 0
            ? $"当前运行 {Instances.Count} 个游戏实例"
            : "没有正在运行的游戏实例";
    }
}
