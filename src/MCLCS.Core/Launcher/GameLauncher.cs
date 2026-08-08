using System.Diagnostics;
using MCLCS.Core.Download;
using MCLCS.Core.Models;
using MCLCS.Core.MultiInstance;
using MCLCS.Core.Profiles;
using MCLCS.Core.Utils;

namespace MCLCS.Core.Launcher;

/// <summary>一次启动的结果。</summary>
public class LaunchResult
{
    public int ExitCode { get; set; }
    public string? CrashReportPath { get; set; }
    public bool Crashed => CrashReportPath is not null;

    /// <summary>崩溃时的分析报告（无崩溃时为 null）。</summary>
    public CrashAnalysis? Analysis { get; set; }

    /// <summary>针对本次崩溃的自动修复方案（无崩溃时为 null）。</summary>
    public CrashRepairPlan? RepairPlan { get; set; }
}

/// <summary>
/// 游戏启动：合并版本、构建 classpath 与参数、解压 natives、启动 Java 进程，
/// 退出后检测崩溃报告。
/// </summary>
public static class GameLauncher
{
    /// <summary>构造 ${...} 变量字典（含 classpath、natives_directory 等）。</summary>
    public static Dictionary<string, string> BuildVariables(VersionJson merged,
        string gameRoot, string leafId, string nativesDir, LaunchOptions options)
    {
        var classpath = ClasspathBuilder.ComputeClasspath(gameRoot, leafId, merged);
        var assetsIndexName = merged.Assets ?? merged.AssetIndex?.Id ?? "";

        var vars = new Dictionary<string, string>
        {
            ["auth_player_name"] = options.Username,
            ["auth_uuid"] = options.Uuid,
            ["auth_access_token"] = options.AccessToken,
            ["auth_session"] = options.AccessToken,
            ["auth_xuid"] = options.UserType == "msa" ? options.Uuid : "0",
            ["user_type"] = options.UserType,
            ["user_properties"] = options.UserProperties,
            ["version_name"] = merged.Id,
            ["version_type"] = merged.Type,
            ["assets_root"] = PathEx.AssetsDir(gameRoot),
            ["assets_index_name"] = assetsIndexName,
            // 隔离版本（整合包）指向 versions/<id>，否则共用 .minecraft
            ["game_directory"] = VersionIsolation.GameDirFor(gameRoot, leafId),
            ["natives_directory"] = nativesDir,
            ["library_directory"] = PathEx.LibrariesDir(gameRoot),
            ["classpath"] = classpath,
            ["classpath_separator"] = Path.PathSeparator.ToString(),
            ["launcher_name"] = GameConstants.LauncherName,
            ["launcher_version"] = GameConstants.LauncherVersion,
            ["max_mem"] = $"{options.MaxMemoryMb}M"
        };

        if (options.Resolution.HasValue)
        {
            vars["resolution_width"] = options.Resolution.Value.Width.ToString();
            vars["resolution_height"] = options.Resolution.Value.Height.ToString();
        }

        return vars;
    }

