using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace S7Tool.Models;

public partial class ChatEntry : ObservableObject
{
    public required string Role { get; init; }
    public bool IsUser { get; init; }

    [ObservableProperty]
    private string text = "";

    [ObservableProperty]
    private bool isTyping;

    public IRelayCommand CopyCommand { get; }

    public ChatEntry()
    {
        CopyCommand = new RelayCommand(() => Clipboard.SetText(Text));
    }
}
