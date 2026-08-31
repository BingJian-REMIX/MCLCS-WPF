using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MCLCS.Core.Auth;

/// <summary>
/// Microsoft 账号认证：OAuth2 授权码流（PKCE）→ Xbox Live → XSTS → Minecraft 令牌。
/// 使用 Minecraft Launcher 官方 client_id（00000000402b5328，已在 XboxLive 白名单中）。
/// 由于该 client_id 注册的回跳地址固定为 https://login.live.com/oauth20_desktop.srf，无法使用 localhost loopback，
/// 因此打开系统浏览器后需要用户将回跳地址粘贴回来（后续可替换为内嵌 WebView2 自动捕获）。
/// </summary>
public class MicrosoftAuthenticator : IAuthenticator
{
    private const string ClientId = "00000000402b5328"; // Minecraft Launcher 官方 client_id
    private const string AuthorizeUrl = "https://login.live.com/oauth20_authorize.srf";
    private const string TokenUrl = "https://login.live.com/oauth20_token.srf";
    private const string DeviceCodeUrl = "https://login.live.com/oauth20_token.srf";
    private const string XboxAuthUrl = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsAuthUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string McLoginUrl = "https://api.minecraftservices.com/authentication/login_with_xbox";
    private const string McProfileUrl = "https://api.minecraftservices.com/minecraft/profile";
    private const string RedirectUri = "https://login.live.com/oauth20_desktop.srf";
    private const string Scope = "service::user.auth.xboxlive.com::MBI_SSL";

    private readonly HttpClient _client;
    private readonly Action<string>? _onUserCode;
    private readonly Func<string, Task<string>>? _onPromptUrl;

    /// <param name="client">HttpClient 实例</param>
    /// <param name="onUserCode">提示回调（显示登录链接或状态）</param>
    /// <param name="onPromptUrl">请求用户粘贴浏览器回跳地址的回调</param>
    public MicrosoftAuthenticator(HttpClient client,
        Action<string>? onUserCode = null,
        Func<string, Task<string>>? onPromptUrl = null)
    {
        _client = client;
        _client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MCLCS/2.5");
        _onUserCode = onUserCode;
        _onPromptUrl = onPromptUrl;
    }

    /// <summary>完整的 MS 认证流程。</summary>
    public async Task<AuthSession> AuthenticateAsync(string? username, CancellationToken ct = default)
    {
        string msToken = await AuthorizationCodeLoginAsync(ct);

        // 后续 Xbox Live / XSTS / Minecraft 流程与授权方式无关。
        var xblToken = await XboxLiveAuthAsync(msToken, ct);
        var (xstsToken, userHash) = await XstsAuthAsync(xblToken, ct);
        var mcToken = await MinecraftAuthAsync(xstsToken, userHash, ct);
        var (uuid, name) = await GetProfileAsync(mcToken, ct);

        return new AuthSession
        {
            Username = name,
            Uuid = uuid,
            AccessToken = mcToken,
            UserType = "msa"
        };
    }

    // ---- 授权码流（PKCE + 系统浏览器 + 粘贴回跳 URL）----
    private async Task<string> AuthorizationCodeLoginAsync(CancellationToken ct)
    {
        var verifier = GenerateCodeVerifier();
        var challenge = ComputeChallenge(verifier);

        var authUrl = AuthorizeUrl +
                      $"?client_id={ClientId}" +
                      "&response_type=code" +
                      $"&scope={Uri.EscapeDataString(Scope)}" +
                      $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                      "&response_mode=query" +
                      $"&code_challenge={challenge}" +
                      "&code_challenge_method=S256";

        TryOpenBrowser(authUrl);

        _onUserCode?.Invoke(
            "已在默认浏览器打开微软登录页。\n" +
            "登录完成后，请将浏览器地址栏中显示的以 https://login.live.com/oauth20_desktop.srf 开头的完整地址粘贴回启动器。");

        if (_onPromptUrl is null)
            throw new InvalidOperationException("未提供粘贴回跳地址的回调，无法完成授权码流。");

        var promptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        promptCts.CancelAfter(TimeSpan.FromMinutes(5));

        string callbackUrl;
        try
        {
            callbackUrl = await _onPromptUrl(
                "请将浏览器登录完成后地址栏里的完整链接粘贴到此处：");
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("等待粘贴回跳地址超时（5 分钟）");
        }

        if (string.IsNullOrWhiteSpace(callbackUrl))
            throw new Exception("未收到回跳地址，登录已取消。");

        var uri = new Uri(callbackUrl);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var code = query["code"];
        var error = query["error"];
        var errorDescription = query["error_description"];

        if (error != null)
            throw new Exception($"微软授权返回错误：{error} ({errorDescription})");
        if (string.IsNullOrEmpty(code))
            throw new Exception("回跳地址中未找到授权码（code），请确认是否已完成登录。");

        var resp = await PostFormAsync(TokenUrl, new()
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = RedirectUri,
            ["scope"] = Scope
        }, ct);

        return resp.GetProperty("access_token").GetString()!;
    }

    private static void TryOpenBrowser(string url)
    {
        try
        {
            var psi = new ProcessStartInfo(url) { UseShellExecute = true };
            Process.Start(psi);
        }
        catch
        {
            // 自动打开失败则依赖回调提示用户手动访问
        }
    }

    // ---- PKCE 辅助 ----
    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64Url(bytes);
    }

    private static string ComputeChallenge(string verifier)
    {
        using var sha = SHA256.Create();
        return Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
    }

    private static string Base64Url(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // ---- 后续 Xbox / XSTS / Minecraft 流程（与原实现一致）----
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

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
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

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await _client.PostAsync(XstsAuthUrl, content, ct);
        resp.EnsureSuccessStatusCode();
        var el = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync(ct));
        return (el.GetProperty("Token").GetString()!, el.GetProperty("DisplayClaims").GetProperty("xui")[0].GetProperty("uhs").GetString()!);
    }

    private async Task<string> MinecraftAuthAsync(string xstsToken, string userHash, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(new { identityToken = $"XBL3.0 x={userHash};{xstsToken}" });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
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
