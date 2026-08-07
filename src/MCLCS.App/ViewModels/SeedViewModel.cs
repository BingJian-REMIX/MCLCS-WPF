using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using MCLCS.Core.Mvvm;
using MCLCS.Core.Profiles;
using MCLCS.Core.Save;
using MCLCS.Core.Toolbox;
using MCLCS.App.Services;

namespace MCLCS.App.ViewModels;

/// <summary>种子库面板：提取存档种子、按种子创建世界、在线搜索热门种子。</summary>
public class SeedViewModel : ObservableObject
{
    private ObservableCollection<string> _saves = new();
    private string _selectedSave = "";
    private long? _extractedSeed;
    private string _newWorldName = "";
    private string _newSeedText = "";
    private ObservableCollection<SeedEntry> _searchResults = new();
    private string _searchQuery = "";
    private string _statusMessage = "";
    private bool _isBusy;

    public ObservableCollection<string> Saves
    {
        get => _saves;
        set => SetField(ref _saves, value);
    }

    public string SelectedSave
    {
        get => _selectedSave;
        set
        {
            if (SetField(ref _selectedSave, value))
                _ = ExtractAsync();
        }
    }

    public long? ExtractedSeed
    {
        get => _extractedSeed;
        set => SetField(ref _extractedSeed, value);
    }

    public string NewWorldName
    {
        get => _newWorldName;
        set => SetField(ref _newWorldName, value);
    }

    public string NewSeedText
    {
        get => _newSeedText;
        set => SetField(ref _newSeedText, value);
    }

    public ObservableCollection<SeedEntry> SearchResults
    {
        get => _searchResults;
        set => SetField(ref _searchResults, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetField(ref _searchQuery, value);
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

    public ICommand RefreshSavesCommand { get; }
    public ICommand CreateWorldCommand { get; }
    public ICommand SearchCommand { get; }

    public SeedViewModel()
    {
        RefreshSavesCommand = new RelayCommand(_ => RefreshSaves());
        CreateWorldCommand = new RelayCommand(_ => CreateWorld());
        SearchCommand = new AsyncRelayCommand(_ => SearchAsync(), _ => !IsBusy);
        RefreshSaves();
    }

    public void RefreshSaves()
    {
        var savesDir = SaveCompatibilityDetector.SavesDir(LauncherService.Instance.GameRoot);
        Saves = new ObservableCollection<string>(
            Directory.Exists(savesDir) ? Directory.GetDirectories(savesDir).Select(Path.GetFileName)! : new List<string>());
    }

    private void CreateWorld()
    {
        if (string.IsNullOrWhiteSpace(NewWorldName))
        {
            StatusMessage = "请填写新世界名称";
            return;
        }
        if (!long.TryParse(NewSeedText, out var seed))
        {
            StatusMessage = "种子需为整数";
            return;
        }
        var savesDir = SaveCompatibilityDetector.SavesDir(LauncherService.Instance.GameRoot);
        var path = SeedLibrary.CreateWorld(savesDir, NewWorldName, seed);
        StatusMessage = $"已创建世界「{NewWorldName}」（种子 {seed}）于 {path}";
        RefreshSaves();
    }

    private async Task ExtractAsync()
    {
        if (string.IsNullOrEmpty(SelectedSave)) { ExtractedSeed = null; return; }
        var savesDir = SaveCompatibilityDetector.SavesDir(LauncherService.Instance.GameRoot);
        var savePath = Path.Combine(savesDir, SelectedSave);
        ExtractedSeed = SeedLibrary.ExtractSeed(savePath);
        StatusMessage = ExtractedSeed.HasValue
            ? $"「{SelectedSave}」的种子：{ExtractedSeed}"
            : $"未能从「{SelectedSave}」读取种子";
    }

    private async Task SearchAsync()
    {
        IsBusy = true;
        try
        {
            var list = await SeedLibrary.SearchSeedsAsync(SearchQuery, null, null);
            SearchResults = new ObservableCollection<SeedEntry>(list);
            StatusMessage = SearchResults.Count > 0 ? $"找到 {SearchResults.Count} 个种子" : "未找到种子（可能网络不可用）";
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
}
