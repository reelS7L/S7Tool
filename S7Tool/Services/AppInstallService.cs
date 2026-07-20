using S7Tool.Models;
using S7Tool.Services.Interfaces;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace S7Tool.Services;

public class AppInstallService : IAppInstallService
{
    private static readonly (string Id, string Name)[] PopularCatalog =
    {
        ("Google.Chrome", "Google Chrome"),
        ("Mozilla.Firefox", "Mozilla Firefox"),
        ("7zip.7zip", "7-Zip"),
        ("VideoLAN.VLC", "VLC media player"),
        ("Notepad++.Notepad++", "Notepad++"),
        ("Discord.Discord", "Discord"),
        ("Spotify.Spotify", "Spotify"),
        ("Zoom.Zoom", "Zoom"),
        ("Adobe.Acrobat.Reader.64-bit", "Adobe Acrobat Reader"),
        ("WinRAR.WinRAR", "WinRAR")
    };

    public List<WingetPackage> GetPopularApps() =>
        PopularCatalog
            .Select(p => new WingetPackage { Id = p.Id, Name = p.Name, Source = "winget" })
            .ToList();

    public async Task<List<WingetPackage>> SearchAsync(string query)
    {
        return await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ResolveWingetExecutable(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                psi.ArgumentList.Add("search");
                psi.ArgumentList.Add(query);
                psi.ArgumentList.Add("--accept-source-agreements");
                psi.ArgumentList.Add("--disable-interactivity");

                using var process = Process.Start(psi);
                if (process == null)
                    return new List<WingetPackage>();

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return ParseSearchOutput(output);
            }
            catch
            {
                return new List<WingetPackage>();
            }
        });
    }

    public async Task InstallAppsAsync(List<WingetPackage> apps, Action<string>? log = null)
    {
        foreach (var app in apps)
        {
            log?.Invoke($"Installation : {app.Name}");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ResolveWingetExecutable(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                psi.ArgumentList.Add("install");
                psi.ArgumentList.Add("--id");
                psi.ArgumentList.Add(app.Id);
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add("--silent");
                psi.ArgumentList.Add("--accept-package-agreements");
                psi.ArgumentList.Add("--accept-source-agreements");
                psi.ArgumentList.Add("--disable-interactivity");

                using var process = Process.Start(psi);

                if (process == null)
                {
                    log?.Invoke($"{app.Name} : winget introuvable");
                    continue;
                }

                string stdout = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    log?.Invoke($"{app.Name} : installé avec succès");
                }
                else
                {
                    log?.Invoke($"{app.Name} : échec (code {process.ExitCode})");

                    string lastLine = stdout
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .LastOrDefault() ?? "";

                    if (!string.IsNullOrWhiteSpace(lastLine))
                        log?.Invoke(lastLine);
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Erreur - {app.Name} : {ex.Message}");
            }
        }
    }

    private static string ResolveWingetExecutable()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string aliasPath = Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe");
        return File.Exists(aliasPath) ? aliasPath : "winget";
    }

    private static List<WingetPackage> ParseSearchOutput(string output)
    {
        var result = new List<WingetPackage>();
        var lines = output.Replace("\r", "").Split('\n');

        int headerIndex = -1;
        for (int i = 0; i < lines.Length - 1; i++)
        {
            if (Regex.IsMatch(lines[i + 1], @"^-{5,}$"))
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex == -1)
            return result;

        var columnStarts = Regex.Matches(lines[headerIndex], @"\S+").Select(m => m.Index).ToList();
        if (columnStarts.Count < 3)
            return result;

        for (int i = headerIndex + 2; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                break;

            var values = new List<string>();

            for (int c = 0; c < columnStarts.Count; c++)
            {
                int start = columnStarts[c];
                if (start >= line.Length)
                {
                    values.Add("");
                    continue;
                }

                int end = c + 1 < columnStarts.Count ? Math.Min(columnStarts[c + 1], line.Length) : line.Length;
                values.Add(line[start..end].Trim());
            }

            if (string.IsNullOrWhiteSpace(values[0]) || string.IsNullOrWhiteSpace(values[1]))
                continue;

            result.Add(new WingetPackage
            {
                Name = values[0],
                Id = values[1],
                Version = values.Count > 2 ? values[2] : "",
                Source = values[^1]
            });
        }

        return result;
    }
}
