using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace S7Tool.ViewModels;

public partial class RenamePCViewModel : ObservableObject
{
    public event EventHandler? CloseRequested;

    public string CurrentName { get; } = Environment.MachineName;

    [ObservableProperty]
    private string newName = "";

    [ObservableProperty]
    private string statusText = "Prêt.";

    [RelayCommand]
    private void Rename()
    {
        string trimmed = NewName.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            StatusText = "Nom invalide.";
            return;
        }

        if (!Regex.IsMatch(trimmed, @"^[a-zA-Z0-9\-]+$"))
        {
            StatusText = "Caractères non autorisés.";
            return;
        }

        try
        {
            StatusText = "Renommage en cours...";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"Rename-Computer -NewName \"{trimmed}\" -Force",
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true
            };

            Process.Start(psi);

            StatusText = "Renommage fait avec succès";
        }
        catch (Exception ex)
        {
            StatusText = "Erreur : " + ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
