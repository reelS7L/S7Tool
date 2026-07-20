using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Tool.Models;
using S7Tool.Services;
using S7Tool.Services.Interfaces;
using S7Tool.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace S7Tool.ViewModels;

public partial class WindowsUpdateViewModel : ObservableObject
{
    private readonly IWindowsUpdateService _windowsUpdateService;
    private readonly IDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;

    private bool _importantAllSelected;
    private bool _optionalAllSelected;

    public ObservableCollection<UpdateItem> ImportantUpdates { get; } = new();
    public ObservableCollection<UpdateItem> OptionalUpdates { get; } = new();

    [ObservableProperty]
    private bool isMicrosoftUpdateEnabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchUpdatesCommand))]
    private bool isBusy;

    public WindowsUpdateViewModel(IWindowsUpdateService windowsUpdateService, IDialogService dialogService, IServiceProvider serviceProvider)
    {
        _windowsUpdateService = windowsUpdateService;
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;

        _ = LoadMicrosoftUpdateStateAsync();
    }

    private async Task LoadMicrosoftUpdateStateAsync()
    {
        try
        {
            IsMicrosoftUpdateEnabled = await _windowsUpdateService.IsMicrosoftUpdateEnabledAsync();
        }
        catch
        {
            IsMicrosoftUpdateEnabled = false;
        }
    }

    [RelayCommand]
    private async Task ToggleMicrosoftUpdateAsync()
    {
        try
        {
            bool target = !IsMicrosoftUpdateEnabled;
            await _windowsUpdateService.SetMicrosoftUpdateEnabledAsync(target);
            IsMicrosoftUpdateEnabled = target;
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_WinUpdate_ToggleErrorTitle"));
        }
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchUpdatesAsync()
    {
        IsBusy = true;
        ImportantUpdates.Clear();
        OptionalUpdates.Clear();

        try
        {
            var updates = await _windowsUpdateService.SearchUpdatesAsync();

            if (updates.Count == 0)
            {
                _dialogService.ShowInfo(LocalizationManager.T("Str_WinUpdate_NoneFound"));
                return;
            }

            foreach (var update in updates)
            {
                if (update.IsImportant)
                    ImportantUpdates.Add(update);
                else
                    OptionalUpdates.Add(update);
            }

            _dialogService.ShowSuccess(string.Format(LocalizationManager.T("Str_WinUpdate_FoundCount"), updates.Count));
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_WinUpdate_SearchErrorTitle"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSearch() => !IsBusy;

    [RelayCommand]
    private async Task InstallUpdatesAsync()
    {
        var selected = ImportantUpdates.Concat(OptionalUpdates).Where(u => u.IsSelected).ToList();

        if (selected.Count == 0)
        {
            _dialogService.ShowWarning(LocalizationManager.T("Str_WinUpdate_NoneSelected"));
            return;
        }

        var progressWindow = _serviceProvider.GetRequiredService<ProgressWindow>();
        var progressViewModel = (ProgressViewModel)progressWindow.DataContext;
        progressWindow.Show();

        try
        {
            await _windowsUpdateService.InstallUpdatesAsync(selected, progressViewModel);
            await Task.Delay(600);
            progressWindow.Close();
            _dialogService.ShowSuccess(LocalizationManager.T("Str_WinUpdate_InstallDone"));
        }
        catch (Exception ex)
        {
            progressWindow.Close();
            _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_WinUpdate_InstallErrorTitle"));
        }
    }

    [RelayCommand]
    private void SelectAllImportant()
    {
        _importantAllSelected = !_importantAllSelected;
        foreach (var update in ImportantUpdates)
            update.IsSelected = _importantAllSelected;
    }

    [RelayCommand]
    private void SelectAllOptional()
    {
        _optionalAllSelected = !_optionalAllSelected;
        foreach (var update in OptionalUpdates)
            update.IsSelected = _optionalAllSelected;
    }
}
