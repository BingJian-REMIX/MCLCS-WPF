using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

    /// <summary>
    /// 获取指定版本的更新日志（发布说明）。
    /// 来源与版本检测一致：CNB 仓库的 <b>annotated git tag 消息</b>。
    /// CNB 对无头客户端仅放行 <c>info/refs</c> 的 tag 列表，raw/API/资产/upload-pack 均被 SPA 拦截；
    /// 唯一可靠取法是借本机 <c>git</c> 拉取该 tag 对象并读取其消息（已实测匿名可用）。
    /// 因此本方法依赖本机 <c>git</c>：未安装 / 网络失败 / 解析失败均返回 <c>null</c>，
    /// 调用方应回退为「前往发布页查看」按钮，绝不抛异常、绝不误报。
    /// </summary>
    /// <param name="latestVersion">新版本号（如 "2.5.4"），内部构造 refs/tags/v{latestVersion}。</param>
    /// <param name="repoGitUrl">仓库 git 地址，缺省用 <see cref="DefaultRepoGitUrl"/>。</param>
    /// <param name="ct">取消令牌。</param>
    public static async Task<string?> FetchChangelogAsync(string latestVersion,
        string? repoGitUrl = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(latestVersion)) return null;
        if (!GitAvailable()) return null;

        var repo = repoGitUrl ?? DefaultRepoGitUrl;
        var tmp = Path.Combine(Path.GetTempPath(), "mclcs-cl-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tmp);
            // 在临时裸仓库里只取该 tag 对象（--depth 1 + --filter=blob:none 避免下载文件内容）。
            await Task.Run(() =>
            {
                RunGit(tmp, "init", "-q");
                RunGit(tmp, "remote", "add", "origin", repo);
                RunGit(tmp, "fetch", "origin",
                    $"refs/tags/v{latestVersion}:refs/tags/v{latestVersion}",
                    "--depth", "1", "--filter=blob:none");
            }, ct);

            var raw = await Task.Run(() => RunGit(tmp, "cat-file", "-p", $"v{latestVersion}"), ct);
            // tag 对象格式：object …\ntype …\ntag …\ntagger …\n\n<消息>。
            // lightweight tag 则 cat-file 输出 commit 对象，消息同样在首个空行之后，解析通用。
            var sep = raw.IndexOf("\n\n", StringComparison.Ordinal);
            var msg = sep >= 0 ? raw[(sep + 2)..] : raw;
            return string.IsNullOrWhiteSpace(msg) ? null : msg.Trim();
        }
        catch
        {
            return null;
        }
        finally
        {
            try { Directory.Delete(tmp, true); }
            catch { /* 临时目录清理失败不影响主流程 */ }
        }
    }

    /// <summary>检测本机是否存在可用的 git 可执行文件。</summary>
    private static bool GitAvailable()
    {
        try
        {
            using var p = StartGit(null, "--version");
            if (p is null) return false;
            // 仅读 stdout（--version 无 stderr 输出），避免管道未读阻塞。
            var _ = p.StandardOutput.ReadToEndAsync();
            return p.WaitForExit(5000) && p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>在指定工作目录同步执行一条 git 命令，返回标准输出；非零退出抛异常。</summary>
    private static string RunGit(string? workingDir, params string[] args)
    {
        using var p = StartGit(workingDir, args);
        if (p is null) throw new InvalidOperationException("无法启动 git");
        // 并发读取 stdout/stderr（git fetch 的进度走 stderr），避免单向 ReadToEnd 导致的管道死锁。
        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(20000))
            throw new InvalidOperationException($"git {string.Join(' ', args)} 执行超时");
        System.Threading.Tasks.Task.WaitAll(outTask, errTask);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', args)} 失败：{errTask.Result.Trim()}");
        return outTask.Result;
    }

    private static Process? StartGit(string? workingDir, params string[] args)
    {
        var psi = new ProcessStartInfo("git", string.Join(' ', args))
        {
            WorkingDirectory = workingDir ?? string.Empty,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        return Process.Start(psi);
    }
}
