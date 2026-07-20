using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Tool.DiskEngine.Models;

namespace S7Tool.DiskManagerPE;

public partial class MainViewModel : ObservableObject
{
    private readonly OfflineDiskManagerService _service = new();

    public ObservableCollection<DiskInfo> Disks { get; } = new();
    public ObservableCollection<DiskPartitionInfo> AllPartitions { get; } = new();
    public ObservableCollection<DiskHealthInfo> DiskHealth { get; } = new();
    public ObservableCollection<string> ActionLogs { get; } = new();

    [ObservableProperty]
    private DiskPartitionInfo? selectedPartition;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadDisksCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyPendingChangesCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = "Prêt.";

    public ObservableCollection<string> FileSystemOptions { get; } = new() { "NTFS", "FAT32", "exFAT" };

    [ObservableProperty]
    private string partitionFileSystem = "NTFS";

    [ObservableProperty]
    private string partitionLabel = "";

    [ObservableProperty]
    private double partitionSizeGb;

    [ObservableProperty]
    private DiskInfo? cloneSourceDisk;

    [ObservableProperty]
    private DiskInfo? cloneDestinationDisk;

    [ObservableProperty]
    private bool sectorCloneVerify = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SectorCloneDiskCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopOperationCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyPendingChangesCommand))]
    private bool isCloning;

    [ObservableProperty]
    private string progressText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyPendingChangesCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelPendingChangesCommand))]
    private bool hasAnyPendingChange;

    private CancellationTokenSource? _operationCts;

    public MainViewModel()
    {
        _ = LoadDisksAsync();
    }

    private bool CanLoadOrManage() => !IsBusy && !IsCloning;

    [RelayCommand(CanExecute = nameof(CanLoadOrManage))]
    private async Task LoadDisksAsync()
    {
        IsBusy = true;
        StatusText = "Analyse des disques...";

        try
        {
            var disks = await _service.GetDisksAsync();
            Disks.Clear();
            AllPartitions.Clear();
            foreach (var disk in disks)
            {
                Disks.Add(disk);
                foreach (var partition in disk.Partitions)
                {
                    partition.PendingSizeGb = partition.SizeGb;
                    AllPartitions.Add(partition);
                }
            }

            HasAnyPendingChange = false;

            _ = Task.WhenAll(AllPartitions.Select(PrepareResizeBoundsAsync));

            var health = await _service.GetHealthAsync(disks.Select(d => d.DiskNumber));
            DiskHealth.Clear();
            foreach (var h in health) DiskHealth.Add(h);

            StatusText = $"{Disks.Count} disque(s) détecté(s).";
        }
        catch (Exception ex)
        {
            ShowError(ex.Message, "Erreur de lecture des disques");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task PrepareResizeBoundsAsync(DiskPartitionInfo partition)
    {
        if (partition.IsUnallocated) return;
        if (partition.MaxSizeBytes > 0) return;

        try
        {
            var (min, max) = await _service.GetSupportedSizeRangeAsync(partition.DiskNumber, partition.PartitionNumber);
            partition.MinSizeBytes = min;
            partition.MaxSizeBytes = max;
        }
        catch
        {
            partition.MinSizeBytes = partition.SizeBytes;
            partition.MaxSizeBytes = partition.SizeBytes;
        }
    }

    public void RefreshPendingState()
    {
        HasAnyPendingChange = AllPartitions.Any(p => p.HasPendingChange);
    }

    public void LogMessage(string message) =>
        Application.Current.Dispatcher.Invoke(() => ActionLogs.Add(message));

    private bool CanApplyPendingChanges() => HasAnyPendingChange && CanLoadOrManage();

    [RelayCommand(CanExecute = nameof(CanApplyPendingChanges))]
    private async Task ApplyPendingChangesAsync()
    {
        var resizes = AllPartitions.Where(p => p.HasPendingChange && !p.IsUnallocated).ToList();
        if (resizes.Count == 0) return;

        var lines = resizes.Select(p => $"• Redimensionner {p.DisplayName} : {p.SizeGb} Go → {Math.Round(p.PendingSizeGb, 1)} Go");

        if (!Confirm("Appliquer les modifications suivantes ?\n\n" + string.Join("\n", lines)))
            return;

        IsBusy = true;
        StatusText = "Application des modifications...";

        try
        {
            foreach (var p in resizes.Where(p => p.PendingSizeGb < p.SizeGb).OrderBy(p => p.PendingSizeGb))
            {
                StatusText = $"Rétrécissement de {p.DisplayName}...";
                await _service.ResizePartitionAsync(p.DiskNumber, p.PartitionNumber, (long)(p.PendingSizeGb * 1024 * 1024 * 1024));
            }

            foreach (var p in resizes.Where(p => p.PendingSizeGb > p.SizeGb).OrderByDescending(p => p.PendingSizeGb))
            {
                StatusText = $"Agrandissement de {p.DisplayName}...";
                await _service.ResizePartitionAsync(p.DiskNumber, p.PartitionNumber, (long)(p.PendingSizeGb * 1024 * 1024 * 1024));
            }

            ShowSuccess("Modification(s) appliquée(s) avec succès.");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message, "Erreur pendant l'application des modifications");
        }
        finally
        {
            IsBusy = false;
            await LoadDisksAsync();
        }
    }

    private bool CanCancelPendingChanges() => HasAnyPendingChange;

    [RelayCommand(CanExecute = nameof(CanCancelPendingChanges))]
    private void CancelPendingChanges()
    {
        foreach (var p in AllPartitions)
            p.PendingSizeGb = p.SizeGb;
        HasAnyPendingChange = false;
    }

    [RelayCommand(CanExecute = nameof(CanLoadOrManage))]
    private async Task CreatePartitionAsync()
    {
        if (SelectedPartition is null || !SelectedPartition.IsUnallocated)
        {
            ShowWarning("Sélectionne d'abord une zone d'espace non alloué.");
            return;
        }

        if (!Confirm($"Créer une nouvelle partition {PartitionFileSystem} de " +
            (PartitionSizeGb > 0 ? $"{PartitionSizeGb} Go" : "toute la taille disponible") +
            $" sur le disque {SelectedPartition.DiskNumber} ?")) return;

        IsBusy = true;
        StatusText = "Création de la partition en cours...";
        try
        {
            long? sizeBytes = PartitionSizeGb > 0 ? (long)(PartitionSizeGb * 1024 * 1024 * 1024) : null;
            await _service.CreatePartitionAsync(SelectedPartition.DiskNumber, sizeBytes, PartitionFileSystem, PartitionLabel);
            ShowSuccess("Partition créée avec succès.");
            await LoadDisksAsync();
        }
        catch (Exception ex) { ShowError(ex.Message, "Erreur de création"); }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanLoadOrManage))]
    private async Task FormatPartitionAsync()
    {
        if (SelectedPartition is null || SelectedPartition.IsUnallocated) { ShowWarning("Sélectionne une partition existante."); return; }

        if (!Confirm($"Formater {SelectedPartition.DisplayName} en {PartitionFileSystem} ? TOUTES LES DONNÉES SERONT EFFACÉES.")) return;

        IsBusy = true;
        StatusText = "Formatage en cours...";
        try
        {
            await _service.FormatPartitionAsync(SelectedPartition.DiskNumber, SelectedPartition.PartitionNumber, PartitionFileSystem, PartitionLabel);
            ShowSuccess("Partition formatée avec succès.");
            await LoadDisksAsync();
        }
        catch (Exception ex) { ShowError(ex.Message, "Erreur de formatage"); }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanLoadOrManage))]
    private async Task RenamePartitionAsync()
    {
        if (SelectedPartition is null || SelectedPartition.IsUnallocated) { ShowWarning("Sélectionne une partition existante."); return; }
        if (string.IsNullOrWhiteSpace(PartitionLabel)) { ShowWarning("Renseigne un nouveau nom."); return; }

        IsBusy = true;
        try
        {
            await _service.RenamePartitionAsync(SelectedPartition.DiskNumber, SelectedPartition.PartitionNumber, PartitionLabel);
            ShowSuccess("Partition renommée avec succès.");
            await LoadDisksAsync();
        }
        catch (Exception ex) { ShowError(ex.Message, "Erreur de renommage"); }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanLoadOrManage))]
    private async Task DeletePartitionAsync()
    {
        if (SelectedPartition is null || SelectedPartition.IsUnallocated) { ShowWarning("Sélectionne une partition existante."); return; }

        if (!Confirm($"Supprimer {SelectedPartition.DisplayName} ? TOUTES LES DONNÉES SERONT DÉFINITIVEMENT PERDUES.")) return;
        if (!Confirm($"Dernière confirmation : {SelectedPartition.DisplayName} ({SelectedPartition.SizeGb} Go) va être définitivement supprimée. Continuer ?")) return;

        IsBusy = true;
        StatusText = "Suppression en cours...";
        try
        {
            await _service.DeletePartitionAsync(SelectedPartition.DiskNumber, SelectedPartition.PartitionNumber);
            ShowSuccess("Partition supprimée avec succès.");
            await LoadDisksAsync();
        }
        catch (Exception ex) { ShowError(ex.Message, "Erreur de suppression"); }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanLoadOrManage))]
    private async Task ToggleHiddenAsync()
    {
        if (SelectedPartition is null || SelectedPartition.IsUnallocated) { ShowWarning("Sélectionne une partition existante."); return; }

        IsBusy = true;
        try
        {
            await _service.SetPartitionHiddenAsync(SelectedPartition.DiskNumber, SelectedPartition.PartitionNumber, !SelectedPartition.IsHidden);
            await LoadDisksAsync();
        }
        catch (Exception ex) { ShowError(ex.Message, "Erreur"); }
        finally { IsBusy = false; }
    }

    private bool CanClone() => !IsCloning;

    [RelayCommand(CanExecute = nameof(CanClone))]
    private async Task SectorCloneDiskAsync()
    {
        if (CloneSourceDisk is null || CloneDestinationDisk is null) { ShowWarning("Sélectionne un disque source et un disque de destination."); return; }
        if (CloneSourceDisk.DiskNumber == CloneDestinationDisk.DiskNumber) { ShowWarning("Le disque source et le disque de destination doivent être différents."); return; }

        if (!Confirm($"Clonage SECTEUR PAR SECTEUR de {CloneSourceDisk.DisplayName} vers {CloneDestinationDisk.DisplayName} ? " +
            $"TOUTES LES DONNÉES DU DISQUE {CloneDestinationDisk.DiskNumber} SERONT DÉFINITIVEMENT ÉCRASÉES.")) return;
        if (!Confirm("Dernière confirmation : accès disque bas niveau. Continuer ?")) return;

        ActionLogs.Clear();
        IsCloning = true;
        ProgressText = "Démarrage...";
        StatusText = "Clonage secteur par secteur en cours...";
        _operationCts = new CancellationTokenSource();

        try
        {
            await _service.SectorCloneDiskAsync(
                CloneSourceDisk.DiskNumber, CloneDestinationDisk.DiskNumber, SectorCloneVerify,
                line => Application.Current.Dispatcher.Invoke(() => ActionLogs.Add(line)),
                p => Application.Current.Dispatcher.Invoke(() =>
                {
                    double percent = p.TotalBytes > 0 ? p.BytesDone * 100.0 / p.TotalBytes : 0;
                    ProgressText = $"{percent:F1}% — {p.MegabytesPerSecond:F0} Mo/s — reste ~{FormatEta(p.Eta)}";
                }),
                _operationCts.Token);

            StatusText = "Clonage terminé avec succès.";
            ShowSuccess("Clonage secteur par secteur terminé avec succès.");
            await LoadDisksAsync();
        }
        catch (OperationCanceledException) { StatusText = "Clonage interrompu."; }
        catch (Exception ex) { StatusText = "Échec du clonage."; ShowError(ex.Message, "Erreur de clonage"); }
        finally
        {
            IsCloning = false;
            _operationCts?.Dispose();
            _operationCts = null;
        }
    }

    private bool CanStopOperation() => IsCloning;

    [RelayCommand(CanExecute = nameof(CanStopOperation))]
    private void StopOperation() => _operationCts?.Cancel();

    private static string FormatEta(TimeSpan eta) => eta.TotalHours >= 1
        ? $"{(int)eta.TotalHours}h{eta.Minutes:D2}"
        : $"{eta.Minutes}min{eta.Seconds:D2}";

    [RelayCommand]
    private void Reboot()
    {
        if (!Confirm("Redémarrer sous Windows maintenant ?")) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "wpeutil.exe", Arguments = "reboot", UseShellExecute = false, CreateNoWindow = true });
        }
        catch { }
    }

    private static bool Confirm(string message) =>
        MessageBox.Show(message, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private static void ShowWarning(string message) => MessageBox.Show(message, "Attention", MessageBoxButton.OK, MessageBoxImage.Warning);
    private static void ShowSuccess(string message) => MessageBox.Show(message, "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
    private static void ShowError(string message, string title) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}
