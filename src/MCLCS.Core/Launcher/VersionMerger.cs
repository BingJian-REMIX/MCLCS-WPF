using MCLCS.Core.Models;
using MCLCS.Core.Utils;

namespace MCLCS.Core.Launcher;

/// <summary>
/// 版本 JSON 加载与合并：处理 inheritsFrom 继承链（原版 ← Fabric ← 自定义）。
/// 合并规则：子版本的 arguments/libraries 排在父版本之前（子优先）；
/// mainClass / assets / assetIndex / minecraftArguments 等标量字段子版本覆盖父版本。
/// </summary>
public static class VersionMerger
{
    public static VersionJson LoadVersion(string gameRoot, string id)
    {
        var path = PathEx.VersionJsonPath(gameRoot, id);
        if (!File.Exists(path))
            throw new FileNotFoundException($"版本 JSON 不存在：{path}", path);
        var json = File.ReadAllText(path);
        return System.Text.Json.JsonSerializer.Deserialize<VersionJson>(json)
               ?? throw new InvalidOperationException($"无法解析版本 JSON：{path}");
    }

    /// <summary>返回继承链：leaf 在前，root 在后。</summary>
    public static List<string> GetHierarchy(string gameRoot, string id)
    {
        var chain = new List<string>();
        var current = id;
        var guard = 0;
        while (!string.IsNullOrEmpty(current) && guard++ < 20)
        {
            if (chain.Contains(current)) break; // 防止循环继承
            chain.Add(current);
            VersionJson? v = null;
            try { v = LoadVersion(gameRoot, current); } catch { break; }
            current = v.InheritsFrom ?? "";
        }
        return chain;
    }

    /// <summary>将继承链上的所有版本合并为一个可直接用于启动的 VersionJson。</summary>
    public static VersionJson Merge(string gameRoot, string id)
    {
        var chain = GetHierarchy(gameRoot, id);
        if (chain.Count == 0)
            throw new ArgumentException($"找不到版本 {id}", nameof(id));

        var leaf = LoadVersion(gameRoot, chain[0]);
        var merged = new VersionJson
        {
            Id = leaf.Id,
            Type = leaf.Type,
            InheritsFrom = leaf.InheritsFrom,
            MainClass = leaf.MainClass,
            MinecraftArguments = leaf.MinecraftArguments,
            Assets = leaf.Assets,
            AssetIndex = leaf.AssetIndex,
            JavaVersion = leaf.JavaVersion,
            Arguments = new Arguments
            {
                Game = new List<ArgumentItem>(leaf.Arguments?.Game ?? new()),
                Jvm = new List<ArgumentItem>(leaf.Arguments?.Jvm ?? new())
            },
            Libraries = new List<Library>(leaf.Libraries),
            Downloads = new Dictionary<string, DownloadInfo>(leaf.Downloads),
            Logging = leaf.Logging,
            MinimumLauncherVersion = leaf.MinimumLauncherVersion,
            ReleaseTime = leaf.ReleaseTime
        };

        for (var i = 1; i < chain.Count; i++)
        {
            var p = LoadVersion(gameRoot, chain[i]);
            // 库：父版本追加在子版本之后（子优先），并按 name 去重
            merged.Libraries.AddRange(p.Libraries);

            // arguments：父版本的 game/jvm 追加在子版本之后
            if (p.Arguments?.Game is not null) merged.Arguments.Game.AddRange(p.Arguments.Game);
            if (p.Arguments?.Jvm is not null) merged.Arguments.Jvm.AddRange(p.Arguments.Jvm);

            // 旧版 minecraftArguments：父版本追加
            if (!string.IsNullOrEmpty(p.MinecraftArguments))
                merged.MinecraftArguments = (merged.MinecraftArguments ?? "") + " " + p.MinecraftArguments;

            // 标量字段：仅当子版本未设置时才回退父版本
            if (string.IsNullOrEmpty(merged.MainClass)) merged.MainClass = p.MainClass;
            if (string.IsNullOrEmpty(merged.Assets)) merged.Assets = p.Assets;
            if (merged.AssetIndex is null) merged.AssetIndex = p.AssetIndex;
            if (merged.JavaVersion is null) merged.JavaVersion = p.JavaVersion;
        }

        // 按 name 去重（保留首次出现 = 子版本优先）
        merged.Libraries = merged.Libraries
            .GroupBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        return merged;
    }
}
