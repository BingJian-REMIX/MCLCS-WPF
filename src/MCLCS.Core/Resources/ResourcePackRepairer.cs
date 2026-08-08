using System.Text.Json;
using MCLCS.Core.Profiles;

namespace MCLCS.Core.Resources;

/// <summary>
/// 资源包格式自动修复（规格 3.13）：检测 pack_format 与 MC 版本不匹配时，
/// 调整 pack.mcmeta 中的 pack_format 字段。仅处理版本号问题。
/// </summary>
public static class ResourcePackRepairer
{
    /// <summary>MC 版本 → 推荐 pack_format 的映射。</summary>
    private static readonly Dictionary<int, int> VersionFormatMap = new()
    {
        { 12, 4 }, { 13, 4 }, { 14, 4 }, { 15, 5 }, { 16, 6 },
        { 17, 7 }, { 18, 8 }, { 19, 9 }, { 20, 15 }, { 21, 34 }
    };

    /// <summary>检查指定资源包的 pack_format 是否匹配目标 MC 主版本。</summary>
    public static (bool Match, int CurrentFormat, int ExpectedFormat) Check(string packDir, int mcMajorVersion)
    {
        var mcmeta = Path.Combine(packDir, "pack.mcmeta");
        if (!File.Exists(mcmeta)) return (false, 0, 0);

        try
        {
            var json = JsonDocument.Parse(File.ReadAllText(mcmeta));
            var currentFormat = json.RootElement
                .GetProperty("pack").GetProperty("pack_format").GetInt32();

            var expected = VersionFormatMap.GetValueOrDefault(mcMajorVersion, 34);
            return (currentFormat == expected, currentFormat, expected);
        }
        catch { return (false, 0, 0); }
    }

    /// <summary>修复 pack_format 为匹配值，修复前自动备份为 .backup。</summary>
    public static bool Repair(string packDir, int expectedFormat)
    {
        var mcmeta = Path.Combine(packDir, "pack.mcmeta");
        if (!File.Exists(mcmeta)) return false;

        try
        {
            // 备份
            var backup = mcmeta + ".backup";
            if (!File.Exists(backup)) File.Copy(mcmeta, backup, overwrite: true);

            var doc = JsonDocument.Parse(File.ReadAllText(mcmeta));
            var root = doc.RootElement;
            using var stream = new MemoryStream();
            var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            writer.WriteStartObject("pack");
            writer.WriteNumber("pack_format", expectedFormat);
            if (root.TryGetProperty("pack", out var pack) && pack.TryGetProperty("description", out var desc))
                writer.WriteString("description", desc.GetString() ?? "");
            writer.WriteEndObject();
            if (root.TryGetProperty("overlays", out _))
            {
                var overlays = root.GetProperty("overlays");
                overlays.WriteTo(writer);
            }
            writer.WriteEndObject();
            writer.Flush();
            File.WriteAllBytes(mcmeta, stream.ToArray());
            return true;
        }
        catch { return false; }
    }
}
