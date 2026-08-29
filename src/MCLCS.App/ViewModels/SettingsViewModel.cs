using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using MCLCS.Core.Ai;
using MCLCS.Core.Auth;
using MCLCS.Core.Hud;
using MCLCS.Core.Launcher;
using MCLCS.Core.Localization;
using MCLCS.Core.Mvvm;
using MCLCS.Core.Profiles;
using MCLCS.Core.Recommend;
using MCLCS.Core.Theme;
using MCLCS.Core.Update;
using MCLCS.Core.Utils;
using MCLCS.App.Services;
using MCLCS.App.Themes;

namespace MCLCS.App.ViewModels;

/// <summary>玩法偏好勾选项。</summary>
public class CategoryPref : ObservableObject
{
    public GameplayCategory Category { get; set; }
    public string Label { get; set; } = "";

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set => SetField(ref _isChecked, value);
    }
}

public class SettingsViewModel : ObservableObject
{
    // ---- 启动 ----
    private string _gamePath = "";
    private string _javaPath = "";
    private ObservableCollection<string> _detectedJavas = new();
    private int _maxMemoryMb = 2048;
    private string _username = "Player";
    private string _extraJvmArgs = "";
    private string _selectedRepairPolicy = "Ask";
    private string _selectedJavaVendor = "Auto";
    private string _selectedAutoInstallMods = "Ask";

    // ---- 通用 ----
    private string _selectedLanguage = "zh_CN";
    private bool _autoStartLauncher;
    private bool _minimizeToTray;
    private bool _animationsEnabled = true;
    private bool _fileWatchEnabled = true;

    // ---- 启动补充 ----
    private bool _prewarmEnabled;
    private bool _hudEnabled;
    private bool _launchCompatCheckEnabled = true;

    // ---- 下载 ----
    private string _selectedDownloadSource = "MirrorFirst";
    private int _maxConcurrentDownloads = 8;
    private bool _autoRepairResourcePacks = true;
    private bool _serverPackCacheEnabled = true;

    // ---- 推荐 ----
    private string _selectedIntelliRecommend = "Enabled";
    private ObservableCollection<CategoryPref> _categoryPreferences = new();

    // ---- AI 助手 ----
    private bool _aiEnabled;
    private string _aiMode = "External"; // 默认外部 API（零下载、填 Key 即用）
    private string _aiApiKey = "";
    private string _aiEndpoint = "https://api.openai.com/v1/chat/completions";
    private string _aiModel = "gpt-4o-mini";
    private bool _aiCrashInterpret = true;
    private bool _aiRecommendReason = true;
    private bool _aiModTranslate = true;

    // ---- AI 助手：本地部署（Ollama）----
    private string _selectedLocalModel = OllamaModels.Default.DisplayName;
    private string _lastCommittedModel = OllamaModels.Default.DisplayName;
    private bool _ollamaInstalled;
    private string _ollamaVersion = "";
    private bool _ollamaInstalling;
    private double _ollamaInstallProgress;
    private string _ollamaInstallText = "";
    private bool _modelDownloading;
    private double _modelDownloadProgress;
    private string _modelDownloadText = "";
    private bool _modelReady;
    private OllamaServiceStatus _ollamaServiceStatus = OllamaServiceStatus.NotRunning;
    private readonly HashSet<string> _pulledTags = new();
    private CancellationTokenSource? _ollamaInstallCts;
    private CancellationTokenSource? _modelCts;

    // ---- 外观 ----
    private string _selectedTheme = "Dark";
    private string _themeColor = "#3a7b4f";
    private string _backgroundImagePath = "";
    private double _fontScale = 1.0;
    private bool _highDpiEnabled;

    // ---- 关于 / 更新 ----
    private bool _autoUpdateCheck = true;
    private string _updateMessage = "";
    private string _launcherVersion = GameConstants.LauncherVersion;

    // ---- 账号 ----
    private ObservableCollection<AccountEntry> _accounts = new();
    private AccountEntry? _selectedAccount;
    private string _newOfflineName = "";
    private string _authlibServerUrl = "";
    private string _authlibEmail = "";
    private string _authlibPassword = "";

    private string _statusMessage = "";

    // ===== 启动 =====

    /// <summary>Minecraft 游戏目录（.minecraft），可自定义（bug #26）。留空表示使用系统默认。</summary>
    public string GamePath
    {
        get => _gamePath;
        set => SetField(ref _gamePath, value);
    }

    /// <summary>游戏目录输入框的水印提示，显示系统默认路径。</summary>
    public string DefaultGamePathHint => GameConstants.SystemGameRoot;

