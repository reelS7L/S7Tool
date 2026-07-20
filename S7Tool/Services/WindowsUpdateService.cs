using S7Tool.Models;
using S7Tool.Services.Interfaces;
using WUApiLib;

namespace S7Tool.Services;

public class WindowsUpdateService : IWindowsUpdateService
{
    private const string MicrosoftUpdateServiceId = "7971f918-a847-4430-9279-4a52d1efe18d";

    public Task<bool> IsMicrosoftUpdateEnabledAsync() => Task.Run(() =>
    {
        var manager = new UpdateServiceManager();
        return manager.Services.Cast<IUpdateService>().Any(s => s.ServiceID == MicrosoftUpdateServiceId);
    });

    public Task SetMicrosoftUpdateEnabledAsync(bool enabled) => Task.Run(() =>
    {
        var manager = new UpdateServiceManager();

        if (enabled)
        {
            manager.AddService2(MicrosoftUpdateServiceId, 7, "");
            return;
        }

        foreach (IUpdateService service in manager.Services)
        {
            if (service.ServiceID == MicrosoftUpdateServiceId)
            {
                manager.RemoveService(service.ServiceID);
                break;
            }
        }
    });

    public Task<IReadOnlyList<UpdateItem>> SearchUpdatesAsync() => RunOnStaThread(() =>
    {
        var session = new UpdateSession();
        var searcher = session.CreateUpdateSearcher();
        var result = searcher.Search("IsInstalled=0 and IsHidden=0");

        var items = new List<UpdateItem>();
        var seen = new HashSet<string>();

        if (result.Updates != null)
        {
            foreach (IUpdate update in result.Updates)
            {
                if (string.IsNullOrWhiteSpace(update.Title) || !seen.Add(update.Title))
                    continue;

                items.Add(new UpdateItem
                {
                    Title = update.Title,
                    IsImportant = update.AutoSelectOnWebSites || update.IsMandatory,
                    NativeUpdate = update
                });
            }
        }

        return (IReadOnlyList<UpdateItem>)items;
    });

    public Task InstallUpdatesAsync(IReadOnlyList<UpdateItem> updates, IProgress<(int Percent, string Status)> progress) => Task.Run(() =>
    {
        var session = new UpdateSession();
        var collection = new UpdateCollection();

        foreach (var item in updates)
            collection.Add((IUpdate)item.NativeUpdate);

        progress.Report((10, LocalizationManager.T("Str_WinUpdate_Downloading")));
        var downloader = session.CreateUpdateDownloader();
        downloader.Updates = collection;
        downloader.Download();

        progress.Report((60, LocalizationManager.T("Str_WinUpdate_Installing")));
        var installer = session.CreateUpdateInstaller();
        installer.Updates = collection;
        installer.Install();

        progress.Report((100, LocalizationManager.T("Str_Common_Done")));
    });

    private static Task<T> RunOnStaThread<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try { tcs.SetResult(action()); }
            catch (Exception ex) { tcs.SetException(ex); }
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tcs.Task;
    }
}
