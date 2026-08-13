using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using MCLCS.Core.Utils;

namespace MCLCS.Core.Update;

/// <summary>远端版本信息。</summary>
public class RemoteVersion
{
    public string? Version { get; set; }
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
    [JsonPropertyName("url")]
    public string? DownloadUrl { get; set; }
    /// <summary>singlefile 自包含包的直链（CNB 发布资产）。更新检查会 HEAD 验证其是否真的发布。</summary>
    [JsonPropertyName("singleFileUrl")]
    public string? SingleFileUrl { get; set; }
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
    /// <summary>singlefile 包是否已在 CNB 发布（HEAD 验证通过）。</summary>
    public bool SingleFileAvailable { get; set; }
    public bool Mandatory { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// 启动器自动更新（全局功能 13）：从远端 version.json 拉取最新版本并比对，
/// 网络不可用时安全返回"无更新"。
/// </summary>
public static class LauncherUpdater
{
    /// <summary>默认远端版本清单地址（CNB 仓库 main 分支的 version.json，raw 读取）。</summary>
    public const string DefaultVersionJsonUrl = GameConstants.CnbVersionJsonUrl;

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
            result.Mandatory = remote.Mandatory;
            result.Available = IsNewer(currentVersion, remote.Version);

            // singlefile 包直链：优先用 singleFileUrl，回退 url
            var singleFileUrl = remote.SingleFileUrl ?? remote.DownloadUrl;
            result.DownloadUrl = singleFileUrl;

            // 仅在「有更新」时校验 singlefile 包是否真的在 CNB 发布：
            // CNB 对缺失/二进制文件会返回 200 + text/html 的 SPA 外壳，
            // 故以 Content-Type 非 text/html 且状态 200 判定为真实可下载。
            // 发布页（/-/releases/）本身即代表已发布，无需严格校验。
            if (result.Available && !string.IsNullOrEmpty(singleFileUrl))
                result.SingleFileAvailable = singleFileUrl.Contains("/-/releases/", StringComparison.OrdinalIgnoreCase)
                                            || await IsRealFileAsync(client, singleFileUrl);
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

    /// <summary>
    /// HEAD 探测 URL 是否为真实可下载文件（而非 CNB 缺失文件返回的 200 + text/html SPA 外壳）。
    /// 真实文件：状态 200 且 Content-Type 不以 text/html 开头。
    /// </summary>
    private static async Task<bool> IsRealFileAsync(HttpClient client, string url)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, url);
            using var resp = await client.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return false;
            var ct = resp.Content.Headers.ContentType?.MediaType ?? "";
            return !ct.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