    public string JavaPath { get => _javaPath; set => SetField(ref _javaPath, value); }
    public ObservableCollection<string> DetectedJavas { get => _detectedJavas; set => SetField(ref _detectedJavas, value); }
    public int MaxMemoryMb { get => _maxMemoryMb; set => SetField(ref _maxMemoryMb, value); }
    public string Username { get => _username; set => SetField(ref _username, value); }
    public string ExtraJvmArgs { get => _extraJvmArgs; set => SetField(ref _extraJvmArgs, value); }
    public string SelectedRepairPolicy { get => _selectedRepairPolicy; set => SetField(ref _selectedRepairPolicy, value); }
    public string SelectedJavaVendor { get => _selectedJavaVendor; set => SetField(ref _selectedJavaVendor, value); }
    public string SelectedAutoInstallMods { get => _selectedAutoInstallMods; set => SetField(ref _selectedAutoInstallMods, value); }

    /// <summary>启动预热开关（规格 2.4 — 启动）。Off → false，Light/Full → true。</summary>
    public bool PrewarmEnabled
    {
        get => _prewarmEnabled;
        set => SetField(ref _prewarmEnabled, value);
    }

    /// <summary>HUD 叠加开关（规格 2.4 — 启动）。</summary>
    public bool HudEnabled
    {
        get => _hudEnabled;
        set => SetField(ref _hudEnabled, value);
    }

    /// <summary>启动前存档兼容性检测（规格 2.4 — 启动）。</summary>
    public bool LaunchCompatCheckEnabled
    {
        get => _launchCompatCheckEnabled;
        set => SetField(ref _launchCompatCheckEnabled, value);
    }

