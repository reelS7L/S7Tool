using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace S7Tool.Models;

public partial class InstalledApp : ObservableObject
{
    public string DisplayName { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string DisplayVersion { get; set; } = string.Empty;

    public string UninstallString { get; set; } = string.Empty;
    public string QuietUninstallString { get; set; } = string.Empty;

    public string DisplayIcon { get; set; } = string.Empty;

    public string? ProductCode { get; set; }
    public ImageSource? Icon { get; set; }

    [ObservableProperty]
    private bool isSelected;
}
