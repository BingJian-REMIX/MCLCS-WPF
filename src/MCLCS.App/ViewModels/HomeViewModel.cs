using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;
using MCLCS.Core.Profiles;
using MCLCS.Core.Recommend;
using MCLCS.Core.Statistics;
using MCLCS.Core.Mvvm;
using MCLCS.App.Services;

namespace MCLCS.App.ViewModels;

/// <summary>主页视图模型：快速启动 + 游玩统计 + 推荐预览。</summary>
public class HomeViewModel : ObservableObject
{
    private ObservableCollection<VersionEntry> _versions = new();
    private VersionEntry? _selectedVersion;
    private string _statusMessage = "";
    private PlayStats _stats = new();
    private ObservableCollection<RecommendationItem> _recommendations = new();

    public ObservableCollection<VersionEntry> Versions
    {
        get => _versions;
        set => SetField(ref _versions, value);
    }

    public VersionEntry? SelectedVersion
    {
        get => _selectedVersion;
        set => SetField(ref _selectedVersion, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public PlayStats Stats
    {
        get => _stats;
        set => SetField(ref _stats, value);
    }

    public ObservableCollection<RecommendationItem> Recommendations
    {
        get => _recommendations;
        set => SetField(ref _recommendations, value);
    }

    public ICommand LaunchCommand { get; }
    public ICommand RefreshCommand { get; }

    public HomeViewModel()
    {
        LaunchCommand = new AsyncRelayCommand(_ => LaunchAsync(), _ => SelectedVersion is not null);
        RefreshCommand = new RelayCommand(_ => Refresh());
        Refresh();
        _ = LoadStatsAndRecommendAsync();
    }

    public void Refresh()
    {
        Versions = new ObservableCollection<VersionEntry>(
            LauncherService.Instance.ListInstalledVersions()
                .Select(t => new VersionEntry { Id = t.Id, Type = t.Type }));
        SelectedVersion = Versions.FirstOrDefault();
        StatusBarViewModel.Current.RefreshAsync();
    }

    private async Task LaunchAsync()
    {
        if (SelectedVersion is null) return;
        await LaunchCoordinator.LaunchAsync(SelectedVersion.Id, s => StatusMessage = s);
    }

    private async Task LoadStatsAndRecommendAsync()
    {
        try
        {
            var gameRoot = LauncherService.Instance.GameRoot;
            Stats = PlaytimeTracker.Load(gameRoot);

            var profile = ProfileStore.Load(gameRoot);
            var list = await RecommendationEngine.BuildAsync(gameRoot, profile, new HttpClient(), null);
            Recommendations = new ObservableCollection<RecommendationItem>(list.Take(4).ToList());
        }
        catch
        {
            /* 推荐/统计加载失败时忽略，页面其余部分仍可用 */
        }
    }
}