    // ===== 通用 =====
    public string SelectedLanguage { get => _selectedLanguage; set => SetField(ref _selectedLanguage, value); }
    public bool AutoStartLauncher { get => _autoStartLauncher; set => SetField(ref _autoStartLauncher, value); }
    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set
        {
            if (SetField(ref _minimizeToTray, value))
            {
                // bug #9：即时落盘，避免「切换后未点保存就最小化」导致托盘不生效
                try
                {
                    var p = ProfileStore.Load(GameConstants.DefaultGameRoot);
                    p.MinimizeToTray = value;
                    ProfileStore.Save(p);
                }
                catch { /* 忽略持久化失败 */ }
            }
        }
    }
    public bool AnimationsEnabled
    {
        get => _animationsEnabled;
        set
        {
            if (SetField(ref _animationsEnabled, value))
                MCLCS.App.MainWindow.AnimationsEnabled = value;   // 即时生效，无需重启（bug #4）
        }
    }

    /// <summary>文件变更检测开关（规格 2.4 — 通用）。</summary>
    public bool FileWatchEnabled
    {
        get => _fileWatchEnabled;
        set => SetField(ref _fileWatchEnabled, value);
    }

    /// <summary>游戏启动时音乐自动降音量 / 暂停（规格 2.3 面板 14，可配置）。代理到音乐播放器单例。</summary>
    public bool MusicAutoDuck
    {
        get => MusicPlayerViewModel.Instance.AutoDuck;
        set => MusicPlayerViewModel.Instance.AutoDuck = value;
    }

    /// <summary>启动器启动时自动续播上次音乐（bug #10）。代理到音乐播放器单例。</summary>
    public bool MusicResumeOnLaunch
    {
        get => MusicPlayerViewModel.Instance.ResumeOnLaunch;
        set => MusicPlayerViewModel.Instance.ResumeOnLaunch = value;
    }

    // ===== 下载 =====
    public string SelectedDownloadSource { get => _selectedDownloadSource; set => SetField(ref _selectedDownloadSource, value); }
    public int MaxConcurrentDownloads { get => _maxConcurrentDownloads; set => SetField(ref _maxConcurrentDownloads, value); }

    /// <summary>进服时自动修复资源包问题（规格 2.4 — 下载）。</summary>
    public bool AutoRepairResourcePacks
    {
        get => _autoRepairResourcePacks;
        set => SetField(ref _autoRepairResourcePacks, value);
    }

    /// <summary>服务器资源包缓存开关（规格 2.4 — 下载）。关闭时进服不缓存资源包。</summary>
    public bool ServerPackCacheEnabled
    {
        get => _serverPackCacheEnabled;
        set => SetField(ref _serverPackCacheEnabled, value);
    }

    // ===== 推荐 =====
    public string SelectedIntelliRecommend { get => _selectedIntelliRecommend; set => SetField(ref _selectedIntelliRecommend, value); }
    public ObservableCollection<CategoryPref> CategoryPreferences { get => _categoryPreferences; set => SetField(ref _categoryPreferences, value); }

    // ===== AI 助手 =====
    public bool AiEnabled
    {
        get => _aiEnabled;
        set
        {
            if (SetField(ref _aiEnabled, value))
            {
                OnPropertyChanged(nameof(AiDetailsVisibility));
                OnPropertyChanged(nameof(LocalDeployVisibility));
                OnPropertyChanged(nameof(ExternalDeployVisibility));
            }
        }
    }

    public string AiMode
    {
        get => _aiMode;
        set
        {
            if (SetField(ref _aiMode, value))
            {
                OnPropertyChanged(nameof(LocalDeployVisibility));
                OnPropertyChanged(nameof(ExternalDeployVisibility));
            }
        }
    }

    public string AiApiKey { get => _aiApiKey; set => SetField(ref _aiApiKey, value); }
    public string AiEndpoint { get => _aiEndpoint; set => SetField(ref _aiEndpoint, value); }
    public string AiModel { get => _aiModel; set => SetField(ref _aiModel, value); }
    public bool AiCrashInterpret { get => _aiCrashInterpret; set => SetField(ref _aiCrashInterpret, value); }
    public bool AiRecommendReason { get => _aiRecommendReason; set => SetField(ref _aiRecommendReason, value); }
    public bool AiModTranslate { get => _aiModTranslate; set => SetField(ref _aiModTranslate, value); }

    // ===== AI 助手：本地部署（Ollama）=====
    public IReadOnlyList<LocalModelInfo> LocalModels => OllamaModels.Catalog;

    public string SelectedLocalModel
    {
        get => _selectedLocalModel;
        set
        {
            if (SetField(ref _selectedLocalModel, value))
            {
                OnPropertyChanged(nameof(SelectedModelSubText));
                OnPropertyChanged(nameof(SelectedModelSizeText));
                OnPropertyChanged(nameof(ModelButtonText));
                RefreshModelReady();
            }
        }
    }

    public string SelectedModelSubText => OllamaModels.ByDisplayName(SelectedLocalModel)?.SubText ?? "";
    public string SelectedModelSizeText =>
        OllamaModels.ByDisplayName(SelectedLocalModel) is { } m ? $"{m.SizeGb} GB · {m.RecommendTag}" : "";
    public string ModelButtonText =>
        ModelReady ? "已就绪"
        : (OllamaModels.ByDisplayName(SelectedLocalModel) is { } m ? $"下载模型 ({m.SizeGb}GB)" : "下载模型");

    public bool OllamaInstalled { get => _ollamaInstalled; set => SetField(ref _ollamaInstalled, value); }
    public string OllamaVersion { get => _ollamaVersion; set => SetField(ref _ollamaVersion, value); }
    public bool OllamaInstalling { get => _ollamaInstalling; set => SetField(ref _ollamaInstalling, value); }
    public double OllamaInstallProgress { get => _ollamaInstallProgress; set => SetField(ref _ollamaInstallProgress, value); }
    public string OllamaInstallText { get => _ollamaInstallText; set => SetField(ref _ollamaInstallText, value); }
    public bool ModelDownloading { get => _modelDownloading; set => SetField(ref _modelDownloading, value); }
    public double ModelDownloadProgress { get => _modelDownloadProgress; set => SetField(ref _modelDownloadProgress, value); }
    public string ModelDownloadText { get => _modelDownloadText; set => SetField(ref _modelDownloadText, value); }
    public bool ModelReady { get => _modelReady; set => SetField(ref _modelReady, value); }
    public OllamaServiceStatus OllamaServiceStatus { get => _ollamaServiceStatus; set => SetField(ref _ollamaServiceStatus, value); }

    // 分层可见性（依总开关与部署方式）
    public Visibility AiDetailsVisibility => AiEnabled ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LocalDeployVisibility => (AiEnabled && AiMode == "Local") ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ExternalDeployVisibility => (AiEnabled && AiMode == "External") ? Visibility.Visible : Visibility.Collapsed;

    // ===== 外观 =====
    public string SelectedTheme { get => _selectedTheme; set => SetField(ref _selectedTheme, value); }
    public string ThemeColor
    {
        get => _themeColor;
        set { if (SetField(ref _themeColor, value)) MCLCS.App.App.ApplyAccentColor(value); }
    }
    public string BackgroundImagePath
    {
        get => _backgroundImagePath;
        set
        {
            if (SetField(ref _backgroundImagePath, value))
                MCLCS.App.App.ApplyBackgroundImage(value); // 即时预览（bug #20）
        }
    }
    public double FontScale
    {
        get => _fontScale;
        set { if (SetField(ref _fontScale, value)) MCLCS.App.App.ApplyFontScale(value); }
    }

    /// <summary>适配高分辨率屏幕：开启后图标加载 2x 高清资源（规格 2.4 — 外观）。实时驱动 IconManager。</summary>
    public bool HighDpiEnabled
    {
        get => _highDpiEnabled;
        set
        {
            if (SetField(ref _highDpiEnabled, value))
                IconManager.HighDpi = value;
        }
    }

    // ===== 关于 / 更新 =====
    public bool AutoUpdateCheck { get => _autoUpdateCheck; set => SetField(ref _autoUpdateCheck, value); }
    public string UpdateMessage { get => _updateMessage; set => SetField(ref _updateMessage, value); }
    public string LauncherVersion => _launcherVersion;

    // ===== 账号 =====
    public ObservableCollection<AccountEntry> Accounts { get => _accounts; set => SetField(ref _accounts, value); }
    public AccountEntry? SelectedAccount { get => _selectedAccount; set => SetField(ref _selectedAccount, value); }
    public string NewOfflineName { get => _newOfflineName; set => SetField(ref _newOfflineName, value); }
    public string AuthlibServerUrl { get => _authlibServerUrl; set => SetField(ref _authlibServerUrl, value); }
    public string AuthlibEmail { get => _authlibEmail; set => SetField(ref _authlibEmail, value); }
    public string AuthlibPassword { get => _authlibPassword; set => SetField(ref _authlibPassword, value); }

    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

    public List<string> AvailableLanguages => LocaleManager.AvailableLocales;
    public ICommand SaveCommand { get; }
    public ICommand AutoDetectJavaCommand { get; }
    public ICommand RefreshAccountsCommand { get; }
    public ICommand SetActiveAccountCommand { get; }
    public ICommand AddOfflineAccountCommand { get; }
    public ICommand RemoveAccountCommand { get; }
    public ICommand BrowseBackgroundCommand { get; }
    public ICommand BrowseGameRootCommand { get; }
    public ICommand OpenGameRootCommand { get; }
    public ICommand ResetGameRootCommand { get; }
    public ICommand CheckUpdateCommand { get; }
    public ICommand LoginMicrosoftCommand { get; }

    // AI 助手命令
    public ICommand InstallOllamaCommand { get; }
    public ICommand CancelOllamaInstallCommand { get; }
    public ICommand PullModelCommand { get; }
    public ICommand CancelModelDownloadCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand RefreshAiStatusCommand { get; }

    public SettingsViewModel()
    {
        SaveCommand = new RelayCommand(_ => Save());
        AutoDetectJavaCommand = new AsyncRelayCommand(_ => AutoDetectJavaAsync());
        RefreshAccountsCommand = new RelayCommand(_ => RefreshAccounts());
        SetActiveAccountCommand = new RelayCommand(_ => SetActiveAccount());
        AddOfflineAccountCommand = new RelayCommand(_ => AddOfflineAccount());
        RemoveAccountCommand = new RelayCommand(p => RemoveAccount(p as AccountEntry));
        BrowseBackgroundCommand = new RelayCommand(_ => BrowseBackground());
        BrowseGameRootCommand = new RelayCommand(_ => BrowseGameRoot());
        OpenGameRootCommand = new RelayCommand(_ => OpenGameRoot());
        ResetGameRootCommand = new RelayCommand(_ => ResetGameRoot());
        CheckUpdateCommand = new AsyncRelayCommand(_ => CheckUpdateAsync());
        LoginMicrosoftCommand = new AsyncRelayCommand(_ => LoginMicrosoftAsync());

        // AI 助手命令
        InstallOllamaCommand = new AsyncRelayCommand(_ => InstallOllamaAsync());
        CancelOllamaInstallCommand = new RelayCommand(_ => _ollamaInstallCts?.Cancel());
        PullModelCommand = new AsyncRelayCommand(_ => PullModelAsync());
        CancelModelDownloadCommand = new RelayCommand(_ => _modelCts?.Cancel());
        TestConnectionCommand = new AsyncRelayCommand(_ => TestConnectionAsync());
        RefreshAiStatusCommand = new AsyncRelayCommand(_ => RefreshOllamaStatusAsync());

        var profile = ProfileStore.Load(GameConstants.DefaultGameRoot);
        LoadFromProfile(profile);
        RefreshAccounts();

        // 主题/语言偏好
        ThemeManager.LoadPreference(GameConstants.DefaultGameRoot);
        _selectedTheme = ThemeManager.Current.ToString();
        _selectedLanguage = LocaleManager.CurrentLocale;

        // 同步运行时 AI 配置
        Assistant.Config = profile.Ai ?? new AiConfig();

        // 后台刷新 Ollama 安装/服务/已拉取模型状态（不阻塞界面）
        _ = RefreshOllamaStatusAsync();
    }

    private void LoadFromProfile(LauncherProfile profile)
    {
        // 游戏目录以启动器级配置为准（bug #26）：未自定义时留空，输入框显示水印默认路径
        GamePath = GameConstants.IsGameRootCustomized ? GameConstants.DefaultGameRoot : "";
        JavaPath = profile.JavaPath ?? "";
        MaxMemoryMb = profile.MaxMemoryMb;
        Username = profile.DefaultUsername;
        ExtraJvmArgs = string.Join(" ", profile.ExtraJvmArgs);
        SelectedRepairPolicy = profile.RepairPolicy.ToString();
        SelectedJavaVendor = profile.PreferredJavaVendor.ToString();
        SelectedAutoInstallMods = profile.AutoInstallMissingMods.ToString();
        SelectedIntelliRecommend = profile.IntelliRecommend.ToString();
        LoadCategoryPreferences(profile);

        // 通用
        SelectedLanguage = profile.Language;
        AutoStartLauncher = profile.AutoStartLauncher;
        MinimizeToTray = profile.MinimizeToTray;
        AnimationsEnabled = profile.AnimationsEnabled;
        FileWatchEnabled = profile.FileWatchEnabled;

        // 启动补充
        PrewarmEnabled = profile.Prewarm.Mode != PrewarmMode.Off;
        HudEnabled = profile.Hud.Enabled;
        LaunchCompatCheckEnabled = profile.LaunchCompatCheckEnabled;

        // 下载
        SelectedDownloadSource = profile.DownloadSource.ToString();
        MaxConcurrentDownloads = profile.MaxConcurrentDownloads;
        AutoRepairResourcePacks = profile.AutoRepairResourcePacks;
        ServerPackCacheEnabled = profile.ServerPackCacheEnabled;

        // AI
        var ai = profile.Ai ?? new AiConfig();
        AiEnabled = ai.Enabled;
        AiMode = ai.Mode.ToString();
        SelectedLocalModel = OllamaModels.ByTag(ai.SelectedLocalModel)?.DisplayName ?? OllamaModels.Default.DisplayName;
        _lastCommittedModel = SelectedLocalModel;
        AiApiKey = ai.ApiKey ?? "";
        AiEndpoint = ai.Endpoint;
        AiModel = ai.Model;
        AiCrashInterpret = ai.CrashInterpret;
        AiRecommendReason = ai.RecommendReason;
        AiModTranslate = ai.ModTranslate;

        // 外观
        ThemeColor = profile.ThemeColor;
        BackgroundImagePath = profile.BackgroundImagePath ?? "";
        FontScale = profile.FontScale;
        HighDpiEnabled = profile.HighDpiIcons;

        // 关于
        AutoUpdateCheck = profile.AutoUpdateCheck;
    }

    private void LoadCategoryPreferences(LauncherProfile profile)
    {
        var prefs = new ObservableCollection<CategoryPref>();
        foreach (var cat in GameplayCategoryMap.All)
        {
            prefs.Add(new CategoryPref
            {
                Category = cat,
                Label = GameplayCategoryMap.DisplayName(cat),
                IsChecked = profile.PreferredCategories.Contains(cat)
            });
        }
        CategoryPreferences = prefs;
    }

    // ===== 主题 / 语言 即时生效 =====

    public void ApplyTheme()
    {
        if (Enum.TryParse<ThemeType>(SelectedTheme, out var t))
        {
            ThemeManager.Current = t;
            ThemeManager.SavePreference(GameConstants.DefaultGameRoot);
        }
    }

    public void ApplyLanguage()
    {
        LocaleManager.CurrentLocale = SelectedLanguage;
    }

    private void Save()
    {
        // 游戏目录可能是用户手输的，先应用再写 profile，保证 profile 落到正确的目录里（bug #26）
        var typed = string.IsNullOrWhiteSpace(GamePath) ? null : GamePath.Trim();
        if (!string.Equals(typed ?? GameConstants.SystemGameRoot,
                           GameConstants.DefaultGameRoot, StringComparison.OrdinalIgnoreCase))
            ApplyGameRoot(typed);

        var profile = new LauncherProfile
        {
            JavaPath = string.IsNullOrWhiteSpace(JavaPath) ? null : JavaPath,
            MaxMemoryMb = MaxMemoryMb,
            DefaultUsername = Username,
            GameRoot = GameConstants.DefaultGameRoot,
            ExtraJvmArgs = string.IsNullOrWhiteSpace(ExtraJvmArgs)
                ? new List<string>()
                : ExtraJvmArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(),
            RepairPolicy = Enum.TryParse<CrashRepairPolicy>(SelectedRepairPolicy, out var rp) ? rp : CrashRepairPolicy.Ask,
            PreferredJavaVendor = Enum.TryParse<JavaVendor>(SelectedJavaVendor, out var jv) ? jv : JavaVendor.Auto,
            AutoInstallMissingMods = Enum.TryParse<AutoInstallPolicy>(SelectedAutoInstallMods, out var ai) ? ai : AutoInstallPolicy.Ask,
            IntelliRecommend = Enum.TryParse<IntelliRecommendMode>(SelectedIntelliRecommend, out var ir) ? ir : IntelliRecommendMode.Enabled,
            PreferredCategories = CategoryPreferences.Where(p => p.IsChecked).Select(p => p.Category).ToList(),

            // 通用
            Language = SelectedLanguage,
            AutoStartLauncher = AutoStartLauncher,
            MinimizeToTray = MinimizeToTray,
            AnimationsEnabled = AnimationsEnabled,
            FileWatchEnabled = FileWatchEnabled,

            // 启动补充（规格 2.4）
            Prewarm = new PrewarmConfig { Mode = PrewarmEnabled ? PrewarmMode.Light : PrewarmMode.Off },
            Hud = new HudConfig { Enabled = HudEnabled },
            LaunchCompatCheckEnabled = LaunchCompatCheckEnabled,

            // 下载
            DownloadSource = Enum.TryParse<DownloadSourcePreference>(SelectedDownloadSource, out var ds) ? ds : DownloadSourcePreference.MirrorFirst,
            MaxConcurrentDownloads = MaxConcurrentDownloads,
            AutoRepairResourcePacks = AutoRepairResourcePacks,
            ServerPackCacheEnabled = ServerPackCacheEnabled,

            // 外观
            ThemeColor = ThemeColor,
            BackgroundImagePath = string.IsNullOrWhiteSpace(BackgroundImagePath) ? null : BackgroundImagePath,
            FontScale = FontScale,
            HighDpiIcons = HighDpiEnabled,

            // 关于 / 更新
            AutoUpdateCheck = AutoUpdateCheck,

            // AI
            Ai = new AiConfig
            {
                Enabled = AiEnabled,
                // 本类有同名 string 属性 AiMode，会遮蔽枚举类型，故用完全限定名。
                Mode = Enum.TryParse<MCLCS.Core.Ai.AiMode>(AiMode, out var am)
                    ? am
                    : MCLCS.Core.Ai.AiMode.External,
                SelectedLocalModel = OllamaModels.ByDisplayName(SelectedLocalModel)?.OllamaTag ?? OllamaModels.Default.OllamaTag,
                ApiKey = string.IsNullOrWhiteSpace(AiApiKey) ? null : AiApiKey,
                Endpoint = AiEndpoint,
                Model = AiModel,
                CrashInterpret = AiCrashInterpret,
                RecommendReason = AiRecommendReason,
                ModTranslate = AiModTranslate
            }
        };
        ProfileStore.Save(profile);

        // 即时生效
        ApplyTheme();
        App.ApplyBackgroundImage(profile.BackgroundImagePath); // 保存后确保背景图片生效（bug #20）
        ApplyLanguage();
        Assistant.Config = profile.Ai;

        StatusMessage = "已保存设置";
    }

    private async Task AutoDetectJavaAsync()
    {
        var all = await JavaDetector.DetectAsync();
        var paths = all.OrderByDescending(j => j.MajorVersion)
                       .Select(j => $"[Java {j.MajorVersion}] {j.JavaExe}")
                       .Distinct()
                       .ToList();
        DetectedJavas = new ObservableCollection<string>(paths);

        var best = await JavaDetector.FindBestAsync(GameConstants.MinimumJavaMajorVersion);
        if (best is not null)
        {
            JavaPath = best.JavaExe;
            StatusMessage = $"找到 {all.Count} 个 Java，已选择 Java {best.MajorVersion}";
        }
        else
        {
            StatusMessage = "未检测到 Java 21，请安装 Java 21 或以上";
        }
    }

    // ===== 账号 =====
    private void RefreshAccounts()
    {
        Accounts = new ObservableCollection<AccountEntry>(AccountStore.Load(GameConstants.DefaultGameRoot));
        var last = AccountStore.GetLastUsed(GameConstants.DefaultGameRoot);
        SelectedAccount = Accounts.FirstOrDefault(a => a.Id == last?.Id) ?? Accounts.FirstOrDefault();
    }

    private void SetActiveAccount()
    {
        if (SelectedAccount is null) return;
        AccountStore.MarkUsed(GameConstants.DefaultGameRoot, SelectedAccount.Id);
        StatusMessage = $"当前账号: {SelectedAccount.DisplayName} ({SelectedAccount.AuthType})";
    }

    private void AddOfflineAccount()
    {
        if (string.IsNullOrWhiteSpace(NewOfflineName)) { StatusMessage = "请填写离线用户名"; return; }
        var session = new OfflineAuthenticator().AuthenticateAsync(NewOfflineName).GetAwaiter().GetResult();
        AccountStore.Upsert(GameConstants.DefaultGameRoot, new AccountEntry
        {
            DisplayName = NewOfflineName,
            AuthType = "offline",
            Username = session.Username,
            Uuid = session.Uuid
        });
        NewOfflineName = "";
        RefreshAccounts();
        StatusMessage = $"已添加离线账号：{session.Username}";
    }

    /// <summary>添加 Authlib-Injector 账号（由视图读取密码后调用）。</summary>
    public async Task AddAuthlibAccount(string serverUrl, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(email))
        {
            StatusMessage = "请填写服务器地址与邮箱";
            return;
        }
        try
        {
            // 必须异步等待：Authlib 认证含网络往返，使用 .GetAwaiter().GetResult() 会在 UI 线程上
            // 同步阻塞导致界面卡死（bug：配置外置登录时异常卡死）。
            StatusMessage = "Authlib 登录中…";
            var auth = new AuthlibInjectorAuthenticator(new HttpClient(), serverUrl, email, password);
            var session = await auth.AuthenticateAsync(email);
            AccountStore.Upsert(GameConstants.DefaultGameRoot, new AccountEntry
            {
                DisplayName = session.Username,
                AuthType = "authlib",
                Username = session.Username,
                Uuid = session.Uuid,
                AccessToken = session.AccessToken,
                AuthlibServerUrl = serverUrl
            });
            AuthlibServerUrl = "";
            AuthlibEmail = "";
            AuthlibPassword = "";
            RefreshAccounts();
            StatusMessage = $"已添加 Authlib 账号：{session.Username}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Authlib 登录失败：{ex.Message}";
        }
    }

    private async Task LoginMicrosoftAsync()
    {
        try
        {
            var auth = new MicrosoftAuthenticator(new HttpClient(),
                code => UIService.ShowMessage(code, "微软登录"));
            var session = await auth.AuthenticateAsync(null);
            AccountStore.Upsert(GameConstants.DefaultGameRoot, new AccountEntry
            {
                DisplayName = session.Username,
                AuthType = "microsoft",
                Username = session.Username,
                Uuid = session.Uuid,
                AccessToken = session.AccessToken
            });
            RefreshAccounts();
            StatusMessage = $"已添加微软账号：{session.Username}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"微软登录失败：{ex.Message}";
        }
    }

    private void RemoveAccount(AccountEntry? account)
    {
        if (account is null) return;
        if (UIService.Confirm($"确定删除账号 {account.DisplayName}？", "删除账号"))
        {
            AccountStore.Remove(GameConstants.DefaultGameRoot, account.Id);
            RefreshAccounts();
            StatusMessage = $"已删除账号：{account.DisplayName}";
        }
    }

    private void BrowseBackground()
    {
        var path = UIService.PickFile("图片|*.png;*.jpg;*.jpeg;*.bmp", "选择背景图片");
        if (!string.IsNullOrEmpty(path)) BackgroundImagePath = path;
    }

    // ===== 游戏目录（bug #26）=====

    /// <summary>选择 Minecraft 游戏目录；选中后立即生效并持久化。</summary>
    private void BrowseGameRoot()
    {
        var path = UIService.PickFolder("选择 Minecraft 游戏目录（.minecraft）");
        if (string.IsNullOrWhiteSpace(path)) return;
        ApplyGameRoot(path);
    }

    /// <summary>恢复为系统默认目录 %APPDATA%\.minecraft。</summary>
    private void ResetGameRoot() => ApplyGameRoot(null);

    /// <summary>在资源管理器中打开当前游戏目录。</summary>
    private void OpenGameRoot()
    {
        var dir = string.IsNullOrWhiteSpace(GamePath) ? GameConstants.SystemGameRoot : GamePath;
        try
        {
            System.IO.Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开目录失败：{ex.Message}";
        }
    }

    /// <summary>应用游戏目录：持久化 → 重建 LauncherService → 回填界面。传 null 恢复默认。</summary>
    private void ApplyGameRoot(string? path)
    {
        try
        {
            GameConstants.SetGameRoot(path);
            var effective = GameConstants.DefaultGameRoot;
            GamePath = GameConstants.IsGameRootCustomized ? effective : "";
            LauncherService.Reinitialize(effective);
            StatusMessage = GameConstants.IsGameRootCustomized
                ? $"游戏目录已切换到：{effective}"
                : $"已恢复默认游戏目录：{effective}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"设置游戏目录失败：{ex.Message}";
        }
    }

    private async Task CheckUpdateAsync()
    {
        var result = await UpdateNotifier.CheckAndShowAsync();
        if (!string.IsNullOrEmpty(result.Error))
            UpdateMessage = $"检查更新失败：{result.Error}";
        else if (result.Available)
            UpdateMessage = $"发现新版本 {result.LatestVersion}（当前 {result.CurrentVersion}）{(result.Mandatory ? "，建议立即更新" : "")} · singlefile 包已发布（CNB 发布页）";
        else
            UpdateMessage = $"已是最新版本（{result.CurrentVersion}）";
    }

    // ===== AI 助手：本地部署（Ollama）=====

    /// <summary>检测 Ollama 安装、服务状态与已拉取模型（后台刷新）。</summary>
    private async Task RefreshOllamaStatusAsync()
    {
        try
        {
            var det = await OllamaManager.DetectAsync();
            OllamaInstalled = det.Installed;
            OllamaVersion = det.Version;
            OllamaServiceStatus = await OllamaManager.GetServiceStatusAsync();

            _pulledTags.Clear();
            foreach (var m in OllamaModels.Catalog)
                if (await OllamaManager.IsModelPulledAsync(m.OllamaTag))
                    _pulledTags.Add(m.OllamaTag);

            var info = OllamaModels.ByDisplayName(SelectedLocalModel);
            ModelReady = info is not null && _pulledTags.Contains(info.OllamaTag);
        }
        catch
        {
            // 离线/无 Ollama 时不阻塞界面
        }
    }

    private void RefreshModelReady()
    {
        var info = OllamaModels.ByDisplayName(SelectedLocalModel);
        ModelReady = info is not null && _pulledTags.Contains(info.OllamaTag);
        OnPropertyChanged(nameof(ModelButtonText));
    }

    /// <summary>用户切换本地模型时的回调：已拉取直接接受；未拉取则按规格弹确认窗，取消则回退。</summary>
    public async Task TrySelectLocalModelAsync(string displayName)
    {
        var info = OllamaModels.ByDisplayName(displayName);
        if (info is null) return;
        if (displayName == _lastCommittedModel || _pulledTags.Contains(info.OllamaTag))
        {
            _lastCommittedModel = displayName;
            return;
        }

        var msg = info.OllamaTag.Contains("phi")
            ? "需额外下载 2.2GB，文件较大，是否继续？"
            : info.OllamaTag.Contains("internlm")
                ? "需额外下载 1.1GB，是否继续？"
                : $"需额外下载 {info.SizeGb}GB，是否继续？";

        if (!UIService.Confirm(msg, "下载模型"))
        {
            // 回退到上一次已确认的模型
            SelectedLocalModel = _lastCommittedModel;
            return;
        }
        _lastCommittedModel = displayName;
    }

    /// <summary>一键安装 Ollama：下载安装器并静默安装，支持取消与临时文件清理。</summary>
    private async Task InstallOllamaAsync()
    {
        _ollamaInstallCts = new CancellationTokenSource();
        OllamaInstalling = true;
        OllamaInstallProgress = 0;
        OllamaInstallText = "正在下载 Ollama 安装程序…";
        try
        {
            await OllamaManager.InstallAsync(new Progress<double>(p => OllamaInstallProgress = p), _ollamaInstallCts.Token);
            var det = await OllamaManager.DetectAsync();
            OllamaInstalled = det.Installed;
            OllamaVersion = det.Version;
            OllamaInstallText = det.Installed
                ? $"Ollama 已安装（{det.Version}）"
                : "安装完成，但未检测到 ollama 命令，请重启启动器后重试。";
            await RefreshOllamaStatusAsync();
        }
        catch (OperationCanceledException)
        {
            OllamaInstallText = "已取消安装，临时文件已清理。";
        }
        catch (Exception ex)
        {
            OllamaInstallText = $"安装失败：{ex.Message}";
        }
        finally
        {
            OllamaInstalling = false;
            _ollamaInstallCts = null;
        }
    }

    /// <summary>拉取选中的本地模型，支持进度与取消。</summary>
    private async Task PullModelAsync()
    {
        var info = OllamaModels.ByDisplayName(SelectedLocalModel);
        if (info is null) return;
        _modelCts = new CancellationTokenSource();
        ModelDownloading = true;
        ModelDownloadProgress = 0;
        ModelDownloadText = $"正在下载 {info.DisplayName}…";
        try
        {
            await OllamaManager.PullModelAsync(info.OllamaTag,
                new Progress<double>(p => ModelDownloadProgress = p), _modelCts.Token);
            _pulledTags.Add(info.OllamaTag);
            _lastCommittedModel = SelectedLocalModel;
            ModelReady = true;
            ModelDownloadText = "已就绪";
        }
        catch (OperationCanceledException)
        {
            ModelDownloadText = "已取消下载。";
        }
        catch (Exception ex)
        {
            ModelDownloadText = $"下载失败：{ex.Message}";
        }
        finally
        {
            ModelDownloading = false;
            _modelCts = null;
            RefreshModelReady();
        }
    }

    /// <summary>测试外部 API 连接，成功后按地址自动补全默认模型名。</summary>
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(AiEndpoint))
        {
            StatusMessage = "请先填写 API 地址";
            return;
        }
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var body = new
            {
                model = string.IsNullOrWhiteSpace(AiModel) ? "gpt-4o-mini" : AiModel,
                messages = new[] { new { role = "user", content = "ping" } },
                max_tokens = 1
            };
            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            if (!string.IsNullOrWhiteSpace(AiApiKey))
                content.Headers.Add("Authorization", "Bearer " + AiApiKey);

            using var resp = await client.PostAsync(AiEndpoint, content);
            if (resp.IsSuccessStatusCode)
            {
                var suggested = Assistant.SuggestModelForEndpoint(AiEndpoint);
                if (!string.IsNullOrEmpty(suggested) && suggested != AiModel)
                {
                    AiModel = suggested;
                    StatusMessage = $"连接成功，已自动填充模型：{suggested}";
                }
                else
                {
                    StatusMessage = "连接成功";
                }
            }
            else
            {
                StatusMessage = $"连接失败：HTTP {(int)resp.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"连接失败：{ex.Message}";
        }
    }
}
