using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using S7Tool.Services;
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
    private string statusText = LocalizationManager.T("Str_Common_Ready");

    [RelayCommand]
    private void Rename()
    {
        string trimmed = NewName.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            StatusText = LocalizationManager.T("Str_Rename_InvalidName");
            return;
        }

        if (!Regex.IsMatch(trimmed, @"^[a-zA-Z0-9\-]+$"))
        {
            StatusText = LocalizationManager.T("Str_Rename_ForbiddenChars");
            return;
        }

        try
        {
            StatusText = LocalizationManager.T("Str_Rename_InProgress");

            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"Rename-Computer -NewName \"{trimmed}\" -Force",
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true
            };

            Process.Start(psi);

            StatusText = LocalizationManager.T("Str_Rename_Success");
        }
        catch (Exception ex)
        {
            StatusText = LocalizationManager.T("Str_Rename_Error") + ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
