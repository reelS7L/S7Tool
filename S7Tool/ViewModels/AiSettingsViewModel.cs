using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Tool.Services.Interfaces;

namespace S7Tool.ViewModels;

public partial class AiSettingsViewModel : ObservableObject
{
    private readonly ISecretsProvider _secretsProvider;
    private readonly IDialogService _dialogService;

    public event EventHandler? CloseRequested;

    [ObservableProperty]
    private string apiKey = "";

    public AiSettingsViewModel(ISecretsProvider secretsProvider, IDialogService dialogService)
    {
        _secretsProvider = secretsProvider;
        _dialogService = dialogService;

        ApiKey = _secretsProvider.GetGeminiApiKey() ?? "";
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            _dialogService.ShowWarning("Entre une clé API valide.");
            return;
        }

        try
        {
            _secretsProvider.SetGeminiApiKey(ApiKey.Trim());
            _dialogService.ShowSuccess("Clé API enregistrée. Elle est immédiatement active.");
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, "Erreur d'enregistrement");
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
