using CommunityToolkit.Mvvm.ComponentModel;

namespace S7Tool.Models;

public partial class NetworkHost : ObservableObject
{
    public required string IpAddress { get; init; }

    [ObservableProperty]
    private string hostname = "(résolution...)";

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
