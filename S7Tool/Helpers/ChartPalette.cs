using System.Windows.Media;

namespace S7Tool.Helpers;

public static class ChartPalette
{
    private static readonly Color[] Colors =
    {
        Color.FromRgb(0x63, 0x66, 0xF1),
        Color.FromRgb(0x22, 0xC5, 0x5E),
        Color.FromRgb(0xF5, 0x9E, 0x0B),
        Color.FromRgb(0x38, 0xBD, 0xF8),
        Color.FromRgb(0xEF, 0x44, 0x44),
        Color.FromRgb(0xA8, 0x55, 0xF7),
        Color.FromRgb(0xF4, 0x72, 0xB6),
        Color.FromRgb(0x2D, 0xD4, 0xBF),
        Color.FromRgb(0xFB, 0x92, 0x3C),
        Color.FromRgb(0x84, 0xCC, 0x16),
    };

    public static Color ForIndex(int index) => Colors[((index % Colors.Length) + Colors.Length) % Colors.Length];

    public static SolidColorBrush BrushForIndex(int index)
    {
        var brush = new SolidColorBrush(ForIndex(index));
        brush.Freeze();
        return brush;
    }
}
