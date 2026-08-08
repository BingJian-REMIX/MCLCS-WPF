using System.Text.Json;
using MCLCS.Core.Utils;

namespace MCLCS.Core.Toolbox;

/// <summary>从 .minecraft/assets 提取的 MC 原声曲目（已映射到 objects/&lt;hash&gt; 实体文件）。</summary>
public class McOstTrack
{
    public string Title { get; init; } = "";
    public string Category { get; init; } = "";
    public string FilePath { get; init; } = "";
    public string Hash { get; init; } = "";

    /// <summary>转为统一播放列表曲目（Artist 标注为 Minecraft 原声）。</summary>
    public Track ToTrack() => new()
    {
        Path = FilePath,
        Title = Title,
        Artist = Category == "records" ? "Minecraft 唱片" : "C418 / Lena Raine"
    };
}

/// <summary>MC 原声按分类分组的视图模型。</summary>
public class McOstGroup
{
    public string Category { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public List<McOstTrack> Tracks { get; init; } = new();
}

/// <summary>
/// MC 原声提取：读取 <c>assets/indexes/&lt;version&gt;.json</c>，把
/// <c>minecraft/sounds/music</c>、<c>records</c>、<c>ambient</c> 等分类下的音频
/// 映射到 <c>assets/objects/&lt;hash前2位&gt;/&lt;hash&gt;</c> 实体文件，供播放器直接使用。
/// <para>注意：MC 原声为 .ogg 格式，依赖系统解码器（Windows Media Foundation 默认不含 OGG，
/// 用户机器若缺解码器需自行安装；提取逻辑本身不依赖解码）。</para>
/// </summary>
public static class McOstExtractor
{
    private static readonly HashSet<string> MusicPrefixes = new()
    {
        "minecraft/sounds/music/",
        "minecraft/sounds/records/",
        "minecraft/sounds/jukebox/",
        "minecraft/sounds/ambient/"
    };

    /// <summary>按相对路径归类（music / records / ambient / other）。</summary>
    private static string Classify(string path)
    {
        if (path.Contains("sounds/records/") || path.Contains("sounds/jukebox/")) return "records";
        if (path.Contains("sounds/music/")) return "music";
        if (path.Contains("sounds/ambient/")) return "ambient";
        return "other";
    }

    private static readonly Dictionary<string, string> CategoryDisplay = new()
    {
        ["music"] = "背景音乐",
        ["records"] = "唱片",
        ["ambient"] = "环境音",
        ["other"] = "其它"
    };

    /// <summary>
    /// 扫描 MC 原声。优先使用 <paramref name="versionId"/> 对应的索引；不传则取目录下最新（字典序最大）的索引。
    /// 仅返回文件实际存在的曲目。
    /// </summary>
    public static List<McOstGroup> Scan(string gameRoot, string? versionId = null)
    {
        var groups = new List<McOstGroup>();
        try
        {
            var indexDir = PathEx.AssetsIndexDir(gameRoot);
            if (!Directory.Exists(indexDir)) return groups;

            var indexFile = !string.IsNullOrWhiteSpace(versionId)
                ? Path.Combine(indexDir, versionId + ".json")
                : Directory.GetFiles(indexDir, "*.json")
                    .OrderByDescending(f => Path.GetFileNameWithoutExtension(f))
                    .FirstOrDefault();

            if (string.IsNullOrEmpty(indexFile) || !File.Exists(indexFile)) return groups;

            using var doc = JsonDocument.Parse(File.ReadAllText(indexFile));
            if (!doc.RootElement.TryGetProperty("objects", out var objects)
                || objects.ValueKind != JsonValueKind.Object) return groups;

            var byCat = new Dictionary<string, List<McOstTrack>>();
            foreach (var prop in objects.EnumerateObject())
            {
                var key = prop.Name;
                if (!MusicPrefixes.Any(p => key.StartsWith(p, StringComparison.OrdinalIgnoreCase))) continue;
                if (!key.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                    && !key.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                    && !key.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) continue;

                if (!prop.Value.TryGetProperty("hash", out var hashEl) || hashEl.ValueKind != JsonValueKind.String) continue;
                var hash = hashEl.GetString()!;
                if (hash.Length < 3) continue;

                var filePath = Path.Combine(PathEx.AssetsObjectsDir(gameRoot), hash[..2], hash);
                if (!File.Exists(filePath)) continue;

                var cat = Classify(key);
                var track = new McOstTrack
                {
                    Title = FriendlyTitle(key, cat),
                    Category = cat,
                    FilePath = filePath,
                    Hash = hash
                };
                if (!byCat.TryGetValue(cat, out var list))
                    byCat[cat] = list = new List<McOstTrack>();
                list.Add(track);
            }

            foreach (var cat in new[] { "music", "records", "ambient", "other" })
            {
                if (!byCat.TryGetValue(cat, out var tracks) || tracks.Count == 0) continue;
                groups.Add(new McOstGroup
                {
                    Category = cat,
                    DisplayName = CategoryDisplay.GetValueOrDefault(cat, cat),
                    Tracks = tracks.OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ToList()
                });
            }
        }
        catch
        {
            // 提取失败（无 assets / 索引损坏）时返回空列表，不影响其它音源
        }
        return groups;
    }

    /// <summary>把 assets 路径文件名的友好中文标题（唱片加「唱片」前缀）。</summary>
    private static string FriendlyTitle(string path, string category)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        name = name.Replace('_', ' ');
        if (category == "records")
            return "唱片 " + char.ToUpperInvariant(name[0]) + name[1..];
        return char.ToUpperInvariant(name[0]) + name[1..];
    }
}
