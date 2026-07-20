namespace S7Tool.Services.Interfaces;

public interface IGeminiChatService
{
    bool IsConfigured { get; }

    Task StreamMessageAsync(
        IReadOnlyList<(string Role, string Text)> history,
        Action<string> onUpdate,
        CancellationToken cancellationToken = default);

    string GetStatus();
}
