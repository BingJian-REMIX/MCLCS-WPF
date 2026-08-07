using System.Text.Json.Serialization;

namespace MCLCS.Core.Models;

/// <summary>CurseForge 整合包 manifest.json。</summary>
public class CurseForgeManifest
{
    [JsonPropertyName("minecraft")]
    public CurseForgeMinecraftInfo Minecraft { get; set; } = new();

    [JsonPropertyName("manifestType")]
    public string ManifestType { get; set; } = "";

    [JsonPropertyName("manifestVersion")]
    public int ManifestVersion { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("files")]
    public List<CurseForgePackFile> Files { get; set; } = new();

    [JsonPropertyName("overrides")]
    public string Overrides { get; set; } = "overrides";
}

public class CurseForgeMinecraftInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("modLoaders")]
    public List<CurseForgeModLoader> ModLoaders { get; set; } = new();
}

public class CurseForgeModLoader
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }
}

public class CurseForgePackFile
{
    [JsonPropertyName("projectID")]
    public int ProjectId { get; set; }

    [JsonPropertyName("fileID")]
    public int FileId { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; } = true;
}

// ---- CurseForge API 模型 ----

/// <summary>CurseForge API v1 文件信息。</summary>
public class CurseForgeApiFile
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("fileLength")]
    public long FileLength { get; set; }

    [JsonPropertyName("hashes")]
    public List<CurseForgeHash> Hashes { get; set; } = new();

    public string? GetHash(string algo)
    {
        foreach (var h in Hashes)
            if (h.AlgorithmName.Equals(algo, StringComparison.OrdinalIgnoreCase))
                return h.Value;
        return null;
    }
}

public class CurseForgeHash
{
    [JsonPropertyName("algo")]
    public int Algo { get; set; } // 1 = SHA1, 2 = MD5

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    public string AlgorithmName => Algo switch { 1 => "SHA1", 2 => "MD5", _ => $"Unknown({Algo})" };
}

/// <summary>CurseForge API v1 mods 批量查询响应。</summary>
public class CurseForgeModsResponse
{
    [JsonPropertyName("data")]
    public List<CurseForgeModInfo> Data { get; set; } = new();
}

public class CurseForgeModInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("latestFiles")]
    public List<CurseForgeApiFile> LatestFiles { get; set; } = new();
}

/// <summary>CurseForge API v1 mods/files 批量响应。</summary>
public class CurseForgeFilesResponse
{
    [JsonPropertyName("data")]
    public List<CurseForgeApiFile> Data { get; set; } = new();
}
