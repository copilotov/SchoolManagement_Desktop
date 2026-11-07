using System.Collections;
using System.Windows;
using System.Windows.Media;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - a tiny dependency-free sparkline drawn with a polyline.
///
/// The control only paints; the caller supplies the brush through a
/// <c>DynamicResource</c>, so a theme switch repaints it with no code. It is
/// deliberately not an animation, so it costs nothing on a dense dashboard.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(
            nameof(Values),
            typeof(IEnumerable),
            typeof(Sparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(
            nameof(Stroke),
            typeof(Brush),
            typeof(Sparkline),
            new FrameworkPropertyMetadata(Brushes.Teal, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(
            nameof(Fill),
            typeof(Brush),
            typeof(Sparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(Sparkline),
            new FrameworkPropertyMetadata(2d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowAreaProperty =
        DependencyProperty.Register(
            nameof(ShowArea),
            typeof(bool),
            typeof(Sparkline),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? Values
    {
        get => (IEnumerable?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public bool ShowArea
    {
        get => (bool)GetValue(ShowAreaProperty);
        set => SetValue(ShowAreaProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var values = ReadValues();
        if (values.Count < 2)
        {
            return;
        }

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var thickness = Math.Max(1, StrokeThickness);
        var inset = thickness / 2;
        var minimum = values.Min();
        var maximum = values.Max();
        var span = maximum - minimum;
        if (span <= 0)
        {
            span = 1;
        }

        var step = (width - (inset * 2)) / (values.Count - 1);
        var points = new Point[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            var ratio = (values[index] - minimum) / span;
            points[index] = new Point(
                inset + (step * index),
                height - inset - (ratio * (height - (inset * 2))));
        }

        if (ShowArea && Fill is not null)
        {
            var area = new StreamGeometry();
            using (var context = area.Open())
            {
                context.BeginFigure(new Point(points[0].X, height), isFilled: true, isClosed: true);
                context.LineTo(points[0], isStroked: false, isSmoothJoin: false);
                foreach (var point in points.Skip(1))
                {
                    context.LineTo(point, isStroked: false, isSmoothJoin: false);
                }

                context.LineTo(new Point(points[^1].X, height), isStroked: false, isSmoothJoin: false);
            }

            area.Freeze();
            drawingContext.DrawGeometry(Fill, null, area);
        }

        if (Stroke is null)
        {
            return;
        }

        var line = new StreamGeometry();
        using (var context = line.Open())
        {
            context.BeginFigure(points[0], isFilled: false, isClosed: false);
            foreach (var point in points.Skip(1))
            {
                context.LineTo(point, isStroked: true, isSmoothJoin: true);
            }
        }

        line.Freeze();
        var pen = new Pen(Stroke, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        pen.Freeze();
        drawingContext.DrawGeometry(null, pen, line);
    }

    private List<double> ReadValues()
    {
        var list = new List<double>();
        if (Values is null)
        {
            return list;
        }

        foreach (var item in Values)
        {
            if (item is null)
            {
                continue;
            }

            try
            {
                list.Add(Convert.ToDouble(item, System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                // A non-numeric sample is ignored rather than crashing the paint pass.
            }
        }

        return list;
    }
}
