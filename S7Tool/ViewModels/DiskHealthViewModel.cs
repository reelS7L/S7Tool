using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Tool.DiskEngine.Models;
using S7Tool.Models;
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
    private string statusText = "Prêt.";

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
        StatusText = "Lecture SMART/NVMe en cours...";

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

            StatusText = $"{DiskCount} disque(s) analysé(s).";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, "Erreur de lecture SMART");
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
                _dialogService.ShowWarning($"{r.DisplayName} : température élevée ({r.TemperatureDisplay}, seuil {AlertMaxTemperature} °C).", "Alerte disque");
            if (wearAlert)
                _dialogService.ShowWarning($"{r.DisplayName} : endurance restante faible ({r.WearDisplay}, seuil {AlertMinWearPercent} %).", "Alerte disque");
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
        if (SelectedDisk is null) { _dialogService.ShowWarning("Sélectionne un disque."); return; }

        if (!_dialogService.Confirm(
            $"Lancer un auto-test SMART {(extended ? "étendu" : "court")} sur {SelectedDisk.DisplayName} ?\n\n" +
            "Le disque exécute le test en arrière-plan (peut prendre de quelques minutes à plusieurs dizaines de minutes pour un test étendu) — pas de blocage de l'interface, relance une actualisation plus tard pour voir le résultat.",
            "Auto-test SMART"))
            return;

        IsBusy = true;
        StatusText = "Lancement de l'auto-test...";
        try
        {
            bool started = await _healthService.StartSelfTestAsync(SelectedDisk.DiskNumber, extended);
            StatusText = started ? "Auto-test lancé." : "Ce disque ne prend pas en charge l'auto-test SMART bas niveau.";
            if (!started) _dialogService.ShowWarning("Ce disque (ou son contrôleur) ne prend pas en charge le déclenchement d'auto-test SMART.", "Auto-test indisponible");
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

    [RelayCommand]
    private void SaveAlertThresholds()
    {
        _healthService.SetAlertThresholds(new AlertThresholds(AlertMaxTemperature, AlertMinWearPercent));
        _dialogService.ShowSuccess("Seuils d'alerte enregistrés.");
    }

    [RelayCommand]
    private async Task ExportAsync(object? formatParameter)
    {
        string format = formatParameter as string ?? "TXT";
        string extension = format.ToUpperInvariant() switch { "CSV" => "csv", "JSON" => "json", _ => "txt" };

        var dialog = new SaveFileDialog
        {
            FileName = $"sante-disques.{extension}",
            Filter = $"Fichier {format.ToUpperInvariant()}|*.{extension}"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            await _healthService.ExportAsync(Disks.Select(d => d.Smart), format, dialog.FileName);
            _dialogService.ShowSuccess($"Export {format.ToUpperInvariant()} enregistré : {dialog.FileName}");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, "Erreur d'export");
        }
    }
}
