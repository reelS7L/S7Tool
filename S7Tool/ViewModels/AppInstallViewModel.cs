using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Tool.Models;
using S7Tool.Services.Interfaces;
using System.Collections.ObjectModel;

namespace S7Tool.ViewModels;

public partial class AppInstallViewModel : ObservableObject
{
    private readonly IAppInstallService _installService;
    private readonly IDialogService _dialogService;

    public ObservableCollection<WingetPackage> PopularApps { get; } = new();
    public ObservableCollection<WingetPackage> SearchResults { get; } = new();
    public ObservableCollection<string> Logs { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private string searchText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    private bool isSearching;

    [ObservableProperty]
    private string installButtonLabel = "Installer (0)";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallSelectedCommand))]
    private bool isBusy;

    public AppInstallViewModel(IAppInstallService installService, IDialogService dialogService)
    {
        _installService = installService;
        _dialogService = dialogService;

        foreach (var app in _installService.GetPopularApps())
            PopularApps.Add(app);
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        IsSearching = true;
        SearchResults.Clear();

        var results = await _installService.SearchAsync(SearchText);
        foreach (var app in results)
            SearchResults.Add(app);

        if (results.Count == 0)
            AddLog($"Aucun résultat pour \"{SearchText}\"");

        UpdateButtonLabel();
        IsSearching = false;
    }

    private bool CanSearch() => !IsSearching && !string.IsNullOrWhiteSpace(SearchText);

    [RelayCommand]
    private void ToggleSelection(WingetPackage app)
    {
        app.IsSelected = !app.IsSelected;
        UpdateButtonLabel();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var app in PopularApps) app.IsSelected = false;
        foreach (var app in SearchResults) app.IsSelected = false;
        UpdateButtonLabel();
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallSelectedAsync()
    {
        var selected = PopularApps.Concat(SearchResults)
            .Where(a => a.IsSelected)
            .GroupBy(a => a.Id)
            .Select(g => g.First())
            .ToList();

        if (selected.Count == 0)
        {
            _dialogService.ShowWarning("Aucune sélection");
            return;
        }

        IsBusy = true;
        AddLog($"Installation de {selected.Count} application(s)...");

        await _installService.InstallAppsAsync(selected, AddLog);

        AddLog("Terminé");
        ClearSelection();
        IsBusy = false;
    }

    private bool CanInstall() => !IsBusy;

    private void UpdateButtonLabel()
    {
        int count = PopularApps.Count(a => a.IsSelected) + SearchResults.Count(a => a.IsSelected);
        InstallButtonLabel = $"Installer ({count})";
    }

    private void AddLog(string message) => Logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
}
