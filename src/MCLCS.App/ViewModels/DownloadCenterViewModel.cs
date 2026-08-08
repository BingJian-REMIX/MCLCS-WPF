using System.Collections.ObjectModel;
using System.Windows.Input;
using MCLCS.Core.Download;
using MCLCS.Core.Models;
using MCLCS.Core.Mvvm;
using MCLCS.Core.Utils;
using MCLCS.App.Services;
using MCLCS.App.ViewModels;

namespace MCLCS.App.ViewModels;

public class SearchResultEntry
{
    public string ProjectId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";

    /// <summary>下载种类：mod（含 shader/resourcepack）/ modpack（整合包）/ map（地图）。</summary>
    public string Kind { get; init; } = "mod";

    /// <summary>地图 slug（Kind=map 时用于回查详情直链）。</summary>
    public string? Slug { get; init; }
}

/// <summary>下载队列中的一项。</summary>
public class DownloadQueueItem : ObservableObject
{
    public string ProjectId { get; init; } = "";
    public string Title { get; init; } = "";
    public string TargetDir { get; init; } = "";
    public string? GameVersion { get; init; }
    public LoaderType Loader { get; init; }

    /// <summary>队列项摘要（卡片副标题），用于队列列表二行展示。</summary>
    public string Summary { get; init; } = "";

    /// <summary>整合包来源（modrinth），仅 Kind=modpack 使用。</summary>
    public string Source { get; init; } = "modrinth";

    /// <summary>
    /// 队列项类别，决定执行时走哪条下载/安装路径：
    /// mod / shader / resourcepack（Modrinth 文件下载）、modpack（整合包）、map（像素茶艺地图）、
    /// version（Minecraft 版本安装，配合 <see cref="InstallLoader"/>）。
    /// </summary>
    public string Kind { get; init; } = "mod";

    /// <summary>地图 slug（Kind=map 时用于回查详情直链）。</summary>
    public string? Slug { get; init; }

    /// <summary>版本安装所选加载器（none / forge / fabric / neoforge / quilt），仅 Kind=version 使用。</summary>
    public string InstallLoader { get; init; } = "none";

    public CancellationTokenSource? Cts { get; set; }

    private string _status = "排队中";
    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set => SetField(ref _progress, value);
    }
}

public class DownloadCenterViewModel : ObservableObject
{
    private string _query = "";
    private string _selectedLoader = "Any";
    private string _selectedGameVersion = "";
    private string _selectedProjectType = "mod";
    private ObservableCollection<SearchResultEntry> _results = new();
    private SearchResultEntry? _selectedResult;
    private string _statusMessage = "";
    private bool _isBusy;

    private ObservableCollection<DownloadQueueItem> _queue = new();

    public ObservableCollection<string> Loaders { get; } = new() { "Any", "Fabric", "Forge", "Quilt" };
    public ObservableCollection<string> ProjectTypes { get; } = new() { "mod", "shader", "resourcepack" };
    public ObservableCollection<string> GameVersions { get; } = new() { "" };

    public string Query
    {
        get => _query;
        set => SetField(ref _query, value);
    }

    public string SelectedLoader
    {
        get => _selectedLoader;
        set => SetField(ref _selectedLoader, value);
    }

    public string SelectedGameVersion
    {
        get => _selectedGameVersion;
        set => SetField(ref _selectedGameVersion, value);
    }

    public string SelectedProjectType
    {
        get => _selectedProjectType;
        set => SetField(ref _selectedProjectType, value);
    }

    public ObservableCollection<SearchResultEntry> Results
    {
        get => _results;
        set => SetField(ref _results, value);
    }

