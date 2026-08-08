using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCLCS.Core.Update;

/// <summary>远端版本信息。</summary>
public class RemoteVersion
{
    public string? Version { get; set; }
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
    [JsonPropertyName("url")]
    public string? DownloadUrl { get; set; }
    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; }
}

/// <summary>更新检查结果。</summary>
public class UpdateCheckResult
{
    public bool Available { get; set; }
    public string? CurrentVersion { get; set; }
    public string? LatestVersion { get; set; }
    public string? Notes { get; set; }
    public string? DownloadUrl { get; set; }
    public bool Mandatory { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// 启动器自动更新（全局功能 13）：从远端 version.json 拉取最新版本并比对，
/// 网络不可用时安全返回"无更新"。
/// </summary>
public static class LauncherUpdater
{
    public const string DefaultVersionJsonUrl = "https://mclcs.example.com/version.json";

    /// <summary>比对两个版本号字符串（如 "0.5.0" 与 "1.0.0"），返回 latest &gt; current 的结果。</summary>
    public static bool IsNewer(string current, string latest)
    {
        var a = Parse(current);
        var b = Parse(latest);
        for (var i = 0; i < Math.Max(a.Count, b.Count); i++)
        {
            var x = i < a.Count ? a[i] : 0;
            var y = i < b.Count ? b[i] : 0;
            if (y > x) return true;
            if (y < x) return false;
        }
        return false;
    }

    private static List<int> Parse(string v)
    {
        var list = new List<int>();
        foreach (var part in v.Split('.'))
            if (int.TryParse(new string(part.Where(char.IsDigit).ToArray()), out var n))
                list.Add(n);
        return list;
    }

    /// <summary>检查更新；异常时返回 Available=false（带 Error）。</summary>
    public static async Task<UpdateCheckResult> CheckAsync(string currentVersion,
        string? versionJsonUrl = null, HttpClient? client = null)
    {
        var result = new UpdateCheckResult { CurrentVersion = currentVersion };
        var url = versionJsonUrl ?? DefaultVersionJsonUrl;
        var own = client is null;
        client ??= new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        try
        {
            var json = await client.GetStringAsync(url);
            var remote = JsonSerializer.Deserialize<RemoteVersion>(json);
            if (remote?.Version is null) return result;
            result.LatestVersion = remote.Version;
            result.Notes = remote.Notes;
            result.DownloadUrl = remote.DownloadUrl;
            result.Mandatory = remote.Mandatory;
            result.Available = IsNewer(currentVersion, remote.Version);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        finally
        {
            if (own) client.Dispose();
        }
        return result;
    }
}
