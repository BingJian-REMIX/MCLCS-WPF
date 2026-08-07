using System.Text.Json;
using MCLCS.Core.Download;
using MCLCS.Core.Models;
using MCLCS.Core.Utils;

namespace MCLCS.Core.Installers;

/// <summary>
/// CurseForge 整合包安装器（.zip 格式）。
/// 流程：解压 → 读 manifest.json → 安装 MC + loader → 下载 mods → 复制 overrides。
/// </summary>
public class CurseForgeModpackInstaller
{
    private readonly string _gameRoot;
    private readonly HttpClient _client;
    private readonly IDownloader _downloader;
    private readonly ILogger? _logger;

    public CurseForgeModpackInstaller(string gameRoot, HttpClient client, IDownloader downloader, ILogger? logger = null)
    {
        _gameRoot = gameRoot;
        _client = client;
        _downloader = downloader;
        _logger = logger;
    }

    /// <summary>安装 CurseForge .zip 整合包。</summary>
    public async Task InstallAsync(string zipPath,
        IProgress<(int Done, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(zipPath))
            throw new FileNotFoundException("整合包文件不存在", zipPath);

        var tempDir = Path.Combine(Path.GetTempPath(), "mclcs_cf_" + Guid.NewGuid().ToString("N"));
        try
        {
            // 1. 解压
            _logger?.Log("解压 CurseForge 整合包 ...");
            Unzip.ExtractToDirectory(zipPath, tempDir);
            var manifestPath = Path.Combine(tempDir, "manifest.json");
            if (!File.Exists(manifestPath))
                throw new InvalidDataException("整合包缺少 manifest.json");

            var manifest = JsonSerializer.Deserialize<CurseForgeManifest>(await File.ReadAllTextAsync(manifestPath, ct))
                           ?? throw new InvalidDataException("无法解析 manifest.json");

            _logger?.Log($"整合包: {manifest.Name} v{manifest.Version} (MC {manifest.Minecraft.Version}, 作者 {manifest.Author})");

            // 2. 安装 MC + loader
            await InstallGameAndLoader(manifest, progress, ct);

            // 3. 下载 mods
            await DownloadPackFiles(manifest, progress, ct);

            // 4. 复制 overrides
            CopyOverrides(tempDir, manifest.Overrides);

            _logger?.Log($"整合包 {manifest.Name} 安装完成");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    private async Task InstallGameAndLoader(CurseForgeManifest manifest,
        IProgress<(int Done, int Total)>? progress,
        CancellationToken ct)
    {
        var mcVersion = manifest.Minecraft.Version;
        _logger?.Log($"安装原版 Minecraft {mcVersion} ...");
        var vanilla = new VanillaInstaller(_gameRoot, _client, _downloader, _logger);
        await vanilla.InstallAsync(mcVersion, progress, ct);

        foreach (var loader in manifest.Minecraft.ModLoaders)
        {
            var id = loader.Id;
            _logger?.Log($"安装 loader: {id} (primary={loader.Primary})");

            if (id.StartsWith("fabric-", StringComparison.OrdinalIgnoreCase))
            {
                var version = id["fabric-".Length..];
                _logger?.Log($"  Fabric Loader {version}");
                await new FabricInstaller(_gameRoot, _client, _downloader, _logger)
                    .InstallAsync(mcVersion, progress, ct);
            }
            else if (id.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
            {
                var version = id["forge-".Length..];
                _logger?.Log($"  Forge {version}");
                await new ForgeInstaller(_gameRoot, _client, _downloader, _logger)
                    .InstallAsync(mcVersion, progress, ct);
            }
            else if (id.StartsWith("neoforge-", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.Log("NeoForge 暂未实现，跳过");
            }
            else if (id.StartsWith("quilt-", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.Log("Quilt 暂未实现，跳过");
            }
            else
            {
                _logger?.Log($"未知 loader: {id}，跳过");
            }
        }
    }

    private async Task DownloadPackFiles(CurseForgeManifest manifest,
        IProgress<(int Done, int Total)>? progress,
        CancellationToken ct)
    {
        if (manifest.Files.Count == 0) return;

        var cfClient = new CurseForgeClient(_client);
        var modsDir = PathEx.ModsDir(_gameRoot);
        Directory.CreateDirectory(modsDir);

        // 分批获取文件下载 URL
        var fileKeys = manifest.Files.Select(f => ((int)f.ProjectId, (int)f.FileId)).ToList();
        var fileInfos = await cfClient.GetFilesAsync(fileKeys, ct);

        var items = new List<DownloadItem>();
        foreach (var packFile in manifest.Files)
        {
            if (fileInfos.TryGetValue(((int)packFile.ProjectId, (int)packFile.FileId), out var info))
            {
                if (string.IsNullOrEmpty(info.DownloadUrl))
                {
                    _logger?.Log($"项目 {packFile.ProjectId} 文件 {packFile.FileId} 无下载 URL，跳过");
                    continue;
                }
                var dest = Path.Combine(modsDir, info.FileName);
                var sha1 = info.GetHash("SHA1");
                items.Add(new DownloadItem(new[] { info.DownloadUrl }, dest, sha1, info.FileLength));
            }
            else
            {
                _logger?.Log($"项目 {packFile.ProjectId} 文件 {packFile.FileId} 查询失败，跳过");
            }
        }

        if (items.Count > 0)
        {
            _logger?.Log($"下载 {items.Count} 个 Mod ...");
            await _downloader.DownloadBatchAsync(items, progress, ct);
        }
    }

    private void CopyOverrides(string tempDir, string overridesDirName)
    {
        var overridesDir = Path.Combine(tempDir, overridesDirName);
        if (!Directory.Exists(overridesDir)) return;

        _logger?.Log($"复制 {overridesDirName} ...");
        foreach (var file in Directory.EnumerateFiles(overridesDir, "*", SearchOption.AllDirectories))
        {
            var relative = file[(overridesDir.Length + 1)..];
            var dest = Path.Combine(_gameRoot, relative);
            var destDir = Path.GetDirectoryName(dest);
            if (destDir is not null) Directory.CreateDirectory(destDir);
            File.Copy(file, dest, overwrite: true);
        }
    }
}
