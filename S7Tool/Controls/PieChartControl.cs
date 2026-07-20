using S7Tool.Models;
using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace S7Tool.Controls;

public class PieChartControl : FrameworkElement
{
    private readonly List<(double Start, double Sweep, ChartLegendEntry Entry)> _slices = new();

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(PieChartControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public event Action<FileSystemNode>? ItemClicked;
    public event Action<FileSystemNode>? ItemRightClicked;

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PieChartControl)d;
        if (e.OldValue is INotifyCollectionChanged oldIncc) oldIncc.CollectionChanged -= control.OnSourceCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newIncc) newIncc.CollectionChanged += control.OnSourceCollectionChanged;
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        _slices.Clear();

        var entries = ItemsSource?.OfType<ChartLegendEntry>().Where(e => e.Percent > 0).ToList();
        double w = ActualWidth, h = ActualHeight;
        if (entries is null || entries.Count == 0 || w <= 1 || h <= 1) return;

        double size = Math.Min(w, h) - 12;
        if (size <= 4) return;
        var center = new Point(w / 2, h / 2);
        double radius = size / 2;

        double startAngle = -90;
        foreach (var entry in entries)
        {
            double sweep = entry.Percent / 100.0 * 360.0;
            DrawSlice(dc, center, radius, startAngle, sweep, entry.ColorBrush);
            _slices.Add((startAngle, sweep, entry));
            startAngle += sweep;
        }
    }

    private static void DrawSlice(DrawingContext dc, Point center, double radius, double startDeg, double sweepDeg, Brush brush)
    {
        if (sweepDeg <= 0.05) return;

        if (sweepDeg >= 359.9)
        {
            dc.DrawEllipse(brush, BorderPen, center, radius, radius);
            return;
        }

        double startRad = startDeg * Math.PI / 180;
        double endRad = (startDeg + sweepDeg) * Math.PI / 180;
        var startPoint = new Point(center.X + radius * Math.Cos(startRad), center.Y + radius * Math.Sin(startRad));
        var endPoint = new Point(center.X + radius * Math.Cos(endRad), center.Y + radius * Math.Sin(endRad));
        bool isLargeArc = sweepDeg > 180;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(center, isFilled: true, isClosed: true);
            ctx.LineTo(startPoint, isStroked: true, isSmoothJoin: false);
            ctx.ArcTo(endPoint, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }
        geometry.Freeze();
        dc.DrawGeometry(brush, BorderPen, geometry);
    }

    private static readonly Pen BorderPen = CreateBorderPen();
    private static Pen CreateBorderPen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), 1);
        pen.Freeze();
        return pen;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var node = FindNodeAt(e.GetPosition(this));
        if (node is not null) ItemClicked?.Invoke(node);
    }

    protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseRightButtonDown(e);
        var node = FindNodeAt(e.GetPosition(this));
        if (node is not null) ItemRightClicked?.Invoke(node);
    }

    private FileSystemNode? FindNodeAt(Point point)
    {
        double w = ActualWidth, h = ActualHeight;
        var center = new Point(w / 2, h / 2);
        double dx = point.X - center.X, dy = point.Y - center.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double maxRadius = Math.Min(w, h) / 2;
        if (dist > maxRadius) return null;

        double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
        if (angle < -90) angle += 360;

        foreach (var (start, sweep, entry) in _slices)
            if (angle >= start && angle < start + sweep && entry.Node is not null)
                return entry.Node;
        return null;
    }
}
