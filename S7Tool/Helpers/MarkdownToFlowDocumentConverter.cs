using System.Globalization;
using System.Windows.Data;

namespace S7Tool.Helpers;

public class MarkdownToFlowDocumentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        MarkdownRenderer.Render(value as string ?? "");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
