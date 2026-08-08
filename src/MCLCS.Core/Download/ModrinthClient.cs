using MCLCS.Core.Models;
using MCLCS.Core.Utils;

namespace MCLCS.Core.Download;

/// <summary>Modrinth REST API 客户端（搜索 / 版本 / 文件选择）。</summary>
public class ModrinthClient
{
    private readonly HttpClient _client;

    public ModrinthClient(HttpClient client) => _client = client;

    public async Task<ModrinthSearchResult> SearchAsync(string query,
        string? gameVersion = null,
        LoaderType loader = LoaderType.Any,
        ModrinthProjectType? type = null,
        int limit = 25,
        int offset = 0,
        string? index = null,
        CancellationToken ct = default)
    {
        var facets = new List<List<string>>();
        if (type.HasValue) facets.Add(new List<string> { "project_type:" + ProjectTypeString(type.Value) });
        if (loader != LoaderType.Any) facets.Add(new List<string> { "categories:" + LoaderString(loader) });
        if (!string.IsNullOrEmpty(gameVersion)) facets.Add(new List<string> { "versions:" + gameVersion });

        var url = $"{GameConstants.ModrinthApiBase}/search?limit={limit}&offset={offset}";
        if (!string.IsNullOrEmpty(query))
            url += "&query=" + Uri.EscapeDataString(query);
        if (!string.IsNullOrEmpty(index))
            url += "&index=" + Uri.EscapeDataString(index);
        if (facets.Count > 0)
            url += "&facets=" + Uri.EscapeDataString(System.Text.Json.JsonSerializer.Serialize(facets));

        var json = await _client.GetStringAsync(url, ct);
        return System.Text.Json.JsonSerializer.Deserialize<ModrinthSearchResult>(json)
               ?? new ModrinthSearchResult();
    }

    public async Task<List<ModrinthVersion>> GetVersionsAsync(string projectId, CancellationToken ct = default)
    {
        var json = await _client.GetStringAsync($"{GameConstants.ModrinthApiBase}/project/{projectId}/version", ct);
        return System.Text.Json.JsonSerializer.Deserialize<List<ModrinthVersion>>(json)
               ?? new List<ModrinthVersion>();
    }

    /// <summary>从某个版本中挑选最合适的文件：优先与游戏版本/加载器完全匹配且 primary 的文件。</summary>
    public ModrinthFile? SelectBestFile(ModrinthVersion version, string? gameVersion, LoaderType loader)
    {
        ModrinthFile? fallback = null;
        foreach (var f in version.Files)
        {
            if (!string.IsNullOrEmpty(gameVersion) && !version.GameVersions.Contains(gameVersion))
                continue;
            if (loader != LoaderType.Any && !version.Loaders.Contains(LoaderString(loader), StringComparer.OrdinalIgnoreCase))
                continue;
            if (f.Primary) return f;
            fallback ??= f;
        }
        return fallback;
    }

    public static string LoaderString(LoaderType loader) => loader switch
    {
        LoaderType.Fabric => "fabric",
        LoaderType.Forge => "forge",
        LoaderType.Quilt => "quilt",
        LoaderType.NeoForge => "neoforge",
        _ => ""
    };

    public static string ProjectTypeString(ModrinthProjectType type) => type switch
    {
        ModrinthProjectType.Mod => "mod",
        ModrinthProjectType.Shader => "shader",
        ModrinthProjectType.ResourcePack => "resourcepack",
        ModrinthProjectType.Modpack => "modpack",
        _ => "mod"
    };

    /// <summary>获取项目详情。</summary>
    public async Task<ModrinthProject?> GetProjectAsync(string projectId, CancellationToken ct = default)
    {
        var json = await _client.GetStringAsync($"{GameConstants.ModrinthApiBase}/project/{projectId}", ct);
        return System.Text.Json.JsonSerializer.Deserialize<ModrinthProject>(json);
    }

    /// <summary>获取版本依赖信息。</summary>
    public async Task<List<ModrinthDependency>> GetDependenciesAsync(string versionId, CancellationToken ct = default)
    {
        var json = await _client.GetStringAsync($"{GameConstants.ModrinthApiBase}/version/{versionId}", ct);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("dependencies", out var deps))
            return System.Text.Json.JsonSerializer.Deserialize<List<ModrinthDependency>>(deps.GetRawText()) ?? new();
        return new List<ModrinthDependency>();
    }
}
