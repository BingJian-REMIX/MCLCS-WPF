using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using MCLCS.App.Services;
using MCLCS.Core.Download;
using MCLCS.Core.Installers;
using MCLCS.Core.Models;
using MCLCS.Core.Mvvm;
using MCLCS.Core.Utils;
namespace MCLCS.App.ViewModels;

/// <summary>整合包在线来源的绑定包装（ComboBox 用）。</summary>
public class ModpackSourceEntry
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public bool IsAvailable { get; init; }
    public string? UnavailableReason { get; init; }

    public override string ToString() => DisplayName;
}

/// <summary>
/// 下载页（规格 2.2）ViewModel：按全局侧边栏副标签（mod / shader / resourcepack / modpack / map）切换内容，
/// 顶部版本 + 加载器筛选（地图为分类 / 版本 / 排序 / 分页），卡片网格（外联封面由 ExternalIcon 渲染），
/// 底部下载队列（进度 / 暂停 / 取消）。Mod 类走 Modrinth，地图走像素茶艺（PixelMap），
/// 整合包在线浏览走 <see cref="IModpackSource"/>（Modrinth 常驻，CurseForge 需设置页填 Key）。
/// </summary>
public class DownloadPageViewModel : ObservableObject
{
    private string _currentSubTab = "mod";
    private bool _isMap;
    private string _query = "";
    private string _selectedGameVersion = "";
    private string _selectedLoader = "Any";
    private string _statusMessage = "";
    private bool _isBusy;
    private DownloadCardItem? _selectedCard;

    // 地图专属
    private string _selectedCategory = "";
    private string _selectedMapVersion = "";
    private MapSort _mapSort = MapSort.Published;
    private int _mapPage = 1;
    private int _mapTotalPages = 1;
    private bool _mapFacetsLoaded;

    // 地图详情弹窗
    private PixelMapDetail? _currentMapDetail;
    private bool _isDetailOpen;
    private string _detailHint = "";

    // 整合包在线浏览（规格 2.2 → 整合包：在线浏览 Modrinth / CurseForge，一键安装）
    private bool _isModpack;
    private ObservableCollection<ModpackSourceEntry> _modpackSources = new();
    private string _selectedModpackSource = "modrinth";
    private ModpackDetail? _currentModpackDetail;
    private bool _isModpackDetailOpen;
    private ModpackVersion? _selectedModpackVersion;
    private bool _installIsolated = true;
    private string _currentModpackSourceId = "modrinth";
    private string _modpackDetailHint = "";

    // Minecraft 版本下载（规格新增）
    private bool _isMinecraft;
    // 注意：本命名空间下另有一个用于版本列表页展示的 VersionEntry（仅 Id/Type），
    // 会屏蔽 using 别名，故此处用完全限定名指向 Core 的清单实体（带 Url / ReleaseTime）。
    private List<MCLCS.Core.Models.VersionEntry>? _versionEntries;
    private string _selectedVersionType = "all";
    private DownloadCardItem? _currentInstallVersion;
    private bool _isInstallOpen;
    private string _selectedInstallLoader = "none";
    private bool _isInstalling;
    private string _installStatus = "";

    public ObservableCollection<DownloadCardItem> Cards { get; } = new();
    public ObservableCollection<DownloadQueueItem> Queue { get; } = new();

    public ObservableCollection<string> Loaders { get; } =
        new() { "Any", "Fabric", "Forge", "Quilt", "NeoForge" };

    public ObservableCollection<string> GameVersions { get; } = new() { "" };
    public ObservableCollection<PixelMapCategory> MapCategories { get; } = new();
    public ObservableCollection<string> MapVersions { get; } = new() { "" };

    public string CurrentSubTab
    {
        get => _currentSubTab;
        set => SetField(ref _currentSubTab, value);
    }

    /// <summary>当前是否为地图副标签（驱动筛选行显隐）。</summary>
    public bool IsMap
    {
        get => _isMap;
        private set
        {
            if (SetField(ref _isMap, value))
                OnPropertyChanged(nameof(ShowModFilters));
        }
    }

    /// <summary>当前是否为 Minecraft 版本下载副标签。</summary>
    public bool IsMinecraft
    {
        get => _isMinecraft;
        private set
        {
            if (SetField(ref _isMinecraft, value))
                OnPropertyChanged(nameof(ShowModFilters));
        }
    }

    /// <summary>当前是否为整合包在线浏览副标签。</summary>
    public bool IsModpack
    {
        get => _isModpack;
        private set
        {
            if (SetField(ref _isModpack, value))
                OnPropertyChanged(nameof(ModpackSourceVisible));
        }
    }

