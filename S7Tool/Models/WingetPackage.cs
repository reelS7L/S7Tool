using CommunityToolkit.Mvvm.ComponentModel;

namespace S7Tool.Models;

public partial class WingetPackage : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;

    [ObservableProperty]
    private bool isSelected;
}
