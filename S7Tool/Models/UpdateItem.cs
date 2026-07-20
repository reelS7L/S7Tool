using CommunityToolkit.Mvvm.ComponentModel;

namespace S7Tool.Models;

public partial class UpdateItem : ObservableObject
{
    public required string Title { get; init; }
    public bool IsImportant { get; init; }
    public required object NativeUpdate { get; init; }

    [ObservableProperty]
    private bool isSelected;
}