    /// <summary>整合包来源选择行是否可见（仅整合包子标签）。</summary>
    public bool ModpackSourceVisible => IsModpack;

    /// <summary>Modrinth 风格筛选行（版本 + 加载器）是否可见：非地图、非 Minecraft、非整合包。</summary>
    public bool ModrinthFilterVisible => !IsMap && !IsMinecraft && !IsModpack;

    /// <summary>是否显示 Modrinth 风格的 版本+加载器 筛选行（非地图且非 Minecraft 时）。</summary>
    public bool ShowModFilters => !IsMap && !IsMinecraft;

    /// <summary>Minecraft 版本类型筛选：all / release / snapshot / old。</summary>
    public string SelectedVersionType
    {
        get => _selectedVersionType;
        set
        {
            if (SetField(ref _selectedVersionType, value) && IsMinecraft)
                ApplyVersionFilter();
        }
    }

    /// <summary>当前在弹窗中待安装的 Minecraft 版本卡片。</summary>
    public DownloadCardItem? CurrentInstallVersion
    {
        get => _currentInstallVersion;
        set => SetField(ref _currentInstallVersion, value);
    }

    /// <summary>安装弹窗是否打开。</summary>
    public bool IsInstallOpen
    {
        get => _isInstallOpen;
        set => SetField(ref _isInstallOpen, value);
    }

    /// <summary>选中的加载器：none / forge / fabric / neoforge / quilt。</summary>
    public string SelectedInstallLoader
    {
        get => _selectedInstallLoader;
        set => SetField(ref _selectedInstallLoader, value);
    }

    /// <summary>安装进行中（弹窗内显示进度）。</summary>
    public bool IsInstalling
    {
        get => _isInstalling;
        set => SetField(ref _isInstalling, value);
    }

    /// <summary>安装状态文案（弹窗内显示）。</summary>
    public string InstallStatus
    {
        get => _installStatus;
        set => SetField(ref _installStatus, value);
    }

    public string Query
    {
        get => _query;
        set
        {
            if (SetField(ref _query, value) && IsMinecraft)
                ApplyVersionFilter();
        }
    }

    public string SelectedGameVersion
    {
        get => _selectedGameVersion;
        set => SetField(ref _selectedGameVersion, value);
    }

    public string SelectedLoader
    {
        get => _selectedLoader;
        set => SetField(ref _selectedLoader, value);
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set => SetField(ref _selectedCategory, value);
    }

    public string SelectedMapVersion
    {
        get => _selectedMapVersion;
        set => SetField(ref _selectedMapVersion, value);
    }

    public MapSort MapSort
    {
        get => _mapSort;
        set => SetField(ref _mapSort, value);
    }

    /// <summary>地图排序（字符串形式，便于 ComboBox 双向绑定：published / views）。</summary>
    public string MapSortKey
    {
        get => _mapSort == MapSort.Views ? "views" : "published";
        set => MapSort = value == "views" ? MapSort.Views : MapSort.Published;
    }

    public int MapPage
    {
        get => _mapPage;
        set => SetField(ref _mapPage, value);
    }

    public int MapTotalPages
    {
        get => _mapTotalPages;
        set => SetField(ref _mapTotalPages, value);
    }

