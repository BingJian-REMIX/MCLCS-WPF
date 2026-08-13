using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MCLCS.Core.Utils;

namespace MCLCS.Core.Update;

/// <summary>更新检查结果。</summary>
public class UpdateCheckResult
{
    public bool Available { get; set; }
    public string? CurrentVersion { get; set; }
    public string? LatestVersion { get; set; }
    /// <summary>新版本更新日志（发布说明），来自 latest.json 的 changelog 字段；为空时弹窗回退为「前往发布页」。</summary>
    public string? Changelog { get; set; }
    /// <summary>下载入口（latest.json 的 downloadUrl，缺省按 CNB 发布页 v{版本} 格式构造）。</summary>
    public string? DownloadUrl { get; set; }
    /// <summary>singlefile 包是否已在 CNB 发布（latest.json 的 singleFileAvailable 字段）。</summary>
    public bool SingleFileAvailable { get; set; }
    public bool Mandatory { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// 启动器自动更新（全局功能 13）。
/// 更新源为 CNB Pages 托管的静态 <c>latest.json</c>（<see cref="GameConstants.UpdateInfoUrl"/>，cnb.cool 官方静态页、国内直连）：
/// 普通 HTTPS GET 即可读取，终端用户零 git 依赖、不写临时仓库、无头客户端可达。
/// 网络不可用 / JSON 解析失败时安全返回「无更新」（带 Error），绝不误报。
/// 下载由 UI 层调用内置 <c>HttpDownloader</c> 直接拉取 latest.json 中的 cnb 发布直链，不依赖 winget / 浏览器。
/// </summary>
public static class LauncherUpdater
{
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

    /// <summary>检查更新；异常 / 解析失败时返回 Available=false（带 Error）。</summary>
    public static async Task<UpdateCheckResult> CheckAsync(string currentVersion, HttpClient? client = null)
    {
        var result = new UpdateCheckResult { CurrentVersion = currentVersion };
        var own = client is null;
        client ??= new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            var json = await client.GetStringAsync(GameConstants.UpdateInfoUrl);
            UpdateInfo? info;
            try
            {
                info = JsonSerializer.Deserialize<UpdateInfo>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                result.Error = $"更新信息解析失败：{ex.Message}";
                return result;
            }

            if (info is null || string.IsNullOrWhiteSpace(info.Version))
            {
                result.Error = "更新信息为空或缺少 version 字段";
                return result;
            }

            result.LatestVersion = info.Version;
            // 仅当最新版本严格大于当前版本才提示更新（当前==最新不会误报「更新到自身」）。
            result.Available = IsNewer(currentVersion, info.Version);
            if (result.Available)
            {
                result.Changelog = info.Changelog;
                result.DownloadUrl = info.DownloadUrl
                    ?? $"{GameConstants.CnbRepoUrl}/-/releases/download/v{info.Version}/MCLCS-v{info.Version}-win-x64.zip";
                result.SingleFileAvailable = info.SingleFileAvailable;
                result.Mandatory = info.Mandatory;
            }
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

    /// <summary>EdgeOne Pages 上 latest.json 的字段映射（大小写不敏感）。</summary>
    private sealed class UpdateInfo
    {
        [JsonPropertyName("version")] public string? Version { get; set; }
        [JsonPropertyName("channel")] public string? Channel { get; set; }
        [JsonPropertyName("mandatory")] public bool Mandatory { get; set; }
        [JsonPropertyName("releaseDate")] public string? ReleaseDate { get; set; }
        [JsonPropertyName("downloadUrl")] public string? DownloadUrl { get; set; }
        [JsonPropertyName("singleFileAvailable")] public bool SingleFileAvailable { get; set; }
        [JsonPropertyName("changelog")] public string? Changelog { get; set; }
    }
}
