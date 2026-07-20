using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Tool.Services.Interfaces;
using System.Collections.ObjectModel;

namespace S7Tool.ViewModels;

public partial class DiskManagerViewModel : ObservableObject
{
    private readonly IDiskManagerService _diskManager;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _offlineEnvironmentCts;

    public ObservableCollection<string> OfflineEnvironmentLogs { get; } = new();

    [ObservableProperty]
    private bool isOfflineEnvironmentReady;

    [ObservableProperty]
    private bool isOfflineEnvironmentUpdateAvailable;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrepareOfflineEnvironmentCommand))]
    [NotifyCanExecuteChangedFor(nameof(LaunchOfflineDiskManagerCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = "Prêt.";

    public DiskManagerViewModel(IDiskManagerService diskManager, IDialogService dialogService)
    {
        _diskManager = diskManager;
        _dialogService = dialogService;

        _ = RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        IsOfflineEnvironmentReady = _diskManager.IsOfflineEnvironmentReady;
        IsOfflineEnvironmentUpdateAvailable = await _diskManager.IsOfflineEnvironmentUpdateAvailableAsync();

        StatusText = IsOfflineEnvironmentReady
            ? (IsOfflineEnvironmentUpdateAvailable ? "Une mise à jour de l'environnement hors ligne est disponible." : "Environnement hors ligne prêt.")
            : "Environnement hors ligne non préparé.";
    }

    private bool CanUseOfflineEnvironment() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanUseOfflineEnvironment))]
    private async Task PrepareOfflineEnvironmentAsync()
    {
        var confirmed = _dialogService.Confirm(
            IsOfflineEnvironmentReady
                ? "Mettre à jour l'environnement hors ligne (WinPE) sur ce poste ?"
                : "Préparer l'environnement hors ligne (WinPE) sur ce poste ? Rapide (copie ou téléchargement de fichiers), à faire une seule fois par poste.",
            "Préparer l'environnement hors ligne");

        if (!confirmed) return;

        OfflineEnvironmentLogs.Clear();
        IsBusy = true;
        StatusText = "Préparation de l'environnement hors ligne...";
        _offlineEnvironmentCts = new CancellationTokenSource();

        try
        {
            await _diskManager.PrepareOfflineEnvironmentAsync(force: true,
                line => System.Windows.Application.Current.Dispatcher.Invoke(() => OfflineEnvironmentLogs.Add(line)),
                _offlineEnvironmentCts.Token);

            await RefreshStatusAsync();
            _dialogService.ShowSuccess("Environnement hors ligne prêt.");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Préparation de l'environnement hors ligne interrompue.";
        }
        catch (Exception ex)
        {
            StatusText = "Échec de la préparation de l'environnement hors ligne.";
            _dialogService.ShowError(ex.Message, "Erreur");
        }
        finally
        {
            IsBusy = false;
            _offlineEnvironmentCts?.Dispose();
            _offlineEnvironmentCts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseOfflineEnvironment))]
    private async Task LaunchOfflineDiskManagerAsync()
    {
        if (!IsOfflineEnvironmentReady)
        {
            _dialogService.ShowWarning("Prépare d'abord l'environnement hors ligne.");
            return;
        }

        var confirmed = _dialogService.Confirm(
            "Le poste va redémarrer immédiatement vers le gestionnaire de disques hors ligne.\n\n" +
            "Ferme tout travail en cours avant de continuer : ce démarrage est à usage unique, le poste " +
            "revient automatiquement sous Windows une fois le gestionnaire de disques fermé.",
            "Lancer le gestionnaire de disques hors ligne");

        if (!confirmed) return;

        IsBusy = true;

        try
        {
            await _diskManager.LaunchOfflineDiskManagerAsync(
                line => System.Windows.Application.Current.Dispatcher.Invoke(() => OfflineEnvironmentLogs.Add(line)),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, "Erreur");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
