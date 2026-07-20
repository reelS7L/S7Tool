using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Tool.Helpers;
using S7Tool.Models;
using S7Tool.Services.Interfaces;
using Microsoft.Win32;
using System.Collections.ObjectModel;

namespace S7Tool.ViewModels;

public partial class PortScannerViewModel : ObservableObject
{
    private readonly IPortScannerService _scanner;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _cts;

    public ObservableCollection<PortScanResult> OpenPorts { get; } = new();

    [ObservableProperty]
    private string host = "127.0.0.1";

    [ObservableProperty]
    private string startPort = "1";

    [ObservableProperty]
    private string endPort = "1024";

    [ObservableProperty]
    private int progress;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool isScanning;

    [ObservableProperty]
    private string statusText = "Prêt.";

    public PortScannerViewModel(IPortScannerService scanner, IDialogService dialogService)
    {
        _scanner = scanner;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private void UseCommonPorts()
    {
        StartPort = "1";
        EndPort = "1024";
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (!int.TryParse(StartPort, out int start) || !int.TryParse(EndPort, out int end) || start < 1 || end > 65535 || start > end)
        {
            _dialogService.ShowWarning("Plage de ports invalide (1-65535).");
            return;
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            _dialogService.ShowWarning("Indique une adresse IP ou un nom d'hôte.");
            return;
        }

        OpenPorts.Clear();
        IsScanning = true;
        Progress = 0;
        StatusText = $"Scan de {Host} ({start}-{end})...";
        _cts = new CancellationTokenSource();

        var progressReporter = new Progress<int>(p => Progress = p);

        try
        {
            await _scanner.ScanPortsAsync(Host, start, end, result =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => OpenPorts.Add(result));
            }, progressReporter, _cts.Token);

            StatusText = $"Scan terminé : {OpenPorts.Count} port(s) ouvert(s).";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan interrompu.";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, "Erreur de scan");
        }
        finally
        {
            IsScanning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool CanScan() => !IsScanning;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _cts?.Cancel();

    private bool CanStop() => IsScanning;

    [RelayCommand]
    private void ExportCsv()
    {
        if (OpenPorts.Count == 0)
        {
            _dialogService.ShowWarning("Aucun résultat à exporter.");
            return;
        }

        var dialog = new SaveFileDialog { Filter = "Fichier CSV (*.csv)|*.csv", FileName = "scan_ports.csv" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            CsvExporter.Export(
                dialog.FileName,
                new[] { "Port", "Service" },
                OpenPorts.Select(p => new[] { p.Port.ToString(), p.ServiceName }));

            _dialogService.ShowSuccess("Export CSV terminé.");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, "Erreur d'export");
        }
    }
}
