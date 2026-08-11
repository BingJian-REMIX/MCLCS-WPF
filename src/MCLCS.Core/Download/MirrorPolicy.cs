using MCLCS.Core.Utils;

namespace MCLCS.Core.Download;

/// <summary>
/// 镜像源策略：BMCLAPI 优先，官方源回退。
/// 提供各资源的候选 URL 列表（按优先级排序）。
/// </summary>
public static class MirrorPolicy
{
    /// <summary>版本清单候选 URL（BMCLAPI 优先）。</summary>
    public static IEnumerable<string> VersionManifestUrls()
        => new[] { GameConstants.BmclapiVersionManifest, GameConstants.OfficialVersionManifest };

    /// <summary>
    /// 版本 JSON 候选 URL。官方源需要 manifest 中的 url，这里传入其官方地址作为回退。
    /// BMCLAPI 的正确路径为 <c>/version/{id}/json</c>；漏掉结尾的 <c>/json</c> 会 404（bug #20，已实测确认）。
    /// </summary>
    public static IEnumerable<string> VersionJsonUrls(string id, string? officialUrl = null)
    {
        yield return $"{GameConstants.BmclapiBase}/version/{id}/json";
        if (!string.IsNullOrEmpty(officialUrl))
            yield return officialUrl!;
    }

    /// <summary>Library 候选 URL（path 为本地仓库相对路径）。</summary>
    public static IEnumerable<string> LibraryUrls(string path)
        => new[] { $"{GameConstants.BmclapiBase}/libraries/{path}", $"{GameConstants.OfficialLibrariesBase}/{path}" };

    /// <summary>
    /// 资源对象候选 URL（hash 为资源 sha1）。
    /// 三个源都按 Mojang 约定以 hash 前两位分目录：<c>/{hash[0:2]}/{hash}</c>。
    /// 缺少该分目录段（形如 <c>/assets/{hash}</c> 或 <c>/assets/objects/{hash}</c>）会一律 404，
    /// 这正是"Minecraft 核心游戏文件无法下载"的根因（bug #20），已实测确认。
    /// </summary>
    public static IEnumerable<string> AssetUrls(string hash)
    {
        var prefix = hash[..2];
        yield return $"{GameConstants.BmclapiBase}/assets/{prefix}/{hash}";
        yield return $"{GameConstants.BmclapiBase}/objects/{prefix}/{hash}";
        yield return $"{GameConstants.OfficialAssetsBase}/{prefix}/{hash}";
    }

    /// <summary>依次尝试候选 URL，返回首个成功的内容。全部失败抛异常。</summary>
    public static async Task<string> GetStringWithFallback(IEnumerable<string> urls, HttpClient client, CancellationToken ct = default)
    {
        var last = (Exception?)null;
        foreach (var url in urls)
        {
            try { return await client.GetStringAsync(url, ct); }
            catch (Exception ex) { last = ex; }
        }
        throw new HttpRequestException($"所有镜像源均失败：{string.Join(", ", urls)}", last);
    }

    /// <summary>依次尝试候选 URL 下载到流，返回首次成功的字节数组。</summary>
    public static async Task<byte[]> DownloadBytesWithFallback(IEnumerable<string> urls, HttpClient client, IProgress<double>? progress, CancellationToken ct)
    {
        var last = (Exception?)null;
        foreach (var url in urls)
        {
            try
            {
                using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? -1L;
                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var ms = new MemoryStream();
                var buffer = new byte[8192];
                long read = 0;
                int n;
                while ((n = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    await ms.WriteAsync(buffer.AsMemory(0, n), ct);
                    read += n;
                    if (total > 0) progress?.Report((double)read / total);
                }
                return ms.ToArray();
            }
            catch (Exception ex) { last = ex; }
        }
        throw new HttpRequestException($"所有镜像源均失败", last);
    }
}
