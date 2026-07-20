using S7Tool.Models;

namespace S7Tool.Services.Interfaces;

public interface IWindowsUpdateService
{
    Task<bool> IsMicrosoftUpdateEnabledAsync();
    Task SetMicrosoftUpdateEnabledAsync(bool enabled);
    Task<IReadOnlyList<UpdateItem>> SearchUpdatesAsync();
    Task InstallUpdatesAsync(IReadOnlyList<UpdateItem> updates, IProgress<(int Percent, string Status)> progress);
}
