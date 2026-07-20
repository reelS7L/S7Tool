using S7Tool.Models;

namespace S7Tool.Services.Interfaces;

public interface IAppInstallService
{
    List<WingetPackage> GetPopularApps();
    Task<List<WingetPackage>> SearchAsync(string query);
    Task InstallAppsAsync(List<WingetPackage> apps, Action<string>? log = null);
}
