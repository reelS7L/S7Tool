using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Tool.Services;
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
    private string statusText = "";

    public DiskManagerViewModel(IDiskManagerService diskManager, IDialogService dialogService)
    {
        _diskManager = diskManager;
        _dialogService = dialogService;

        statusText = LocalizationManager.T("Str_Common_Ready");

        _ = RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        IsOfflineEnvironmentReady = _diskManager.IsOfflineEnvironmentReady;
        IsOfflineEnvironmentUpdateAvailable = await _diskManager.IsOfflineEnvironmentUpdateAvailableAsync();

        StatusText = IsOfflineEnvironmentReady
            ? LocalizationManager.T(IsOfflineEnvironmentUpdateAvailable ? "Str_DiskMgr_UpdateAvailable" : "Str_DiskMgr_EnvReady")
            : LocalizationManager.T("Str_DiskMgr_EnvNotPrepared");
    }

    private bool CanUseOfflineEnvironment() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanUseOfflineEnvironment))]
    private async Task PrepareOfflineEnvironmentAsync()
    {
        var confirmed = _dialogService.Confirm(
            IsOfflineEnvironmentReady
                ? LocalizationManager.T("Str_DiskMgr_ConfirmUpdate")
                : LocalizationManager.T("Str_DiskMgr_ConfirmPrepare"),
            LocalizationManager.T("Str_DiskMgr_PrepareTitle"));

        if (!confirmed) return;

        OfflineEnvironmentLogs.Clear();
        IsBusy = true;
        StatusText = LocalizationManager.T("Str_DiskMgr_Preparing");
        _offlineEnvironmentCts = new CancellationTokenSource();

        try
        {
            await _diskManager.PrepareOfflineEnvironmentAsync(force: true,
                line => System.Windows.Application.Current.Dispatcher.Invoke(() => OfflineEnvironmentLogs.Add(line)),
                _offlineEnvironmentCts.Token);

            await RefreshStatusAsync();
            _dialogService.ShowSuccess(LocalizationManager.T("Str_DiskMgr_EnvReady"));
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationManager.T("Str_DiskMgr_PreparationInterrupted");
        }
        catch (Exception ex)
        {
            StatusText = LocalizationManager.T("Str_DiskMgr_PreparationFailed");
            _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_Dialog_Error"));
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
            _dialogService.ShowWarning(LocalizationManager.T("Str_DiskMgr_PrepareFirst"));
            return;
        }

        var confirmed = _dialogService.Confirm(
            LocalizationManager.T("Str_DiskMgr_ConfirmLaunch"),
            LocalizationManager.T("Str_DiskMgr_LaunchTitle"));

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
            _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_Dialog_Error"));
        }
        finally
        {
            IsBusy = false;
        }
    }
}
