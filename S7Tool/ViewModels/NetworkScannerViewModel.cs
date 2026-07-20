using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Tool.Helpers;
using S7Tool.Models;
using S7Tool.Services;
using S7Tool.Services.Interfaces;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;

namespace S7Tool.ViewModels;

public partial class NetworkScannerViewModel : ObservableObject
{
    private readonly INetworkScannerService _scanner;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _cts;

    public ObservableCollection<NetworkHost> Hosts { get; } = new();

    [ObservableProperty]
    private string ipRanges = "";

    [ObservableProperty]
    private int progress;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool isScanning;

    [ObservableProperty]
    private string statusText = "";

    public NetworkScannerViewModel(INetworkScannerService scanner, IDialogService dialogService)
    {
        _scanner = scanner;
        _dialogService = dialogService;

        var (localIp, start, end) = _scanner.DetectLocalSubnet();
        IpRanges = $"{start}-{end}";
        StatusText = string.Format(LocalizationManager.T("Str_NetScan_DetectedFrom"), localIp);
    }

    [RelayCommand]
    private void AddDetectedRange()
    {
        var (_, start, end) = _scanner.DetectLocalSubnet();
        var range = $"{start}-{end}";
        IpRanges = string.IsNullOrWhiteSpace(IpRanges) ? range : $"{IpRanges}, {range}";
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        var ranges = ParseRanges(IpRanges);

        if (ranges.Count == 0)
        {
            _dialogService.ShowWarning(LocalizationManager.T("Str_NetScan_InvalidRanges"));
            return;
        }

        Hosts.Clear();
        IsScanning = true;
        Progress = 0;
        StatusText = string.Format(LocalizationManager.T("Str_NetScan_ScanningRanges"), ranges.Count);
        _cts = new CancellationTokenSource();

        try
        {
            for (int i = 0; i < ranges.Count; i++)
            {
                var (start, end) = ranges[i];
                int rangeIndex = i;

                var rangeProgress = new Progress<int>(p =>
                    Progress = (int)((rangeIndex * 100.0 + p) / ranges.Count));

                await _scanner.ScanRangeAsync(start, end, host =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => InsertSorted(host));
                }, rangeProgress, _cts.Token);
            }

            Progress = 100;
            StatusText = string.Format(LocalizationManager.T("Str_NetScan_ScanDone"), Hosts.Count, ranges.Count);
        }
        catch (OperationCanceledException)
        {
            StatusText = LocalizationManager.T("Str_NetScan_ScanInterrupted");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_NetScan_ScanErrorTitle"));
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
    private void WakeOnLan(NetworkHost? host)
    {
        if (host is null || string.IsNullOrWhiteSpace(host.MacAddress))
        {
            _dialogService.ShowWarning(LocalizationManager.T("Str_NetScan_UnknownMac"));
            return;
        }

        try
        {
            _ = _scanner.SendWakeOnLanAsync(host.MacAddress);
            _dialogService.ShowSuccess(string.Format(LocalizationManager.T("Str_NetScan_WolSent"), host.MacAddress));
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_NetScan_WolErrorTitle"));
        }
    }

    [RelayCommand]
    private void OpenInBrowser(NetworkHost? host)
    {
        if (host?.WebUrl is null) return;

        try
        {
            Process.Start(new ProcessStartInfo(host.WebUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_NetScan_OpenErrorTitle"));
        }
    }

    [RelayCommand]
    private void ExportCsv()
    {
        if (Hosts.Count == 0)
        {
            _dialogService.ShowWarning(LocalizationManager.T("Str_NetScan_NoResultsToExport"));
            return;
        }

        var dialog = new SaveFileDialog { Filter = LocalizationManager.T("Str_NetScan_CsvFilter"), FileName = "scan_reseau.csv" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            CsvExporter.Export(
                dialog.FileName,
                new[] { "IP Address", "Hostname", "MAC Address", "Response Time (ms)", "Detected Ports" },
                Hosts.Select(h => new[] { h.IpAddress, h.Hostname, h.MacAddress, h.ResponseTimeMs.ToString(), h.OpenPortsSummary }));

            _dialogService.ShowSuccess(LocalizationManager.T("Str_NetScan_CsvExportDone"));
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_NetScan_ExportErrorTitle"));
        }
    }

    private void InsertSorted(NetworkHost host)
    {
        var hostBytes = IPAddress.Parse(host.IpAddress).GetAddressBytes();
        int index = Hosts.Count;

        for (int i = 0; i < Hosts.Count; i++)
        {
            var existingBytes = IPAddress.Parse(Hosts[i].IpAddress).GetAddressBytes();
            if (CompareBytes(hostBytes, existingBytes) < 0)
            {
                index = i;
                break;
            }
        }

        Hosts.Insert(index, host);
    }

    private static int CompareBytes(byte[] a, byte[] b)
    {
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return a[i].CompareTo(b[i]);

        return 0;
    }

    private static List<(string Start, string End)> ParseRanges(string input)
    {
        var result = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(input)) return result;

        var segments = input.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in segments)
        {
            var segment = raw.Trim();
            if (segment.Length == 0) continue;

            var parts = segment.Split('-');

            if (parts.Length == 1 && IPAddress.TryParse(parts[0].Trim(), out _))
            {
                result.Add((parts[0].Trim(), parts[0].Trim()));
            }
            else if (parts.Length == 2 &&
                     IPAddress.TryParse(parts[0].Trim(), out _) &&
                     IPAddress.TryParse(parts[1].Trim(), out _))
            {
                result.Add((parts[0].Trim(), parts[1].Trim()));
            }
        }

        return result;
    }
}
