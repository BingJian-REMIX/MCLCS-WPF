using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MCLCS.Core.Utils;

namespace MCLCS.Core.Update;

/// <summary>更新检查结果。</summary>
public class UpdateCheckResult
{
    public bool Available { get; set; }
    public string? CurrentVersion { get; set; }
    public string? LatestVersion { get; set; }
    public string? Notes { get; set; }
    /// <summary>singlefile 包的下载入口（CNB 发布页，按 v{版本} 格式构造）。</summary>
    public string? DownloadUrl { get; set; }
    /// <summary>singlefile 包是否已在 CNB 发布（tag 存在即代表 Release 已出，含 singlefile 包）。</summary>
    public bool SingleFileAvailable { get; set; }
    public bool Mandatory { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// 启动器自动更新（全局功能 13）。
/// 更新源为 CNB 仓库的 git smart-HTTP 引用广播（<c>info/refs?service=git-upload-pack</c>）：
/// 无需鉴权、无头客户端可直接读取，列出全部 <c>vX.Y.Z</c> tag 取最新版本。
/// 网络不可用时安全返回「无更新」。
/// 注：CNB 的 -/raw/ 与 API 对无头客户端只返回 SPA 页面，无法读取 version.json，故采用 tag 方式。
/// </summary>
public static class LauncherUpdater
{
    /// <summary>默认更新源：CNB 仓库 git 地址（附加 /info/refs 读取 tag）。</summary>
    public const string DefaultRepoGitUrl = GameConstants.CnbRepoGitUrl;

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
        string? repoGitUrl = null, HttpClient? client = null)
    {
        var result = new UpdateCheckResult { CurrentVersion = currentVersion };
        var own = client is null;
        client ??= new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            var repo = repoGitUrl ?? DefaultRepoGitUrl;
            var infoRefs = repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
                ? repo + "/info/refs?service=git-upload-pack"
                : repo + ".git/info/refs?service=git-upload-pack";

            var txt = await client.GetStringAsync(infoRefs);

            // 解析 refs/tags/vX.Y.Z（ peeled 的 vX.Y.Z^{} 也会匹配，靠 HashSet 去重）
            var versions = new HashSet<string>();
            foreach (Match m in Regex.Matches(txt, @"refs/tags/v(\d+\.\d+\.\d+)"))
                versions.Add(m.Groups[1].Value);
            if (versions.Count == 0) return result;

            // 取语义版本最大者
            var latest = (string?)null;
            foreach (var v in versions)
                if (latest is null || IsNewer(latest, v)) latest = v;

            result.LatestVersion = latest;
            result.Available = IsNewer(currentVersion, latest!);
            if (result.Available)
            {
                // 按格式构造 singlefile 下载入口：CNB 发布页 v{版本}
                result.DownloadUrl = $"{GameConstants.CnbRepoUrl}/-/releases/v{latest}";
                // tag 存在即代表该版本 Release 已发布（含 singlefile 包），按格式视为已发布
                result.SingleFileAvailable = true;
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
}
