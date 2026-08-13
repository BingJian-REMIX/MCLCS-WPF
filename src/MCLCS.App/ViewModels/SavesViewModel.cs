using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using MCLCS.Core.Mvvm;
using MCLCS.Core.Profiles;
using MCLCS.Core.Save;
using MCLCS.App.Services;

namespace MCLCS.App.ViewModels;

/// <summary>「存档」标签页的单行：一个世界及其兼容性/备份信息。</summary>
public class SaveRow : ObservableObject
{
    public string SaveName { get; init; } = "";
    public string SavePath { get; init; } = "";

    private string _currentVersion = "";
    private int _dataVersion;
    private string _compatText = "";
    private string _severityColor = "#888888";
    private bool _hasBackup;
    private string _backupText = "";
    private string _corruptionText = "";
    private string _corruptionColor = "#888888";
    private bool _hasCorruption;

    public string CurrentVersion { get => _currentVersion; set => SetField(ref _currentVersion, value); }
    public int DataVersion { get => _dataVersion; set => SetField(ref _dataVersion, value); }
    public string CompatText { get => _compatText; set => SetField(ref _compatText, value); }
    public string SeverityColor { get => _severityColor; set => SetField(ref _severityColor, value); }
    public bool HasBackup { get => _hasBackup; set => SetField(ref _hasBackup, value); }
    public string BackupText { get => _backupText; set => SetField(ref _backupText, value); }

    /// <summary>存档损坏检测结果文本。</summary>
    public string CorruptionText { get => _corruptionText; set => SetField(ref _corruptionText, value); }
    /// <summary>损坏指示颜色。</summary>
    public string CorruptionColor { get => _corruptionColor; set => SetField(ref _corruptionColor, value); }
    /// <summary>是否存在致命损坏（无法加载）。</summary>
    public bool HasCorruption { get => _hasCorruption; set => SetField(ref _hasCorruption, value); }
}

/// <summary>
/// 「存档」标签页视图模型（§二.4 兼容性检测 + §三 降级 / 备份回滚）。
/// 选择目标游戏版本后可扫描全部存档的兼容性；对每个存档可一键降级（方案 A）或回滚到备份。
/// </summary>
public class SavesViewModel : ObservableObject
{
    private string _targetVersion = "";
    private ObservableCollection<string> _availableVersions = new();
    private ObservableCollection<SaveRow> _saves = new();
    private string _statusMessage = "";
    private bool _isBusy;

    public ObservableCollection<string> AvailableVersions
    {
        get => _availableVersions;
        set => SetField(ref _availableVersions, value);
    }

    public string TargetVersion
    {
        get => _targetVersion;
        set => SetField(ref _targetVersion, value);
    }

