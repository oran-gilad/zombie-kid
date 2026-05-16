using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZombieKid;

public sealed class GitHubApiSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly GitHubApiSyncSettings _settings;
    private readonly string _dataDirectory;
    private readonly HttpClient _httpClient = new();
    private DateTime _lastSync = DateTime.MinValue;

    public GitHubApiSyncService(GitHubApiSyncSettings settings, string dataDirectory)
    {
        _settings = settings;
        _dataDirectory = dataDirectory;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ZombieKid");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task TrySyncAsync(CancellationToken cancellationToken, bool force = false)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.Token) || !Directory.Exists(_dataDirectory))
        {
            return;
        }

        if (!force && DateTime.Now - _lastSync < TimeSpan.FromMinutes(Math.Max(1, _settings.SyncIntervalMinutes)))
        {
            return;
        }

        _lastSync = DateTime.Now;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Token);

        foreach (var file in Directory.EnumerateFiles(_dataDirectory, "*.json").Order(StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(file);
            var content = await File.ReadAllTextAsync(file, cancellationToken);
            await UploadFileAsync($"data/{fileName}", content, cancellationToken);
            await UploadFileAsync($"docs/data/{fileName}", content, cancellationToken);
        }
    }

    private async Task UploadFileAsync(string repoPath, string content, CancellationToken cancellationToken)
    {
        var existing = await GetExistingFileAsync(repoPath, cancellationToken);
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var contentBase64 = Convert.ToBase64String(contentBytes);

        if (existing?.DecodedContent is not null && ContentsEqual(existing.DecodedContent, content))
        {
            return;
        }

        var body = new Dictionary<string, object?>
        {
            ["message"] = $"Update {repoPath}",
            ["content"] = contentBase64,
            ["branch"] = _settings.Branch
        };

        if (!string.IsNullOrWhiteSpace(existing?.Sha))
        {
            body["sha"] = existing.Sha;
        }

        var requestJson = JsonSerializer.Serialize(body);
        using var request = new HttpRequestMessage(HttpMethod.Put, ApiUrl(repoPath))
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<GitHubContent?> GetExistingFileAsync(string repoPath, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(ApiUrl(repoPath) + $"?ref={Uri.EscapeDataString(_settings.Branch)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var content = JsonSerializer.Deserialize<GitHubContent>(json, JsonOptions);
        if (content is null || string.IsNullOrWhiteSpace(content.Content))
        {
            return content;
        }

        var normalizedBase64 = content.Content.Replace("\n", "", StringComparison.Ordinal).Replace("\r", "", StringComparison.Ordinal);
        content.DecodedContent = Encoding.UTF8.GetString(Convert.FromBase64String(normalizedBase64));
        return content;
    }

    private string ApiUrl(string repoPath)
    {
        return $"https://api.github.com/repos/{Uri.EscapeDataString(_settings.Owner)}/{Uri.EscapeDataString(_settings.Repo)}/contents/{EscapePath(repoPath)}";
    }

    private static string EscapePath(string path)
    {
        return string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
    }

    private static bool ContentsEqual(string left, string right)
    {
        return NormalizeLineEndings(left).TrimEnd() == NormalizeLineEndings(right).TrimEnd();
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
    }

    private sealed class GitHubContent
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        public string? DecodedContent { get; set; }
    }
}