    public DownloadCardItem? SelectedCard
    {
        get => _selectedCard;
        set => SetField(ref _selectedCard, value);
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

    public PixelMapDetail? CurrentMapDetail
    {
        get => _currentMapDetail;
        set
        {
            if (!SetField(ref _currentMapDetail, value)) return;
            OnPropertyChanged(nameof(DetailCanDownload));
            OnPropertyChanged(nameof(DetailHasExtra));
        }
    }

    /// <summary>详情窗：地图本体可直接下载（作者允许 + 有直链）。</summary>
    public bool DetailCanDownload => CurrentMapDetail?.CanDownload == true;

    /// <summary>详情窗：存在附加资源（资源包 / 光影），决定"附加资源"按钮显隐。</summary>
    public bool DetailHasExtra => CurrentMapDetail?.HasAdditionalResources == true;

    /// <summary>详情窗底部提示（下载 / 安装结果、不可下载原因）。</summary>
    public string DetailHint
    {
        get => _detailHint;
        set => SetField(ref _detailHint, value);
    }

    public bool IsDetailOpen
    {
        get => _isDetailOpen;
        set => SetField(ref _isDetailOpen, value);
    }

    // ---- 整合包在线浏览 ----

    public ObservableCollection<ModpackSourceEntry> ModpackSources
    {
        get => _modpackSources;
        set => SetField(ref _modpackSources, value);
    }

    public string SelectedModpackSource
    {
        get => _selectedModpackSource;
        set
        {
            if (SetField(ref _selectedModpackSource, value))
                _ = SearchAsync();
        }
    }

    public ModpackDetail? CurrentModpackDetail
    {
        get => _currentModpackDetail;
        set
        {
            if (!SetField(ref _currentModpackDetail, value)) return;
            OnPropertyChanged(nameof(ModpackDetailCanInstall));
            OnPropertyChanged(nameof(ModpackDetailIsCurseForge));
            OnPropertyChanged(nameof(ModpackDetailSourceNote));
            OnPropertyChanged(nameof(ModpackDetailLoaders));
            OnPropertyChanged(nameof(ModpackDetailLatestVersion));
        }
    }

    public bool IsModpackDetailOpen
    {
        get => _isModpackDetailOpen;
        set => SetField(ref _isModpackDetailOpen, value);
    }

    public ModpackVersion? SelectedModpackVersion
    {
        get => _selectedModpackVersion;
        set
        {
            if (SetField(ref _selectedModpackVersion, value))
            {
                OnPropertyChanged(nameof(ModpackDetailCanInstall));
                _modpackDetailHint = "";
                OnPropertyChanged(nameof(ModpackDetailHint));
            }
        }
    }

    /// <summary>隔离安装开关（默认开启，符合独立版本隔离目录决策）。CurseForge 不支持隔离。</summary>
    public bool InstallIsolated
    {
        get => _installIsolated;
        set => SetField(ref _installIsolated, value);
    }

    /// <summary>当前打开详情的整合包所属来源 Id（驱动安装/打开页面行为）。</summary>
    public string CurrentModpackSourceId
    {
        get => _currentModpackSourceId;
        set => SetField(ref _currentModpackSourceId, value);
    }

    /// <summary>详情窗：所选版本是否有可直链下载的文件。</summary>
    public bool ModpackDetailCanInstall => !string.IsNullOrEmpty(SelectedModpackVersion?.FileUrl);

    /// <summary>详情窗：当前是否为 CurseForge 整合包（CurseForge 不支持隔离安装）。</summary>
    public bool ModpackDetailIsCurseForge => CurrentModpackSourceId == "curseforge";

    /// <summary>详情窗：来源相关的提示（如 CurseForge 不支持隔离）。</summary>
    public string ModpackDetailSourceNote => ModpackDetailIsCurseForge
        ? "CurseForge 整合包以共享目录方式安装（不支持隔离）。"
        : "";

    /// <summary>详情窗：从版本列表聚合的加载器摘要（首字母大写、去重）。</summary>
    public string ModpackDetailLoaders
    {
        get
        {
            if (CurrentModpackDetail is null) return "";
            var loaders = CurrentModpackDetail.Versions
                .Select(v => v.Loader)
                .Where(l => !string.IsNullOrEmpty(l))
                .Select(l => char.ToUpperInvariant(l![0]) + l[1..])
                .Distinct()
                .ToList();
            return string.Join(" / ", loaders);
        }
    }

    /// <summary>详情窗：最新支持的游戏版本（版本列表首项）。</summary>
    public string ModpackDetailLatestVersion => CurrentModpackDetail?.Versions.FirstOrDefault()?.GameVersion ?? "";

    public string ModpackDetailHint
    {
        get => _modpackDetailHint;
        set => SetField(ref _modpackDetailHint, value);
    }

    public ICommand SetSubTabCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand EnqueueCommand { get; }
    public ICommand StartQueueCommand { get; }
    public ICommand PauseItemCommand { get; }
    public ICommand CancelItemCommand { get; }
    public ICommand OpenDetailCommand { get; }
    public ICommand ChangeMapPageCommand { get; }
    public ICommand SetModpackSourceCommand { get; }
    public ICommand OpenModpackDetailCommand { get; }
    public ICommand InstallModpackCommand { get; }
    public ICommand CloseModpackDetailCommand { get; }
    public ICommand OpenModpackPageCommand { get; }
    public ICommand DownloadDetailCommand { get; }

    /// <summary>下载并分发地图附加资源（资源包 → resourcepacks，光影 → shaderpacks）。</summary>
    public ICommand DownloadExtraCommand { get; }

    /// <summary>在浏览器打开地图站详情页（作者未授权直接下载时的出路）。</summary>
    public ICommand OpenMapPageCommand { get; }

    public ICommand CloseDetailCommand { get; }
    public ICommand EnqueueInstallCommand { get; }
    public ICommand CloseInstallCommand { get; }

    public DownloadPageViewModel()
    {
        SetSubTabCommand = new RelayCommand(p => SetSubTab(p as string));
        SearchCommand = new AsyncRelayCommand(_ => SearchAsync(), _ => !IsBusy);
        EnqueueCommand = new RelayCommand(p => EnqueueSelected(p as DownloadCardItem));
        StartQueueCommand = new AsyncRelayCommand(_ => StartQueueAsync(), _ => !IsBusy);
        PauseItemCommand = new RelayCommand(p => PauseItem(p as DownloadQueueItem));
        CancelItemCommand = new RelayCommand(p => CancelItem(p as DownloadQueueItem));
        OpenDetailCommand = new RelayCommand(p => OpenDetail(p as DownloadCardItem));
        ChangeMapPageCommand = new RelayCommand(p => ChangeMapPage(p as string));
        SetModpackSourceCommand = new RelayCommand(p => SetModpackSource(p as string));
        OpenModpackDetailCommand = new RelayCommand(p => _ = OpenModpackDetailAsync(p as DownloadCardItem));
        InstallModpackCommand = new AsyncRelayCommand(_ => InstallModpackAsync(), _ => ModpackDetailCanInstall && !IsBusy);
        CloseModpackDetailCommand = new RelayCommand(_ => IsModpackDetailOpen = false);
        OpenModpackPageCommand = new RelayCommand(_ => OpenModpackPage());
        DownloadDetailCommand = new AsyncRelayCommand(_ => DownloadDetailAsync(), _ => DetailCanDownload && !IsBusy);
        DownloadExtraCommand = new AsyncRelayCommand(_ => DownloadExtraAsync(), _ => DetailHasExtra && !IsBusy);
        OpenMapPageCommand = new RelayCommand(_ => OpenMapPage());
        CloseDetailCommand = new RelayCommand(_ => IsDetailOpen = false);
        EnqueueInstallCommand = new RelayCommand(_ => EnqueueVersionInstall(), _ => CurrentInstallVersion is not null);
        CloseInstallCommand = new RelayCommand(_ => IsInstallOpen = false);

        _ = LoadGameVersionsAsync();
    }

    /// <summary>切换副标签（由 MainWindow 侧边栏路由调用）。</summary>
    public void SetSubTab(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) id = "minecraft";
        if (id is not ("minecraft" or "mod" or "shader" or "resourcepack" or "modpack" or "map")) id = "minecraft";

        CurrentSubTab = id;
        IsMap = id == "map";
        IsMinecraft = id == "minecraft";
        IsModpack = id == "modpack";

        if (id == "map" && !_mapFacetsLoaded)
            _ = LoadMapFacetsAsync();

        if (id == "modpack")
            RefreshModpackSources();

        _ = SearchAsync();
    }

