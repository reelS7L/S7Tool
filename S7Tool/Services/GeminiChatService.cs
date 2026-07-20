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
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:streamGenerateContent?alt=sse";

    private const string NotConfiguredMessage =
        "Clé API Gemini non configurée. Ouvre les paramètres de l'IA (icône ⚙) pour la renseigner, " +
        "ou définis la variable d'environnement S7TOOL_GEMINI_API_KEY.";

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
            onUpdate(NotConfiguredMessage);
            return;
        }

        if (!_tracker.CanSend())
        {
            onUpdate("Limite de requêtes atteinte, réessaie dans un instant.");
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
                    onUpdate("Trop de requêtes envoyées à l'API. Réessaie dans environ 30s.");
                    return;
                }

                if ((int)response.StatusCode == 503 && attempt < 2)
                {
                    await Task.Delay(1000 * (attempt + 1), cancellationToken);
                    continue;
                }

                onUpdate($"Erreur API Gemini (code {(int)response.StatusCode}).");
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

        onUpdate("Service Gemini indisponible pour le moment, réessaie plus tard.");
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
        return IsConfigured ? _tracker.GetStatus() : "Clé API non configurée";
    }
}
