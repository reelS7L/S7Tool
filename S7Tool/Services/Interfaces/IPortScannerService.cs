using S7Tool.Models;

namespace S7Tool.Services.Interfaces;

public interface IPortScannerService
{
    Task ScanPortsAsync(
        string host,
        int startPort,
        int endPort,
        Action<PortScanResult> onPortOpen,
        IProgress<int> progress,
        CancellationToken cancellationToken);
}
