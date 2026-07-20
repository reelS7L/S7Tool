using System.Globalization;
using System.Windows.Data;

namespace S7Tool.DiskManagerPE.Helpers;

public class ProportionalBarWidthConverter : IMultiValueConverter
{
    public const double TotalBarWidth = 900;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2 || values[0] is not double partitionGb || values[1] is not double diskGb || diskGb <= 0)
            return 0.0;

        return Math.Max(30, partitionGb / diskGb * TotalBarWidth);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
