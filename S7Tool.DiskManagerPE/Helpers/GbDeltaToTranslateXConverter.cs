using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace S7Tool.DiskManagerPE.Helpers;

public class GbDeltaToTranslateXConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2 || values[0] is not double deltaGb || values[1] is not double diskGb || diskGb <= 0)
            return new TranslateTransform(0, 0);

        double pixelsPerGb = ProportionalBarWidthConverter.TotalBarWidth / diskGb;
        return new TranslateTransform(deltaGb * pixelsPerGb, 0);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