    /// <summary>启动游戏并等待退出，随后检测崩溃报告。</summary>
    public static async Task<LaunchResult> LaunchAsync(string gameRoot,
        string versionId,
        JavaInfo java,
        LaunchOptions options,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        var merged = VersionMerger.Merge(gameRoot, versionId);
        var nativesDir = PathEx.NativesDir(gameRoot, versionId);
        Directory.CreateDirectory(nativesDir);

        // 隔离版本的工作目录为 versions/<id>，需保证 mods / saves 等子目录存在
        var gameDir = VersionIsolation.GameDirFor(gameRoot, versionId);
        if (!string.Equals(gameDir, gameRoot, StringComparison.Ordinal))
        {
            VersionIsolation.EnsureFolders(gameDir);
            logger?.Log($"版本 {versionId} 已启用隔离，工作目录：{gameDir}");
        }

        // 注入 logging 日志配置（下载 log4j XML 并注入 -Dlog4j.configurationFile）
        var loggingArgs = await InjectLoggingConfigAsync(gameRoot, versionId, merged, ct);

        var variables = BuildVariables(merged, gameRoot, versionId, nativesDir, options);
        var resolved = ArgumentProcessor.Process(merged, variables, options, nativesDir);

        // 将 logging 参数追加到 JVM 参数末尾
        if (loggingArgs.Count > 0)
            resolved.JvmArgs.AddRange(loggingArgs);

        // 直接连入服务器：追加 --server <host> --port <port>
        if (!string.IsNullOrWhiteSpace(options.ServerAddress))
        {
            var parts = options.ServerAddress.Split(':');
            resolved.GameArgs.Add("--server");
            resolved.GameArgs.Add(parts[0].Trim());
            resolved.GameArgs.Add("--port");
            resolved.GameArgs.Add(parts.Length > 1 ? parts[1].Trim() : "25565");
        }

        // 解压原生库
        var natives = ClasspathBuilder.GetNativeEntries(gameRoot, merged, nativesDir);
        ClasspathBuilder.ExtractNatives(natives);

        var psi = new ProcessStartInfo
        {
            FileName = java.JavaExe,
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = gameDir
        };

        foreach (var a in resolved.JvmArgs) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add(resolved.MainClass);
        foreach (var a in resolved.GameArgs) psi.ArgumentList.Add(a);

        logger?.Log($"启动版本 {merged.Id}（{java.MajorVersion}）：{resolved.MainClass}");

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动游戏进程");

        InstanceTracker.Register(proc.Id, versionId);

        await proc.WaitForExitAsync(ct);
        var exitCode = proc.ExitCode;

        // 隔离版本的崩溃报告落在自己的工作目录下
        var crash = CrashDetector.FindLatestCrashReport(gameDir);
        if (crash is not null)
            logger?.Log($"检测到崩溃报告：{crash}（退出码 {exitCode}）");
        else
            logger?.Log($"游戏进程已退出（退出码 {exitCode}），未检测到崩溃报告。");

        var result = new LaunchResult { ExitCode = exitCode, CrashReportPath = crash };

        // 崩溃时自动分析并规划修复方案（离线、非破坏性路径）
        if (crash is not null)
        {
            try
            {
                var text = await File.ReadAllTextAsync(crash, ct);
                var analysis = CrashAnalyzer.Analyze(text);
                analysis.RawReport = text;
                var profile = ProfileStore.Load(gameRoot);
                var plan = CrashRepairEngine.BuildPlan(analysis, profile, java, gameRoot, versionId);
                result.Analysis = analysis;
                result.RepairPlan = plan;
                logger?.Log($"崩溃类别：{analysis.Category}，可自动修复：{plan.CanRepair}（{plan.Strategy}）");
            }
            catch (Exception ex)
            {
                logger?.Log($"崩溃分析失败：{ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// 下载 log4j 配置文件并注入 -Dlog4j.configurationFile 等 JVM 参数。
    /// 解析 version.json 中的 logging.client，下载配置文件到版本目录。
    /// </summary>
    private static async Task<List<string>> InjectLoggingConfigAsync(string gameRoot,
        string versionId, VersionJson merged, CancellationToken ct)
    {
        var args = new List<string>();
        var logging = merged.Logging?.Client;
        if (logging is null || logging.File is null) return args;

        try
        {
            // 下载日志配置文件到版本目录
            var loggingDir = Path.Combine(PathEx.VersionDir(gameRoot, versionId), "logging");
            Directory.CreateDirectory(loggingDir);

            var destPath = Path.Combine(loggingDir, logging.File.Url?.Split('/').Last() ?? "log4j.xml");
            if (!File.Exists(destPath) && logging.File.Url is not null)
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var data = await client.GetByteArrayAsync(logging.File.Url, ct);
                await File.WriteAllBytesAsync(destPath, data, ct);
            }

            if (File.Exists(destPath))
            {
                // 注入 log4j 配置
                args.Add($"-Dlog4j.configurationFile={destPath}");
            }

            // 如果有 argument 模板（如 "-Dlog4j2.formatMsgNoLookups=true"），也注入
            if (!string.IsNullOrEmpty(logging.Argument))
            {
                // argument 可能含 ${path} 占位，替换为实际路径
                var arg = logging.Argument.Replace("${path}", destPath);
                args.Add(arg);
            }
        }
        catch
        {
            // logging 配置失败不阻塞启动
        }

        return args;
    }
}
