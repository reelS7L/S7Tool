using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Tool.Models;
using S7Tool.Services.Interfaces;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

namespace S7Tool.ViewModels;

public partial class UninstallViewModel : ObservableObject
{
    private readonly IAppUninstallService _uninstallService;
    private readonly IDialogService _dialogService;

    public ObservableCollection<InstalledApp> Apps { get; } = new();
    public ObservableCollection<string> Logs { get; } = new();
    public ICollectionView AppsView { get; }

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private string uninstallButtonLabel = "Désinstaller (0)";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UninstallSelectedCommand))]
    private bool isBusy;

    public UninstallViewModel(IAppUninstallService uninstallService, IDialogService dialogService)
    {
        _uninstallService = uninstallService;
        _dialogService = dialogService;

        AppsView = CollectionViewSource.GetDefaultView(Apps);
        AppsView.Filter = FilterApps;

        LoadApps();
    }

    partial void OnSearchTextChanged(string value) => AppsView.Refresh();

    private bool FilterApps(object obj) =>
        obj is InstalledApp app &&
        (app.DisplayName ?? "").Contains(SearchText, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void LoadApps()
    {
        Apps.Clear();
        foreach (var app in _uninstallService.GetInstalledApps())
            Apps.Add(app);

        AddLog($"Chargé : {Apps.Count} logiciels");
        UpdateButtonLabel();
    }

    [RelayCommand]
    private void ToggleSelection(InstalledApp app)
    {
        app.IsSelected = !app.IsSelected;
        UpdateButtonLabel();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var app in Apps) app.IsSelected = true;
        UpdateButtonLabel();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var app in Apps) app.IsSelected = false;
        UpdateButtonLabel();
    }

    [RelayCommand(CanExecute = nameof(CanUninstall))]
    private async Task UninstallSelectedAsync()
    {
        var selected = Apps.Where(a => a.IsSelected).ToList();

        if (selected.Count == 0)
        {
            _dialogService.ShowWarning("Aucune sélection");
            return;
        }

        IsBusy = true;
        AddLog($"Désinstallation de {selected.Count} apps...");

        await _uninstallService.UninstallAppsAsync(selected, AddLog);

        AddLog("Terminé");
        UpdateButtonLabel();
        IsBusy = false;
    }

    private bool CanUninstall() => !IsBusy;

    private void UpdateButtonLabel()
    {
        int count = Apps.Count(a => a.IsSelected);
        UninstallButtonLabel = $"Désinstaller ({count})";
    }

    private void AddLog(string message) => Logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
}
