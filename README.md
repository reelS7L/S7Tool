# s7Tool

*[Lire en français](README.fr.md)*

s7Tool is a Windows sysadmin toolbox: a WPF dashboard bundling a set of native, dependency-light utilities for everyday technician and admin work — disk health and space analysis, offline partition management, network/port scanning, process/task management, and a few Windows housekeeping tools — plus an optional AI assistant.

It is built with a strong bias toward native Win32 APIs (P/Invoke) instead of PowerShell or WMI wrappers wherever performance and reliability matter (disk access, SMART/NVMe health), so most tools run fast and work even on locked-down machines.

## Projects

The repository contains three .NET 8 projects (no solution file — build each individually or open the folder in your IDE):

- **S7Tool** — the main WPF application (MVVM via CommunityToolkit.Mvvm, DI via Microsoft.Extensions.DependencyInjection). `Views/MainWindow.xaml` is the dashboard; each tool is a separate window resolved through DI and registered in `App.xaml.cs`.
- **S7Tool.DiskEngine** — a low-level native disk engine (raw P/Invoke, no PowerShell/WMI): raw physical disk access, GPT table editing, sector-level clone/copy, and SMART (ATA passthrough) / NVMe health reporting. Shared by the main app and the WinPE tool.
- **S7Tool.DiskManagerPE** — a separate WPF app that runs as the shell of a custom WinPE boot image, used for offline disk partition resize/clone when Windows itself can't be touched (e.g. resizing the volume Windows is running from). Built into a bootable image by `tools/Build-WinPE.ps1` (requires the Windows ADK on the build machine) and shipped pre-built, embedded in the main app under `S7Tool/Resources/WinPE/`.

## Tools

- **Task Manager** — process list and monitoring.
- **Network Scanner** — LAN host discovery.
- **Port Scanner** — TCP port scanning.
- **Disk Health** — SMART (ATA) and NVMe health dashboard, read via raw passthrough, no WMI.
- **Disk Space Analyzer** — list, treemap, and pie-chart views of disk usage, each with a right-click context menu (open, open in Explorer, copy path, refresh, properties, delete).
- **Uninstaller** — installed-application removal.
- **App Installer** — browse a curated list of popular apps or search the winget catalog, check the ones you want, and install them all silently via winget.
- **Rename PC** — computer name management.
- **Windows Update Manager** — Windows Update control via the WUApi COM interface.
- **Disable Fast Startup** — one-click toggle.
- **WinPE Disk Manager** — boots a bundled WinPE image to resize/clone partitions offline, including the system volume; live visual feedback so the adjacent free-space bar shrinks correctly while dragging to extend a partition.
- **AI Helper** — chat assistant backed by Google Gemini, using your own API key (nothing is bundled or proxied).

The app's own UI can be switched between French and English at runtime with the small language toggle button next to the logo on the dashboard.

## Requirements

- Windows 10/11, x64.
- .NET 8 SDK to build from source (the published release is self-contained and needs nothing installed).
- Administrator privileges at runtime for most tools (raw disk access, network scanning, system configuration).
- Windows ADK only if you intend to rebuild the WinPE image yourself via `tools/Build-WinPE.ps1`.

## Building

Each project can be built independently with the .NET SDK, e.g.:

```
dotnet build S7Tool/S7Tool.csproj
dotnet build S7Tool.DiskManagerPE/S7Tool.DiskManagerPE.csproj
```

`S7Tool.DiskEngine` is referenced as a project dependency by both WPF apps and does not need to be built separately.

To publish a self-contained build of the main app:

```
dotnet publish S7Tool/S7Tool.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

`Resources/WinPE/boot.wim` and `boot.sdi` are tracked with Git LFS (they're pre-built binaries, not source) — make sure `git lfs pull` has run before building, otherwise the WinPE Disk Manager feature will be missing its boot image.

## Releases

Pre-built, self-contained binaries are published under [Releases](../../releases) — download, extract, and run `S7Tool.exe`. No installer, no dependencies.

## License

No license has been chosen yet for this project.
