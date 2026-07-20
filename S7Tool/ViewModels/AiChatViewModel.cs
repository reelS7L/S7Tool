using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Tool.Models;
using S7Tool.Services;
using S7Tool.Services.Interfaces;
using S7Tool.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace S7Tool.ViewModels;

public partial class AiChatViewModel : ObservableObject
{
    private readonly IGeminiChatService _gemini;
    private readonly IServiceProvider _serviceProvider;
    private CancellationTokenSource? _cts;

    public ObservableCollection<ChatEntry> Messages { get; } = new();

    [ObservableProperty]
    private string inputText = "";

    [ObservableProperty]
    private string statusText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(RegenerateLastCommand))]
    private bool isSending;

    public bool HasMessages => Messages.Count > 0;

    public AiChatViewModel(IGeminiChatService gemini, IServiceProvider serviceProvider)
    {
        _gemini = gemini;
        _serviceProvider = serviceProvider;
        Messages.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasMessages));
        RefreshStatus();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var window = _serviceProvider.GetRequiredService<AiSettingsWindow>();
        window.Owner = System.Windows.Application.Current.Windows
            .OfType<AiChatWindow>()
            .FirstOrDefault();

        window.ShowDialog();
        RefreshStatus();
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = InputText?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        Messages.Add(new ChatEntry { Role = LocalizationManager.T("Str_AiChat_You"), IsUser = true, Text = text });
        InputText = "";

        var assistantEntry = new ChatEntry { Role = LocalizationManager.T("Str_AiChat_Ai"), IsUser = false, IsTyping = true };
        Messages.Add(assistantEntry);

        await StreamIntoAsync(assistantEntry);
    }

    private bool CanSend() => !IsSending;

    [RelayCommand(CanExecute = nameof(CanRegenerateLast))]
    private async Task RegenerateLastAsync()
    {
        var lastAssistant = Messages.LastOrDefault(m => !m.IsUser);
        if (lastAssistant is null) return;

        int index = Messages.IndexOf(lastAssistant);
        Messages.RemoveAt(index);

        var assistantEntry = new ChatEntry { Role = LocalizationManager.T("Str_AiChat_Ai"), IsUser = false, IsTyping = true };
        Messages.Insert(index, assistantEntry);

        await StreamIntoAsync(assistantEntry);
    }

    private bool CanRegenerateLast() => !IsSending && Messages.Any(m => !m.IsUser);

    private async Task StreamIntoAsync(ChatEntry assistantEntry)
    {
        IsSending = true;
        _cts = new CancellationTokenSource();

        var history = Messages
            .TakeWhile(m => m != assistantEntry)
            .Where(m => !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => (Role: m.IsUser ? "user" : "model", Text: m.Text))
            .ToList();

        try
        {
            await _gemini.StreamMessageAsync(history, partial =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    assistantEntry.IsTyping = false;
                    assistantEntry.Text = partial;
                });
            }, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            assistantEntry.IsTyping = false;
            if (string.IsNullOrEmpty(assistantEntry.Text))
                assistantEntry.Text = LocalizationManager.T("Str_AiChat_ResponseInterrupted");
        }
        finally
        {
            assistantEntry.IsTyping = false;
            _cts?.Dispose();
            _cts = null;
            IsSending = false;
            RefreshStatus();
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _cts?.Cancel();

    private bool CanStop() => IsSending;

    [RelayCommand]
    private void ClearConversation()
    {
        if (IsSending) return;
        Messages.Clear();
        RefreshStatus();
    }

    private void RefreshStatus() => StatusText = _gemini.GetStatus();
}
