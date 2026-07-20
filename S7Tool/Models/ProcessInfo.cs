using CommunityToolkit.Mvvm.ComponentModel;

namespace S7Tool.Models;

public partial class ProcessInfo : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public int Id { get; set; }

    [ObservableProperty] private double memory;
    [ObservableProperty] private double cpu;
    [ObservableProperty] private double disk;
}
