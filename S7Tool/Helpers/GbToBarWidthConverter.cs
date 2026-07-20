using System.Globalization;
using System.Windows.Data;

namespace S7Tool.Helpers;

public class GbToBarWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double gb = value is double d ? d : 0;
        return Math.Max(36, gb * 2.2);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
