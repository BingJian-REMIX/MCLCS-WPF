using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace MCLCS.Core.Launcher;

/// <summary>一个 Java 安装的信息。</summary>
public class JavaInfo
{
    /// <summary>java(.exe) 完整路径。</summary>
    public string JavaExe { get; set; } = "";

    /// <summary>主版本号（如 21、17、8）。</summary>
    public int MajorVersion { get; set; }

    /// <summary>展示用版本串。</summary>
    public string RawVersion { get; set; } = "";

    public override string ToString() => $"Java {MajorVersion} ({JavaExe})";
}

/// <summary>
/// 智能 Java 选择：扫描 JAVA_HOME、注册表、常见目录、.minecraft\runtime，解析版本号，筛选 ≥ 要求版本。
/// </summary>
public static class JavaDetector
{
    private static readonly string JavaExeName =
        OperatingSystem.IsWindows() ? "java.exe" : "java";

    /// <summary>从 "21.0.3" / "1.8.0_301" 解析主版本号。</summary>
    public static int MajorFromVersionString(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return 0;
        var m = Regex.Match(version, @"(\d+)(\.(\d+))?");
        if (!m.Success) return 0;
        if (m.Groups[1].Value == "1" && m.Groups[3].Success
            && int.TryParse(m.Groups[3].Value, out var legacy))
            return legacy; // Java 8 及更早：1.8 -> 8
        return int.TryParse(m.Groups[1].Value, out var major) ? major : 0;
    }

    /// <summary>运行 java -version 解析主版本号（输出通常在 stderr）。</summary>
    public static async Task<(int major, string raw)> QueryVersionAsync(string javaExe)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = javaExe,
                Arguments = "-version",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi)!;
            var err = await proc.StandardError.ReadToEndAsync();
            var outp = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            var text = outp + "\n" + err;
            var m = Regex.Match(text, @"version\s+""([^""]+)""");
            var raw = m.Success ? m.Groups[1].Value : "";
            return (MajorFromVersionString(raw), raw);
        }
        catch
        {
            return (0, "");
        }
    }

    /// <summary>扫描所有候选位置，返回发现的 Java 列表（含版本号）。</summary>
    public static async Task<List<JavaInfo>> DetectAsync(IEnumerable<string>? extraDirs = null)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var home = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(home)) candidates.Add(home);

        if (OperatingSystem.IsWindows())
        {
            AddRegistryJava(candidates);
            foreach (var baseDir in new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Adoptium"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "jdk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Zulu"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BellSoft", "Liberica"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Java"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "runtime")
            })
                if (Directory.Exists(baseDir)) candidates.Add(baseDir);
        }
        else
        {
            foreach (var d in new[] { "/usr/lib/jvm", "/opt/java", "/Library/Java/JavaVirtualMachines" })
                if (Directory.Exists(d)) candidates.Add(d);
        }

        if (extraDirs is not null)
            foreach (var d in extraDirs) candidates.Add(d);

        var results = new List<JavaInfo>();
        foreach (var dir in candidates)
            results.AddRange(await ScanDirectoryAsync(dir));

        return results.DistinctBy(r => r.JavaExe).ToList();
    }

    private static void AddRegistryJava(HashSet<string> candidates)
    {
        try
        {
            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                using var key = root.OpenSubKey(@"SOFTWARE\JavaSoft");
                if (key is null) continue;
                foreach (var sub in new[] { "Java Runtime Environment", "JDK" })
                {
                    using var sk = key.OpenSubKey(sub);
                    if (sk is null) continue;
                    foreach (var ver in sk.GetSubKeyNames())
                    {
                        using var vk = sk.OpenSubKey(ver);
                        var javaHome = vk?.GetValue("JavaHome") as string;
                        if (!string.IsNullOrEmpty(javaHome)) candidates.Add(javaHome);
                    }
                }
            }
        }
        catch { /* 忽略注册表访问错误 */ }
    }

    private static async Task<List<JavaInfo>> ScanDirectoryAsync(string dir)
    {
        var found = new List<JavaInfo>();
        if (!Directory.Exists(dir)) return found;

        // 该目录或其子目录（至多 4 层）下的 java 可执行文件
        IEnumerable<string> javaExes;
        try
        {
            javaExes = Directory.EnumerateFiles(dir, JavaExeName, SearchOption.AllDirectories)
                .Where(p => IsWithinDepth(dir, p, 4));
        }
        catch
        {
            return found;
        }

        foreach (var exe in javaExes)
        {
            var (major, raw) = await QueryVersionAsync(exe);
            if (major > 0)
                found.Add(new JavaInfo { JavaExe = exe, MajorVersion = major, RawVersion = raw });
        }
        return found;
    }

    private static bool IsWithinDepth(string root, string path, int maxDepth)
    {
        var rel = path[(root.Length + 1)..];
        var sep = Path.DirectorySeparatorChar;
        var depth = rel.Count(c => c == sep);
        return depth <= maxDepth;
    }

    /// <summary>筛选满足最小版本要求、且版本最高的 Java。</summary>
    public static async Task<JavaInfo?> FindBestAsync(int minMajor, IEnumerable<string>? extraDirs = null)
    {
        var all = await DetectAsync(extraDirs);
        return all.Where(j => j.MajorVersion >= minMajor)
                  .OrderByDescending(j => j.MajorVersion)
                  .FirstOrDefault();
    }
}
