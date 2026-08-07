using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using MCLCS.Core.Auth;
using MCLCS.Core.Download;
using MCLCS.Core.Installers;
using MCLCS.Core.Launcher;
using MCLCS.Core.Models;
using MCLCS.Core.Profiles;
using MCLCS.Core.Save;
using MCLCS.Core.Utils;

namespace MCLCS.App.Services;

/// <summary>
/// 应用级服务：持有 HttpClient / 下载器 / 游戏目录，并暴露安装、启动、下载 Mod、版本列表等操作。
/// 实现 ILogger 以将进度回传到 UI。
/// </summary>
public class LauncherService : ILogger
{
    public static LauncherService Instance { get; private set; } = new(GameConstants.DefaultGameRoot);

    private readonly HttpClient _client = new();
    private readonly IDownloader _downloader;

    /// <summary>整合包在线源注册表（Modrinth 常驻；CurseForge 仅在设置页填入 Key 后可用）。</summary>
    private readonly IModpackSource[] _modpackSources;

    public string GameRoot { get; }

    public event Action<string>? Logged;

    /// <summary>底层 HttpClient（供外联图标/封面等 UI 层复用，已带 MCLCS User-Agent）。</summary>
    public HttpClient ApiClient => _client;

    /// <summary>像素茶艺（PixelMap）地图站客户端（下载页 → 地图）。</summary>
    public PixelmapClient Pixelmap { get; }

    public LauncherService(string gameRoot)
    {
        GameRoot = gameRoot;
        _downloader = new HttpDownloader(_client, 8, this);

        // 规格 2.2：地图站要求 User-Agent 为 MCLCS/版本号 (Windows; +仓库地址)
        _client.DefaultRequestHeaders.UserAgent.TryParseAdd(
            $"MCLCS/{GameConstants.LauncherVersion} (Windows; +https://github.com/bingjianremix/MCLCS)");

        Pixelmap = new PixelmapClient(_client);

        // 整合包在线源：Modrinth 免 Key 常驻；CurseForge 的 Key 通过延迟读取设置页配置，
        // 用户未填时其 IsAvailable=false（下载页禁用该来源，避免出现点了没反应的死按钮）。
        _modpackSources = new IModpackSource[]
        {
            new ModrinthModpackSource(_client),
            new CurseForgeModpackSource(_client, () => ProfileStore.Load(gameRoot).CurseForgeApiKey)
        };
    }

    /// <summary>当前可用的整合包在线源。</summary>
    public IReadOnlyList<IModpackSource> ModpackSources => _modpackSources;

    /// <summary>按 Id 取得整合包源（未知 Id 回退到 Modrinth）。</summary>
    public IModpackSource GetModpackSource(string? id) =>
        _modpackSources.FirstOrDefault(s => s.Id == (id ?? "")) ?? _modpackSources[0];

    /// <summary>搜索整合包（聚合 Modrinth / CurseForge，依 <paramref name="sourceId"/> 选择来源）。</summary>
    public async Task<List<ModpackItem>> SearchModpacksAsync(string? keyword, string? gameVersion,
        string? loader, string? sourceId, CancellationToken ct)
    {
        var source = GetModpackSource(sourceId);
        if (!source.IsAvailable) return new List<ModpackItem>();
        return await source.SearchAsync(keyword, gameVersion, loader, 24, 0, ct);
    }

    /// <summary>获取整合包详情（含可安装版本列表）。</summary>
    public async Task<ModpackDetail?> GetModpackDetailAsync(string? sourceId, string id, CancellationToken ct)
    {
        var source = GetModpackSource(sourceId);
        if (!source.IsAvailable) return null;
        return await source.GetDetailAsync(id, ct);
    }

