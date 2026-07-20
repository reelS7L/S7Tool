using CommunityToolkit.Mvvm.ComponentModel;

namespace S7Tool.Models;

public partial class PortScanResult : ObservableObject
{
    public required int Port { get; init; }
    public required string ServiceName { get; init; }
}
