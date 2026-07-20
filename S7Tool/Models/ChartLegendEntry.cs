using System.Windows.Media;

namespace S7Tool.Models;

public record ChartLegendEntry(string Name, string SizeDisplay, double Percent, Brush ColorBrush, FileSystemNode? Node);
