using System.Text.Json;
using MCLCS.Core.Models;
using MCLCS.Core.Utils;

namespace MCLCS.Core.Download;

/// <summary>
/// CurseForge API v1 客户端（无需 API key 的公开端点）。
/// </summary>
public class CurseForgeClient
{
    private readonly HttpClient _client;

    public CurseForgeClient(HttpClient client) => _client = client;

    /// <summary>通过 fileId 获取下载链接。</summary>
    public async Task<CurseForgeApiFile?> GetFileAsync(int modId, int fileId, CancellationToken ct = default)
    {
        var url = $"https://api.curseforge.com/v1/mods/{modId}/files/{fileId}";
        var json = await _client.GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("data", out var data))
            return JsonSerializer.Deserialize<CurseForgeApiFile>(data.GetRawText());
        return null;
    }

    /// <summary>批量获取 mod 信息。</summary>
    public async Task<Dictionary<int, CurseForgeModInfo>> GetModsAsync(IEnumerable<int> modIds, CancellationToken ct = default)
    {
        var ids = modIds.Distinct().ToList();
        if (ids.Count == 0) return new();
        var result = new Dictionary<int, CurseForgeModInfo>();

        // CurseForge API 建议一次不超过 50 个 ID
        foreach (var batch in ids.Chunk(50))
        {
            var body = JsonSerializer.Serialize(new { modIds = batch });
            var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            var resp = await _client.PostAsync("https://api.curseforge.com/v1/mods", content, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            var mods = JsonSerializer.Deserialize<CurseForgeModsResponse>(json);
            if (mods?.Data != null)
                foreach (var m in mods.Data)
                    result[m.Id] = m;
        }
        return result;
    }

    /// <summary>批量获取文件信息（获取下载 URL）。</summary>
    public async Task<Dictionary<(int ModId, int FileId), CurseForgeApiFile>> GetFilesAsync(
        IEnumerable<(int ModId, int FileId)> fileKeys, CancellationToken ct = default)
    {
        var result = new Dictionary<(int, int), CurseForgeApiFile>();
        foreach (var batch in fileKeys.Chunk(50))
        {
            var body = JsonSerializer.Serialize(new { fileIds = batch.Select(k => k.FileId).ToList() });
            var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            var resp = await _client.PostAsync("https://api.curseforge.com/v1/mods/files", content, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            var filesResp = JsonSerializer.Deserialize<CurseForgeFilesResponse>(json);
            if (filesResp?.Data != null)
            {
                // 响应按请求顺序返回，我们按顺序匹配
                for (var i = 0; i < Math.Min(batch.Length, filesResp.Data.Count); i++)
                    result[batch[i]] = filesResp.Data[i];
            }
        }
        return result;
    }
}
