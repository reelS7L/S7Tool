using CommunityToolkit.Mvvm.ComponentModel;
using S7Tool.Services;

namespace S7Tool.Models;

public partial class NetworkHost : ObservableObject
{
    public required string IpAddress { get; init; }

    [ObservableProperty]
    private string hostname = LocalizationManager.T("Str_NetScan_Resolving");

    [ObservableProperty]
    private string macAddress = "";

    [ObservableProperty]
    private long responseTimeMs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWeb))]
    private bool hasHttp;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWeb))]
    private bool hasHttps;

    [ObservableProperty]
    private string openPortsSummary = "";

    public bool HasWeb => HasHttp || HasHttps;
    public string? WebUrl => HasHttps ? $"https://{IpAddress}" : HasHttp ? $"http://{IpAddress}" : null;
}
