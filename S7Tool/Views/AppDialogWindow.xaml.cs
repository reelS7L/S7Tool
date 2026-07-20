using S7Tool.Services.Interfaces;
using System.Windows;
using System.Windows.Media;

namespace S7Tool.Views;

public partial class AppDialogWindow : Window
{
    public AppDialogWindow(string title, string message, DialogSeverity severity, bool isConfirm)
    {
        InitializeComponent();

        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;

        var (glyph, colorKey) = severity switch
        {
            DialogSeverity.Success => ("", "SuccessBrush"),
            DialogSeverity.Warning => ("", "WarningBrush"),
            DialogSeverity.Error => ("", "DangerBrush"),
            _ => ("", "AccentBrush"),
        };

        IconGlyph.Text = glyph;
        IconBadge.Background = (Brush)Application.Current.Resources[colorKey];

        if (isConfirm)
        {
            CancelButton.Visibility = Visibility.Visible;
            ConfirmButton.Content = "Oui";
            ConfirmButton.Style = (Style)Application.Current.Resources["DangerButtonStyle"];
        }
        else
        {
            CancelButton.Visibility = Visibility.Collapsed;
            ConfirmButton.Content = "OK";
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
