using System.Text.Json.Serialization;

namespace MCLCS.Core.Models;

/// <summary>
/// launchermeta 的版本清单（version_manifest.json）。
/// </summary>
public class VersionManifest
{
    [JsonPropertyName("latest")]
    public LatestVersion Latest { get; set; } = new();

    [JsonPropertyName("versions")]
    public List<VersionEntry> Versions { get; set; } = new();
}

public class LatestVersion
{
    [JsonPropertyName("release")]
    public string Release { get; set; } = "";

    [JsonPropertyName("snapshot")]
    public string Snapshot { get; set; } = "";
}

public class VersionEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("time")]
    public string Time { get; set; } = "";

    [JsonPropertyName("releaseTime")]
    public string ReleaseTime { get; set; } = "";
}
