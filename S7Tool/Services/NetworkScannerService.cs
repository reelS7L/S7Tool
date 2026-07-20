using S7Tool.Models;
using S7Tool.Services.Interfaces;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace S7Tool.Services;

public class NetworkScannerService : INetworkScannerService
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(int destIp, int srcIp, byte[] macAddr, ref int macAddrLen);

    public (string LocalIp, string SubnetStart, string SubnetEnd) DetectLocalSubnet()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (addr.IPv4Mask == null) continue;

                var network = CombineAddress(addr.Address, addr.IPv4Mask, and: true);
                var broadcast = CombineAddress(addr.Address, addr.IPv4Mask, and: false);

                var startBytes = network.GetAddressBytes();
                startBytes[3]++;
                var endBytes = broadcast.GetAddressBytes();
                endBytes[3]--;

                return (addr.Address.ToString(), new IPAddress(startBytes).ToString(), new IPAddress(endBytes).ToString());
            }
        }

        return ("127.0.0.1", "192.168.1.1", "192.168.1.254");
    }

    private static IPAddress CombineAddress(IPAddress address, IPAddress mask, bool and)
    {
        var addrBytes = address.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        var result = new byte[addrBytes.Length];

        for (int i = 0; i < result.Length; i++)
            result[i] = and
                ? (byte)(addrBytes[i] & maskBytes[i])
                : (byte)(addrBytes[i] | (maskBytes[i] ^ 0xFF));

        return new IPAddress(result);
    }

    public async Task ScanRangeAsync(
        string startIp,
        string endIp,
        Action<NetworkHost> onHostFound,
        IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        uint start = ToUint(IPAddress.Parse(startIp).GetAddressBytes());
        uint end = ToUint(IPAddress.Parse(endIp).GetAddressBytes());
        if (end < start) (start, end) = (end, start);

        int total = (int)(end - start + 1);
        int completed = 0;
        using var throttle = new SemaphoreSlim(64);
        var tasks = new List<Task>();

        for (uint current = start; current <= end; current++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ip = ToIp(current);

            await throttle.WaitAsync(cancellationToken);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await ProbeHostAsync(ip, onHostFound, cancellationToken);
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

    private static async Task ProbeHostAsync(string ip, Action<NetworkHost> onHostFound, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, 800);

            if (reply.Status != IPStatus.Success)
                return;

            var host = new NetworkHost { IpAddress = ip, ResponseTimeMs = reply.RoundtripTime };
            host.MacAddress = GetMacAddress(ip);

            onHostFound(host);

            string resolvedName;
            try
            {
                var entryTask = Dns.GetHostEntryAsync(ip);
                var winner = await Task.WhenAny(entryTask, Task.Delay(700, cancellationToken));
                resolvedName = winner == entryTask && entryTask.IsCompletedSuccessfully
                    ? entryTask.Result.HostName
                    : "(inconnu)";
            }
            catch
            {
                resolvedName = "(inconnu)";
            }

            var openPorts = await ProbeQuickPortsAsync(ip);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                host.Hostname = resolvedName;
                host.HasHttp = openPorts.Contains(80);
                host.HasHttps = openPorts.Contains(443);
                host.OpenPortsSummary = openPorts.Count == 0
                    ? ""
                    : string.Join(", ", openPorts.Select(p => QuickPortNames.TryGetValue(p, out var name) ? name : p.ToString()));
            });
        }
        catch
        {
        }
    }

    private static readonly int[] QuickPorts = { 80, 443, 21, 22, 445, 3389 };

    private static readonly Dictionary<int, string> QuickPortNames = new()
    {
        [80] = "HTTP", [443] = "HTTPS", [21] = "FTP", [22] = "SSH", [445] = "SMB", [3389] = "RDP"
    };

    private static async Task<List<int>> ProbeQuickPortsAsync(string ip)
    {
        var open = new List<int>();

        var tasks = QuickPorts.Select(async port =>
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(ip, port);
                var winner = await Task.WhenAny(connectTask, Task.Delay(400));

                if (winner == connectTask && client.Connected)
                {
                    lock (open) { open.Add(port); }
                }
            }
            catch
            {
            }
        });

        await Task.WhenAll(tasks);
        return open;
    }

    private static string GetMacAddress(string ip)
    {
        try
        {
            var addr = BitConverter.ToInt32(IPAddress.Parse(ip).GetAddressBytes(), 0);
            var macBytes = new byte[6];
            int len = macBytes.Length;

            int result = SendARP(addr, 0, macBytes, ref len);
            if (result != 0 || len == 0) return "";

            return string.Join(":", macBytes.Take(len).Select(b => b.ToString("X2")));
        }
        catch
        {
            return "";
        }
    }

    private static uint ToUint(byte[] bytes) =>
        (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);

    private static string ToIp(uint value) => new IPAddress(new[]
    {
        (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value
    }).ToString();

    public async Task SendWakeOnLanAsync(string macAddress)
    {
        var macBytes = macAddress
            .Split(new[] { ':', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(h => Convert.ToByte(h, 16))
            .ToArray();

        if (macBytes.Length != 6)
            throw new ArgumentException("Adresse MAC invalide.", nameof(macAddress));

        var packet = new byte[6 + 16 * macBytes.Length];
        for (int i = 0; i < 6; i++) packet[i] = 0xFF;
        for (int i = 0; i < 16; i++) macBytes.CopyTo(packet, 6 + i * macBytes.Length);

        using var client = new UdpClient();
        client.EnableBroadcast = true;
        await client.SendAsync(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 9));
    }
}
