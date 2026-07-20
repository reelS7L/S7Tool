using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace S7Tool.Helpers;

public class HealthTierToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (value as string) switch
    {
        "Excellent" or "Bon" or "OK" => System.Windows.Application.Current.FindResource("SuccessBrush"),
        "Prudence" or "Attention" => System.Windows.Application.Current.FindResource("WarningBrush"),
        "Critique" => System.Windows.Application.Current.FindResource("DangerBrush"),
        _ => System.Windows.Application.Current.FindResource("TextMutedBrush")
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