    /// <summary>
    /// 安装指定整合包（按 <paramref name="version"/> 直链下载后落地）。
    /// Modrinth .mrpack 支持<paramref name="isolated"/> 隔离目录；CurseForge .zip 复用现有安装器（非隔离）。
    /// </summary>
    public async Task<ModpackInstallResult?> InstallModpackVersionAsync(
        string? sourceId, ModpackVersion version, bool isolated, string? preferredName,
        IProgress<double>? progress, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(version.FileUrl)) return null;

        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")
            + (sourceId == "curseforge" ? ".zip" : ".mrpack"));
        try
        {
            await _downloader.DownloadAsync(new DownloadItem(new[] { version.FileUrl }, tmp, version.Sha1), progress, ct);

            if (sourceId == "curseforge")
            {
                var cf = new CurseForgeModpackInstaller(GameRoot, _client, _downloader, this);
                await cf.InstallAsync(tmp, null, ct);
                return new ModpackInstallResult
                {
                    Name = string.IsNullOrWhiteSpace(version.VersionNumber) ? version.FileName : version.VersionNumber,
                    Isolated = false
                };
            }

            var installer = new ModpackInstaller(GameRoot, _client, _downloader, this);
            return await installer.InstallAsync(tmp, isolated, preferredName, null, ct);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* 忽略 */ }
        }
    }

    /// <summary>
    /// 便捷安装：按项目 Id 取详情并挑选最新（匹配 <paramref name="gameVersion"/> 优先）版本安装。
    /// 默认隔离安装（规格用户决策：独立版本隔离目录）。
    /// </summary>
    public async Task<ModpackInstallResult?> InstallModpackAsync(
        string? sourceId, string projectId, string? gameVersion, string? preferredName,
        IProgress<double>? progress, CancellationToken ct)
    {
        var detail = await GetModpackDetailAsync(sourceId, projectId, ct);
        if (detail is null || detail.Versions.Count == 0) return null;

        var version = detail.Versions.FirstOrDefault(v =>
                          string.IsNullOrEmpty(gameVersion) || string.Equals(v.GameVersion, gameVersion, StringComparison.OrdinalIgnoreCase))
                      ?? detail.Versions[0];

        return await InstallModpackVersionAsync(sourceId, version, isolated: true, preferredName, progress, ct);
    }

    public void Log(string message) => Logged?.Invoke(message);

    // ---- 版本列表 ----

    public async Task<List<string>> GetVanillaVersionsAsync()
    {
        try
        {
            var json = await MirrorPolicy.GetStringWithFallback(MirrorPolicy.VersionManifestUrls(), _client);
            var manifest = System.Text.Json.JsonSerializer.Deserialize<VersionManifest>(json);
            return manifest?.Versions.Select(v => v.Id).ToList() ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public List<(string Id, string Type)> ListInstalledVersions()
    {
        var result = new List<(string Id, string Type)>();
        var versionsDir = PathEx.VersionsDir(GameRoot);
        if (!Directory.Exists(versionsDir)) return result;

        foreach (var dir in Directory.GetDirectories(versionsDir))
        {
            var id = Path.GetFileName(dir);
            var jsonPath = PathEx.VersionJsonPath(GameRoot, id);
            if (!File.Exists(jsonPath)) continue;
            var type = "";
            try
            {
                var json = File.ReadAllText(jsonPath);
                var v = System.Text.Json.JsonSerializer.Deserialize<VersionJson>(json);
                type = v?.Type ?? "";
            }
            catch { /* 忽略解析错误 */ }
            result.Add((id, type));
        }
        return result;
    }

    // ---- 安装 ----

    public async Task InstallAsync(string installType, string versionId,
        IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        InstallerBase installer = installType.ToLowerInvariant() switch
        {
            "fabric" => new FabricInstaller(GameRoot, _client, _downloader, this),
            "forge" => new ForgeInstaller(GameRoot, _client, _downloader, this),
            "neoforge" => new NeoForgeInstaller(GameRoot, _client, _downloader, this),
            "quilt" => new QuiltInstaller(GameRoot, _client, _downloader, this),
            _ => new VanillaInstaller(GameRoot, _client, _downloader, this)
        };

        switch (installer)
        {
            case VanillaInstaller v: await v.InstallAsync(versionId, progress, ct); break;
            case FabricInstaller f: await f.InstallAsync(versionId, progress, ct); break;
            case ForgeInstaller g: await g.InstallAsync(versionId, progress, ct); break;
            case NeoForgeInstaller n: await n.InstallAsync(versionId, progress, ct); break;
            case QuiltInstaller q: await q.InstallAsync(versionId, progress, ct); break;
        }
    }

    // ---- 版本安装（下载页 → Minecraft 下载）----

    /// <summary>返回完整版本清单条目（含类型 / 发布时间），供下载页 Minecraft 子页列举与分类。</summary>
    public async Task<List<VersionEntry>> GetVanillaVersionsDetailedAsync()
    {
        try
        {
            var json = await MirrorPolicy.GetStringWithFallback(MirrorPolicy.VersionManifestUrls(), _client);
            var manifest = System.Text.Json.JsonSerializer.Deserialize<VersionManifest>(json);
            return manifest?.Versions ?? new List<VersionEntry>();
        }
        catch
        {
            return new List<VersionEntry>();
        }
    }

    /// <summary>
    /// 安装一个 Minecraft 版本（下载页 Minecraft 子页调用）。
    /// <paramref name="loader"/> 为 none / forge / fabric / neoforge / quilt；
    /// fabric 由 <see cref="FabricInstaller"/> 自动配对最新稳定 Fabric API。
    /// 返回安装得到的新版本 Id（原版返回 <paramref name="mcVersion"/>）。
    /// </summary>
    public async Task<string?> InstallVersionAsync(string mcVersion, string loader,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        switch (loader.ToLowerInvariant())
        {
            case "fabric":
                return await new FabricInstaller(GameRoot, _client, _downloader, this).InstallAsync(mcVersion, null, ct);
            case "forge":
                return await new ForgeInstaller(GameRoot, _client, _downloader, this).InstallAsync(mcVersion, null, ct);
            case "neoforge":
                return await new NeoForgeInstaller(GameRoot, _client, _downloader, this).InstallAsync(mcVersion, null, ct);
            case "quilt":
                return await new QuiltInstaller(GameRoot, _client, _downloader, this).InstallAsync(mcVersion, null, ct);
            default:
                await new VanillaInstaller(GameRoot, _client, _downloader, this).InstallAsync(mcVersion, null, ct);
                return mcVersion;
        }
    }

    // ---- 启动 ----

    /// <summary>使用指定认证方式启动游戏。authenticator 为 null 时默认离线。cliOverrides 可覆盖内存/用户名/Java。</summary>
    public async Task<LaunchResult> LaunchAsync(string versionId,
        IAuthenticator? authenticator = null,
        LaunchCliOverrides? cliOverrides = null,
        CancellationToken ct = default)
    {
        var profile = ProfileStore.Load(GameRoot);
        var java = await ResolveJavaAsync(profile, cliOverrides, ct);

        // 解析账号
        AuthSession session;
        if (authenticator is not null)
        {
            var account = AccountStore.GetLastUsed(GameRoot);
            var username = cliOverrides?.Username ?? account?.Username ?? profile.DefaultUsername;
            session = await authenticator.AuthenticateAsync(username, ct);
        }
        else
        {
            var username = cliOverrides?.Username ?? profile.DefaultUsername;
            session = await new OfflineAuthenticator().AuthenticateAsync(username, ct);
        }

        var options = new LaunchOptions
        {
            Username = session.Username,
            Uuid = session.Uuid,
            AccessToken = session.AccessToken,
            UserType = session.UserType,
            MaxMemoryMb = cliOverrides?.MaxMemoryMb ?? profile.MaxMemoryMb,
            MinMemoryMb = cliOverrides?.MinMemoryMb ?? profile.MinMemoryMb,
            ExtraJvmArgs = profile.ExtraJvmArgs,
            ServerAddress = cliOverrides?.ServerAddress
        };

        if (profile.ResolutionWidth.HasValue && profile.ResolutionHeight.HasValue)
            options.Resolution = (profile.ResolutionWidth.Value, profile.ResolutionHeight.Value);

        // 记住最后使用的版本
        profile.LastVersionId = versionId;
        ProfileStore.Save(profile);

        return await GameLauncher.LaunchAsync(GameRoot, versionId, java, options, this, ct);
    }

    /// <summary>
    /// 执行一次崩溃自动修复。所有修复均为非破坏性：
    /// 调大内存仅改启动器配置、切换 Java 仅影响外部 Java、重下库仅重写依赖缓存、
    /// 禁用冲突 Mod 仅重命名为 .disabled（可还原）、安装缺失前置仅向 mods 目录新增文件。
    /// 返回修复是否成功执行。
    /// </summary>
    public async Task<bool> ApplyRepairAsync(CrashRepairPlan plan, CancellationToken ct = default)
    {
        var profile = ProfileStore.Load(GameRoot);

        switch (plan.Strategy)
        {
            case RepairStrategy.IncreaseMemory:
                if (plan.TargetMemoryMb is not null)
                {
                    profile.MaxMemoryMb = plan.TargetMemoryMb.Value;
                    ProfileStore.Save(profile);
                    Log($"自动修复：内存调整至 {plan.TargetMemoryMb.Value}MB");
                    return true;
                }
                return false;

            case RepairStrategy.SwitchJava:
            {
                var required = plan.RequiredJavaMajor ?? GameConstants.MinimumJavaMajorVersion;
                var java = await JavaDetector.FindBestAsync(required);
                if (java is null)
                {
                    Log($"未找到 Java {required}+，尝试下载安装（{profile.PreferredJavaVendor}）…");
                    java = await JavaInstaller.EnsureJavaAsync(required, GameRoot, _downloader, profile.PreferredJavaVendor, this, ct);
                }
                if (java is null)
                {
                    Log($"自动修复失败：无法获取 Java {required}+");
                    return false;
                }
                profile.JavaPath = java.JavaExe;
                ProfileStore.Save(profile);
                Log($"自动修复：切换 Java 至 {java}");
                return true;
            }

            case RepairStrategy.RedownloadLibraries:
                if (string.IsNullOrEmpty(plan.VersionId)) return false;
                var repair = await LibraryRepair.RepairAsync(GameRoot, plan.VersionId, _client, _downloader, this, ct);
                return repair.Success || repair.AllHealthy;

            case RepairStrategy.DisableConflictingMods:
                return ApplyDisableConflictingMods(plan);

            case RepairStrategy.InstallMissingModDependency:
                return await ApplyInstallMissingModsAsync(plan, ct);

            // §四.2 降级联动
            case RepairStrategy.RevertDowngradeBackup:
            case RepairStrategy.RetryDowngradeOtherMethod:
            case RepairStrategy.InstallOriginalVersion:
                return await ApplyDowngradeRecoveryAsync(plan, ct);

            case RepairStrategy.None:
            default:
                return false;
        }
    }

    /// <summary>禁用冲突 Mod：保留用户选定的一个，其余重命名为 .disabled（不删除）。</summary>
    private bool ApplyDisableConflictingMods(CrashRepairPlan plan)
    {
        bool any = false;
        foreach (var mod in plan.ConflictingMods)
        {
            if (string.IsNullOrEmpty(mod.FilePath)) continue;
            if (string.Equals(mod.FilePath, plan.KeepModFile, StringComparison.OrdinalIgnoreCase)) continue;
            if (!File.Exists(mod.FilePath)) continue;
            if (mod.FilePath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)) continue;

            var disabled = mod.FilePath + ".disabled";
            try
            {
                if (File.Exists(disabled))
                {
                    // 已存在 .disabled 副本，直接移除启用的那份
                    File.Delete(mod.FilePath);
                }
                else
                {
                    File.Move(mod.FilePath, disabled);
                }
                Log($"禁用冲突 Mod：{Path.GetFileName(mod.FilePath)} → {Path.GetFileName(disabled)}");
                any = true;
            }
            catch (Exception ex)
            {
                Log($"禁用 Mod 失败 {mod.FilePath}：{ex.Message}");
            }
        }
        return any || plan.ConflictingMods.Count > 0;
    }

    /// <summary>自动安装缺失的 Mod 前置依赖（从 Modrinth 下载到 mods 目录）。</summary>
    private async Task<bool> ApplyInstallMissingModsAsync(CrashRepairPlan plan, CancellationToken ct)
    {
        if (plan.MissingModDependencies.Count == 0) return false;

        var loader = DetectLoader(GameRoot, plan.VersionId);
        var gameVersion = ExtractGameVersion(GameRoot, plan.VersionId);
        var client = new ModrinthClient(_client);

        var allOk = true;
        foreach (var id in plan.MissingModDependencies)
        {
            try
            {
                var ok = await InstallModDependencyAsync(client, id, loader, gameVersion, ct);
                if (ok) Log($"已安装缺失前置：{id}");
                else { Log($"未找到可安装的缺失前置：{id}"); allOk = false; }
            }
            catch (Exception ex)
            {
                Log($"安装缺失前置失败 {id}：{ex.Message}");
                allOk = false;
            }
        }
        return allOk;
    }

    /// <summary>§四.2 降级联动恢复：回滚备份 / 改用 Amulet 重试 / 安装存档原版本。</summary>
    private async Task<bool> ApplyDowngradeRecoveryAsync(CrashRepairPlan plan, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(plan.SavePath))
        {
            Log("降级联动恢复失败：未指定存档路径。");
            return false;
        }

        switch (plan.Strategy)
        {
            case RepairStrategy.RevertDowngradeBackup:
            {
                if (string.IsNullOrEmpty(plan.BackupPath) || !Directory.Exists(plan.BackupPath))
                {
                    Log("回滚失败：找不到降级备份。");
                    return false;
                }
                var replaced = SaveDowngrader.RestoreBackupAsync(plan.BackupPath, plan.SavePath);
                Log($"已回滚到降级前备份（当前存档另存于 {replaced}）。");
                return true;
            }

            case RepairStrategy.RetryDowngradeOtherMethod:
            {
                if (string.IsNullOrEmpty(plan.BackupPath) || !Directory.Exists(plan.BackupPath))
                {
                    Log("改用其他方式失败：找不到降级备份。");
                    return false;
                }
                // 以当前存档的目标 DataVersion 为基准，先回滚到干净备份，再用 Amulet 重做降级
                var targetDv = SaveDowngrader.GetSaveDataVersion(plan.SavePath);
                var targetVer = DataVersionMap.ToGameVersion(targetDv);
                if (targetVer is null)
                {
                    Log($"改用其他方式失败：目标 DataVersion {targetDv} 不在对照表中。");
                    return false;
                }
                SaveDowngrader.RestoreBackupAsync(plan.BackupPath, plan.SavePath);
                var dp = await SaveDowngrader.DowngradeAsync(plan.SavePath, targetVer, DowngradeMethod.Amulet);
                if (dp.Success) Log($"已用 Amulet 重新降级到 {targetVer}。");
                else Log($"改用 Amulet 降级失败：{dp.ErrorMessage}");
                return dp.Success;
            }

            case RepairStrategy.InstallOriginalVersion:
            {
                if (string.IsNullOrEmpty(plan.VersionId))
                {
                    Log("安装原版本失败：未记录原版本号。");
                    return false;
                }
                try
                {
                    var installType = DetectInstallType(plan.VersionId);
                    Log($"正在安装存档原版本 {plan.VersionId}（{installType}）…");
                    await InstallAsync(installType, plan.VersionId, ct: ct);
                    // 安装后把"最后使用版本"指向原版本，使后续启动用正确版本打开存档
                    var p = ProfileStore.Load(GameRoot);
                    p.LastVersionId = plan.VersionId;
                    ProfileStore.Save(p);
                    Log($"已安装原版本 {plan.VersionId}，将用该版本打开存档。");
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"安装原版本失败：{ex.Message}（可手动在「安装新版本」中安装 {plan.VersionId}）");
                    return false;
                }
            }

            default:
                return false;
        }
    }

    /// <summary>由版本 id 前缀推断安装类型（vanilla / fabric / forge / quilt / neoforge）。</summary>
    private static string DetectInstallType(string versionId)
    {
        var v = versionId.ToLowerInvariant();
        if (v.StartsWith("neoforge")) return "neoforge";
        if (v.StartsWith("fabric")) return "fabric";
        if (v.StartsWith("forge")) return "forge";
        if (v.StartsWith("quilt")) return "quilt";
        return "vanilla";
    }

    private async Task<bool> InstallModDependencyAsync(ModrinthClient client, string modId,
        LoaderType loader, string? gameVersion, CancellationToken ct)
    {
        var search = await client.SearchAsync(modId, gameVersion, loader, ModrinthProjectType.Mod, limit: 5, ct: ct);
        var hit = search.Hits.FirstOrDefault(h => string.Equals(h.Slug, modId, StringComparison.OrdinalIgnoreCase))
                  ?? search.Hits.FirstOrDefault();
        if (hit is null) return false;

        var versions = await client.GetVersionsAsync(hit.ProjectId, ct);
        var ver = versions.FirstOrDefault(v =>
                        (gameVersion is null || v.GameVersions.Contains(gameVersion))
                        && (loader == LoaderType.Any || v.Loaders.Contains(ModrinthClient.LoaderString(loader), StringComparer.OrdinalIgnoreCase)))
                  ?? versions.FirstOrDefault();
        if (ver is null) return false;

        var file = client.SelectBestFile(ver, gameVersion, loader);
        if (file is null) return false;

        var modsDir = PathEx.ModsDir(GameRoot);
        Directory.CreateDirectory(modsDir);
        var dest = Path.Combine(modsDir, file.FileName);
        await _downloader.DownloadAsync(new DownloadItem(new[] { file.Url }, dest, file.Hashes.Sha1), null, ct);
        return true;
    }

    /// <summary>从版本合并结果推断加载器类型。</summary>
    private static LoaderType DetectLoader(string gameRoot, string? versionId)
    {
        if (string.IsNullOrEmpty(versionId)) return LoaderType.Any;
        try
        {
            var merged = VersionMerger.Merge(gameRoot, versionId);
            var mc = merged.MainClass ?? "";
            if (mc.Contains("fabricmc", StringComparison.OrdinalIgnoreCase)) return LoaderType.Fabric;
            if (mc.Contains("neoforge", StringComparison.OrdinalIgnoreCase)) return LoaderType.NeoForge;
            if (mc.Contains("forge", StringComparison.OrdinalIgnoreCase)) return LoaderType.Forge;
            if (mc.Contains("quilt", StringComparison.OrdinalIgnoreCase)) return LoaderType.Quilt;
        }
        catch { /* 忽略 */ }
        return LoaderType.Any;
    }

    /// <summary>从版本 id（如 fabric-1.20.1）中提取 Minecraft 游戏版本号。</summary>
    private static string? ExtractGameVersion(string gameRoot, string? versionId)
    {
        if (string.IsNullOrEmpty(versionId)) return null;
        var m = Regex.Match(versionId, @"\d+\.\d+(?:\.\d+)?");
        return m.Success ? m.Value : null;
    }

    private async Task<JavaInfo> ResolveJavaAsync(LauncherProfile profile, LaunchCliOverrides? cliOverrides, CancellationToken ct)
    {
        // CLI 指定的 Java 路径优先
        if (!string.IsNullOrEmpty(cliOverrides?.JavaPath) && File.Exists(cliOverrides.JavaPath))
        {
            var (major, raw) = await JavaDetector.QueryVersionAsync(cliOverrides.JavaPath);
            if (major >= GameConstants.MinimumJavaMajorVersion)
                return new JavaInfo { JavaExe = cliOverrides.JavaPath, MajorVersion = major, RawVersion = raw };
        }

        JavaInfo? java = null;
        if (!string.IsNullOrEmpty(profile.JavaPath) && File.Exists(profile.JavaPath))
        {
            var (major, raw) = await JavaDetector.QueryVersionAsync(profile.JavaPath);
            if (major >= GameConstants.MinimumJavaMajorVersion)
                java = new JavaInfo { JavaExe = profile.JavaPath, MajorVersion = major, RawVersion = raw };
        }

        java ??= await JavaDetector.FindBestAsync(GameConstants.MinimumJavaMajorVersion);
        if (java is null)
            java = await JavaInstaller.EnsureJavaAsync(GameConstants.MinimumJavaMajorVersion, GameRoot, _downloader, this, ct);

        return java ?? throw new InvalidOperationException("未找到可用的 Java 运行环境");
    }

    // ---- 下载中心（Modrinth）----

    public async Task<List<ModrinthHit>> SearchModsAsync(string query, string? gameVersion, LoaderType loader, ModrinthProjectType type)
    {
        var client = new ModrinthClient(_client);
        var result = await client.SearchAsync(query, gameVersion, loader, type);
        return result.Hits;
    }

    public async Task<bool> DownloadModAsync(string projectId, string targetDir, string? gameVersion, LoaderType loader)
    {
        var client = new ModrinthClient(_client);
        var versions = await client.GetVersionsAsync(projectId);
        var ver = versions.FirstOrDefault(v =>
                        (string.IsNullOrEmpty(gameVersion) || v.GameVersions.Contains(gameVersion))
                        && (loader == LoaderType.Any || v.Loaders.Contains(ModrinthClient.LoaderString(loader), StringComparer.OrdinalIgnoreCase)))
                  ?? versions.FirstOrDefault();
        if (ver is null) return false;

        var file = client.SelectBestFile(ver, gameVersion, loader);
        if (file is null) return false;

        Directory.CreateDirectory(targetDir);
        var dest = Path.Combine(targetDir, file.FileName);
        await _downloader.DownloadAsync(new DownloadItem(new[] { file.Url }, dest, file.Hashes.Sha1), null, CancellationToken.None);
        return true;
    }

    /// <summary>带进度与取消的 Mod 下载（供下载队列使用）。</summary>
    public async Task<bool> DownloadModAsync(string projectId, string targetDir, string? gameVersion,
        LoaderType loader, IProgress<double>? progress, CancellationToken ct)
    {
        var client = new ModrinthClient(_client);
        var versions = await client.GetVersionsAsync(projectId);
        var ver = versions.FirstOrDefault(v =>
                        (string.IsNullOrEmpty(gameVersion) || v.GameVersions.Contains(gameVersion))
                        && (loader == LoaderType.Any || v.Loaders.Contains(ModrinthClient.LoaderString(loader), StringComparer.OrdinalIgnoreCase)))
                  ?? versions.FirstOrDefault();
        if (ver is null) return false;

        var file = client.SelectBestFile(ver, gameVersion, loader);
        if (file is null) return false;

        Directory.CreateDirectory(targetDir);
        var dest = Path.Combine(targetDir, file.FileName);
        await _downloader.DownloadAsync(new DownloadItem(new[] { file.Url }, dest, file.Hashes.Sha1), progress, ct);
        return true;
    }

    // ---- 下载页：地图（像素茶艺）----

    /// <summary>下载并安装地图：先取详情直链，下载压缩包，再用 MapInstaller 解压进 saves。</summary>
    public async Task<bool> DownloadMapAsync(string slug, IProgress<double>? progress, CancellationToken ct)
    {
        var detail = await Pixelmap.GetDetailAsync(slug, ct);
        if (detail is null || !detail.CanDownload) return false;

        var item = PixelmapClient.ToDownloadItem(detail, GameRoot);
        if (item is null) return false;

        await _downloader.DownloadAsync(item, progress, ct);
        var result = MapInstaller.Install(item.Destination, GameRoot);
        return result.Ok;
    }

    /// <summary>
    /// 下载地图的附加资源（资源包 / 光影）并智能分发到 resourcepacks / shaderpacks。
    /// 详情不含附加资源直链时返回 null。
    /// </summary>
    public async Task<ExtraResourceInstallResult?> DownloadMapExtraAsync(
        PixelMapDetail detail, IProgress<double>? progress, CancellationToken ct)
    {
        var item = PixelmapClient.ToExtraDownloadItem(detail, GameRoot);
        if (item is null) return null;

        await _downloader.DownloadAsync(item, progress, ct);
        return ExtraResourceInstaller.Install(item.Destination, GameRoot, detail.Title);
    }

    // ---- 下载页：整合包（Modrinth .mrpack / CurseForge .zip）----
    // 安装入口见 InstallModpackAsync / InstallModpackVersionAsync（源无关、支持隔离安装）。
}

/// <summary>CLI 传入的启动参数覆盖（不持久化）。</summary>
public class LaunchCliOverrides
{
    public string? Username { get; set; }
    public int? MaxMemoryMb { get; set; }
    public int? MinMemoryMb { get; set; }
    public string? JavaPath { get; set; }
    public List<string>? ExtraJvmArgs { get; set; }
    /// <summary>直接连入服务器地址（host:port），启动后跳过主菜单自动加入。</summary>
    public string? ServerAddress { get; set; }
}
