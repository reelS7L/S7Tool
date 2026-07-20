namespace S7Tool.Services.Interfaces;

public interface ISecretsProvider
{
    string? GetGeminiApiKey();

    void SetGeminiApiKey(string apiKey);
}
