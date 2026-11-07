using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Fluent-style progress ring. Draws a circular track plus an arc.
///
/// Determinate mode (IsIndeterminate = false) fills <see cref="Value"/> /
/// <see cref="Maximum"/> of the ring. Indeterminate mode rotates a 90 degree
/// arc continuously, which is the splash / loading spinner.
///
/// The animation is skipped when Windows "Animation effects" is disabled, so a
/// reduced-motion user gets a static arc instead of a spinner.
/// </summary>
public sealed class ProgressRing : FrameworkElement
{
    private static readonly DependencyPropertyKey IsAnimatingPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsAnimating),
            typeof(bool),
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty IsAnimatingProperty =
        IsAnimatingPropertyKey.DependencyProperty;

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(true, OnVisualPropertyChanged));

    public static readonly DependencyProperty IsIndeterminateProperty =
        DependencyProperty.Register(
            nameof(IsIndeterminate),
            typeof(bool),
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(true, OnVisualPropertyChanged));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(0d, OnVisualPropertyChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(double),
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(100d, OnVisualPropertyChanged));

    public static readonly DependencyProperty ThicknessProperty =
        DependencyProperty.Register(
            nameof(Thickness),
            typeof(double),
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(3d, OnVisualPropertyChanged));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Brush),
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(Brushes.Teal, OnVisualPropertyChanged));

    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(
            nameof(TrackBrush),
            typeof(Brush),
            typeof(ProgressRing),
            new FrameworkPropertyMetadata(Brushes.LightGray, OnVisualPropertyChanged));

    private readonly RotateTransform _rotation = new(0, 0.5, 0.5);
    private Storyboard? _storyboard;

    public ProgressRing()
    {
        RenderTransform = _rotation;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    public bool IsAnimating => (bool)GetValue(IsAnimatingProperty);

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ring = (ProgressRing)d;
        ring.InvalidateVisual();
        ring.SyncAnimation();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => SyncAnimation();

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopAnimation();

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => SyncAnimation();

    private void SyncAnimation()
    {
        if (!IsLoaded || !IsVisible || !IsActive || !IsIndeterminate || !MotionSettings.IsMotionEnabled)
        {
            StopAnimation();
            return;
        }

        if (_storyboard is not null)
        {
            return;
        }

        var animation = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromMilliseconds(900)))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };

        Storyboard.SetTarget(animation, this);
        Storyboard.SetTargetProperty(
            animation,
            new PropertyPath("RenderTransform.(RotateTransform.Angle)"));

        _storyboard = new Storyboard();
        _storyboard.Children.Add(animation);
        _storyboard.Begin(this, isControllable: true);
        SetValue(IsAnimatingPropertyKey, true);
    }

    private void StopAnimation()
    {
        if (_storyboard is null)
        {
            return;
        }

        _storyboard.Stop(this);
        _storyboard.Remove(this);
        _storyboard = null;
        _rotation.Angle = 0;
        SetValue(IsAnimatingPropertyKey, false);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (!IsActive)
        {
            return;
        }

        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
        {
            return;
        }

        var thickness = Math.Max(1, Math.Min(Thickness, size / 2));
        var radius = (size - thickness) / 2;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);

        var trackPen = new Pen(TrackBrush, thickness);
        drawingContext.DrawEllipse(null, trackPen, center, radius, radius);

        var arcPen = new Pen(Foreground, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };

        double sweep;
        if (IsIndeterminate)
        {
            sweep = 90;
        }
        else
        {
            var max = Maximum <= 0 ? 100 : Maximum;
            var ratio = Math.Clamp(Value / max, 0, 1);
            sweep = 360 * ratio;
        }

        if (sweep <= 0)
        {
            return;
        }

        // Arc starts at 12 o'clock (-90 degrees) and sweeps clockwise.
        var start = -90d;
        var end = start + sweep;

        var startPoint = PointOnCircle(center, radius, start);
        var endPoint = PointOnCircle(center, radius, end);

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(startPoint, isFilled: false, isClosed: false);
            context.ArcTo(
                endPoint,
                new Size(radius, radius),
                rotationAngle: 0,
                isLargeArc: sweep > 180,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: false);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, arcPen, geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(
            center.X + (radius * Math.Cos(radians)),
            center.Y + (radius * Math.Sin(radians)));
    }
}
