using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Tool.Models;
using S7Tool.Services.Interfaces;
using S7Tool.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace S7Tool.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IProcessMonitorService _processService;
    private readonly IDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly DispatcherTimer _processTimer;

    public ObservableCollection<ProcessInfo> Processes { get; } = new();

    [ObservableProperty]
    private string sortMode = "CPU";

    [ObservableProperty]
    private bool isPaused;

    [ObservableProperty]
    private string pauseButtonLabel = "Pause";

    public MainViewModel(
        IProcessMonitorService processService,
        IDialogService dialogService,
        IServiceProvider serviceProvider)
    {
        _processService = processService;
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;

        _processTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _processTimer.Tick += (_, _) => RefreshProcesses();
        _processTimer.Start();

        RefreshProcesses();
    }

    partial void OnSortModeChanged(string value) => RefreshProcesses();

    [RelayCommand]
    private void RefreshProcesses()
    {
        if (IsPaused) return;

        var latest = _processService.GetProcesses();

        var sorted = SortMode switch
        {
            "RAM" => latest.OrderByDescending(x => x.Memory).ToList(),
            "CPU" => latest.OrderByDescending(x => x.Cpu).ToList(),
            "DISK" => latest.OrderByDescending(x => x.Disk).ToList(),
            "NAME" => latest.OrderBy(x => x.Name).ToList(),
            _ => latest.OrderByDescending(x => x.Cpu).ToList()
        };

        SyncProcesses(sorted);
    }

    private void SyncProcesses(List<ProcessInfo> latest)
    {
        var latestIds = new HashSet<int>(latest.Select(p => p.Id));

        for (int i = Processes.Count - 1; i >= 0; i--)
        {
            if (!latestIds.Contains(Processes[i].Id))
                Processes.RemoveAt(i);
        }

        var index = Processes.ToDictionary(p => p.Id);

        for (int i = 0; i < latest.Count; i++)
        {
            var info = latest[i];

            if (index.TryGetValue(info.Id, out var existing))
            {
                existing.Cpu = info.Cpu;
                existing.Memory = info.Memory;
                existing.Disk = info.Disk;

                int currentIndex = Processes.IndexOf(existing);
                if (currentIndex != i)
                    Processes.Move(currentIndex, Math.Min(i, Processes.Count - 1));
            }
            else
            {
                Processes.Insert(Math.Min(i, Processes.Count), info);
            }
        }
    }

    [RelayCommand]
    private void KillProcess(int pid)
    {
        _processService.KillProcess(pid);
        RefreshProcesses();
    }

    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;

        if (IsPaused)
        {
            _processTimer.Stop();
            PauseButtonLabel = "Reprendre";
        }
        else
        {
            _processTimer.Start();
            PauseButtonLabel = "Pause";
        }
    }

    [RelayCommand]
    private void UninstallApps()
    {
        var window = _serviceProvider.GetRequiredService<UninstallWindow>();
        window.Owner = System.Windows.Application.Current.MainWindow;
        window.Show();
    }

    [RelayCommand]
    private void OpenAiChat() => _serviceProvider.GetRequiredService<AiChatWindow>().Show();

    [RelayCommand]
    private void RenamePC() => _serviceProvider.GetRequiredService<RenamePCWindow>().Show();

    [RelayCommand]
    private void OpenWindowsUpdate() => _serviceProvider.GetRequiredService<WindowsUpdateWindow>().Show();

    [RelayCommand]
    private void OpenNetworkScanner() => _serviceProvider.GetRequiredService<NetworkScannerWindow>().Show();

    [RelayCommand]
    private void OpenPortScanner() => _serviceProvider.GetRequiredService<PortScannerWindow>().Show();

    [RelayCommand]
    private void OpenDiskManager() => _serviceProvider.GetRequiredService<DiskManagerWindow>().Show();

    [RelayCommand]
    private void OpenDiskHealth() => _serviceProvider.GetRequiredService<DiskHealthWindow>().Show();

    [RelayCommand]
    private void OpenDiskSpaceAnalyzer() => _serviceProvider.GetRequiredService<DiskSpaceAnalyzerWindow>().Show();

    [RelayCommand]
    private void DisableFastStartup()
    {
        try
        {
            Registry.SetValue(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Power",
                "HiberbootEnabled",
                0,
                RegistryValueKind.DWord);

            _dialogService.ShowSuccess("Démarrage rapide désactivé !");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private void Quit() => System.Windows.Application.Current.Shutdown();
}
