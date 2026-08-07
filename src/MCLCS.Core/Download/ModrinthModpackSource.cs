using MCLCS.Core.Models;
using MCLCS.Core.Utils;

namespace MCLCS.Core.Download;

/// <summary>
/// Modrinth 整合包源。免 API Key，直接走 <c>/v2/search</c> + <c>/v2/project/{id}/version</c>。
/// </summary>
public class ModrinthModpackSource : IModpackSource
{
    private readonly ModrinthClient _client;

    public ModrinthModpackSource(HttpClient http) => _client = new ModrinthClient(http);

    public string Id => "modrinth";
    public string DisplayName => "Modrinth";
    public bool IsAvailable => true;
    public string? UnavailableReason => null;

    /// <summary>已知的加载器标签（用于从 categories 里筛出加载器）。</summary>
    private static readonly string[] KnownLoaders = { "fabric", "forge", "quilt", "neoforge", "liteloader", "rift" };

    public async Task<List<ModpackItem>> SearchAsync(string? keyword, string? gameVersion, string? loader,
        int limit = 24, int offset = 0, CancellationToken ct = default)
    {
        try
        {
            var loaderType = ParseLoader(loader);
            var result = await _client.SearchAsync(
                keyword ?? "", gameVersion, loaderType, ModrinthProjectType.Modpack,
                limit, offset, index: null, ct).ConfigureAwait(false);

            return result.Hits.Select(h => new ModpackItem
            {
                Source = Id,
                Id = h.ProjectId,
                Title = h.Title,
                Summary = h.Description,
                Author = h.Slug,
                IconUrl = string.IsNullOrWhiteSpace(h.IconUrl) ? null : h.IconUrl,
                Downloads = h.Downloads,
                GameVersions = SortVersionsDescending(h.Versions),
                Loaders = h.Categories.Where(c => KnownLoaders.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList()
            }).ToList();
        }
        catch
        {
            return new List<ModpackItem>();
        }
    }

    public async Task<ModpackDetail?> GetDetailAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        try
        {
            var project = await _client.GetProjectAsync(id, ct).ConfigureAwait(false);
            if (project is null) return null;

            var versions = await _client.GetVersionsAsync(id, ct).ConfigureAwait(false);

            return new ModpackDetail
            {
                Source = Id,
                Id = project.Id,
                Title = project.Title,
                Summary = project.Description,
                Author = project.Slug,
                IconUrl = string.IsNullOrWhiteSpace(project.IconUrl) ? null : project.IconUrl,
                Description = MarkdownText.ToPlainText(project.Body, 4000),
                Downloads = project.Downloads,
                Versions = MapVersions(versions),
                PageUrl = $"https://modrinth.com/modpack/{project.Slug}"
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>把 Modrinth 版本列表映射成统一版本视图（纯函数，便于自检）。</summary>
    public static List<ModpackVersion> MapVersions(List<ModrinthVersion> versions)
    {
        var result = new List<ModpackVersion>();
        foreach (var v in versions)
        {
            // 整合包本体优先取 .mrpack；没有则退回 primary 文件
            var file = v.Files.FirstOrDefault(f => f.FileName.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase))
                       ?? v.Files.FirstOrDefault(f => f.Primary)
                       ?? v.Files.FirstOrDefault();
            if (file is null) continue;

            result.Add(new ModpackVersion
            {
                Id = v.Id,
                Name = v.Name,
                VersionNumber = v.VersionNumber,
                GameVersion = v.GameVersions.LastOrDefault() ?? "",
                Loader = v.Loaders.FirstOrDefault() ?? "",
                FileUrl = file.Url,
                FileName = file.FileName,
                Sha1 = string.IsNullOrWhiteSpace(file.Hashes.Sha1) ? null : file.Hashes.Sha1,
                FileSize = file.Size
            });
        }
        return result;
    }

    /// <summary>把版本号按语义倒序（新版本在前），无法解析的排到末尾。</summary>
    public static List<string> SortVersionsDescending(IEnumerable<string> versions)
    {
        return versions
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct()
            .OrderByDescending(VersionKey)
            .ToList();
    }

    private static (int Major, int Minor, int Patch) VersionKey(string v)
    {
        var parts = v.Split('.', '-', '_');
        int Get(int i) => parts.Length > i && int.TryParse(parts[i], out var n) ? n : 0;
        return (Get(0), Get(1), Get(2));
    }

    private static LoaderType ParseLoader(string? loader) => (loader ?? "").ToLowerInvariant() switch
    {
        "fabric" => LoaderType.Fabric,
        "forge" => LoaderType.Forge,
        "quilt" => LoaderType.Quilt,
        "neoforge" => LoaderType.NeoForge,
        _ => LoaderType.Any
    };
}

/// <summary>
/// CurseForge 整合包源。CurseForge 的所有 REST 端点（含 <c>/v1/mods/search</c>）都强制要求
/// <c>x-api-key</c>，官方不提供匿名层，且服务条款禁止在开源项目中分发共享 Key。
/// 因此这里只在用户于设置页填入自己的 Key 后才启用。
/// </summary>
public class CurseForgeModpackSource : IModpackSource
{
    /// <summary>Minecraft 的 gameId。</summary>
    public const int MinecraftGameId = 432;

    /// <summary>整合包分类的 classId。</summary>
    public const int ModpackClassId = 4471;

    private readonly HttpClient _http;
    private readonly Func<string?> _apiKeyProvider;

    /// <param name="apiKeyProvider">读取用户配置的 Key（延迟求值，设置页改完立即生效）。</param>
    public CurseForgeModpackSource(HttpClient http, Func<string?> apiKeyProvider)
    {
        _http = http;
        _apiKeyProvider = apiKeyProvider;
    }

    public string Id => "curseforge";
    public string DisplayName => "CurseForge";

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_apiKeyProvider());

    public string? UnavailableReason => IsAvailable
        ? null
        : "CurseForge 需要 API Key：请在 设置 → 下载 中填入你自己申请的 Key（官方所有接口均强制校验，不提供匿名访问）。";

    public async Task<List<ModpackItem>> SearchAsync(string? keyword, string? gameVersion, string? loader,
        int limit = 24, int offset = 0, CancellationToken ct = default)
    {
        var key = _apiKeyProvider();
        if (string.IsNullOrWhiteSpace(key)) return new List<ModpackItem>();

        try
        {
            var url = BuildSearchUrl(keyword, gameVersion, loader, limit, offset);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("x-api-key", key);

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return new List<ModpackItem>();

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseSearch(json);
        }
        catch
        {
            return new List<ModpackItem>();
        }
    }

    public async Task<ModpackDetail?> GetDetailAsync(string id, CancellationToken ct = default)
    {
        var key = _apiKeyProvider();
        if (string.IsNullOrWhiteSpace(key) || !int.TryParse(id, out var modId)) return null;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.curseforge.com/v1/mods/{modId}/files?pageSize=50");
            req.Headers.TryAddWithoutValidation("x-api-key", key);

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseDetail(json, id);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>构造搜索 URL（纯函数，便于自检）。</summary>
    public static string BuildSearchUrl(string? keyword, string? gameVersion, string? loader, int limit, int offset)
    {
        var qs = new List<string>
        {
            "gameId=" + MinecraftGameId,
            "classId=" + ModpackClassId,
            "pageSize=" + Math.Clamp(limit, 1, 50),
            "index=" + Math.Max(0, offset),
            "sortField=2",      // Popularity
            "sortOrder=desc"
        };
        if (!string.IsNullOrWhiteSpace(keyword)) qs.Add("searchFilter=" + Uri.EscapeDataString(keyword!.Trim()));
        if (!string.IsNullOrWhiteSpace(gameVersion)) qs.Add("gameVersion=" + Uri.EscapeDataString(gameVersion!.Trim()));

        var loaderId = LoaderTypeId(loader);
        if (loaderId > 0) qs.Add("modLoaderType=" + loaderId);

        return "https://api.curseforge.com/v1/mods/search?" + string.Join("&", qs);
    }

    /// <summary>CurseForge 的 modLoaderType 枚举：1=Forge 4=Fabric 5=Quilt 6=NeoForge。</summary>
    public static int LoaderTypeId(string? loader) => (loader ?? "").ToLowerInvariant() switch
    {
        "forge" => 1,
        "fabric" => 4,
        "quilt" => 5,
        "neoforge" => 6,
        _ => 0
    };

    /// <summary>解析搜索响应（纯函数）。</summary>
    public static List<ModpackItem> ParseSearch(string? json)
    {
        var list = new List<ModpackItem>();
        if (string.IsNullOrWhiteSpace(json)) return list;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != System.Text.Json.JsonValueKind.Array) return list;

            foreach (var m in data.EnumerateArray())
            {
                string? Str(string n) =>
                    m.TryGetProperty(n, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;

                var id = m.TryGetProperty("id", out var idv) && idv.TryGetInt32(out var i) ? i.ToString() : "";
                if (id.Length == 0) continue;

                string? icon = null;
                if (m.TryGetProperty("logo", out var logo) && logo.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    logo.TryGetProperty("thumbnailUrl", out var tu) && tu.ValueKind == System.Text.Json.JsonValueKind.String)
                    icon = tu.GetString();

                var author = "";
                if (m.TryGetProperty("authors", out var authors) && authors.ValueKind == System.Text.Json.JsonValueKind.Array)
                    author = authors.EnumerateArray()
                        .Select(a => a.TryGetProperty("name", out var an) ? an.GetString() : null)
                        .FirstOrDefault(s => !string.IsNullOrEmpty(s)) ?? "";

                var gameVersions = new List<string>();
                if (m.TryGetProperty("latestFilesIndexes", out var idx) && idx.ValueKind == System.Text.Json.JsonValueKind.Array)
                    foreach (var f in idx.EnumerateArray())
                        if (f.TryGetProperty("gameVersion", out var gv) && gv.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var s = gv.GetString();
                            if (!string.IsNullOrEmpty(s) && !gameVersions.Contains(s)) gameVersions.Add(s!);
                        }

                var downloads = 0;
                if (m.TryGetProperty("downloadCount", out var dc) && dc.TryGetDouble(out var d)) downloads = (int)Math.Min(d, int.MaxValue);

                list.Add(new ModpackItem
                {
                    Source = "curseforge",
                    Id = id,
                    Title = Str("name") ?? "",
                    Summary = Str("summary") ?? "",
                    Author = author,
                    IconUrl = icon,
                    Downloads = downloads,
                    GameVersions = ModrinthModpackSource.SortVersionsDescending(gameVersions)
                });
            }
        }
        catch
        {
            // 解析失败按空结果处理
        }
        return list;
    }

    /// <summary>解析文件列表响应为详情（纯函数）。</summary>
    public static ModpackDetail? ParseDetail(string? json, string id)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != System.Text.Json.JsonValueKind.Array) return null;

