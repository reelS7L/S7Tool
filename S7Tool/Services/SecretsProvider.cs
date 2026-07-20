using S7Tool.Services.Interfaces;
using System.IO;
using System.Text.Json;

namespace S7Tool.Services;

public class SecretsProvider : ISecretsProvider
{
    private const string EnvVarName = "S7TOOL_GEMINI_API_KEY";

    private static readonly string SecretsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "S7Tool",
        "secrets.json");

    public string? GetGeminiApiKey()
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        try
        {
            if (!File.Exists(SecretsFilePath))
                return null;

            using var stream = File.OpenRead(SecretsFilePath);
            using var doc = JsonDocument.Parse(stream);

            if (doc.RootElement.TryGetProperty("GeminiApiKey", out var value))
                return value.GetString();
        }
        catch
        {
        }

        return null;
    }

    public void SetGeminiApiKey(string apiKey)
    {
        var directory = Path.GetDirectoryName(SecretsFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(new { GeminiApiKey = apiKey });
        File.WriteAllText(SecretsFilePath, json);
    }
}
