namespace MCLCS.Core.Utils;

/// <summary>
/// 全局常量：目录约定、镜像源基址、启动器标识。
/// 镜像策略统一封装在 Download/MirrorPolicy.cs，此处仅存放最底层常量。
/// </summary>
public static class GameConstants
{
    /// <summary>.minecraft 默认根目录（%APPDATA%/.minecraft）。</summary>
    public static string DefaultGameRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

    /// <summary>启动器名称（写入 ${launcher_name}）。</summary>
    public const string LauncherName = "MCLCS";

    /// <summary>启动器版本（写入 ${launcher_version}）。</summary>
    public const string LauncherVersion = "2.4.1";

    /// <summary>离线账号类型（写入 ${user_type}）。</summary>
    public const string OfflineUserType = "mojang";

    /// <summary>要求的最低 Java 主版本号。</summary>
    public const int MinimumJavaMajorVersion = 21;

    /// <summary>自动修复调大内存时的上限（MB），超过此值不再自动调大。</summary>
    public const int MaxRepairMemoryMb = 12288;

    /// <summary>自动修复（始终开启策略）单次启动的最大尝试次数，避免无限循环。</summary>
    public const int MaxRepairAttempts = 5;

    /// <summary>Fabric 官方的 Fabric API 项目 slug（用于自动安装）。</summary>
    public const string FabricApiProjectId = "fabric-api";

    /// <summary>Fabric Loader maven 坐标前缀。</summary>
    public const string FabricLoaderGroup = "net.fabricmc";

    // ---- 镜像源：BMCLAPI 优先，官方回退 ----

    public const string BmclapiBase = "https://bmclapi2.bangbang93.com";
    public const string OfficialMetaBase = "https://launchermeta.mojang.com";
    public const string OfficialLibrariesBase = "https://libraries.minecraft.net";
    public const string OfficialAssetsBase = "https://resources.download.minecraft.net";
    public const string BmclapiVersionManifest = BmclapiBase + "/mc/game/version_manifest.json";
    public const string OfficialVersionManifest = OfficialMetaBase + "/mc/game/version_manifest.json";

    public const string FabricMetaBase = "https://meta.fabricmc.net/v2";
    public const string FabricMavenBase = "https://maven.fabricmc.net";

    public const string BmclapiForgeList = BmclapiBase + "/forge/minecraft";
    public const string OfficialForgeBase = "https://files.minecraftforge.net";

    public const string ModrinthApiBase = "https://api.modrinth.com/v2";

    public const string AdoptiumApiBase = "https://api.adoptium.net/v3";
}
