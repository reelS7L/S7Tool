using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Tool.DiskEngine.Models;
using S7Tool.Models;
using S7Tool.Services;
using S7Tool.Services.Interfaces;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace S7Tool.ViewModels;

public partial class DiskHealthViewModel : ObservableObject
{
    private readonly IDiskHealthService _healthService;
    private readonly IDialogService _dialogService;
    private readonly DispatcherTimer _pollTimer;

    public ObservableCollection<DiskHealthRow> Disks { get; } = new();
    public ObservableCollection<SmartAttribute> SelectedAttributes { get; } = new();
    public ObservableCollection<double> SelectedTemperatureHistory { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedAttributesHaveCritical))]
    private DiskHealthRow? selectedDisk;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunShortSelfTestCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunExtendedSelfTestCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = "";

    [ObservableProperty] private int diskCount;
    [ObservableProperty] private double totalCapacityGb;
    [ObservableProperty] private double totalFreeGb;
    [ObservableProperty] private double averageTemperature;
    [ObservableProperty] private int alertCount;

    [ObservableProperty] private double alertMaxTemperature;
    [ObservableProperty] private double alertMinWearPercent;

    public bool SelectedAttributesHaveCritical => SelectedDisk?.Smart.Attributes.Any(a => a.IsCritical) ?? false;

    public DiskHealthViewModel(IDiskHealthService healthService, IDialogService dialogService)
    {
        _healthService = healthService;
        _dialogService = dialogService;

        statusText = LocalizationManager.T("Str_Common_Ready");

        var thresholds = _healthService.GetAlertThresholds();
        alertMaxTemperature = thresholds.MaxTemperatureCelsius;
        alertMinWearPercent = thresholds.MinWearPercentRemaining;

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _pollTimer.Tick += async (_, _) => await RefreshAsync();
        _pollTimer.Start();

        _ = RefreshAsync();
    }

    public void StopPolling() => _pollTimer.Stop();

    private bool CanLoadOrManage() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanLoadOrManage))]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusText = LocalizationManager.T("Str_DiskHealth_ReadingStatus");

        try
        {
            var rows = await _healthService.GetAllAsync();

            int? selectedDiskNumber = SelectedDisk?.DiskNumber;
            Disks.Clear();
            foreach (var row in rows) Disks.Add(row);

            DiskCount = rows.Count;
            TotalCapacityGb = Math.Round(rows.Sum(r => r.SizeGb), 1);
            TotalFreeGb = Math.Round(rows.Sum(r => r.FreeGb ?? 0), 1);
            var temps = rows.Where(r => r.TemperatureCelsius.HasValue).Select(r => r.TemperatureCelsius!.Value).ToList();
            AverageTemperature = temps.Count > 0 ? Math.Round(temps.Average(), 1) : 0;
            AlertCount = rows.Count(r => r.HealthTier is "Prudence" or "Critique");

            await _healthService.AppendHistoryAsync(rows.Select(r => r.Smart));
            CheckAlerts(rows);

            SelectedDisk = rows.FirstOrDefault(r => r.DiskNumber == selectedDiskNumber) ?? rows.FirstOrDefault();

            StatusText = string.Format(LocalizationManager.T("Str_DiskHealth_DisksAnalyzed"), DiskCount);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_DiskHealth_ReadErrorTitle"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CheckAlerts(List<DiskHealthRow> rows)
    {
        foreach (var r in rows)
        {
            bool tempAlert = r.TemperatureCelsius > AlertMaxTemperature;
            bool wearAlert = r.WearPercentUsed.HasValue && r.WearPercentUsed < AlertMinWearPercent;

            if (tempAlert)
                _dialogService.ShowWarning(string.Format(LocalizationManager.T("Str_DiskHealth_TempAlert"), r.DisplayName, r.TemperatureDisplay, AlertMaxTemperature), LocalizationManager.T("Str_DiskHealth_AlertTitle"));
            if (wearAlert)
                _dialogService.ShowWarning(string.Format(LocalizationManager.T("Str_DiskHealth_WearAlert"), r.DisplayName, r.WearDisplay, AlertMinWearPercent), LocalizationManager.T("Str_DiskHealth_AlertTitle"));
        }
    }

    partial void OnSelectedDiskChanged(DiskHealthRow? value)
    {
        SelectedAttributes.Clear();
        if (value is not null)
            foreach (var a in value.Smart.Attributes.OrderByDescending(a => a.IsCritical))
                SelectedAttributes.Add(a);

        OnPropertyChanged(nameof(SelectedAttributesHaveCritical));
        _ = LoadHistoryAsync(value);
    }

    private async Task LoadHistoryAsync(DiskHealthRow? row)
    {
        SelectedTemperatureHistory.Clear();
        if (row is null) return;

        var history = await _healthService.GetHistoryAsync(row.DiskNumber);
        foreach (var point in history.Where(p => p.TemperatureCelsius.HasValue))
            SelectedTemperatureHistory.Add(point.TemperatureCelsius!.Value);
    }

    [RelayCommand(CanExecute = nameof(CanLoadOrManage))]
    private async Task RunShortSelfTestAsync() => await RunSelfTestAsync(extended: false);

    [RelayCommand(CanExecute = nameof(CanLoadOrManage))]
    private async Task RunExtendedSelfTestAsync() => await RunSelfTestAsync(extended: true);

    private async Task RunSelfTestAsync(bool extended)
    {
        if (SelectedDisk is null) { _dialogService.ShowWarning(LocalizationManager.T("Str_DiskHealth_SelectDisk")); return; }

        string extendedWord = LocalizationManager.T(extended ? "Str_DiskHealth_SelfTestExtended" : "Str_DiskHealth_SelfTestShort");
        if (!_dialogService.Confirm(
            string.Format(LocalizationManager.T("Str_DiskHealth_ConfirmSelfTest"), extendedWord, SelectedDisk.DisplayName),
            LocalizationManager.T("Str_DiskHealth_SelfTestTitle")))
            return;

        IsBusy = true;
        StatusText = LocalizationManager.T("Str_DiskHealth_StartingSelfTest");
        try
        {
            bool started = await _healthService.StartSelfTestAsync(SelectedDisk.DiskNumber, extended);
            StatusText = LocalizationManager.T(started ? "Str_DiskHealth_SelfTestStarted" : "Str_DiskHealth_SelfTestUnsupported");
            if (!started) _dialogService.ShowWarning(LocalizationManager.T("Str_DiskHealth_SelfTestUnavailableMessage"), LocalizationManager.T("Str_DiskHealth_SelfTestUnavailableTitle"));
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

    [RelayCommand]
    private void SaveAlertThresholds()
    {
        _healthService.SetAlertThresholds(new AlertThresholds(AlertMaxTemperature, AlertMinWearPercent));
        _dialogService.ShowSuccess(LocalizationManager.T("Str_DiskHealth_ThresholdsSaved"));
    }

    [RelayCommand]
    private async Task ExportAsync(object? formatParameter)
    {
        string format = formatParameter as string ?? "TXT";
        string extension = format.ToUpperInvariant() switch { "CSV" => "csv", "JSON" => "json", _ => "txt" };

        var dialog = new SaveFileDialog
        {
            FileName = $"disk-health.{extension}",
            Filter = $"{format.ToUpperInvariant()} file|*.{extension}"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            await _healthService.ExportAsync(Disks.Select(d => d.Smart), format, dialog.FileName);
            _dialogService.ShowSuccess(string.Format(LocalizationManager.T("Str_DiskHealth_ExportSaved"), format.ToUpperInvariant(), dialog.FileName));
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_DiskHealth_ExportErrorTitle"));
        }
    }
}
