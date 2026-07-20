namespace S7Tool.Services.Interfaces;

public interface IDiskManagerService
{
    bool IsOfflineEnvironmentReady { get; }

    Task PrepareOfflineEnvironmentAsync(bool force, Action<string> onLog, CancellationToken cancellationToken);

    Task<bool> IsOfflineEnvironmentUpdateAvailableAsync();

    Task LaunchOfflineDiskManagerAsync(Action<string> onLog, CancellationToken cancellationToken);
}