    public ObservableCollection<SaveRow> Saves
    {
        get => _saves;
        set => SetField(ref _saves, value);
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

    public ICommand ScanCommand { get; }
    public ICommand ScanCorruptionCommand { get; }
    public ICommand DowngradeCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand BackupSaveCommand { get; }
    public ICommand DeleteSaveCommand { get; }
    public ICommand ExtractSeedCommand { get; }

    public SavesViewModel()
    {
        ScanCommand = new AsyncRelayCommand(_ => ScanAsync(), _ => !IsBusy);
        ScanCorruptionCommand = new AsyncRelayCommand(_ => ScanCorruptionAsync(), _ => !IsBusy);
        DowngradeCommand = new AsyncRelayCommand(p => DowngradeAsync(p as string), _ => !IsBusy);
        RestoreCommand = new AsyncRelayCommand(p => RestoreAsync(p as string), _ => !IsBusy);
        BackupSaveCommand = new AsyncRelayCommand(p => BackupSaveAsync(p as string), _ => !IsBusy);
        DeleteSaveCommand = new AsyncRelayCommand(p => DeleteSaveAsync(p as string), _ => !IsBusy);
        ExtractSeedCommand = new RelayCommand(p => ExtractSeed(p as string));
        RefreshVersions();
        _ = ScanAsync();
    }

    private void RefreshVersions()
    {
        var gameRoot = LauncherService.Instance.GameRoot;
        var installed = LauncherService.Instance.ListInstalledVersions().Select(v => v.Id);
        var known = DataVersionMap.KnownVersions();
        AvailableVersions = new ObservableCollection<string>(
            installed.Concat(known).Distinct().OrderBy(v => v).ToList());
        TargetVersion = installed.FirstOrDefault() ?? known.LastOrDefault() ?? "";
    }

    private async Task ScanAsync()
    {
        IsBusy = true;
        try
        {
            var gameRoot = LauncherService.Instance.GameRoot;
            var target = TargetVersion;
            var rows = new ObservableCollection<SaveRow>();

            var savesDir = SaveCompatibilityDetector.SavesDir(gameRoot);
            if (!Directory.Exists(savesDir))
            {
                Saves = rows;
                StatusMessage = "未找到 saves 目录。";
                return;
            }

            var reports = string.IsNullOrEmpty(target)
                ? new List<SaveCompatibilityReport>()
                : SaveCompatibilityDetector.Scan(gameRoot, target);

            foreach (var dir in Directory.GetDirectories(savesDir))
            {
                var name = Path.GetFileName(dir);
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\.backup-\d{14}$")) continue;

                var dv = SaveDowngrader.GetSaveDataVersion(dir);
                var curVer = DataVersionMap.DescribeDataVersion(dv);
                var row = new SaveRow
                {
                    SaveName = name,
                    SavePath = dir,
                    CurrentVersion = curVer,
                    DataVersion = dv
                };

                var rep = reports.FirstOrDefault(r => r.SaveName == name);
                if (rep is not null)
                {
                    row.CompatText = rep.Message;
                    row.SeverityColor = rep.Compatible ? "#5BBF6A"
                        : rep.Severity == SaveCompatibilitySeverity.MuchNewer ? "#E0533A"
                        : "#E0A040";
                }
                else
                {
                    row.CompatText = string.IsNullOrEmpty(target)
                        ? "选择目标游戏版本后可检测兼容性。"
                        : "兼容。";
                    row.SeverityColor = "#5BBF6A";
                }

                var backups = SaveCompatibilityDetector.FindBackups(savesDir, name);
                row.HasBackup = backups.Count > 0;
                row.BackupText = backups.Count > 0
                    ? $"{backups.Count} 个备份（最新 {backups[^1].CreatedUtc:yyyy-MM-dd HH:mm}）"
                    : "无备份";

                // 存档损坏检测（只读，不修复）
                var corrupt = SaveCorruptionDetector.ScanSingle(dir);
                if (corrupt is not null && corrupt.Severity != SaveCorruptionSeverity.Ok)
                {
                    row.HasCorruption = corrupt.IsCorrupt;
                    row.CorruptionColor = corrupt.Severity == SaveCorruptionSeverity.Corrupt ? "#E0533A" : "#E0A040";
                    row.CorruptionText = corrupt.Summary;
                }
                else
                {
                    row.CorruptionText = "未检测到损坏。";
                    row.CorruptionColor = "#5BBF6A";
                }

                rows.Add(row);
            }

            Saves = rows;
            StatusMessage = rows.Count > 0
                ? $"共 {rows.Count} 个存档（目标版本：{target}）"
                : "未发现存档。";
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

    private async Task ScanCorruptionAsync()
    {
        IsBusy = true;
        try
        {
            var gameRoot = LauncherService.Instance.GameRoot;
            var reports = await Task.Run(() => SaveCorruptionDetector.Scan(gameRoot));

            var corrupt = reports.Where(r => r.IsCorrupt).ToList();
            var warn = reports.Where(r => !r.IsCorrupt && r.Severity == SaveCorruptionSeverity.Warning).ToList();

            // 同步刷新列表中各行的损坏状态
            foreach (var row in Saves)
            {
                var hit = reports.FirstOrDefault(r => r.SaveName == row.SaveName);
                if (hit is null || hit.Severity == SaveCorruptionSeverity.Ok)
                {
                    row.HasCorruption = false;
                    row.CorruptionColor = "#5BBF6A";
                    row.CorruptionText = "未检测到损坏。";
                }
                else
                {
                    row.HasCorruption = hit.IsCorrupt;
                    row.CorruptionColor = hit.Severity == SaveCorruptionSeverity.Corrupt ? "#E0533A" : "#E0A040";
                    row.CorruptionText = hit.Summary;
                }
            }

            if (corrupt.Count == 0 && warn.Count == 0)
                StatusMessage = "存档损坏扫描完成：未发现损坏。";
            else
                StatusMessage = $"存档损坏扫描完成：{corrupt.Count} 个可能损坏，{warn.Count} 个需注意。";
            ToastService.Show("存档损坏检测",
                $"{corrupt.Count} 个可能损坏，{warn.Count} 个需注意。",
                corrupt.Count > 0 ? ToastKind.Warning : ToastKind.Success);
        }
        catch (Exception ex)
        {
            StatusMessage = $"损坏扫描失败：{ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private async Task DowngradeAsync(string? saveName)
    {
        if (string.IsNullOrEmpty(saveName) || string.IsNullOrEmpty(TargetVersion)) return;
        IsBusy = true;
        try
        {
            var gameRoot = LauncherService.Instance.GameRoot;
            var savePath = Path.Combine(SaveCompatibilityDetector.SavesDir(gameRoot), saveName);
            var plan = await SaveDowngrader.DowngradeAsync(savePath, TargetVersion, DowngradeMethod.QuickModifyDataVersion);
            StatusMessage = plan.Success
                ? $"已降级 {saveName} 到 {TargetVersion}（已备份原档）。"
                : $"降级失败：{plan.ErrorMessage}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"降级异常：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            await ScanAsync();
        }
    }

    private async Task RestoreAsync(string? saveName)
    {
        if (string.IsNullOrEmpty(saveName)) return;
        IsBusy = true;
        try
        {
            var gameRoot = LauncherService.Instance.GameRoot;
            var savesDir = SaveCompatibilityDetector.SavesDir(gameRoot);
            var backups = SaveCompatibilityDetector.FindBackups(savesDir, saveName);
            if (backups.Count == 0)
            {
                StatusMessage = $"{saveName} 没有可回滚的备份。";
                return;
            }
            var latest = backups[^1];
            var savePath = Path.Combine(savesDir, saveName);
            var replaced = SaveDowngrader.RestoreBackupAsync(latest.BackupPath, savePath);
            StatusMessage = $"已回滚 {saveName} 到备份（当前档另存于 {Path.GetFileName(replaced)}）。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"回滚异常：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
            await ScanAsync();
        }
    }

    private async Task BackupSaveAsync(string? saveName)
    {
        if (string.IsNullOrEmpty(saveName)) return;
        IsBusy = true;
        try
        {
            var gameRoot = LauncherService.Instance.GameRoot;
            var sourceDir = Path.Combine(SaveCompatibilityDetector.SavesDir(gameRoot), saveName);
            var result = await Task.Run(() =>
                MCLCS.Core.Toolbox.BackupManager.Create(gameRoot, sourceDir, MCLCS.Core.Toolbox.BackupKind.Save,
                    $"手动备份 {saveName}", auto: false, policy: ProfileStore.Load(gameRoot).Backup));
            StatusMessage = result.Ok
                ? $"已备份 {saveName}（{result.Record?.SizeText ?? "?"}）"
                : $"备份失败：{result.Error}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"备份异常：{ex.Message}";
        }
        finally { IsBusy = false; await ScanAsync(); }
    }

    private async Task DeleteSaveAsync(string? saveName)
    {
        if (string.IsNullOrEmpty(saveName)) return;
        if (!UIService.Confirm($"确定删除存档「{saveName}」及其数据？操作不可撤销。", "确认删除")) return;

        IsBusy = true;
        try
        {
            var gameRoot = LauncherService.Instance.GameRoot;
            var saveDir = Path.Combine(SaveCompatibilityDetector.SavesDir(gameRoot), saveName);
            await Task.Run(() =>
            {
                if (Directory.Exists(saveDir)) Directory.Delete(saveDir, true);
            });
            StatusMessage = $"已删除存档 {saveName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除失败：{ex.Message}";
        }
        finally { IsBusy = false; await ScanAsync(); }
    }

    private void ExtractSeed(string? saveName)
    {
        if (string.IsNullOrEmpty(saveName)) return;
        try
        {
            var gameRoot = LauncherService.Instance.GameRoot;
            var levelDat = Path.Combine(SaveCompatibilityDetector.SavesDir(gameRoot), saveName, "level.dat");
            if (!File.Exists(levelDat)) { StatusMessage = "找不到 level.dat"; return; }
            long seed = 0;
            try
            {
                var raw = File.ReadAllText(levelDat);
                var idx = raw.IndexOf("RandomSeed", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    var start = raw.IndexOfAny(new[] { ':', ' ' }, idx + 10) + 1;
                    while (start < raw.Length && raw[start] == ' ') start++;
                    var end = raw.IndexOfAny(new[] { ',', '}', '\n', '\r' }, start);
                    if (end > start && long.TryParse(raw[start..end].Trim(), out var s)) seed = s;
                }
            }
            catch { StatusMessage = "种子提取失败"; return; }
            System.Windows.Clipboard.SetText(seed.ToString());
            StatusMessage = $"种子 {seed} 已复制到剪贴板";
            ToastService.Show("种子", $"{saveName}: {seed}", ToastKind.Success);
        }
        catch (Exception ex) { StatusMessage = $"提取失败: {ex.Message}"; }
    }
}
