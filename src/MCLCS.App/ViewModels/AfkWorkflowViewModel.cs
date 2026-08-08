using System.Collections.ObjectModel;
using System.Windows.Input;
using MCLCS.Core.Mvvm;
using MCLCS.Core.Profiles;
using MCLCS.App.Services;

namespace MCLCS.App.ViewModels;

/// <summary>挂机工作流中一个动作的编辑模型。</summary>
public class AfkActionItem : ObservableObject
{
    private string _actionType = "F";
    private string _param = "";

    public string ActionType { get => _actionType; set { SetField(ref _actionType, value); UpdatePreview(); } }
    public string Param { get => _param; set { SetField(ref _param, value); UpdatePreview(); } }

    /// <summary>人类可读的标签。</summary>
    public string Display => ActionType switch
    {
        "F" => $"帧率限制 → {Param} FPS",
        "D" => $"渲染距离 → {Param} 区块",
        "V" => $"音量 → {Param}%",
        "L" => $"视角朝向 → {ParseDirection(Param)}",
        "K" => $"模拟按键 → {Param}",
        "C" => Param.Contains('-') ? $"鼠标连点 → {Param.Split('-')[0] switch { "0" => "左键", _ => "右键" }} 每{Param.Split('-').Last()}ms" : $"鼠标连点 → {Param}",
        "*" => Param == "0" ? "循环 (无限)" : $"循环 ({Param}次)",
        _ => $"{ActionType}:{Param}"
    };

    public string Token => $"{ActionType}{Param}";

    public static string ParseDirection(string param)
    {
        if (int.TryParse(param, out var d) && d is >= 0 and <= 7)
            return d switch { 0 => "北", 1 => "东北", 2 => "东", 3 => "东南", 4 => "南", 5 => "西南", 6 => "西", 7 => "西北" };
        return param;
    }

    public event Action? Changed;
    private void UpdatePreview() { OnPropertyChanged(nameof(Display)); OnPropertyChanged(nameof(Token)); Changed?.Invoke(); }
}

/// <summary>
/// 挂机工作流编辑器（规格 §4）：可视化编辑动作序列，生成/导入/保存 Token。
/// </summary>
public class AfkWorkflowViewModel : ObservableObject
{
    private ObservableCollection<AfkActionItem> _actions = new();
    private AfkActionItem? _selectedAction;
    private string _workflowName = "";
    private ObservableCollection<string> _savedNames = new();
    private string _importToken = "";
    private string _statusMessage = "";

    public ObservableCollection<AfkActionItem> Actions { get => _actions; set => SetField(ref _actions, value); }
    public AfkActionItem? SelectedAction { get => _selectedAction; set => SetField(ref _selectedAction, value); }
    public string WorkflowName { get => _workflowName; set => SetField(ref _workflowName, value); }
    public ObservableCollection<string> SavedNames { get => _savedNames; set => SetField(ref _savedNames, value); }

    /// <summary>当前序列生成的 Token。</summary>
    public string TokenText => string.Join(";", Actions.Select(a => a.Token));

    public string ImportToken { get => _importToken; set => SetField(ref _importToken, value); }

    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

    public ICommand AddActionCommand { get; }
    public ICommand RemoveActionCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand CopyTokenCommand { get; }

    public AfkWorkflowViewModel()
    {
        AddActionCommand = new RelayCommand(_ => AddAction());
        RemoveActionCommand = new RelayCommand(_ => RemoveAction(), _ => SelectedAction is not null);
        MoveUpCommand = new RelayCommand(_ => Move(-1));
        MoveDownCommand = new RelayCommand(_ => Move(1));
        SaveCommand = new RelayCommand(_ => Save());
        LoadCommand = new RelayCommand(p => Load(p as string));
        DeleteCommand = new RelayCommand(p => Delete(p as string));
        ImportCommand = new RelayCommand(_ => Import());
        CopyTokenCommand = new RelayCommand(_ => CopyToken());

        RefreshSavedList();
    }

    private void RefreshSavedList()
    {
        var profile = ProfileStore.Load(LauncherService.Instance.GameRoot);
        SavedNames = new ObservableCollection<string>(profile.AfkWorkflows.Keys);
    }

    private void AddAction()
    {
        var item = new AfkActionItem();
        item.Changed += () => OnPropertyChanged(nameof(TokenText));
        Actions.Add(item);
        OnPropertyChanged(nameof(TokenText));
    }

    private void RemoveAction()
    {
        if (SelectedAction is null) return;
        Actions.Remove(SelectedAction);
        OnPropertyChanged(nameof(TokenText));
    }

    private void Move(int delta)
    {
        var idx = Actions.IndexOf(SelectedAction!);
        if (idx < 0) return;
        var newIdx = idx + delta;
        if (newIdx < 0 || newIdx >= Actions.Count) return;
        Actions.Move(idx, newIdx);
        OnPropertyChanged(nameof(TokenText));
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(WorkflowName)) { StatusMessage = "请输入工作流名称"; return; }
        var token = TokenText;
        if (string.IsNullOrEmpty(token)) { StatusMessage = "工作流不能为空"; return; }

        var profile = ProfileStore.Load(LauncherService.Instance.GameRoot);
        profile.AfkWorkflows[WorkflowName.Trim()] = token;
        ProfileStore.Save(profile);
        StatusMessage = $"已保存「{WorkflowName}」";
        RefreshSavedList();
        ToastService.Show("工作流", $"已保存 {WorkflowName}", ToastKind.Success);
    }

    private void Load(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var profile = ProfileStore.Load(LauncherService.Instance.GameRoot);
        if (!profile.AfkWorkflows.TryGetValue(name, out var token)) { StatusMessage = "未找到该工作流"; return; }

        ImportFromToken(token);
        WorkflowName = name;
        StatusMessage = $"已载入「{name}」";
    }

    private void Delete(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var profile = ProfileStore.Load(LauncherService.Instance.GameRoot);
        profile.AfkWorkflows.Remove(name);
        ProfileStore.Save(profile);
        RefreshSavedList();
        StatusMessage = $"已删除「{name}」";
    }

    private void Import()
    {
        if (string.IsNullOrWhiteSpace(ImportToken)) { StatusMessage = "请粘贴 Token"; return; }
        ImportFromToken(ImportToken.Trim());
        StatusMessage = "已从 Token 载入";
    }

    private void ImportFromToken(string token)
    {
        Actions.Clear();
        var parts = token.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Length < 2) continue;
            var type = part[0].ToString();
            var param = part[1..];
            // 跳过版本前缀
            if (char.IsLetter(type[0]) && type != "*" && type != "F" && type != "D" && type != "V" && type != "L" && type != "K" && type != "C")
            {
                if (part.StartsWith("v")) { type = part[1].ToString(); param = part[2..]; }
                else continue;
            }
            var item = new AfkActionItem { ActionType = type, Param = param };
            item.Changed += () => OnPropertyChanged(nameof(TokenText));
            Actions.Add(item);
        }
        OnPropertyChanged(nameof(TokenText));
    }

    private void CopyToken()
    {
        var token = TokenText;
        if (string.IsNullOrEmpty(token)) { StatusMessage = "没有可复制的内容"; return; }
        try { System.Windows.Clipboard.SetText(token); StatusMessage = "已复制到剪贴板"; }
        catch (Exception ex) { StatusMessage = $"复制失败：{ex.Message}"; }
    }
}
