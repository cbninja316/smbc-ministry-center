using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmbcStatusBoard.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace SmbcStatusBoard.Api.Services;

public class PraiseChartsService(AppDbContext db, IHttpClientFactory httpClientFactory)
{
    private const string ApiBase = "https://api.praisecharts.com/v3";

    // ── OAuth credential helpers ─────────────────────────────────────────────

    public async Task<(string? consumerKey, string? consumerSecret, string? accessToken, string? accessSecret)> GetCredentialsAsync()
    {
        var keys = new[] { "PraiseCharts:ConsumerKey", "PraiseCharts:ConsumerSecret", "PraiseCharts:AccessToken", "PraiseCharts:AccessSecret" };
        var settings = await db.AppSettings.Where(s => keys.Contains(s.Key)).ToListAsync();
        return (
            settings.FirstOrDefault(s => s.Key == "PraiseCharts:ConsumerKey")?.Value,
            settings.FirstOrDefault(s => s.Key == "PraiseCharts:ConsumerSecret")?.Value,
            settings.FirstOrDefault(s => s.Key == "PraiseCharts:AccessToken")?.Value,
            settings.FirstOrDefault(s => s.Key == "PraiseCharts:AccessSecret")?.Value
        );
    }

    public bool IsConfigured(string? consumerKey, string? consumerSecret, string? accessToken, string? accessSecret)
        => !string.IsNullOrEmpty(consumerKey) && !string.IsNullOrEmpty(consumerSecret)
           && !string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(accessSecret);

    // ── OAuth 1.0a signing (RFC 5849 / HMAC-SHA1) ────────────────────────────

    private static string BuildAuthHeader(string method, string url, Dictionary<string, string> oauthParams,
        Dictionary<string, string>? queryParams, string consumerSecret, string accessSecret)
    {
        // Collect all params for the base string
        var allParams = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in oauthParams) allParams[PercentEncode(kv.Key)] = PercentEncode(kv.Value);
        if (queryParams != null)
            foreach (var kv in queryParams) allParams[PercentEncode(kv.Key)] = PercentEncode(kv.Value);

        var paramString = string.Join("&", allParams.Select(kv => $"{kv.Key}={kv.Value}"));
        var baseString = $"{method.ToUpper()}&{PercentEncode(url)}&{PercentEncode(paramString)}";
        var signingKey = $"{PercentEncode(consumerSecret)}&{PercentEncode(accessSecret)}";

        using var hmac = new HMACSHA1(Encoding.ASCII.GetBytes(signingKey));
        var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.ASCII.GetBytes(baseString)));
        oauthParams["oauth_signature"] = sig;

        var header = "OAuth " + string.Join(", ", oauthParams
            .Select(kv => $"{PercentEncode(kv.Key)}=\"{PercentEncode(kv.Value)}\""));
        return header;
    }

    private static string PercentEncode(string value)
        => Uri.EscapeDataString(value).Replace("+", "%20").Replace("*", "%2A").Replace("%7E", "~");

    private HttpRequestMessage BuildRequest(string method, string path, Dictionary<string, string>? queryParams,
        string consumerKey, string consumerSecret, string accessToken, string accessSecret)
    {
        var url = ApiBase + path;
        var nonce = Guid.NewGuid().ToString("N");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var oauthParams = new Dictionary<string, string>
        {
            ["oauth_consumer_key"] = consumerKey,
            ["oauth_nonce"] = nonce,
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = timestamp,
            ["oauth_token"] = accessToken,
            ["oauth_version"] = "1.0",
        };

        var authHeader = BuildAuthHeader(method, url, oauthParams, queryParams, consumerSecret, accessSecret);

        var fullUrl = queryParams != null && queryParams.Count > 0
            ? url + "?" + string.Join("&", queryParams.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"))
            : url;

        var req = new HttpRequestMessage(new HttpMethod(method), fullUrl);
        req.Headers.Add("Authorization", authHeader);
        req.Headers.Add("Accept", "application/json");
        return req;
    }

    // ── API calls ────────────────────────────────────────────────────────────

    public async Task<JsonElement?> GetLibraryAsync(string consumerKey, string consumerSecret, string accessToken, string accessSecret)
    {
        var client = httpClientFactory.CreateClient("PraiseCharts");
        var req = BuildRequest("GET", "/library", null, consumerKey, consumerSecret, accessToken, accessSecret);
        var resp = await client.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    public async Task<JsonElement?> SearchSongsAsync(string q, int page, string consumerKey, string consumerSecret, string accessToken, string accessSecret)
    {
        var queryParams = new Dictionary<string, string> { ["q"] = q, ["page"] = page.ToString(), ["per_page"] = "20" };
        var client = httpClientFactory.CreateClient("PraiseCharts");
        var req = BuildRequest("GET", "/songs", queryParams, consumerKey, consumerSecret, accessToken, accessSecret);
        var resp = await client.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    public async Task<JsonElement?> GetSongAsync(string songId, string consumerKey, string consumerSecret, string accessToken, string accessSecret)
    {
        var client = httpClientFactory.CreateClient("PraiseCharts");
        var req = BuildRequest("GET", $"/songs/{songId}", null, consumerKey, consumerSecret, accessToken, accessSecret);
        var resp = await client.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    // ── OAuth 1.0 three-legged flow ──────────────────────────────────────────

    public async Task<(string? requestToken, string? requestSecret)> GetRequestTokenAsync(string consumerKey, string consumerSecret, string callbackUrl)
    {
        var url = "https://www.praisecharts.com/api/oauth/request_token";
        var nonce = Guid.NewGuid().ToString("N");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var oauthParams = new Dictionary<string, string>
        {
            ["oauth_callback"] = callbackUrl,
            ["oauth_consumer_key"] = consumerKey,
            ["oauth_nonce"] = nonce,
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = timestamp,
            ["oauth_version"] = "1.0",
        };
        var authHeader = BuildAuthHeader("POST", url, oauthParams, null, consumerSecret, "");
        var client = httpClientFactory.CreateClient("PraiseCharts");
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("Authorization", authHeader);
        var resp = await client.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return (null, null);
        var body = await resp.Content.ReadAsStringAsync();
        var parts = body.Split('&').Select(p => p.Split('=')).Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        parts.TryGetValue("oauth_token", out var token);
        parts.TryGetValue("oauth_token_secret", out var secret);
        return (token, secret);
    }

    public async Task<(string? accessToken, string? accessSecret)> ExchangeForAccessTokenAsync(
        string consumerKey, string consumerSecret, string requestToken, string requestSecret, string verifier)
    {
        var url = "https://www.praisecharts.com/api/oauth/access_token";
        var nonce = Guid.NewGuid().ToString("N");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var oauthParams = new Dictionary<string, string>
        {
            ["oauth_consumer_key"] = consumerKey,
            ["oauth_nonce"] = nonce,
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = timestamp,
            ["oauth_token"] = requestToken,
            ["oauth_verifier"] = verifier,
            ["oauth_version"] = "1.0",
        };
        var authHeader = BuildAuthHeader("POST", url, oauthParams, null, consumerSecret, requestSecret);
        var client = httpClientFactory.CreateClient("PraiseCharts");
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("Authorization", authHeader);
        var resp = await client.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return (null, null);
        var body = await resp.Content.ReadAsStringAsync();
        var parts = body.Split('&').Select(p => p.Split('=')).Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        parts.TryGetValue("oauth_token", out var token);
        parts.TryGetValue("oauth_token_secret", out var secret);
        return (token, secret);
    }
}
