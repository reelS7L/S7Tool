using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace S7Tool.DiskManagerPE;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            HandleFatal(args.ExceptionObject as Exception, "AppDomain.UnhandledException");
        DispatcherUnhandledException += (_, args) =>
        {
            HandleFatal(args.Exception, "DispatcherUnhandledException");
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLogger.Log(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
        };

        try
        {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            RunWpeInitIfPresent();

            new MainWindow().Show();
        }
        catch (Exception ex)
        {
            HandleFatal(ex, "OnStartup");
        }
    }

    private static void HandleFatal(Exception? ex, string source)
    {
        string logPath = CrashLogger.Log(ex, source);
        MessageBox.Show(
            $"Erreur au démarrage du gestionnaire de disques hors ligne :\n\n{ex}\n\nDétails enregistrés dans : {logPath}",
            "Erreur fatale", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void RunWpeInitIfPresent()
    {
        string wpeinitPath = Path.Combine(Environment.SystemDirectory, "wpeinit.exe");
        if (!File.Exists(wpeinitPath)) return;

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = wpeinitPath,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            CrashLogger.Log(ex, "RunWpeInitIfPresent");
        }
    }
}
