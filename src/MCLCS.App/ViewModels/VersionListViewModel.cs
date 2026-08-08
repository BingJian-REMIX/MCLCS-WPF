using System.Collections.ObjectModel;
using System.Windows.Input;
using MCLCS.Core.Mvvm;
using MCLCS.App.Services;

namespace MCLCS.App.ViewModels;

public class VersionEntry
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = "";
    public string DisplayName => string.IsNullOrEmpty(Type) ? Id : $"{Id} ({Type})";
}

public class VersionListViewModel : ObservableObject
{
    private ObservableCollection<VersionEntry> _versions = new();
    private VersionEntry? _selectedVersion;
    private string _statusMessage = "";

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

    public ICommand RefreshCommand { get; }
    public ICommand LaunchCommand { get; }

    public VersionListViewModel()
    {
        RefreshCommand = new RelayCommand(_ => Refresh());
        LaunchCommand = new AsyncRelayCommand(_ => LaunchAsync());
        Refresh();
    }

    public void Refresh()
    {
        Versions = new ObservableCollection<VersionEntry>(
            LauncherService.Instance.ListInstalledVersions()
                .Select(t => new VersionEntry { Id = t.Id, Type = t.Type }));
        StatusMessage = Versions.Count > 0
            ? $"共发现 {Versions.Count} 个版本"
            : "暂无已安装版本，请前往「安装新版本」";
    }

    private async Task LaunchAsync()
    {
        if (SelectedVersion is null)
        {
            StatusMessage = "请先选择一个版本";
            return;
        }

        // 统一启动流程（含存档兼容检测、缺失前置安装、崩溃自动修复）由 LaunchCoordinator 负责
        await LaunchCoordinator.LaunchAsync(SelectedVersion.Id, s => StatusMessage = s);
    }
}
