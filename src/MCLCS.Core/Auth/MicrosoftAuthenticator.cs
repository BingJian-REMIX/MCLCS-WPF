using System.Text.Json;

namespace MCLCS.Core.Auth;

/// <summary>
/// Microsoft 账号认证：OAuth2 设备流 → Xbox Live → XSTS → Minecraft 令牌。
/// 客户端 ID 使用 Minecraft Launcher 官方值（公开）。
/// </summary>
public class MicrosoftAuthenticator : IAuthenticator
{
    private const string ClientId = "00000000402b5328"; // Minecraft Launcher 官方 client_id
    private const string DeviceCodeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
    private const string TokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string XboxAuthUrl = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsAuthUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string McLoginUrl = "https://api.minecraftservices.com/authentication/login_with_xbox";
    private const string McProfileUrl = "https://api.minecraftservices.com/minecraft/profile";

    private readonly HttpClient _client;
    private readonly Action<string>? _onUserCode;

    /// <param name="client">HttpClient 实例</param>
    /// <param name="onUserCode">收到 user_code 时回调（CLI: 打印到控制台；WPF: 展示给用户）</param>
    public MicrosoftAuthenticator(HttpClient client, Action<string>? onUserCode = null)
    {
        _client = client;
        _client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MCLCS/2.5");
        _onUserCode = onUserCode;
    }

    /// <summary>完整的 MS 认证流程。</summary>
    public async Task<AuthSession> AuthenticateAsync(string? username, CancellationToken ct = default)
    {
        // 1. 设备流
        var deviceResp = await PostFormAsync(DeviceCodeUrl, new()
        {
            ["client_id"] = ClientId,
            ["scope"] = "XboxLive.signin offline_access"
        }, ct);

        var deviceCode = deviceResp.GetProperty("device_code").GetString()!;
        var userCode = deviceResp.GetProperty("user_code").GetString()!;
        var verificationUri = deviceResp.GetProperty("verification_uri").GetString()!;
        var interval = deviceResp.TryGetProperty("interval", out var iv) ? iv.GetInt32() : 5;

        _onUserCode?.Invoke($"请在浏览器打开 {verificationUri} 并输入代码 {userCode}");

        // 2. 轮询 token
        var msToken = await PollForTokenAsync(deviceCode, interval, ct);

        // 3. Xbox Live 认证
        var xblToken = await XboxLiveAuthAsync(msToken, ct);

        // 4. XSTS 认证
        var (xstsToken, userHash) = await XstsAuthAsync(xblToken, ct);

        // 5. Minecraft 认证
        var mcToken = await MinecraftAuthAsync(xstsToken, userHash, ct);

        // 6. 获取 Profile
        var (uuid, name) = await GetProfileAsync(mcToken, ct);

        return new AuthSession
        {
            Username = name,
            Uuid = uuid,
            AccessToken = mcToken,
            UserType = "msa"
        };
    }

    private async Task<JsonElement> PostFormAsync(string url, Dictionary<string, string> data, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(data);
        using var resp = await _client.PostAsync(url, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"HTTP {(int)resp.StatusCode} ({resp.StatusCode}): {body}");
        }
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private async Task<string> PollForTokenAsync(string deviceCode, int interval, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMinutes(15);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var resp = await PostFormAsync(TokenUrl, new()
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["client_id"] = ClientId,
                    ["device_code"] = deviceCode
                }, ct);

                return resp.GetProperty("access_token").GetString()!;
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("authorization_pending"))
            {
                await Task.Delay(interval * 1000, ct);
            }
        }
        throw new TimeoutException("MS 登录超时（15 分钟）");
    }

    private async Task<string> XboxLiveAuthAsync(string msAccessToken, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(new
        {
            Properties = new
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = $"d={msAccessToken}"
            },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT"
        });

        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var resp = await _client.PostAsync(XboxAuthUrl, content, ct);
        resp.EnsureSuccessStatusCode();
        var el = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync(ct));
        return el.GetProperty("Token").GetString()!;
    }

    private async Task<(string Token, string UserHash)> XstsAuthAsync(string xblToken, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(new
        {
            Properties = new
            {
                SandboxId = "RETAIL",
                UserTokens = new[] { xblToken }
            },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT"
        });

        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var resp = await _client.PostAsync(XstsAuthUrl, content, ct);
        resp.EnsureSuccessStatusCode();
        var el = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync(ct));
        return (el.GetProperty("Token").GetString()!, el.GetProperty("DisplayClaims").GetProperty("xui")[0].GetProperty("uhs").GetString()!);
    }

    private async Task<string> MinecraftAuthAsync(string xstsToken, string userHash, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(new { identityToken = $"XBL3.0 x={userHash};{xstsToken}" });
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var resp = await _client.PostAsync(McLoginUrl, content, ct);
        resp.EnsureSuccessStatusCode();
        var el = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync(ct));
        return el.GetProperty("access_token").GetString()!;
    }

    private async Task<(string Uuid, string Name)> GetProfileAsync(string mcToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, McProfileUrl);
        req.Headers.Add("Authorization", $"Bearer {mcToken}");
        using var resp = await _client.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var el = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync(ct));
        return (el.GetProperty("id").GetString()!, el.GetProperty("name").GetString()!);
    }
}
