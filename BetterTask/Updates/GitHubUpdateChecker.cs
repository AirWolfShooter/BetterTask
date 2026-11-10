using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using BetterTask.Infrastructure;

namespace BetterTask.Updates;

public enum UpdateCheckStatus
{
    UpdateAvailable,
    UpToDate,
    NoRelease,
    Failed
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    Version CurrentVersion,
    Version? LatestVersion = null,
    string? ReleaseUrl = null,
    string? ReleaseNotes = null,
    string? ErrorMessage = null,
    UpdateAsset? Asset = null);

public sealed record UpdateAsset(string Name, string DownloadUrl, string? Digest, long Size);

public static class GitHubUpdateChecker
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/AirWolfShooter/BetterTask/releases/latest";

    public static Version CurrentVersion { get; } =
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    private static readonly HttpClient client = CreateHttpClient();

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await client.GetAsync(LatestReleaseUrl, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                AppLogger.Info("Updates", "No published GitHub release was found.");
                return new UpdateCheckResult(UpdateCheckStatus.NoRelease, CurrentVersion);
            }

            response.EnsureSuccessStatusCode();
            GitHubRelease? release = await response.Content.ReadFromJsonAsync<GitHubRelease>(
                cancellationToken: cancellationToken);

            if (release is null || !TryParseVersion(release.TagName, out Version latestVersion))
            {
                throw new InvalidDataException("GitHub's latest release does not have a valid version tag.");
            }

            if (!Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out Uri? releaseUri)
                || releaseUri.Scheme != Uri.UriSchemeHttps
                || !string.Equals(releaseUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("GitHub returned an invalid release URL.");
            }

            UpdateCheckStatus status = latestVersion > CurrentVersion
                ? UpdateCheckStatus.UpdateAvailable
                : UpdateCheckStatus.UpToDate;

            AppLogger.Info(
                "Updates",
                status == UpdateCheckStatus.UpdateAvailable
                    ? $"Version {latestVersion} is available; current version is {CurrentVersion}."
                    : $"Current version {CurrentVersion} is up to date.");

            return new UpdateCheckResult(
                status,
                CurrentVersion,
                latestVersion,
                releaseUri.AbsoluteUri,
                release.Body,
                Asset: release.Assets.FirstOrDefault(a =>
                    a.Name.Equals($"BetterTask-v{latestVersion}-win-{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}.exe", StringComparison.OrdinalIgnoreCase))
                    is { } asset ? new UpdateAsset(asset.Name, asset.DownloadUrl, asset.Digest, asset.Size) : null);
        }
        catch (Exception ex) when (ex is HttpRequestException
            or TaskCanceledException
            or JsonException
            or InvalidDataException)
        {
            AppLogger.Warning("Updates", "Could not check GitHub for updates.", ex);
            return new UpdateCheckResult(
                UpdateCheckStatus.Failed,
                CurrentVersion,
                ErrorMessage: ex is TaskCanceledException
                    ? "The request timed out. Check your internet connection and try again."
                    : ex.Message);
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"BetterTask/{CurrentVersion}");
        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
        return httpClient;
    }

    private static bool TryParseVersion(string? tag, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        string value = tag.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
            value = value[1..];

        int metadataStart = value.IndexOfAny(['-', '+']);
        if (metadataStart >= 0)
            value = value[..metadataStart];

        string[] parts = value.Split('.');
        if (parts.Length == 2)
            value += ".0";

        if (!Version.TryParse(value, out Version? parsedVersion) || parsedVersion is null)
            return false;

        version = parsedVersion;
        return true;
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public string DownloadUrl { get; set; } = "";
        [JsonPropertyName("digest")] public string? Digest { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = new();

        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }
    }
}
