using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCLCS.Core.Toolbox;

/// <summary>快照中的单个文件。</summary>
public class FileSnapshotEntry
{
    [JsonPropertyName("path")] public string RelativePath { get; set; } = "";
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("mtime")] public DateTime LastWriteUtc { get; set; }

    /// <summary>SHA-256（快速模式下为空）。</summary>
    [JsonPropertyName("hash")] public string? Hash { get; set; }
}

/// <summary>目录快照。</summary>
public class DirectorySnapshot
{
    [JsonPropertyName("root")] public string Root { get; set; } = "";
    [JsonPropertyName("takenAt")] public DateTime TakenAt { get; set; } = DateTime.Now;

    /// <summary>是否计算了哈希（false 表示只比对大小与修改时间）。</summary>
    [JsonPropertyName("hashed")] public bool Hashed { get; set; }

    [JsonPropertyName("entries")] public List<FileSnapshotEntry> Entries { get; set; } = new();

    public int Count => Entries.Count;
    public long TotalBytes => Entries.Sum(e => e.Size);
}

/// <summary>变更类型。</summary>
public enum FileChangeKind
{
    Added,
    Removed,
    Modified
}

/// <summary>一条变更。</summary>
public sealed class FileChange
{
    public FileChange(FileChangeKind kind, string path, string? detail = null)
    {
        Kind = kind;
        Path = path;
        Detail = detail;
    }

    public FileChangeKind Kind { get; }
    public string Path { get; }
    public string? Detail { get; }

    public string KindText => Kind switch
    {
        FileChangeKind.Added => "新增",
        FileChangeKind.Removed => "删除",
        FileChangeKind.Modified => "修改",
        _ => "?"
    };

    public override string ToString() => $"[{KindText}] {Path}" + (Detail is null ? "" : $" — {Detail}");
}

/// <summary>比对结果。</summary>
public sealed class SnapshotDiff
{
    public List<FileChange> Changes { get; } = new();

    public int Added => Changes.Count(c => c.Kind == FileChangeKind.Added);
    public int Removed => Changes.Count(c => c.Kind == FileChangeKind.Removed);
    public int Modified => Changes.Count(c => c.Kind == FileChangeKind.Modified);
    public bool HasChanges => Changes.Count > 0;

    public string Summary => HasChanges
        ? $"新增 {Added} 个，删除 {Removed} 个，修改 {Modified} 个"
        : "无变更";
}

/// <summary>
/// 文件变更检测（工具箱面板 5）：对目录拍快照并与后续状态比对。
/// 典型用途：装完整合包 / 更新 Mod 后，确认哪些文件被改动。
/// </summary>
public static class FileChangeDetector
{
    /// <summary>默认忽略的目录名。</summary>
    public static readonly string[] DefaultIgnoredDirs =
    {
        "logs", "crash-reports", "backups", ".git", "screenshots", "__pycache__"
    };

    /// <summary>
    /// 规格 2.3-16 要监视的目录：用户手动丢文件进来的三个位置。
    /// </summary>
    public static readonly string[] WatchTargets = { "mods", "resourcepacks", "shaderpacks" };

    /// <summary>监视快照的持久化文件名（存于 gameRoot）。</summary>
    public const string SnapshotFileName = "mclcs_watch_snapshot.json";

    public static string SnapshotPath(string gameRoot) => Path.Combine(gameRoot, SnapshotFileName);

    /// <summary>
    /// 对 <see cref="WatchTargets"/> 三个目录一次性拍快照，
    /// 相对路径带上子目录前缀（如 <c>mods/xxx.jar</c>），便于界面直接展示归属。
    /// 目录不存在时静默跳过（用户可能还没装过 Mod）。
    /// </summary>
    public static DirectorySnapshot TakeWatched(
        string gameRoot, IEnumerable<string>? targets = null, CancellationToken ct = default)
    {
        var snapshot = new DirectorySnapshot { Root = gameRoot, Hashed = false };

        foreach (var target in targets ?? WatchTargets)
        {
            ct.ThrowIfCancellationRequested();
            var dir = Path.Combine(gameRoot, target);
            if (!Directory.Exists(dir)) continue;

            var sub = Take(dir, computeHash: false, ct: ct);
            foreach (var e in sub.Entries)
            {
                e.RelativePath = $"{target}/{e.RelativePath}";
                snapshot.Entries.Add(e);
            }
        }

        snapshot.Entries.Sort((a, b) => string.CompareOrdinal(a.RelativePath, b.RelativePath));
        return snapshot;
    }

    /// <summary>
    /// 检测自上次快照以来的变更：拍新快照，与磁盘上的旧快照比对，然后把新快照落盘。
    /// 首次运行（无旧快照）只建立基线，返回空 diff——否则会把用户现有的几百个 Mod
    /// 全部报成「新增」，那种通知没人想看。
    /// </summary>
    public static SnapshotDiff DetectAndUpdate(string gameRoot, CancellationToken ct = default)
    {
        var path = SnapshotPath(gameRoot);
        var before = Load(path);
        var after = TakeWatched(gameRoot, ct: ct);

        Save(after, path);

        return before is null ? new SnapshotDiff() : Compare(before, after);
    }

