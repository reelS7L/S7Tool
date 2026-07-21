using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Tool.Helpers;
using S7Tool.Models;
using S7Tool.Services;
using S7Tool.Services.Interfaces;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace S7Tool.ViewModels;

public partial class DiskSpaceAnalyzerViewModel : ObservableObject
{
    private readonly IDiskSpaceAnalyzerService _analyzer;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _scanCts;
    private FileSystemNode? _scanRoot;
    private readonly Stopwatch _scanStopwatch = new();
    private readonly DispatcherTimer _liveRefreshTimer;
    private long _scanTargetTotalBytes;

    private const int MaxDisplayedItems = 300;

    private const int MaxChartSlices = 9;

    public ObservableCollection<FileSystemNode> Items { get; } = new();
    public ObservableCollection<FileSystemNode> Breadcrumb { get; } = new();
    public ObservableCollection<ChartLegendEntry> ChartLegend { get; } = new();

    [ObservableProperty]
    private SpaceViewMode viewMode = SpaceViewMode.Liste;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    private string rootPath = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopScanCommand))]
    [NotifyPropertyChangedFor(nameof(IsScanProgressIndeterminate))]
    private bool isScanning;

    [ObservableProperty]
    private FileSystemNode? currentNode;

    [ObservableProperty]
    private FileSystemNode? selectedItem;

    [ObservableProperty]
    private string statusText = "";

    [ObservableProperty]
    private string progressText = "";

    [ObservableProperty]
    private double scanProgressPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScanProgressIndeterminate))]
    private bool hasScanProgressTarget;

    [ObservableProperty]
    private string remainingTimeDisplay = "";

    public bool IsScanProgressIndeterminate => IsScanning && !HasScanProgressTarget;

    [ObservableProperty] private double driveTotalGb;
    [ObservableProperty] private double driveFreeGb;
    [ObservableProperty] private double driveUsedGb;
    [ObservableProperty] private long scannedFileCount;
    [ObservableProperty] private long scannedFolderCount;
    [ObservableProperty] private string scanDurationDisplay = "";

    [ObservableProperty]
    private string filterText = "";

    [ObservableProperty]
    private string filterExtension = "";

    [ObservableProperty]
    private double filterMinSizeMb;

    public ObservableCollection<string> AvailableDrives { get; } = new();

    public DiskSpaceAnalyzerViewModel(IDiskSpaceAnalyzerService analyzer, IDialogService dialogService)
    {
        _analyzer = analyzer;
        _dialogService = dialogService;

        StatusText = LocalizationManager.T("Str_DiskSpace_InitialStatus");

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            AvailableDrives.Add($"{drive.Name} ({Math.Round(drive.TotalSize / 1024.0 / 1024.0 / 1024.0, 0)} Go)");

        _liveRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveRefreshTimer.Tick += (_, _) =>
        {
            ScanDurationDisplay = FormatDuration(_scanStopwatch.Elapsed);
            UpdateDriveInfo();
            RefreshDisplayedItems();
            UpdateRemainingTimeEstimate();
        };
    }

    partial void OnFilterTextChanged(string value) => RefreshDisplayedItems();
    partial void OnFilterExtensionChanged(string value) => RefreshDisplayedItems();
    partial void OnFilterMinSizeMbChanged(double value) => RefreshDisplayedItems();

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() == true) RootPath = dialog.FolderName;
    }

    [RelayCommand]
    private void BrowseDrive(object? driveParameter)
    {
        if (driveParameter is string label) RootPath = label.Split(' ')[0];
    }

    private bool CanScan() => !IsScanning && !string.IsNullOrWhiteSpace(RootPath);

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (!Directory.Exists(RootPath))
        {
            _dialogService.ShowWarning(LocalizationManager.T("Str_DiskSpace_FolderNotFound"));
            return;
        }

        Items.Clear();
        Breadcrumb.Clear();
        IsScanning = true;
        StatusText = LocalizationManager.T("Str_DiskSpace_Scanning");
        _scanCts = new CancellationTokenSource();
        _scanStopwatch.Restart();
        UpdateDriveInfo();

        string? pathRoot = Path.GetPathRoot(RootPath);
        bool isWholeDriveScan = pathRoot is not null &&
            string.Equals(RootPath.TrimEnd('\\'), pathRoot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        _scanTargetTotalBytes = isWholeDriveScan ? (long)(DriveUsedGb * 1024 * 1024 * 1024) : 0;
        HasScanProgressTarget = _scanTargetTotalBytes > 0;
        ScanProgressPercent = 0;
        RemainingTimeDisplay = "";

        _liveRefreshTimer.Start();

        var progress = new Progress<ScanProgress>(p =>
        {
            ScannedFileCount = p.FilesScanned;
            ScannedFolderCount = p.FoldersScanned;
            ProgressText = string.Format(LocalizationManager.T("Str_DiskSpace_ProgressText"), p.FilesScanned, p.FoldersScanned, FormatBytes(p.BytesScanned));

            if (HasScanProgressTarget)
                ScanProgressPercent = Math.Min(100, p.BytesScanned * 100.0 / _scanTargetTotalBytes);
        });

        try
        {
            _scanRoot = await _analyzer.ScanAsync(RootPath,
                root => NavigateTo(root),
                progress, _scanCts.Token);
            _scanStopwatch.Stop();
            ScanDurationDisplay = FormatDuration(_scanStopwatch.Elapsed);
            UpdateDriveInfo();
            RefreshDisplayedItems();

            StatusText = string.Format(LocalizationManager.T("Str_DiskSpace_ScanDone"), ScanDurationDisplay, ScannedFileCount, ScannedFolderCount);
        }
        catch (OperationCanceledException)
        {
            _scanStopwatch.Stop();
            StatusText = LocalizationManager.T("Str_DiskSpace_ScanInterrupted");
        }
        catch (Exception ex)
        {
            _scanStopwatch.Stop();
            _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_DiskSpace_ScanErrorTitle"));
        }
        finally
        {
            _liveRefreshTimer.Stop();
            IsScanning = false;
            RemainingTimeDisplay = "";
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    private void UpdateRemainingTimeEstimate()
    {
        if (!HasScanProgressTarget || ScanProgressPercent < 2)
        {
            RemainingTimeDisplay = "";
            return;
        }

        var elapsed = _scanStopwatch.Elapsed;
        var estimatedTotal = TimeSpan.FromSeconds(elapsed.TotalSeconds * 100.0 / ScanProgressPercent);
        var remaining = estimatedTotal - elapsed;

        RemainingTimeDisplay = remaining > TimeSpan.Zero
            ? string.Format(LocalizationManager.T("Str_DiskSpace_RemainingTime"), FormatDuration(remaining))
            : "";
    }

    private bool CanStopScan() => IsScanning;

    [RelayCommand(CanExecute = nameof(CanStopScan))]
    private void StopScan() => _scanCts?.Cancel();

    private void UpdateDriveInfo()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(RootPath) ?? RootPath);
            if (!drive.IsReady) return;
            DriveTotalGb = Math.Round(drive.TotalSize / 1024.0 / 1024.0 / 1024.0, 1);
            DriveFreeGb = Math.Round(drive.TotalFreeSpace / 1024.0 / 1024.0 / 1024.0, 1);
            DriveUsedGb = Math.Round(DriveTotalGb - DriveFreeGb, 1);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
        }
    }

    [RelayCommand]
    private void NavigateInto(FileSystemNode? node)
    {
        if (node is null || !node.IsDirectory) return;
        NavigateTo(node);
    }

    [RelayCommand]
    private void NavigateUp()
    {
        if (CurrentNode?.Parent is not null) NavigateTo(CurrentNode.Parent);
    }

    private void NavigateTo(FileSystemNode node)
    {
        CurrentNode = node;

        Breadcrumb.Clear();
        var chain = new List<FileSystemNode>();
        for (var n = node; n is not null; n = n.Parent) chain.Add(n);
        chain.Reverse();
        foreach (var n in chain) Breadcrumb.Add(n);

        RefreshDisplayedItems();
    }

    private void RefreshDisplayedItems()
    {
        var previousSelection = SelectedItem;
        Items.Clear();
        if (CurrentNode is null) return;

        var children = CurrentNode.GetChildrenSnapshot();
        long parentSize = CurrentNode.SizeBytes;

        IEnumerable<FileSystemNode> query = children;

        if (!string.IsNullOrWhiteSpace(FilterText))
            query = query.Where(n => n.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(FilterExtension))
            query = query.Where(n => n.IsDirectory || string.Equals(n.Extension, FilterExtension.TrimStart('.'), StringComparison.OrdinalIgnoreCase));
        if (FilterMinSizeMb > 0)
            query = query.Where(n => n.SizeBytes >= FilterMinSizeMb * 1024 * 1024);

        var sorted = query.OrderByDescending(n => n.SizeBytes).Take(MaxDisplayedItems).ToList();

        foreach (var n in sorted)
            n.PercentOfParent = parentSize > 0 ? Math.Round(n.SizeBytes * 100.0 / parentSize, 1) : 0;

        foreach (var n in sorted) Items.Add(n);

        if (previousSelection is not null && sorted.Contains(previousSelection))
            SelectedItem = previousSelection;

        RefreshChartLegend(sorted, parentSize);
    }

    private void RefreshChartLegend(List<FileSystemNode> sorted, long parentSize)
    {
        ChartLegend.Clear();
        if (parentSize <= 0) return;

        var top = sorted.Take(MaxChartSlices).ToList();
        double topSum = top.Sum(n => (double)n.SizeBytes);
        double othersSum = Math.Max(0, parentSize - topSum);

        int colorIndex = 0;
        foreach (var node in top)
        {
            double percent = Math.Round(node.SizeBytes * 100.0 / parentSize, 1);
            ChartLegend.Add(new ChartLegendEntry(node.Name, node.SizeDisplay, percent, ChartPalette.BrushForIndex(colorIndex), node));
            colorIndex++;
        }

        if (othersSum > 0)
        {
            double percent = Math.Round(othersSum * 100.0 / parentSize, 1);
            ChartLegend.Add(new ChartLegendEntry(LocalizationManager.T("Str_DiskSpace_Others"), FormatBytes((long)othersSum), percent, ChartPalette.BrushForIndex(MaxChartSlices), null));
        }
    }

    [RelayCommand]
    private void RefreshDisplay() => RefreshDisplayedItems();

    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedItem is null) return;
        try { Process.Start(new ProcessStartInfo(SelectedItem.FullPath) { UseShellExecute = true }); }
        catch (Exception ex) { _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_DiskSpace_CannotOpenTitle")); }
    }

    [RelayCommand]
    private void ShowInExplorer()
    {
        if (SelectedItem is null) return;
        try { Process.Start("explorer.exe", $"/select,\"{SelectedItem.FullPath}\""); }
        catch (Exception ex) { _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_DiskSpace_CannotOpenExplorerTitle")); }
    }

    [RelayCommand]
    private void CopyPath()
    {
        if (SelectedItem is null) return;
        Clipboard.SetText(SelectedItem.FullPath);
    }

    [RelayCommand]
    private void ShowProperties()
    {
        if (SelectedItem is null) return;
        NativeShell.ShowFileProperties(SelectedItem.FullPath);
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedItem is null) return;

        if (!_dialogService.Confirm(string.Format(LocalizationManager.T("Str_DiskSpace_ConfirmDelete"), SelectedItem.Name, SelectedItem.SizeDisplay), LocalizationManager.T("Str_DiskSpace_ConfirmDeleteTitle")))
            return;

        try
        {
            if (SelectedItem.IsDirectory)
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(SelectedItem.FullPath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            else
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(SelectedItem.FullPath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

            CurrentNode?.Children.Remove(SelectedItem);
            RefreshDisplayedItems();
            StatusText = string.Format(LocalizationManager.T("Str_DiskSpace_SentToRecycleBin"), SelectedItem.Name);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_DiskSpace_DeleteErrorTitle"));
        }
    }

    [RelayCommand]
    private async Task RefreshSelectedFolderAsync()
    {
        if (SelectedItem is null || !SelectedItem.IsDirectory) { _dialogService.ShowWarning(LocalizationManager.T("Str_DiskSpace_SelectFolder")); return; }

        StatusText = string.Format(LocalizationManager.T("Str_DiskSpace_Refreshing"), SelectedItem.Name);
        try
        {
            using var cts = new CancellationTokenSource();
            var progress = new Progress<ScanProgress>(_ => RefreshDisplayedItems());
            var freshNode = await _analyzer.ScanAsync(SelectedItem.FullPath, _ => { }, progress, cts.Token);
            freshNode.Parent = CurrentNode;

            int index = CurrentNode!.Children.IndexOf(SelectedItem);
            if (index >= 0) CurrentNode.Children[index] = freshNode;

            RefreshDisplayedItems();
            StatusText = string.Format(LocalizationManager.T("Str_DiskSpace_Refreshed"), freshNode.Name);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message, LocalizationManager.T("Str_DiskSpace_RefreshErrorTitle"));
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024.0 / 1024.0:0.##} Go",
        >= 1024L * 1024 => $"{bytes / 1024.0 / 1024.0:0.#} Mo",
        _ => $"{bytes / 1024.0:0.#} Ko"
    };

    private static string FormatDuration(TimeSpan d) => d.TotalMinutes >= 1 ? $"{(int)d.TotalMinutes}min{d.Seconds:D2}" : $"{d.TotalSeconds:0.#}s";
}
