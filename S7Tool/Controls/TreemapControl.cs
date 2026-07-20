using S7Tool.Helpers;
using S7Tool.Models;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace S7Tool.Controls;

public class TreemapControl : FrameworkElement
{
    private readonly List<(Rect Rect, FileSystemNode Node)> _layout = new();
    private readonly Typeface _typeface = new("Segoe UI");

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(TreemapControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public event Action<FileSystemNode>? ItemClicked;
    public event Action<FileSystemNode>? ItemRightClicked;

    public TreemapControl()
    {
        ClipToBounds = true;
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TreemapControl)d;
        if (e.OldValue is INotifyCollectionChanged oldIncc) oldIncc.CollectionChanged -= control.OnSourceCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newIncc) newIncc.CollectionChanged += control.OnSourceCollectionChanged;
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        _layout.Clear();

        double width = ActualWidth, height = ActualHeight;
        var surfaceBrush = TryFindResource("SurfaceBrush") as Brush ?? Brushes.Black;
        dc.DrawRectangle(surfaceBrush, null, new Rect(0, 0, Math.Max(width, 0), Math.Max(height, 0)));

        var items = ItemsSource?.OfType<FileSystemNode>().Where(n => n.SizeBytes > 0).ToList();
        if (items is null || items.Count == 0 || width <= 1 || height <= 1) return;

        double total = items.Sum(n => (double)n.SizeBytes);
        if (total <= 0) return;

        Squarify(items, new Rect(0, 0, width, height), total, dc);
    }

    private void Squarify(List<FileSystemNode> items, Rect bounds, double total, DrawingContext dc)
    {
        int i = 0;
        while (i < items.Count && bounds.Width > 0.5 && bounds.Height > 0.5)
        {
            double boundsArea = bounds.Width * bounds.Height;
            bool horizontal = bounds.Width >= bounds.Height;
            double shortSide = horizontal ? bounds.Height : bounds.Width;

            var rowAreas = new List<double>();
            var rowNodes = new List<FileSystemNode>();
            int j = i;

            while (j < items.Count)
            {
                double area = (double)items[j].SizeBytes / total * boundsArea;

                if (rowAreas.Count == 0)
                {
                    rowAreas.Add(area);
                    rowNodes.Add(items[j]);
                    j++;
                    continue;
                }

                double currentWorst = WorstRatio(rowAreas, shortSide);
                var trialAreas = new List<double>(rowAreas) { area };
                double trialWorst = WorstRatio(trialAreas, shortSide);

                if (trialWorst <= currentWorst)
                {
                    rowAreas.Add(area);
                    rowNodes.Add(items[j]);
                    j++;
                }
                else break;
            }

            double rowSum = rowAreas.Sum();
            double rowThickness = rowSum / shortSide;

            double offset = 0;
            for (int k = 0; k < rowNodes.Count; k++)
            {
                double itemLength = rowAreas[k] / rowThickness;
                Rect r = horizontal
                    ? new Rect(bounds.X, bounds.Y + offset, rowThickness, itemLength)
                    : new Rect(bounds.X + offset, bounds.Y, itemLength, rowThickness);

                DrawNode(dc, rowNodes[k], r, i + k);
                _layout.Add((r, rowNodes[k]));
                offset += itemLength;
            }

            bounds = horizontal
                ? new Rect(bounds.X + rowThickness, bounds.Y, Math.Max(0, bounds.Width - rowThickness), bounds.Height)
                : new Rect(bounds.X, bounds.Y + rowThickness, bounds.Width, Math.Max(0, bounds.Height - rowThickness));

            i = j;
        }
    }

    private static double WorstRatio(List<double> areas, double shortSide)
    {
        double sum = areas.Sum();
        double max = areas.Max();
        double min = areas.Min();
        if (sum <= 0 || min <= 0) return double.MaxValue;
        double s2 = shortSide * shortSide;
        return Math.Max(s2 * max / (sum * sum), sum * sum / (s2 * min));
    }

    private void DrawNode(DrawingContext dc, FileSystemNode node, Rect r, int colorIndex)
    {
        if (r.Width < 1 || r.Height < 1) return;

        var baseColor = ChartPalette.ForIndex(colorIndex);
        var fillColor = node.IsDirectory ? baseColor : Darken(baseColor, 0.3);
        var fill = new SolidColorBrush(fillColor);
        fill.Freeze();

        var inset = new Rect(r.X + 0.5, r.Y + 0.5, Math.Max(0, r.Width - 1), Math.Max(0, r.Height - 1));
        dc.DrawRectangle(fill, BorderPen, inset);

        if (r.Width > 34 && r.Height > 16)
        {
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var nameText = new FormattedText(node.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                _typeface, 11, Brushes.White, dpi)
            {
                MaxTextWidth = Math.Max(1, r.Width - 6),
                MaxTextHeight = Math.Max(1, r.Height - 4),
                Trimming = TextTrimming.CharacterEllipsis
            };
            dc.DrawText(nameText, new Point(r.X + 4, r.Y + 2));

            if (r.Height > 32)
            {
                var sizeText = new FormattedText(node.SizeDisplay, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    _typeface, 10, Brushes.WhiteSmoke, dpi)
                { MaxTextWidth = Math.Max(1, r.Width - 6) };
                dc.DrawText(sizeText, new Point(r.X + 4, r.Y + 16));
            }
        }
    }

    private static Color Darken(Color c, double amount)
    {
        byte D(byte channel) => (byte)Math.Max(0, channel - channel * amount);
        return Color.FromRgb(D(c.R), D(c.G), D(c.B));
    }

    private static readonly Pen BorderPen = CreateBorderPen();
    private static Pen CreateBorderPen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), 1);
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
        foreach (var (rect, node) in _layout)
            if (rect.Contains(point)) return node;
        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var node = FindNodeAt(e.GetPosition(this));
        if (node is not null)
        {
            ToolTip = $"{node.Name}\n{node.SizeDisplay} ({node.PercentOfParent:0.#}%)";
            Cursor = node.IsDirectory ? Cursors.Hand : Cursors.Arrow;
            return;
        }
        ToolTip = null;
        Cursor = Cursors.Arrow;
    }
}
