using S7Tool.Services.Interfaces;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;

namespace S7Tool.Services;

public class DiskManagerService : IDiskManagerService
{
    private const string OfflineRootDir = @"C:\S7Tool\WinPE";
    private const string GitHubRepo = "reelS7L/S7Tool";

    private static string BundledResourcesDir => Path.Combine(AppContext.BaseDirectory, "Resources", "WinPE");

    public bool IsOfflineEnvironmentReady =>
        File.Exists(Path.Combine(OfflineRootDir, "boot.wim")) && File.Exists(Path.Combine(OfflineRootDir, "boot.sdi"));

    public async Task<bool> IsOfflineEnvironmentUpdateAvailableAsync()
    {
        if (!IsOfflineEnvironmentReady) return false;

        string? availableVersion = await TryReadBundledOrRemoteVersionAsync();
        if (availableVersion is null) return false;

        string installedVersionPath = Path.Combine(OfflineRootDir, "version.txt");
        if (!File.Exists(installedVersionPath))
            return true;

        string installedVersion = (await File.ReadAllTextAsync(installedVersionPath)).Trim();
        return !string.Equals(installedVersion, availableVersion, StringComparison.Ordinal);
    }

    private static async Task<string?> TryReadBundledOrRemoteVersionAsync()
    {
        string bundledVersionPath = Path.Combine(BundledResourcesDir, "version.txt");
        if (File.Exists(bundledVersionPath))
            return (await File.ReadAllTextAsync(bundledVersionPath)).Trim();

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("S7Tool");
            string url = $"https://raw.githubusercontent.com/{GitHubRepo}/master/S7Tool/Resources/WinPE/version.txt";
            return (await http.GetStringAsync(url)).Trim();
        }
        catch
        {
            return null;
        }
    }

    public async Task PrepareOfflineEnvironmentAsync(bool force, Action<string> onLog, CancellationToken cancellationToken)
    {
        if (!force && IsOfflineEnvironmentReady && !await IsOfflineEnvironmentUpdateAvailableAsync())
        {
            onLog(LocalizationManager.T("Str_DiskMgr_AlreadyReady"));
            return;
        }

        Directory.CreateDirectory(OfflineRootDir);

        string bundledWimPath = Path.Combine(BundledResourcesDir, "boot.wim");
        string bundledSdiPath = Path.Combine(BundledResourcesDir, "boot.sdi");

        if (File.Exists(bundledWimPath) && File.Exists(bundledSdiPath))
        {
            onLog(LocalizationManager.T("Str_DiskMgr_CopyingImage"));
            File.Copy(bundledWimPath, Path.Combine(OfflineRootDir, "boot.wim"), overwrite: true);
            File.Copy(bundledSdiPath, Path.Combine(OfflineRootDir, "boot.sdi"), overwrite: true);

            string bundledVersionPath = Path.Combine(BundledResourcesDir, "version.txt");
            if (File.Exists(bundledVersionPath))
                File.Copy(bundledVersionPath, Path.Combine(OfflineRootDir, "version.txt"), overwrite: true);
        }
        else
        {
            onLog(LocalizationManager.T("Str_DiskMgr_ImageNotFoundDownloading"));
            await DownloadOfflineEnvironmentAsync(onLog, cancellationToken);
        }

        onLog(string.Format(LocalizationManager.T("Str_DiskMgr_EnvReadyAt"), OfflineRootDir));
    }

    private static async Task DownloadOfflineEnvironmentAsync(Action<string> onLog, CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("S7Tool");

        foreach (var (fileName, urlSuffix) in new[]
        {
            ("boot.wim", "boot.wim"),
            ("boot.sdi", "boot.sdi"),
            ("version.txt", "version.txt")
        })
        {
            string url = $"https://github.com/{GitHubRepo}/releases/latest/download/winpe-{urlSuffix}";
            onLog(string.Format(LocalizationManager.T("Str_DiskMgr_Downloading"), fileName));

            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (fileName == "version.txt") continue;
                throw new InvalidOperationException(
                    string.Format(LocalizationManager.T("Str_DiskMgr_DownloadFailed"), fileName, (int)response.StatusCode));
            }

            await using var fileStream = File.Create(Path.Combine(OfflineRootDir, fileName));
            await response.Content.CopyToAsync(fileStream, cancellationToken);
        }
    }

    public async Task LaunchOfflineDiskManagerAsync(Action<string> onLog, CancellationToken cancellationToken)
    {
        if (!IsOfflineEnvironmentReady)
            throw new InvalidOperationException(LocalizationManager.T("Str_DiskMgr_NotPreparedYet"));

        string script = @"
$ErrorActionPreference = 'Stop'

bcdedit /create ""{ramdiskoptions}"" /d ""S7Tool Ramdisk Options"" | Out-Null
bcdedit /set ""{ramdiskoptions}"" ramdisksdidevice partition=C: | Out-Null
bcdedit /set ""{ramdiskoptions}"" ramdisksdipath \S7Tool\WinPE\boot.sdi | Out-Null

$createOutput = bcdedit /create /d ""S7Tool Gestionnaire de disques hors ligne"" /application osloader
$guid = ($createOutput | Select-String -Pattern '\{[0-9a-fA-F-]+\}').Matches[0].Value
if (-not $guid) { throw ""Impossible de creer l'entree de demarrage (bcdedit)."" }

bcdedit /set $guid device ""ramdisk=[C:]\S7Tool\WinPE\boot.wim,{ramdiskoptions}"" | Out-Null
bcdedit /set $guid osdevice ""ramdisk=[C:]\S7Tool\WinPE\boot.wim,{ramdiskoptions}"" | Out-Null
bcdedit /set $guid path \windows\system32\winload.efi | Out-Null
bcdedit /set $guid systemroot \windows | Out-Null
bcdedit /set $guid winpe yes | Out-Null
bcdedit /set $guid detecthal yes | Out-Null
bcdedit /bootsequence $guid | Out-Null

Write-Output ""Entree de demarrage a usage unique creee ($guid), redemarrage dans 5 secondes...""
shutdown /r /t 5
";

        await RunPowerShellLiveAsync(script, onLog, cancellationToken);
    }

    private static async Task RunPowerShellLiveAsync(string script, Action<string> onLog, CancellationToken cancellationToken)
    {
        string scriptPath = Path.Combine(Path.GetTempPath(), $"s7tool_disk_{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(scriptPath, script, Encoding.UTF8, cancellationToken);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            string? errorLine = null;

            process.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) onLog(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) { errorLine = e.Data; onLog(string.Format(LocalizationManager.T("Str_DiskMgr_ErrorLogPrefix"), e.Data)); } };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var reg = cancellationToken.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            });

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
                throw new InvalidOperationException(errorLine ?? string.Format(LocalizationManager.T("Str_DiskMgr_PsCommandFailed"), process.ExitCode));
        }
        finally
        {
            try { File.Delete(scriptPath); } catch { }
        }
    }
}
