using System.Globalization;
using System.Windows.Data;

namespace S7Tool.Helpers;

public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() == parameter as string;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Enum.Parse(targetType, (string)parameter) : Binding.DoNothing;
}