    /// <summary>刷新整合包来源列表（CurseForge 是否可用取决于设置页 Key，可能随时变化）。</summary>
    private void RefreshModpackSources()
    {
        var list = LauncherService.Instance.ModpackSources
            .Select(s => new ModpackSourceEntry
            {
                Id = s.Id,
                DisplayName = s.DisplayName,
                IsAvailable = s.IsAvailable,
                UnavailableReason = s.UnavailableReason
            })
            .ToList();

        ModpackSources = new ObservableCollection<ModpackSourceEntry>(list);

        // 选中源若不可用（如刚清空 Key），回退到第一个可用源
        if (ModpackSources.FirstOrDefault(s => s.Id == _selectedModpackSource && s.IsAvailable) is null)
        {
            var first = ModpackSources.FirstOrDefault(s => s.IsAvailable) ?? ModpackSources.FirstOrDefault();
            _selectedModpackSource = first?.Id ?? "modrinth";
            OnPropertyChanged(nameof(SelectedModpackSource));
        }
    }

    /// <summary>切换整合包来源（来自 ComboBox 选择或源不可用回退）。</summary>
    private void SetModpackSource(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        if (ModpackSources.FirstOrDefault(s => s.Id == id && s.IsAvailable) is null) return;
        SelectedModpackSource = id!;
    }

    private async Task LoadGameVersionsAsync()
    {
        var versions = await LauncherService.Instance.GetVanillaVersionsAsync();
        GameVersions.Clear();
        GameVersions.Add("");
        foreach (var v in versions) GameVersions.Add(v);
    }

