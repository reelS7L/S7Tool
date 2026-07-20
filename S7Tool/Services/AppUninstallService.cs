using S7Tool.Models;
using S7Tool.Services.Interfaces;
using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace S7Tool.Services;

public class AppUninstallService : IAppUninstallService
{
    public List<InstalledApp> GetInstalledApps()
    {
        var apps = new List<InstalledApp>();

        string[] registryPaths =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        RegistryKey[] roots =
        {
            Registry.LocalMachine,
            Registry.CurrentUser
        };

        foreach (var root in roots)
        {
            foreach (string path in registryPaths)
            {
                using RegistryKey? baseKey = root.OpenSubKey(path);
                if (baseKey == null) continue;

                foreach (string subKeyName in baseKey.GetSubKeyNames())
                {
                    using RegistryKey? subKey = baseKey.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    string displayName = subKey.GetValue("DisplayName") as string ?? "";
                    string uninstallString = subKey.GetValue("UninstallString") as string ?? "";
                    string quietUninstallString = subKey.GetValue("QuietUninstallString") as string ?? "";
                    string publisher = subKey.GetValue("Publisher") as string ?? "";
                    string version = subKey.GetValue("DisplayVersion") as string ?? "";
                    string icon = subKey.GetValue("DisplayIcon") as string ?? "";
                    string? productCode = ExtractProductCode(uninstallString) ?? ExtractProductCode(quietUninstallString);

                    if (!string.IsNullOrWhiteSpace(displayName) &&
                        (!string.IsNullOrWhiteSpace(uninstallString) ||
                         !string.IsNullOrWhiteSpace(quietUninstallString)))
                    {
                        var app = new InstalledApp
                        {
                            DisplayName = displayName,
                            UninstallString = uninstallString,
                            QuietUninstallString = quietUninstallString,
                            Publisher = publisher,
                            DisplayVersion = version,
                            DisplayIcon = icon,
                            ProductCode = productCode
                        };

                        app.Icon = GetIcon(icon);
                        apps.Add(app);
                    }
                }
            }
        }

        return apps
            .GroupBy(a => a.DisplayName)
            .Select(g => g.First())
            .OrderBy(a => a.DisplayName)
            .ToList();
    }

    public async Task UninstallAppsAsync(List<InstalledApp> apps, Action<string>? log = null)
    {
        foreach (var app in apps)
        {
            log?.Invoke($"Désinstallation : {app.DisplayName}");

            try
            {
                int exitCode = await UninstallAppWithExitCodeAsync(app, log);

                if (exitCode == 0)
                {
                    log?.Invoke($"{app.DisplayName} : désinstallé avec succès");
                }
                else
                {
                    log?.Invoke($"{app.DisplayName} : code retour {exitCode}");
                    log?.Invoke("Nouvelle tentative en mode visible...");
                    await RunVisibleUninstallAsync(app);
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Erreur - {app.DisplayName} : {ex.Message}");
            }
        }
    }

    private static async Task<int> UninstallAppWithExitCodeAsync(InstalledApp app, Action<string>? log = null)
    {
        return await Task.Run(() =>
        {
            try
            {
                string? command = BuildSilentCommand(app);

                if (string.IsNullOrWhiteSpace(command))
                {
                    log?.Invoke($"Erreur - {app.DisplayName} : commande introuvable");
                    return -1;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c chcp 65001>nul & " + command,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);

                if (process == null)
                    return -1;

                while (!process.HasExited)
                {
                    KillUninstallerPopups();
                    Thread.Sleep(1000);
                }

                return process.ExitCode;
            }
            catch
            {
                return -1;
            }
        });
    }

    private static string? BuildSilentCommand(InstalledApp app)
    {
        if (!string.IsNullOrWhiteSpace(app.QuietUninstallString))
            return app.QuietUninstallString;

        string command = app.UninstallString;

        if (string.IsNullOrWhiteSpace(command))
            return null;

        string lower = command.ToLowerInvariant();

        if (lower.Contains("msiexec"))
        {
            var match = Regex.Match(command, @"\{[A-F0-9\-]+\}", RegexOptions.IgnoreCase);

            if (match.Success)
                return $"msiexec.exe /x {match.Value} /qn /norestart REBOOT=ReallySuppress";

            return command + " /qn /norestart REBOOT=ReallySuppress";
        }

        if (lower.Contains("/quiet") ||
            lower.Contains("/silent") ||
            lower.Contains("/qn") ||
            lower.Contains("/s"))
        {
            return command;
        }

        return command + " /quiet /silent /s /qn /norestart";
    }

    private static void KillUninstallerPopups()
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (!string.IsNullOrEmpty(process.MainWindowTitle))
                {
                    string title = process.MainWindowTitle.ToLower();

                    if (title.Contains("uninstall") ||
                        title.Contains("désinstaller") ||
                        title.Contains("setup"))
                    {
                        process.Kill();
                    }
                }
            }
            catch { }
        }
    }

    private static async Task<bool> RunVisibleUninstallAsync(InstalledApp app)
    {
        return await Task.Run(() =>
        {
            try
            {
                string? uninstallCommand =
                    !string.IsNullOrWhiteSpace(app.UninstallString)
                    ? app.UninstallString
                    : app.QuietUninstallString;

                if (string.IsNullOrWhiteSpace(uninstallCommand))
                    return false;

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c \"" + uninstallCommand + "\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                using var process = Process.Start(psi);
                process?.WaitForExit();

                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        });
    }

    private static string? ExtractProductCode(string? uninstallString)
    {
        if (string.IsNullOrWhiteSpace(uninstallString))
            return null;

        var match = Regex.Match(uninstallString, @"\{[A-F0-9\-]+\}", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }

    public static ImageSource? GetIcon(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (path.Contains(","))
                path = path.Split(',')[0];

            if (!File.Exists(path))
                return null;

            Icon? icon = Icon.ExtractAssociatedIcon(path);
            if (icon == null) return null;

            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(24, 24));
        }
        catch
        {
            return null;
        }
    }
}
