using S7Tool.Services.Interfaces;
using S7Tool.Views;
using System.Linq;
using System.Windows;

namespace S7Tool.Services;

public class DialogService : IDialogService
{
    public void ShowInfo(string message, string title = "Information") => Show(title, message, DialogSeverity.Info, isConfirm: false);

    public void ShowSuccess(string message, string title = "Succès") => Show(title, message, DialogSeverity.Success, isConfirm: false);

    public void ShowWarning(string message, string title = "Attention") => Show(title, message, DialogSeverity.Warning, isConfirm: false);

    public void ShowError(string message, string title = "Erreur") => Show(title, message, DialogSeverity.Error, isConfirm: false);

    public bool Confirm(string message, string title = "Confirmation") => Show(title, message, DialogSeverity.Warning, isConfirm: true);

    private static bool Show(string title, string message, DialogSeverity severity, bool isConfirm)
    {
        var dialog = new AppDialogWindow(title, message, severity, isConfirm)
        {
            Owner = GetActiveWindow()
        };

        return dialog.ShowDialog() == true;
    }

    private static Window? GetActiveWindow() =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current.MainWindow;
}
