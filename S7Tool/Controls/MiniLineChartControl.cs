using System.Windows;
using System.Windows.Media;

namespace S7Tool.Controls;

public class MiniLineChartControl : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IReadOnlyList<double>), typeof(MiniLineChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush), typeof(Brush), typeof(MiniLineChartControl),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Points
    {
        get => (IReadOnlyList<double>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var points = Points;
        double width = ActualWidth, height = ActualHeight;
        if (points is null || points.Count < 2 || width <= 0 || height <= 0) return;

        double min = points.Min();
        double max = points.Max();
        if (Math.Abs(max - min) < 0.001) { min -= 1; max += 1; }

        var pen = new Pen(LineBrush, 2) { LineJoin = PenLineJoin.Round };
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (int i = 0; i < points.Count; i++)
            {
                double x = width * i / (points.Count - 1);
                double y = height - (points[i] - min) / (max - min) * height;
                if (i == 0) ctx.BeginFigure(new Point(x, y), isFilled: false, isClosed: false);
                else ctx.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: true);
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }
}
