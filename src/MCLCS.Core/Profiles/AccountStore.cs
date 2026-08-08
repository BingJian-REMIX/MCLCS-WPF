using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCLCS.Core.Profiles;

/// <summary>一条账号记录。</summary>
public class AccountEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("authType")]
    public string AuthType { get; set; } = "offline"; // offline | microsoft | authlib

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = "0";

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("msExpiresAt")]
    public string? MsExpiresAt { get; set; }

    [JsonPropertyName("authlibServerUrl")]
    public string? AuthlibServerUrl { get; set; }

    [JsonPropertyName("lastUsed")]
    public string? LastUsed { get; set; }

    [JsonPropertyName("skinUrl")]
    public string? SkinUrl { get; set; }
}

/// <summary>多账号存储（mclcs_accounts.json）。</summary>
public static class AccountStore
{
    private static string Path(string gameRoot) => System.IO.Path.Combine(gameRoot, "mclcs_accounts.json");

    public static List<AccountEntry> Load(string gameRoot)
    {
        var p = Path(gameRoot);
        if (!File.Exists(p)) return new List<AccountEntry>();
        try
        {
            return JsonSerializer.Deserialize<List<AccountEntry>>(File.ReadAllText(p)) ?? new();
        }
        catch
        {
            return new List<AccountEntry>();
        }
    }

    public static void Save(string gameRoot, List<AccountEntry> accounts)
    {
        Directory.CreateDirectory(gameRoot);
        var json = JsonSerializer.Serialize(accounts, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path(gameRoot), json);
    }

    public static AccountEntry? GetLastUsed(string gameRoot)
    {
        var accounts = Load(gameRoot);
        return accounts
            .Where(a => !string.IsNullOrEmpty(a.LastUsed))
            .OrderByDescending(a => a.LastUsed)
            .FirstOrDefault()
            ?? accounts.FirstOrDefault();
    }

    public static void MarkUsed(string gameRoot, string accountId)
    {
        var accounts = Load(gameRoot);
        var entry = accounts.Find(a => a.Id == accountId);
        if (entry is not null)
        {
            entry.LastUsed = DateTime.UtcNow.ToString("o");
            Save(gameRoot, accounts);
        }
    }

    public static void Upsert(string gameRoot, AccountEntry account)
    {
        var accounts = Load(gameRoot);
        var idx = accounts.FindIndex(a => a.Id == account.Id);
        if (idx >= 0) accounts[idx] = account;
        else accounts.Add(account);
        account.LastUsed = DateTime.UtcNow.ToString("o");
        Save(gameRoot, accounts);
    }

    public static bool Remove(string gameRoot, string accountId)
    {
        var accounts = Load(gameRoot);
        var removed = accounts.RemoveAll(a => a.Id == accountId) > 0;
        if (removed) Save(gameRoot, accounts);
        return removed;
    }
}