            var versions = new List<ModpackVersion>();
            foreach (var f in data.EnumerateArray())
            {
                var url = f.TryGetProperty("downloadUrl", out var du) && du.ValueKind == System.Text.Json.JsonValueKind.String
                    ? du.GetString() : null;
                if (string.IsNullOrWhiteSpace(url)) continue;   // 作者禁用第三方下载时为 null

                var gv = "";
                var loader = "";
                if (f.TryGetProperty("gameVersions", out var gvs) && gvs.ValueKind == System.Text.Json.JsonValueKind.Array)
                    foreach (var g in gvs.EnumerateArray())
                    {
                        var s = g.GetString() ?? "";
                        if (s.Length == 0) continue;
                        if (char.IsDigit(s[0])) { if (gv.Length == 0) gv = s; }
                        else if (loader.Length == 0) loader = s;
                    }

                versions.Add(new ModpackVersion
                {
                    Id = f.TryGetProperty("id", out var fid) && fid.TryGetInt32(out var fi) ? fi.ToString() : "",
                    Name = f.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "",
                    VersionNumber = f.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? "" : "",
                    GameVersion = gv,
                    Loader = loader,
                    FileUrl = url!,
                    FileName = f.TryGetProperty("fileName", out var fn2) ? fn2.GetString() ?? "" : "",
                    FileSize = f.TryGetProperty("fileLength", out var fl) && fl.TryGetInt64(out var len) ? len : 0
                });
            }

            return new ModpackDetail
            {
                Source = "curseforge",
                Id = id,
                Versions = versions,
                PageUrl = $"https://www.curseforge.com/minecraft/modpacks?search={id}"
            };
        }
        catch
        {
            return null;
        }
    }
}
