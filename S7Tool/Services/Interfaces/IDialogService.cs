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
    void ShowInfo(string message, string? title = null);
    void ShowSuccess(string message, string? title = null);
    void ShowWarning(string message, string? title = null);
    void ShowError(string message, string? title = null);

    bool Confirm(string message, string? title = null);
}