    public SearchResultEntry? SelectedResult
    {
        get => _selectedResult;
        set => SetField(ref _selectedResult, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public ObservableCollection<DownloadQueueItem> Queue
    {
        get => _queue;
        set => SetField(ref _queue, value);
    }

    public ICommand LoadVersionsCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand EnqueueCommand { get; }
    public ICommand StartQueueCommand { get; }
    public ICommand PauseItemCommand { get; }
    public ICommand CancelItemCommand { get; }

    public DownloadCenterViewModel()
    {
        LoadVersionsCommand = new AsyncRelayCommand(_ => LoadGameVersionsAsync());
        SearchCommand = new AsyncRelayCommand(_ => SearchAsync(), _ => !IsBusy);
        EnqueueCommand = new RelayCommand(_ => Enqueue());
        StartQueueCommand = new AsyncRelayCommand(_ => StartQueueAsync(), _ => !IsBusy);
        PauseItemCommand = new RelayCommand(p => PauseItem(p as DownloadQueueItem));
        CancelItemCommand = new RelayCommand(p => CancelItem(p as DownloadQueueItem));
        _ = LoadGameVersionsAsync();
    }

    private async Task LoadGameVersionsAsync()
    {
        var versions = await LauncherService.Instance.GetVanillaVersionsAsync();
        GameVersions.Clear();
        GameVersions.Add(""); // Any
        foreach (var v in versions) GameVersions.Add(v);
    }

    private async Task SearchAsync()
    {
        IsBusy = true;
        try
        {
            var loader = SelectedLoader == "Any" ? LoaderType.Any : Enum.Parse<LoaderType>(SelectedLoader);
            var type = SelectedProjectType switch
            {
                "shader" => ModrinthProjectType.Shader,
                "resourcepack" => ModrinthProjectType.ResourcePack,
                _ => ModrinthProjectType.Mod
            };
            var list = await LauncherService.Instance.SearchModsAsync(Query, string.IsNullOrEmpty(SelectedGameVersion) ? null : SelectedGameVersion, loader, type);
            Results = new ObservableCollection<SearchResultEntry>(
                list.Select(h => new SearchResultEntry { ProjectId = h.ProjectId, Title = h.Title, Summary = h.Description }));
            StatusMessage = Results.Count > 0 ? $"找到 {Results.Count} 个结果" : "未找到结果";
        }
        catch (Exception ex)
        {
            StatusMessage = $"搜索失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Enqueue()
    {
        if (SelectedResult is null) { StatusMessage = "请先选择一个结果"; return; }
        var targetDir = SelectedProjectType switch
        {
            "shader" => PathEx.ShaderPacksDir(GameConstants.DefaultGameRoot),
            "resourcepack" => PathEx.ResourcePacksDir(GameConstants.DefaultGameRoot),
            _ => PathEx.ModsDir(GameConstants.DefaultGameRoot)
        };
        var loader = SelectedLoader == "Any" ? LoaderType.Any : Enum.Parse<LoaderType>(SelectedLoader);
        Queue.Add(new DownloadQueueItem
        {
            ProjectId = SelectedResult.ProjectId,
            Title = SelectedResult.Title,
            TargetDir = targetDir,
            GameVersion = string.IsNullOrEmpty(SelectedGameVersion) ? null : SelectedGameVersion,
            Loader = loader
        });
        StatusMessage = $"已加入队列：{SelectedResult.Title}（共 {Queue.Count} 项）";
    }

    private async Task StartQueueAsync()
    {
        IsBusy = true;
        try
        {
            foreach (var item in Queue.Where(q => q.Status is "排队中" or "已暂停").ToList())
            {
                item.Cts = new CancellationTokenSource();
                item.Status = "下载中";
                item.Progress = 0;
                var ok = false;
                try
                {
                    var local = item;
                    ok = await LauncherService.Instance.DownloadModAsync(
                        item.ProjectId, item.TargetDir, item.GameVersion, item.Loader,
                        new Progress<double>(p =>
                        {
                            local.Progress = p * 100;
                            StatusBarViewModel.Current.DownloadProgress = p * 100;
                            StatusBarViewModel.Current.DownloadText = $"下载 {local.Title}：{p:P0}";
                        }),
                        item.Cts.Token);
                    if (item.Cts.Token.IsCancellationRequested && item.Status != "已暂停")
                        item.Status = "已取消";
                    else
                        item.Status = ok ? "已完成" : "失败";
                }
                catch (OperationCanceledException)
                {
                    item.Status = "已取消";
                }
                catch
                {
                    item.Status = "失败";
                }
                item.Progress = item.Status == "已完成" ? 100 : item.Progress;
            }
            StatusBarViewModel.Current.DownloadText = "下载队列完成";
            StatusBarViewModel.Current.DownloadProgress = 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PauseItem(DownloadQueueItem? item)
    {
        if (item is null) return;
        item.Cts?.Cancel();
        if (item.Status == "下载中") item.Status = "已暂停";
    }

    private void CancelItem(DownloadQueueItem? item)
    {
        if (item is null) return;
        item.Cts?.Cancel();
        item.Status = "已取消";
    }
}
