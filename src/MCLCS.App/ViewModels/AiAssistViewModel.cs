using System.Windows.Input;
using MCLCS.Core.Ai;
using MCLCS.Core.Mvvm;
using MCLCS.Core.Profiles;
using MCLCS.Core.Statistics;
using MCLCS.App.Services;

namespace MCLCS.App.ViewModels;

/// <summary>AI 助手面板的一个功能项。</summary>
public class AiFunctionItem : ObservableObject
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Emoji { get; init; } = "";

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }
}

/// <summary>AI 助手面板：左侧功能列表 + 右侧对话/结果区域（规格 2.3 面板 15）。</summary>
public class AiAssistViewModel : ObservableObject
{
    // ---- 功能列表 ----
    public AiFunctionItem[] Functions { get; } =
    {
        new() { Id = "crash", Label = "崩溃解读", Emoji = "\U0001F4A5" },
        new() { Id = "recommend", Label = "推荐理由", Emoji = "\U0001F48E" },
        new() { Id = "translate", Label = "Mod 翻译", Emoji = "\U0001F30D" },
        new() { Id = "summary", Label = "年度总结", Emoji = "\U0001F4CA" },
    };

    private AiFunctionItem _selectedFunction;
    public AiFunctionItem SelectedFunction
    {
        get => _selectedFunction;
        set
        {
            if (!SetField(ref _selectedFunction, value)) return;
            foreach (var f in Functions) f.IsSelected = (f == value);
            OnPropertyChanged(nameof(ShowCrash));
            OnPropertyChanged(nameof(ShowRecommend));
            OnPropertyChanged(nameof(ShowTranslate));
            OnPropertyChanged(nameof(ShowSummary));
        }
    }

    // 面板可见性（按选中的功能）
    public bool ShowCrash => SelectedFunction?.Id == "crash";
    public bool ShowRecommend => SelectedFunction?.Id == "recommend";
    public bool ShowTranslate => SelectedFunction?.Id == "translate";
    public bool ShowSummary => SelectedFunction?.Id == "summary";

    // ---- 通用 ----
    private string _statusMessage = "";
    private bool _isBusy;

    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public bool IsBusy { get => _isBusy; set => SetField(ref _isBusy, value); }
    public bool AiEnabled => Assistant.Config.Enabled;

    // ---- 崩溃解读 ----
    private string _crashText = "";
    private string _interpretation = "";
    public string CrashText { get => _crashText; set => SetField(ref _crashText, value); }
    public string Interpretation { get => _interpretation; set => SetField(ref _interpretation, value); }

    // ---- 推荐理由 ----
    private string _recommendInput = "";
    private string _recommendOutput = "";
    public string RecommendInput { get => _recommendInput; set => SetField(ref _recommendInput, value); }
    public string RecommendOutput { get => _recommendOutput; set => SetField(ref _recommendOutput, value); }

    // ---- Mod 翻译 ----
    private string _modText = "";
    private string _translated = "";
    public string ModText { get => _modText; set => SetField(ref _modText, value); }
    public string Translated { get => _translated; set => SetField(ref _translated, value); }

    // ---- 年度总结 ----
    private string _annualSummary = "";
    public string AnnualSummary { get => _annualSummary; set => SetField(ref _annualSummary, value); }

    public ICommand InterpretCommand { get; }
    public ICommand TranslateCommand { get; }
    public ICommand RecommendCommand { get; }
    public ICommand SummaryCommand { get; }

    public AiAssistViewModel()
    {
        _selectedFunction = Functions[0];
        SelectedFunction = Functions[0]; // 触发面板可见性

        InterpretCommand = new AsyncRelayCommand(_ => InterpretAsync(), _ => !IsBusy);
        TranslateCommand = new AsyncRelayCommand(_ => TranslateAsync(), _ => !IsBusy);
        RecommendCommand = new AsyncRelayCommand(_ => RecommendAsync(), _ => !IsBusy);
        SummaryCommand = new AsyncRelayCommand(_ => SummaryAsync(), _ => !IsBusy);
    }

    private async Task InterpretAsync()
    {
        if (string.IsNullOrWhiteSpace(CrashText)) { StatusMessage = "请粘贴崩溃日志"; return; }
        IsBusy = true;
        try
        {
            Interpretation = await Assistant.InterpretCrashAsync(CrashText);
            StatusMessage = Assistant.Config.Enabled ? "已解读" : "已使用本地启发式解读";
        }
        finally { IsBusy = false; }
    }

    private async Task TranslateAsync()
    {
        if (string.IsNullOrWhiteSpace(ModText)) { StatusMessage = "请粘贴 Mod 描述"; return; }
        IsBusy = true;
        try { Translated = await Assistant.TranslateModDescriptionAsync(ModText); }
        finally { IsBusy = false; }
    }

    private async Task RecommendAsync()
    {
        if (string.IsNullOrWhiteSpace(RecommendInput)) { StatusMessage = "请描述你的玩法偏好"; return; }
        IsBusy = true;
        try
        {
            if (Assistant.Config.Enabled)
                RecommendOutput = await Assistant.InterpretCrashAsync($"请根据以下偏好推荐5个Minecraft Mod（仅列名称和简要理由）：{RecommendInput}");
            else
                RecommendOutput = "AI 未启用，请在设置中开启后使用此功能。\n\n你可以先在设置 → AI 助手中启用外部 API（填 Endpoint/Key）或本地 Ollama 部署。";
        }
        catch (Exception ex) { RecommendOutput = $"推荐失败：{ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task SummaryAsync()
    {
        if (!AiEnabled) { AnnualSummary = "AI 未启用，请在设置中开启后使用此功能。"; return; }
        IsBusy = true;
        try
        {
            var data = AnnualReport.GenerateFrom(LauncherService.Instance.GameRoot, DateTime.Now.Year);
            var md = data.HasData ? AnnualReport.RenderMarkdown(data) : "今年还没有游玩记录。";
            AnnualSummary = await Assistant.InterpretCrashAsync($"请将以下年度游戏报告总结成一段100字以内的话：\n{md}");
        }
        catch (Exception ex) { AnnualSummary = $"生成失败：{ex.Message}"; }
        finally { IsBusy = false; }
    }
}