    private async Task LoadMapFacetsAsync()
    {
        try
        {
            var client = LauncherService.Instance.Pixelmap;
            var cats = await client.GetCategoriesAsync();
            MapCategories.Clear();
            MapCategories.Add(new PixelMapCategory { Id = "", Name = "全部分类" });
            foreach (var c in cats) MapCategories.Add(c);

            var vers = await client.GetVersionsAsync();
            MapVersions.Clear();
            MapVersions.Add("");
            foreach (var v in vers) MapVersions.Add(v);

            _mapFacetsLoaded = true;
        }
        catch
        {
            // 地图站不可用时保留空列表
        }
    }

    /// <summary>执行搜索。<paramref name="resetPage"/> 为 false 时保留当前地图页码（翻页场景）。</summary>
    private async Task SearchAsync(bool resetPage = true)
    {
        IsBusy = true;
        try
        {
            Cards.Clear();
            if (IsMinecraft) await LoadVersionsAsync();
            else if (IsMap) await SearchMapsAsync(resetPage);
            else if (IsModpack) await SearchModpackAsync();
            else await SearchModrinthAsync();

            StatusMessage = Cards.Count > 0
                ? IsMap && MapTotalPages > 1
                    ? $"找到 {Cards.Count} 个结果（第 {MapPage} / {MapTotalPages} 页）"
                    : $"找到 {Cards.Count} 个结果"
                : "未找到结果";
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

    // ---- Minecraft 版本下载 ----

    private async Task LoadVersionsAsync()
    {
        if (_versionEntries is null)
        {
            try { _versionEntries = await LauncherService.Instance.GetVanillaVersionsDetailedAsync(); }
            catch { _versionEntries = new List<MCLCS.Core.Models.VersionEntry>(); }
        }
        ApplyVersionFilter();
    }

    /// <summary>按搜索词与版本类型本地筛选（无网络），填充卡片网格。</summary>
    private void ApplyVersionFilter()
    {
        if (_versionEntries is null) return;
        var q = (Query ?? "").Trim().ToLowerInvariant();
        var type = SelectedVersionType;

        Cards.Clear();
        foreach (var v in _versionEntries)
        {
            var bucket = v.Type switch
            {
                "release" => "release",
                "snapshot" => "snapshot",
                _ => "old"
            };
            if (type != "all" && bucket != type) continue;
            if (q.Length > 0 && !v.Id.ToLowerInvariant().Contains(q)) continue;

            Cards.Add(new DownloadCardItem
            {
                Id = v.Id,
                Title = v.Id,
                Author = "Mojang",
                Summary = bucket == "release" ? "正式版" : bucket == "snapshot" ? "快照" : "旧版",
                IconUrl = null,
                FallbackToken = "download",
                Source = "Minecraft",
                SubTab = "minecraft",
                VersionType = v.Type
            });
        }
    }

    private async Task SearchModrinthAsync()
    {
        var (type, fallback) = CurrentSubTab switch
        {
            "shader" => (ModrinthProjectType.Shader, "sparkles"),
            "resourcepack" => (ModrinthProjectType.ResourcePack, "image"),
            "modpack" => (ModrinthProjectType.Modpack, "box"),
            _ => (ModrinthProjectType.Mod, "package")
        };

        var loader = SelectedLoader == "Any" ? LoaderType.Any : Enum.Parse<LoaderType>(SelectedLoader);
        var hits = await LauncherService.Instance.SearchModsAsync(
            Query,
            string.IsNullOrEmpty(SelectedGameVersion) ? null : SelectedGameVersion,
            loader, type);

        foreach (var h in hits)
            Cards.Add(new DownloadCardItem
            {
                Id = h.ProjectId,
                Title = h.Title,
                Author = h.Slug,
                Summary = h.Description,
                IconUrl = string.IsNullOrEmpty(h.IconUrl) ? null : h.IconUrl,
                FallbackToken = fallback,
                Source = "Modrinth",
                SubTab = CurrentSubTab
            });
    }

    // ---- 整合包在线浏览（规格 2.2 → 整合包）----

    private async Task SearchModpackAsync()
    {
        var items = await LauncherService.Instance.SearchModpacksAsync(
            Query,
            string.IsNullOrEmpty(SelectedGameVersion) ? null : SelectedGameVersion,
            SelectedLoader == "Any" ? null : SelectedLoader,
            _selectedModpackSource,
            CancellationToken.None);

        foreach (var it in items)
        {
            var meta = new List<string>();
            if (!string.IsNullOrEmpty(it.LatestGameVersion)) meta.Add("MC " + it.LatestGameVersion);
            if (it.Loaders.Count > 0) meta.Add(it.LoaderSummary);
            if (!string.IsNullOrEmpty(it.DownloadsText)) meta.Add(it.DownloadsText);

            Cards.Add(new DownloadCardItem
            {
                Id = it.Id,
                Title = it.Title,
                Author = it.Author,
                Summary = it.Summary,
                IconUrl = it.IconUrl,
                FallbackToken = "box",
                Source = it.Source,
                SubTab = "modpack",
                MetaText = string.Join(" · ", meta)
            });
        }
    }

    private async Task SearchMapsAsync(bool resetPage = true)
    {
        var client = LauncherService.Instance.Pixelmap;

        // 新搜索回到第一页；翻页时沿用 ChangeMapPage 已改好的页码
        if (resetPage) _mapPage = 1;

        var result = await client.SearchAsync(
            keyword: string.IsNullOrWhiteSpace(Query) ? null : Query,
            category: string.IsNullOrEmpty(SelectedCategory) ? null : SelectedCategory,
            version: string.IsNullOrEmpty(SelectedMapVersion) ? null : SelectedMapVersion,
            page: _mapPage, limit: 24, sort: _mapSort);

        MapTotalPages = result.PageCount;
        MapPage = result.Page;

        foreach (var it in result.Items)
            Cards.Add(new DownloadCardItem
            {
                Id = it.Slug,
                Title = it.Title,
                Author = it.Author,
                Summary = it.Summary,
                IconUrl = it.PreviewImage,
                FallbackToken = "map",
                Source = "PixelMap",
                SubTab = "map",
                Slug = it.Slug,
                Views = it.Views,
                VersionSummary = it.VersionSummary,
                CategorySummary = it.CategorySummary,
                CanDownload = it.DownloadAllowed,
                HasExtra = it.ExtensionExist
            });
    }

    private void ChangeMapPage(string? dir)
    {
        if (dir == "next" && _mapPage < _mapTotalPages) _mapPage++;
        else if (dir == "prev" && _mapPage > 1) _mapPage--;
        else return;

        MapPage = _mapPage;
        _ = SearchAsync(resetPage: false);
    }

    private void EnqueueSelected(DownloadCardItem? card)
    {
        if (card is null) { StatusMessage = "请先选择一项"; return; }

        string dir;
        string kind;
        switch (card.SubTab)
        {
            case "shader":
                dir = PathEx.ShaderPacksDir(GameConstants.DefaultGameRoot);
                kind = "mod";
                break;
            case "resourcepack":
                dir = PathEx.ResourcePacksDir(GameConstants.DefaultGameRoot);
                kind = "mod";
                break;
            case "modpack":
                dir = Path.Combine(GameConstants.DefaultGameRoot, "modpacks");
                kind = "modpack";
                break;
            case "map":
                dir = PathEx.SavesDir(GameConstants.DefaultGameRoot);
                kind = "map";
                break;
            default:
                dir = PathEx.ModsDir(GameConstants.DefaultGameRoot);
                kind = "mod";
                break;
        }

        var loader = SelectedLoader == "Any" ? LoaderType.Any : Enum.Parse<LoaderType>(SelectedLoader);
        Queue.Add(new DownloadQueueItem
        {
            ProjectId = card.Id,
            Title = card.Title,
            Summary = card.Summary,
            TargetDir = dir,
            GameVersion = string.IsNullOrEmpty(SelectedGameVersion) ? null : SelectedGameVersion,
            Loader = loader,
            Kind = kind,
            Slug = card.Slug,
            Source = card.SubTab == "modpack" ? card.Source : "modrinth"
        });

        StatusMessage = $"已加入队列：{card.Title}（共 {Queue.Count} 项）";
    }

    private async Task StartQueueAsync()
    {
        IsBusy = true;
        try
        {
            foreach (var item in Queue.Where(q => q.Status is "排队中" or "已暂停").ToList())
            {
                item.Cts = new CancellationTokenSource();
                item.Status = item.Kind == "version" ? "安装中" : "下载中";
                item.Progress = 0;

                var local = item;
                var ok = false;
                try
                {
                    ok = local.Kind switch
                    {
                        "modpack" => (await LauncherService.Instance.InstallModpackAsync(
                            local.Source, local.ProjectId, local.GameVersion, local.Title,
                            Progress(local), local.Cts.Token)) is not null,
                        "map" => await LauncherService.Instance.DownloadMapAsync(
                            local.Slug ?? "", Progress(local), local.Cts.Token),
                        "version" => (await LauncherService.Instance.InstallVersionAsync(
                            local.ProjectId, local.InstallLoader, Progress(local), local.Cts.Token)) is not null,
                        _ => await LauncherService.Instance.DownloadModAsync(
                            local.ProjectId, local.TargetDir, local.GameVersion, local.Loader,
                            Progress(local), local.Cts.Token)
                    };

                    if (local.Cts.Token.IsCancellationRequested && local.Status != "已暂停")
                        local.Status = "已取消";
                    else
                        local.Status = ok ? "已完成" : "失败";
                }
                catch (OperationCanceledException)
                {
                    local.Status = "已取消";
                }
                catch
                {
                    local.Status = "失败";
                }

                local.Progress = local.Status == "已完成" ? 100 : local.Progress;
            }

            StatusBarViewModel.Current.DownloadText = "下载队列完成";
            StatusBarViewModel.Current.DownloadProgress = 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static IProgress<double> Progress(DownloadQueueItem item) =>
        new Progress<double>(p =>
        {
            item.Progress = p * 100;
            StatusBarViewModel.Current.DownloadProgress = p * 100;
            StatusBarViewModel.Current.DownloadText = $"下载 {item.Title}：{p:P0}";
        });

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

    private void OpenDetail(DownloadCardItem? card)
    {
        if (card is null) return;

        if (card.SubTab == "modpack")
        {
            _ = OpenModpackDetailAsync(card);
            return;
        }

        if (card.Source == "Minecraft")
        {
            // 打开加载器选择弹窗
            CurrentInstallVersion = card;
            SelectedInstallLoader = "none";
            IsInstallOpen = true;
            return;
        }

        if (card.Source == "PixelMap")
        {
            _ = LoadMapDetailAsync(card);
        }
        else if (!string.IsNullOrEmpty(card.Id))
        {
            // Modrinth 项目页（浏览器打开）
            var url = $"https://modrinth.com/project/{card.Id}";
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                StatusMessage = "无法打开外部浏览器";
            }
        }
    }

    /// <summary>把选中的 Minecraft 版本（含加载器）加入底部下载队列，随后自动开始队列。
    /// fabric 由 FabricInstaller 自动配对最新 Fabric API。</summary>
    private void EnqueueVersionInstall()
    {
        if (CurrentInstallVersion is null) return;
        var card = CurrentInstallVersion;
        var loader = SelectedInstallLoader;

        Queue.Add(new DownloadQueueItem
        {
            ProjectId = card.Id,
            Title = card.Title,
            Summary = LoaderLabel(loader),
            TargetDir = GameConstants.DefaultGameRoot,
            Loader = LoaderType.Any,
            Kind = "version",
            InstallLoader = loader
        });

        StatusMessage = $"已加入安装队列：Minecraft {card.Title}（{LoaderLabel(loader)}）";
        IsInstallOpen = false;

        // 队列空闲则自动开始（若正在下载，新项随当前轮次一并处理）
        if (!IsBusy) _ = StartQueueAsync();
    }

    private static string LoaderLabel(string loader) => loader switch
    {
        "fabric" => "Fabric（自动配对最新 Fabric API）",
        "forge" => "Forge",
        "neoforge" => "NeoForge",
        "quilt" => "Quilt",
        _ => "原版（无加载器）"
    };

    // ---- 整合包详情弹窗 ----

    private async Task OpenModpackDetailAsync(DownloadCardItem card)
    {
        IsBusy = true;
        try
        {
            var detail = await LauncherService.Instance.GetModpackDetailAsync(card.Source, card.Id, CancellationToken.None);
            if (detail is null) { StatusMessage = "无法获取整合包详情"; return; }

            CurrentModpackSourceId = card.Source;
            CurrentModpackDetail = detail;
            SelectedModpackVersion = detail.Versions.FirstOrDefault();
            InstallIsolated = true;

            ModpackDetailHint = detail.HasVersions
                ? (ModpackDetailCanInstall
                    ? ""
                    : (ModpackDetailIsCurseForge
                        ? "作者未开放第三方下载，请前往来源页获取。"
                        : "该版本无可用的直链下载。"))
                : "该整合包暂无可安装版本。";

            IsModpackDetailOpen = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"整合包详情加载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>按详情窗所选版本安装整合包（支持隔离安装；CurseForge 走共享目录）。</summary>
    private async Task InstallModpackAsync()
    {
        if (CurrentModpackDetail is null || SelectedModpackVersion is null) return;
        IsBusy = true;
        ModpackDetailHint = "正在安装整合包…";
        try
        {
            var progress = new Progress<double>(p =>
            {
                StatusBarViewModel.Current.DownloadProgress = p * 100;
                StatusBarViewModel.Current.DownloadText = $"安装 {CurrentModpackDetail.Title}：{p:P0}";
            });

            var isolated = InstallIsolated && !ModpackDetailIsCurseForge;
            var result = await LauncherService.Instance.InstallModpackVersionAsync(
                CurrentModpackSourceId, SelectedModpackVersion, isolated,
                CurrentModpackDetail.Title, progress, CancellationToken.None);

            if (result is null)
            {
                ModpackDetailHint = "安装失败：所选版本无可用下载链接。";
                StatusMessage = "整合包安装失败";
                return;
            }

            var where = result.Isolated ? $"隔离目录 versions/{result.VersionId}" : "共享目录";
            var renamed = result.Renamed ? "（因重名已自动改名）" : "";
            ModpackDetailHint = $"已安装：{result.Name} → {where}{renamed}（{result.ModCount} 个 Mod）";
            StatusMessage = $"整合包已安装：{result.Name}";
        }
        catch (Exception ex)
        {
            ModpackDetailHint = $"安装失败：{ex.Message}";
            StatusMessage = $"整合包安装失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>在系统浏览器打开当前整合包的站内详情页。</summary>
    private void OpenModpackPage()
    {
        var url = CurrentModpackDetail?.PageUrl;
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            StatusMessage = "无法打开外部浏览器";
        }
    }

    private async Task LoadMapDetailAsync(DownloadCardItem card)
    {
        IsBusy = true;
        try
        {
            var detail = await LauncherService.Instance.Pixelmap.GetDetailAsync(card.Slug ?? "", CancellationToken.None);
            if (detail is null) { StatusMessage = "无法获取地图详情"; return; }

            CurrentMapDetail = detail;
            DetailHint = detail.CanDownload
                ? detail.HasAdditionalResources
                    ? "该地图附带资源包 / 光影，可一并安装。"
                    : ""
                : "作者未授权启动器直接下载，请前往地图站页面获取。";
            IsDetailOpen = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"详情加载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DownloadDetailAsync()
    {
        if (CurrentMapDetail is null) return;
        IsBusy = true;
        DetailHint = "正在下载地图…";
        try
        {
            var ok = await LauncherService.Instance.DownloadMapAsync(CurrentMapDetail.Slug, null, CancellationToken.None);
            StatusMessage = ok ? $"地图已安装：{CurrentMapDetail.Title}" : "地图下载/安装失败";
            DetailHint = ok
                ? $"已解压进 saves：{CurrentMapDetail.Title}"
                : "地图下载或解压失败，可尝试打开地图站页面手动下载。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"地图下载失败：{ex.Message}";
            DetailHint = $"下载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 下载附加资源并按类型分发：资源包进 resourcepacks、光影进 shaderpacks、
    /// 数据包暂存 downloads/extras/datapacks（需用户自行放入具体存档）。
    /// </summary>
    private async Task DownloadExtraAsync()
    {
        if (CurrentMapDetail is null) return;
        IsBusy = true;
        DetailHint = "正在下载附加资源…";
        try
        {
            var result = await LauncherService.Instance.DownloadMapExtraAsync(
                CurrentMapDetail, null, CancellationToken.None);

            if (result is null)
            {
                DetailHint = "该地图没有提供附加资源直链。";
                StatusMessage = "无附加资源";
                return;
            }

            if (!result.Ok)
            {
                DetailHint = $"附加资源安装失败：{result.Error}";
                StatusMessage = "附加资源安装失败";
                return;
            }

            var hint = $"附加资源已安装：{result.Summary}";
            if (result.HasUnknown)
                hint += "（部分文件未能识别类型，已保留在 downloads/extras）";
            DetailHint = hint;
            StatusMessage = hint;
        }
        catch (Exception ex)
        {
            DetailHint = $"附加资源下载失败：{ex.Message}";
            StatusMessage = $"附加资源下载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>在系统浏览器打开当前地图的站内详情页。</summary>
    private void OpenMapPage()
    {
        var url = CurrentMapDetail?.PageUrl;
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            StatusMessage = "无法打开外部浏览器";
        }
    }
}
