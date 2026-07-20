using System.Diagnostics;
using System.IO;
using System.Text;

namespace S7Tool.DiskManagerPE;

public static class DiskpartRunner
{
    public static async Task<string> RunAsync(string script, CancellationToken cancellationToken = default)
    {
        string scriptPath = Path.Combine(Path.GetTempPath(), $"s7tool_diskpart_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(scriptPath, script, Encoding.ASCII, cancellationToken);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "diskpart.exe",
                Arguments = $"/s \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Impossible de démarrer diskpart.");

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync(cancellationToken);

            string output = outputTask.Result;
            string error = errorTask.Result;

            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    $"diskpart a échoué (code {process.ExitCode}) :\n{output}\n{error}".Trim());

            return output;
        }
        finally
        {
            try { File.Delete(scriptPath); } catch { }
        }
    }

    public static async Task<string> RunLabelAsync(string driveLetter, string newLabel, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "label.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add($"{driveLetter.TrimEnd(':')}:");
        psi.ArgumentList.Add(newLabel);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Impossible de démarrer label.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await Task.WhenAll(outputTask, errorTask);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Le renommage a échoué (code {process.ExitCode}) :\n{outputTask.Result}\n{errorTask.Result}".Trim());

        return outputTask.Result;
    }
}
