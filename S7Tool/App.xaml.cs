using S7Tool.Services;
using S7Tool.Services.Interfaces;
using S7Tool.ViewModels;
using S7Tool.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace S7Tool;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (s, ev) =>
        {
            MessageBox.Show(ev.Exception.ToString(), "CRASH GLOBAL");
            ev.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
        {
            MessageBox.Show(ev.ExceptionObject.ToString(), "CRASH DOMAIN");
        };

        base.OnStartup(e);

        Services = ConfigureServices();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ISecretsProvider, SecretsProvider>();
        services.AddSingleton<IGeminiChatService, GeminiChatService>();
        services.AddSingleton<IAppUninstallService, AppUninstallService>();
        services.AddSingleton<IAppInstallService, AppInstallService>();
        services.AddSingleton<IProcessMonitorService, ProcessMonitorService>();
        services.AddSingleton<IWindowsUpdateService, WindowsUpdateService>();
        services.AddSingleton<INetworkScannerService, NetworkScannerService>();
        services.AddSingleton<IPortScannerService, PortScannerService>();
        services.AddSingleton<IDiskManagerService, DiskManagerService>();
        services.AddSingleton<IDiskHealthService, DiskHealthService>();
        services.AddSingleton<IDiskSpaceAnalyzerService, DiskSpaceAnalyzerService>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<UninstallViewModel>();
        services.AddTransient<AppInstallViewModel>();
        services.AddTransient<ProgressViewModel>();
        services.AddTransient<RenamePCViewModel>();
        services.AddTransient<WindowsUpdateViewModel>();
        services.AddTransient<AiChatViewModel>();
        services.AddTransient<AiSettingsViewModel>();
        services.AddTransient<NetworkScannerViewModel>();
        services.AddTransient<PortScannerViewModel>();
        services.AddTransient<DiskManagerViewModel>();
        services.AddTransient<DiskHealthViewModel>();
        services.AddTransient<DiskSpaceAnalyzerViewModel>();

        services.AddTransient<MainWindow>();
        services.AddTransient<UninstallWindow>();
        services.AddTransient<AppInstallWindow>();
        services.AddTransient<ProgressWindow>();
        services.AddTransient<RenamePCWindow>();
        services.AddTransient<WindowsUpdateWindow>();
        services.AddTransient<AiChatWindow>();
        services.AddTransient<AiSettingsWindow>();
        services.AddTransient<NetworkScannerWindow>();
        services.AddTransient<PortScannerWindow>();
        services.AddTransient<DiskManagerWindow>();
        services.AddTransient<DiskHealthWindow>();
        services.AddTransient<DiskSpaceAnalyzerWindow>();

        return services.BuildServiceProvider();
    }
}
