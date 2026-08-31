using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MCLCS.Core.Auth;

/// <summary>
/// Microsoft 账号认证：OAuth2 授权码流（PKCE + 本地 loopback 回跳）→ Xbox Live → XSTS → Minecraft 令牌。
/// 客户端 ID 使用 Minecraft Launcher 官方值（公开，已在微软 XboxLive 白名单中）。
/// 授权码流相比设备流不被微软限制，是第三方启动器的标准做法；若本地回跳端口不可用则回退设备流。
/// </summary>
public class MicrosoftAuthenticator : IAuthenticator
{
    private const string ClientId = "00000000402b5328"; // Minecraft Launcher 官方 client_id
    private const string AuthorizeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
    private const string DeviceCodeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
    private const string TokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string XboxAuthUrl = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsAuthUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string McLoginUrl = "https://api.minecraftservices.com/authentication/login_with_xbox";
    private const string McProfileUrl = "https://api.minecraftservices.com/minecraft/profile";
    private const string Scope = "XboxLive.signin offline_access";

    private readonly HttpClient _client;
    private readonly Action<string>? _onUserCode;

    /// <param name="client">HttpClient 实例</param>
    /// <param name="onUserCode">交互提示回调（WPF: 弹窗提示用户；CLI: 打印到控制台）</param>
    public MicrosoftAuthenticator(HttpClient client, Action<string>? onUserCode = null)
    {
        _client = client;
        _client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MCLCS/2.5");
        _onUserCode = onUserCode;
    }

    /// <summary>完整的 MS 认证流程。</summary>
    public async Task<AuthSession> AuthenticateAsync(string? username, CancellationToken ct = default)
    {
        // 优先走授权码流；若本地回跳端口被占用/权限不足（HttpListenerException），回退设备流。
        string msToken;
        try
        {
            msToken = await AuthorizationCodeLoginAsync(ct);
        }
        catch (HttpListenerException ex)
        {
            _onUserCode?.Invoke($"本地回跳端口不可用（{ex.Message}），已回退设备流，请按提示在浏览器输入代码。");
            msToken = await DeviceCodeLoginAsync(ct);
        }

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

    // ---- 授权码流（PKCE + loopback）----
    private async Task<string> AuthorizationCodeLoginAsync(CancellationToken ct)
    {
        var listener = AcquireLoopbackListener(out var port);
        var redirectUri = $"http://localhost:{port}/mclcs/";
        var verifier = GenerateCodeVerifier();
        var challenge = ComputeChallenge(verifier);

        var authUrl = AuthorizeUrl +
                      $"?client_id={ClientId}" +
                      "&response_type=code" +
                      $"&scope={Uri.EscapeDataString(Scope)}" +
                      $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                      "&response_mode=query" +
                      $"&code_challenge={challenge}" +
                      "&code_challenge_method=S256";

        try
        {
            TryOpenBrowser(authUrl);
            _onUserCode?.Invoke(
                "已在默认浏览器打开微软登录页，登录完成后本页会自动返回。\n" +
                "如未自动打开，请手动访问下方地址完成登录：\n" + authUrl);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));
            var ctxTask = listener.GetContextAsync();
            var delayTask = Task.Delay(Timeout.Infinite, timeoutCts.Token);
            var completed = await Task.WhenAny(ctxTask, delayTask);
            if (completed != ctxTask)
            {
                ct.ThrowIfCancellationRequested();
                throw new TimeoutException("微软登录超时（5 分钟）");
            }

            var ctx = await ctxTask;
            var code = ctx.Request.QueryString["code"];
            var error = ctx.Request.QueryString["error"];

            var body = error == null
                ? "<html><body style='font-family:sans-serif'><h3>登录成功</h3><p>MCLCS 已获取授权，可关闭此页面返回启动器。</p></body></html>"
                : $"<html><body style='font-family:sans-serif'><h3>登录失败</h3><p>{error}</p></body></html>";
            var buf = Encoding.UTF8.GetBytes(body);
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = buf.Length;
            await ctx.Response.OutputStream.WriteAsync(buf, 0, buf.Length, ct);
            ctx.Response.Close();

            if (error != null) throw new Exception($"微软授权返回错误：{error}");
            if (string.IsNullOrEmpty(code)) throw new Exception("未收到授权码");

            var resp = await PostFormAsync(TokenUrl, new()
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = ClientId,
                ["code"] = code,
                ["code_verifier"] = verifier,
                ["redirect_uri"] = redirectUri
            }, ct);

            return resp.GetProperty("access_token").GetString()!;
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    // ---- 设备流（回退）----
    private async Task<string> DeviceCodeLoginAsync(CancellationToken ct)
    {
        var deviceResp = await PostFormAsync(DeviceCodeUrl, new()
        {
            ["client_id"] = ClientId,
            ["scope"] = Scope
        }, ct);

        var deviceCode = deviceResp.GetProperty("device_code").GetString()!;
        var userCode = deviceResp.GetProperty("user_code").GetString()!;
        var verificationUri = deviceResp.GetProperty("verification_uri").GetString()!;
        var interval = deviceResp.TryGetProperty("interval", out var iv) ? iv.GetInt32() : 5;

        _onUserCode?.Invoke($"请在浏览器打开 {verificationUri} 并输入代码 {userCode}");

        return await PollForTokenAsync(deviceCode, interval, ct);
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

    // ---- loopback 监听辅助 ----
    private static HttpListener AcquireLoopbackListener(out int port)
    {
        for (int p = 49152; p < 49202; p++)
        {
            var l = new HttpListener();
            l.Prefixes.Add($"http://localhost:{p}/mclcs/");
            try
            {
                l.Start();
                port = p;
                return l;
            }
            catch (HttpListenerException)
            {
                l.Close();
            }
        }
        throw new HttpListenerException(5,
            "无法在本地获取回跳端口（可能被防火墙或权限阻止，建议以管理员身份运行，或改用外置登录）");
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
