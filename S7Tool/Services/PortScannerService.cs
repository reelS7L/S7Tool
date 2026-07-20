using S7Tool.Models;
using S7Tool.Services.Interfaces;
using System.Net.Sockets;

namespace S7Tool.Services;

public class PortScannerService : IPortScannerService
{
    public static readonly Dictionary<int, string> WellKnownPorts = new()
    {
        [21] = "FTP", [22] = "SSH", [23] = "Telnet", [25] = "SMTP", [53] = "DNS",
        [80] = "HTTP", [110] = "POP3", [111] = "RPC", [135] = "RPC (Windows)", [139] = "NetBIOS",
        [143] = "IMAP", [443] = "HTTPS", [445] = "SMB", [993] = "IMAPS", [995] = "POP3S",
        [1433] = "SQL Server", [1521] = "Oracle", [3306] = "MySQL", [3389] = "RDP (Bureau à distance)",
        [5060] = "SIP", [5432] = "PostgreSQL", [5900] = "VNC", [6379] = "Redis",
        [8080] = "HTTP (alt)", [8443] = "HTTPS (alt)", [27017] = "MongoDB"
    };

    public async Task ScanPortsAsync(
        string host,
        int startPort,
        int endPort,
        Action<PortScanResult> onPortOpen,
        IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        int total = endPort - startPort + 1;
        int completed = 0;
        using var throttle = new SemaphoreSlim(200);
        var tasks = new List<Task>();

        for (int port = startPort; port <= endPort; port++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int currentPort = port;

            await throttle.WaitAsync(cancellationToken);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await ProbePortAsync(host, currentPort, onPortOpen, cancellationToken);
                }
                finally
                {
                    Interlocked.Increment(ref completed);
                    progress.Report((int)(completed * 100.0 / total));
                    throttle.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }

    private static async Task ProbePortAsync(string host, int port, Action<PortScanResult> onPortOpen, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port, cancellationToken).AsTask();
            var winner = await Task.WhenAny(connectTask, Task.Delay(1000, cancellationToken));

            if (winner == connectTask && client.Connected)
            {
                var serviceName = WellKnownPorts.TryGetValue(port, out var name) ? name : "Inconnu";
                onPortOpen(new PortScanResult { Port = port, ServiceName = serviceName });
            }
        }
        catch
        {
        }
    }
}
