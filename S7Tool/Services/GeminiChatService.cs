using S7Tool.Services.Interfaces;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace S7Tool.Services;

public class GeminiChatService : IGeminiChatService
{
    private readonly HttpClient _httpClient = new();
    private readonly ApiRateTracker _tracker = new();
    private readonly ISecretsProvider _secretsProvider;

    private const string StreamEndpoint =
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:streamGenerateContent?alt=sse";

    public GeminiChatService(ISecretsProvider secretsProvider)
    {
        _secretsProvider = secretsProvider;
    }

    private string? ApiKey => _secretsProvider.GetGeminiApiKey();

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public async Task StreamMessageAsync(
        IReadOnlyList<(string Role, string Text)> history,
        Action<string> onUpdate,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            onUpdate(LocalizationManager.T("Str_AiChat_NotConfigured"));
            return;
        }

        if (!_tracker.CanSend())
        {
            onUpdate(LocalizationManager.T("Str_AiChat_RateLimited"));
            return;
        }

        _tracker.RegisterRequest();

        for (int attempt = 0; attempt < 3; attempt++)
        {
            var body = new
            {
                contents = history.Select(h => new
                {
                    role = h.Role,
                    parts = new[] { new { text = h.Text } }
                }).ToArray()
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, StreamEndpoint);
            request.Headers.Add("X-goog-api-key", ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == 429)
                {
                    _tracker.SetCooldown(30);
                    onUpdate(LocalizationManager.T("Str_AiChat_TooManyRequests"));
                    return;
                }

                if ((int)response.StatusCode == 503 && attempt < 2)
                {
                    await Task.Delay(1000 * (attempt + 1), cancellationToken);
                    continue;
                }

                string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                string detail = TryExtractErrorMessage(errorBody) ?? errorBody;
                onUpdate(string.Format(LocalizationManager.T("Str_AiChat_ApiError"), (int)response.StatusCode) +
                    (string.IsNullOrWhiteSpace(detail) ? "" : $"\n{detail}"));
                return;
            }

            var accumulated = new StringBuilder();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (!line.StartsWith("data: ", StringComparison.Ordinal))
                    continue;

                var json = line["data: ".Length..].Trim();
                if (string.IsNullOrEmpty(json))
                    continue;

                string? chunk = TryExtractText(json);
                if (!string.IsNullOrEmpty(chunk))
                {
                    accumulated.Append(chunk);
                    onUpdate(accumulated.ToString());
                }
            }

            return;
        }

        onUpdate(LocalizationManager.T("Str_AiChat_ServiceUnavailable"));
    }

    private static string? TryExtractErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("error").GetProperty("message").GetString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? TryExtractText(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public string GetStatus()
    {
        return IsConfigured ? _tracker.GetStatus() : LocalizationManager.T("Str_AiChat_KeyNotConfigured");
    }
}
