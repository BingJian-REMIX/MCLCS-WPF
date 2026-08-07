using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using MCLCS.App.Services;
using MCLCS.Core.Mvvm;
using MCLCS.Core.Profiles;
using MCLCS.Core.Toolbox;

namespace MCLCS.App.ViewModels;

/// <summary>
/// 文件变更检测面板（工具箱面板 16，规格 2.3-16 / 3.13）：
/// 展示自上次「标记为已知」以来，手动丢入 mods/resourcepacks/shaderpacks 的新文件；
/// 支持重新扫描、标记为已知（重建基线）、以及本面板开关总开关（同步到 profile）。
/// </summary>
public class FileWatchViewModel : ObservableObject
{
    private ObservableCollection<FileChange> _changes = new();
    private bool _fileWatchEnabled = true;
    private string _statusMessage = "";
    private string _summary = "";
    private bool _isBusy;

    public ObservableCollection<FileChange> Changes
    {
        get => _changes;
        set => SetField(ref _changes, value);
    }

    /// <summary>总开关（设置 → 通用 → 文件变更检测）。</summary>
    public bool FileWatchEnabled
    {
        get => _fileWatchEnabled;
        set
        {
            if (!SetField(ref _fileWatchEnabled, value)) return;
            SaveEnabled();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string Summary
    {
        get => _summary;
        set => SetField(ref _summary, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand ResetBaselineCommand { get; }

    public FileWatchViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(), _ => !IsBusy);
        ResetBaselineCommand = new AsyncRelayCommand(_ => ResetAsync(), _ => !IsBusy);

        _fileWatchEnabled = ProfileStore.Load(GameRoot).FileWatchEnabled;
        _ = RefreshAsync();
    }

    private static string GameRoot => LauncherService.Instance.GameRoot;

    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            // 只看不更新基线：面板可反复刷新而不清掉「待确认」的列表
            var diff = await Task.Run(() => FileChangeDetector.PreviewChanges(GameRoot));
            Changes = new ObservableCollection<FileChange>(diff.Changes);
            Summary = diff.HasChanges ? diff.Summary : "与基线一致，没有未确认的变更";
            StatusMessage = diff.HasChanges
                ? "下列变更尚未「标记为已知」，启动前会再次提醒"
                : "没有未确认的变更";
        }
        catch (Exception ex)
        {
            StatusMessage = $"扫描失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ResetAsync()
    {
        if (Changes.Count == 0)
        {
            StatusMessage = "当前没有待确认的变更，无需标记";
            return;
        }

        IsBusy = true;
        try
        {
            var ok = await Task.Run(() => FileChangeDetector.ResetBaseline(GameRoot));
            StatusMessage = ok
                ? $"已把 {Changes.Count} 个文件标记为已知（重建基线）"
                : "重建基线失败";
            ToastService.Show("文件变更检测", StatusMessage, ok ? ToastKind.Success : ToastKind.Error);
            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SaveEnabled()
    {
        try
        {
            var p = ProfileStore.Load(GameRoot);
            p.FileWatchEnabled = _fileWatchEnabled;
            ProfileStore.Save(p);
            StatusMessage = _fileWatchEnabled ? "已开启文件变更检测" : "已关闭文件变更检测";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存设置失败：{ex.Message}";
        }
    }
}
