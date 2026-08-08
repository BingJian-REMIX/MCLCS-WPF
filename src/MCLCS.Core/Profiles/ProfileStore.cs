using System.Text.Json;

namespace MCLCS.Core.Profiles;

/// <summary>配置读写（profiles.json）。</summary>
public static class ProfileStore
{
    private static string ProfilePath(string gameRoot)
        => Path.Combine(gameRoot, "mclcs_profiles.json");

    public static LauncherProfile Load(string gameRoot)
    {
        var path = ProfilePath(gameRoot);
        if (!File.Exists(path)) return new LauncherProfile { GameRoot = gameRoot };
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LauncherProfile>(json) ?? new LauncherProfile();
        }
        catch
        {
            return new LauncherProfile { GameRoot = gameRoot };
        }
    }

    public static void Save(LauncherProfile profile)
    {
        Directory.CreateDirectory(profile.GameRoot);
        var path = ProfilePath(profile.GameRoot);
        var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
