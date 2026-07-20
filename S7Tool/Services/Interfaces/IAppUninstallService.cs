using S7Tool.Models;

namespace S7Tool.Services.Interfaces;

public interface IAppUninstallService
{
    List<InstalledApp> GetInstalledApps();
    Task UninstallAppsAsync(List<InstalledApp> apps, Action<string>? log = null);
}
