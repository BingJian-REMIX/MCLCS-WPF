using System.Net.Http;
using System.Text;
using System.Text.Json;
using MCLCS.Core.Save;

namespace MCLCS.Core.Toolbox;

/// <summary>一条种子搜索结果（来自第三方种子库 API）。</summary>
public class SeedEntry
{
    public string Seed { get; set; } = "";
    public string? Title { get; set; }
    public string? Biome { get; set; }
    public string? Version { get; set; }
    public string? Source { get; set; }
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// 种子库（工具箱功能 3）：从存档 <c>level.dat</c> 提取世界种子、创建带指定种子的世界，
/// 并可集成第三方种子 API 搜索热门种子（按版本/特性筛选）。
/// </summary>
public static class SeedLibrary
{
    /// <summary>从存档目录的 level.dat 提取 RandomSeed；找不到返回 null。</summary>
    public static long? ExtractSeed(string savePath)
    {
        var levelDat = Path.Combine(savePath, "level.dat");
        if (!File.Exists(levelDat)) return null;
        try
        {
            var root = NbtFile.ReadGzip(levelDat);
            var seedTag = root.Find("RandomSeed");
            if (seedTag is not null && seedTag.Type == NbtTagType.Long)
                return seedTag.LongValue;
            // 旧版可能放在 Data.RandomSeed
            var data = root.GetChild("Data");
            var seed2 = data?.GetChild("RandomSeed");
            if (seed2 is not null && seed2.Type == NbtTagType.Long)
                return seed2.LongValue;
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 创建一个带指定种子的世界（仅生成最小 level.dat，不含地形，由游戏首次进入时生成）。
    /// 返回创建的 level.dat 路径。
    /// </summary>
    public static string CreateWorld(string savesDir, string worldName, long seed, int dataVersion = 0)
    {
        Directory.CreateDirectory(savesDir);
        var worldDir = Path.Combine(savesDir, worldName);
        Directory.CreateDirectory(worldDir);

        var data = NbtTag.Compound("Data");
        data.Children!.Add(NbtTag.Int("DataVersion", dataVersion));
        data.Children.Add(NbtTag.Int("GameType", 0));
        data.Children.Add(NbtTag.Long("RandomSeed", seed));
        data.Children.Add(NbtTag.Long("Time", 0));
        data.Children.Add(NbtTag.Int("SpawnX", 0));
        data.Children.Add(NbtTag.Int("SpawnY", 64));
        data.Children.Add(NbtTag.Int("SpawnZ", 0));

        var root = NbtTag.Compound();
        root.Children!.Add(data);

        var levelDat = Path.Combine(worldDir, "level.dat");
        NbtFile.WriteGzip(levelDat, root);
        return levelDat;
    }

    /// <summary>
    /// 从第三方种子 API 搜索热门种子（按版本/特性筛选）。
    /// 网络不可用时安全返回空列表；<paramref name="apiBase"/> 可指向任意兼容服务。
    /// </summary>
    public static async Task<List<SeedEntry>> SearchSeedsAsync(string? query = null,
        string? version = null, string? feature = null, string apiBase = "https://api.mcseed.net/v1",
        HttpClient? client = null)
    {
        try
        {
            client ??= new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var q = new StringBuilder(apiBase.TrimEnd('/')).Append("/seeds?limit=20");
            if (!string.IsNullOrWhiteSpace(query)) q.Append("&q=").Append(Uri.EscapeDataString(query));
            if (!string.IsNullOrWhiteSpace(version)) q.Append("&version=").Append(Uri.EscapeDataString(version));
            if (!string.IsNullOrWhiteSpace(feature)) q.Append("&tag=").Append(Uri.EscapeDataString(feature));

            var json = await client.GetStringAsync(q.ToString());
            var doc = JsonDocument.Parse(json);
            var results = new List<SeedEntry>();
            if (doc.RootElement.TryGetProperty("seeds", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var entry = new SeedEntry();
                    if (item.TryGetProperty("seed", out var s)) entry.Seed = s.GetString() ?? "";
                    if (item.TryGetProperty("title", out var t)) entry.Title = t.GetString();
                    if (item.TryGetProperty("biome", out var b)) entry.Biome = b.GetString();
                    if (item.TryGetProperty("version", out var v)) entry.Version = v.GetString();
                    results.Add(entry);
                }
            }
            foreach (var e in results) e.Source = apiBase;
            return results;
        }
        catch
        {
            return new List<SeedEntry>();
        }
    }
}
