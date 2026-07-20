namespace S7Tool.Services.Interfaces;

public enum DialogSeverity
{
    Info,
    Success,
    Warning,
    Error
}

public interface IDialogService
{
    void ShowInfo(string message, string title = "Information");
    void ShowSuccess(string message, string title = "Succès");
    void ShowWarning(string message, string title = "Attention");
    void ShowError(string message, string title = "Erreur");

    bool Confirm(string message, string title = "Confirmation");
}
