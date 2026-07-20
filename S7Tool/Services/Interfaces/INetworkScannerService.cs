using S7Tool.Models;

namespace S7Tool.Services.Interfaces;

public interface INetworkScannerService
{
    (string LocalIp, string SubnetStart, string SubnetEnd) DetectLocalSubnet();

    Task ScanRangeAsync(
        string startIp,
        string endIp,
        Action<NetworkHost> onHostFound,
        IProgress<int> progress,
        CancellationToken cancellationToken);

    Task SendWakeOnLanAsync(string macAddress);
}
