using System.Windows;
using System.Windows.Input;
using S7Tool.DiskEngine.Models;

namespace S7Tool.DiskManagerPE;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    private const double TotalBarWidth = Helpers.ProportionalBarWidthConverter.TotalBarWidth;

    private bool _isDragging;
    private DiskPartitionInfo? _dragPartition;
    private double _dragPartitionDiskGb;
    private double _dragStartX;
    private double _dragStartGb;
    private DiskPartitionInfo? _adjacentGap;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement grip || grip.DataContext is not DiskPartitionInfo partition)
            return;

        if (partition.IsUnallocated)
            return;

        var disk = _viewModel.Disks.FirstOrDefault(d => d.Partitions.Contains(partition));
        if (disk is null || disk.SizeGb <= 0)
            return;

        if (partition.MaxSizeBytes <= 0)
            Task.Run(() => _viewModel.PrepareResizeBoundsAsync(partition)).GetAwaiter().GetResult();

        double minGb = Math.Round(partition.MinSizeBytes / 1024.0 / 1024.0 / 1024.0, 1);
        double maxGb = Math.Round(partition.MaxSizeBytes / 1024.0 / 1024.0 / 1024.0, 1);

        if (maxGb - minGb < 0.1)
        {
            _viewModel.LogMessage($"{partition.DisplayName} : redimensionnement indisponible (aucune marge — partition non-NTFS sans espace libre juste après, ou partition protégée).");
            return;
        }

        var ordered = disk.Partitions.OrderBy(p => p.StartOffsetBytes).ToList();
        int index = ordered.IndexOf(partition);
        _adjacentGap = index >= 0 && index + 1 < ordered.Count && ordered[index + 1].IsUnallocated
            ? ordered[index + 1]
            : null;

        _dragPartition = partition;
        _dragPartitionDiskGb = disk.SizeGb;
        _dragStartX = e.GetPosition(this).X;
        _dragStartGb = partition.PendingSizeGb;
        _isDragging = true;

        _viewModel.LogMessage($"Redimensionnement : {partition.DisplayName} — glisse entre {minGb} et {maxGb} Go, puis Appliquer.");

        grip.CaptureMouse();
        e.Handled = true;
    }

    private void ResizeGrip_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _dragPartition is null)
            return;

        double pixelsPerGb = TotalBarWidth / _dragPartitionDiskGb;
        double deltaX = e.GetPosition(this).X - _dragStartX;
        double deltaGb = deltaX / pixelsPerGb;

        double minGb = Math.Round(_dragPartition.MinSizeBytes / 1024.0 / 1024.0 / 1024.0, 1);
        double maxGb = Math.Round(_dragPartition.MaxSizeBytes / 1024.0 / 1024.0 / 1024.0, 1);

        _dragPartition.PendingSizeGb = Math.Clamp(_dragStartGb + deltaGb, minGb, maxGb);

        if (_adjacentGap is not null)
        {
            double delta = _dragPartition.PendingSizeGb - _dragStartGb;
            _adjacentGap.PendingSizeGb = Math.Max(0, _adjacentGap.SizeGb - delta);
        }

        _viewModel.RefreshPendingState();
    }

    private void ResizeGrip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement grip)
            grip.ReleaseMouseCapture();

        if (_isDragging && _dragPartition is { HasPendingChange: true })
            _viewModel.StatusText = "Modification en attente — clique sur Appliquer pour l'exécuter, ou Annuler.";

        _isDragging = false;
        _dragPartition = null;
        _adjacentGap = null;
        _viewModel.RefreshPendingState();
    }
}
