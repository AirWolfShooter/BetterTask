using System.Net.Http.Json;
using System.Text.Json;
using BetterTask.Core.RecordingAndPlayback;
using BetterTask.Interface.Managers;

namespace BetterTask.Core.Sharing;

internal static class MacroShareClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private const int MaxPayloadBytes = 1_000_000;
    private const int MaxEvents = 10_000;

    public static async Task<MacroShareLink> ShareAsync(RecordingFile recording, CancellationToken cancellationToken = default)
    {
        Uri baseUri = GetBaseUri();
        Validate(recording);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(recording);
        if (payload.Length > MaxPayloadBytes)
            throw new InvalidDataException("This macro is too large to share (maximum 1 MB).");

        using var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        using HttpResponseMessage response = await Http.PostAsync(new Uri(baseUri, "api/macros"), content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        ShareCreated? result = await response.Content.ReadFromJsonAsync<ShareCreated>(JsonOptions, cancellationToken);
        if (result is null || string.IsNullOrWhiteSpace(result.Code))
            throw new InvalidDataException("The sharing service returned an invalid response.");
        return new MacroShareLink(result.Code, result.ExpiresAt, result.Reused);
    }

    public static async Task<RecordingFile> DownloadAsync(string code, CancellationToken cancellationToken = default)
    {
        Uri baseUri = GetBaseUri();
        string normalized = new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (normalized.Length != 12)
            throw new ArgumentException("Enter a 12-character sharing code.", nameof(code));

        using HttpResponseMessage response = await Http.GetAsync(
            new Uri(baseUri, $"api/macros/{Uri.EscapeDataString(normalized)}"),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var limited = new MemoryStream();
        byte[] buffer = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (limited.Length + read > MaxPayloadBytes)
                throw new InvalidDataException("The downloaded macro exceeds the 1 MB safety limit.");
            limited.Write(buffer, 0, read);
        }

        RecordingFile? recording = JsonSerializer.Deserialize<RecordingFile>(limited.ToArray(), JsonOptions);
        if (recording is null) throw new InvalidDataException("The sharing service returned an invalid macro.");
        Validate(recording);
        return recording;
    }

    private static Uri GetBaseUri()
    {
        string configured = SettingsManager.Instance.settings.MacroShareApiUrl.Trim();
        if (!Uri.TryCreate(configured, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttps && !(uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp)))
            throw new InvalidOperationException("Set the macro sharing service HTTPS address in Settings → General first.");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }

    private static void Validate(RecordingFile recording)
    {
        if (string.IsNullOrWhiteSpace(recording.Name) || recording.Name.Length > 100)
            throw new InvalidDataException("Macro names must contain 1–100 characters.");
        if (recording.Events is null || recording.Events.Count is 0 or > MaxEvents)
            throw new InvalidDataException($"Macros must contain between 1 and {MaxEvents:N0} events to share.");
        if (recording.Events.Any(item => item is null || item.Time < 0))
            throw new InvalidDataException("The macro contains invalid event data.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        string message = response.StatusCode switch
        {
            System.Net.HttpStatusCode.NotFound => "That sharing code was not found or has expired.",
            System.Net.HttpStatusCode.TooManyRequests => "The sharing service is busy. Try again in a minute.",
            _ => $"Sharing service request failed ({(int)response.StatusCode})."
        };
        throw new HttpRequestException(message, null, response.StatusCode);
    }

    internal sealed record MacroShareLink(string Code, DateTimeOffset ExpiresAt, bool Reused);

    private sealed class ShareCreated
    {
        public string Code { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public bool Reused { get; set; }
    }
}