    /// <summary>只看「新增」的变更（手动丢文件的典型形态），供 Toast 通知使用。</summary>
    public static List<FileChange> NewFilesOnly(SnapshotDiff diff) =>
        diff.Changes.Where(c => c.Kind == FileChangeKind.Added).ToList();

    /// <summary>
    /// 只看不更新：对比当前状态与磁盘上的旧基线返回变更，但<b>不</b>落盘新快照。
    /// 供工具箱面板反复展示「自上次标记已知以来又变了什么」，而不影响基线本身。
    /// </summary>
    public static SnapshotDiff PreviewChanges(string gameRoot, CancellationToken ct = default)
    {
        var before = Load(SnapshotPath(gameRoot));
        var after = TakeWatched(gameRoot, ct: ct);
        return before is null ? new SnapshotDiff() : Compare(before, after);
    }

    /// <summary>建立/重置基线快照（用户点「标记为已知」时调用）。</summary>
    public static bool ResetBaseline(string gameRoot, CancellationToken ct = default) =>
        Save(TakeWatched(gameRoot, ct: ct), SnapshotPath(gameRoot));

    /// <summary>
    /// 对目录拍快照。<paramref name="computeHash"/> 为 true 时计算 SHA-256（慢但准确）。
    /// </summary>
    public static DirectorySnapshot Take(
        string root, bool computeHash = false,
        IEnumerable<string>? ignoredDirs = null,
        CancellationToken ct = default)
    {
        var snapshot = new DirectorySnapshot { Root = root, Hashed = computeHash };
        if (!Directory.Exists(root)) return snapshot;

        var ignores = new HashSet<string>(ignoredDirs ?? DefaultIgnoredDirs, StringComparer.OrdinalIgnoreCase);
        var rootFull = Path.GetFullPath(root);

        foreach (var file in EnumerateFiles(rootFull, ignores, ct))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(file);
                snapshot.Entries.Add(new FileSnapshotEntry
                {
                    RelativePath = Normalize(Path.GetRelativePath(rootFull, file)),
                    Size = info.Length,
                    LastWriteUtc = info.LastWriteTimeUtc,
                    Hash = computeHash ? HashFile(file) : null
                });
            }
            catch
            {
                // 文件被占用 / 无权限：跳过
            }
        }

        snapshot.Entries.Sort((a, b) => string.CompareOrdinal(a.RelativePath, b.RelativePath));
        return snapshot;
    }

    /// <summary>比对两份快照（纯函数）。</summary>
    public static SnapshotDiff Compare(DirectorySnapshot before, DirectorySnapshot after)
    {
        var diff = new SnapshotDiff();
        var beforeMap = before.Entries.ToDictionary(e => e.RelativePath, StringComparer.OrdinalIgnoreCase);
        var afterMap = after.Entries.ToDictionary(e => e.RelativePath, StringComparer.OrdinalIgnoreCase);

        foreach (var kv in afterMap)
        {
            if (!beforeMap.TryGetValue(kv.Key, out var old))
            {
                diff.Changes.Add(new FileChange(FileChangeKind.Added, kv.Key, FormatSize(kv.Value.Size)));
                continue;
            }

            var changed = old.Hash is not null && kv.Value.Hash is not null
                ? !string.Equals(old.Hash, kv.Value.Hash, StringComparison.OrdinalIgnoreCase)
                : old.Size != kv.Value.Size || old.LastWriteUtc != kv.Value.LastWriteUtc;

            if (changed)
            {
                var detail = old.Size != kv.Value.Size
                    ? $"{FormatSize(old.Size)} → {FormatSize(kv.Value.Size)}"
                    : "内容已变化";
                diff.Changes.Add(new FileChange(FileChangeKind.Modified, kv.Key, detail));
            }
        }

        foreach (var kv in beforeMap.Where(kv => !afterMap.ContainsKey(kv.Key)))
            diff.Changes.Add(new FileChange(FileChangeKind.Removed, kv.Key, FormatSize(kv.Value.Size)));

        diff.Changes.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return diff;
    }

    /// <summary>保存快照到 JSON。</summary>
    public static bool Save(DirectorySnapshot snapshot, string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(snapshot));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>从 JSON 载入快照；失败返回 null。</summary>
    public static DirectorySnapshot? Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<DirectorySnapshot>(File.ReadAllText(path))
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> EnumerateFiles(string root, HashSet<string> ignores, CancellationToken ct)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();

            string[] subDirs;
            string[] files;
            try
            {
                subDirs = Directory.GetDirectories(dir);
                files = Directory.GetFiles(dir);
            }
            catch
            {
                continue;
            }

            foreach (var sub in subDirs)
                if (!ignores.Contains(Path.GetFileName(sub)))
                    stack.Push(sub);

            foreach (var f in files) yield return f;
        }
    }

    private static string Normalize(string p) => p.Replace('\\', '/');

    private static string HashFile(string path)
    {
        using var fs = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / 1024.0 / 1024:F1} MB"
    };
}
