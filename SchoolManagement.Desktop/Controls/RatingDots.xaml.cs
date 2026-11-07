using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - a compact 0..Maximum rating. Filled dots follow the tone and the
/// control mirrors correctly: in RTL the first dot is on the right and a click
/// on the rightmost dot still means 1.
/// </summary>
public partial class RatingDots : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value), typeof(double), typeof(RatingDots),
            new FrameworkPropertyMetadata(
                0d,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum), typeof(int), typeof(RatingDots),
            new PropertyMetadata(5, OnValueChanged));

    public static readonly DependencyProperty ToneProperty =
        DependencyProperty.Register(
            nameof(Tone), typeof(StatusTone), typeof(RatingDots),
            new PropertyMetadata(StatusTone.Accent, OnValueChanged));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly), typeof(bool), typeof(RatingDots),
            new PropertyMetadata(false, OnValueChanged));

    public RatingDots()
    {
        InitializeComponent();
        BuildDots();
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public StatusTone Tone
    {
        get => (StatusTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((RatingDots)d).BuildDots();

    private void BuildDots()
    {
        if (Host is null)
        {
            return;
        }

        Host.Children.Clear();
        Host.Cursor = IsReadOnly ? Cursors.Arrow : Cursors.Hand;

        var count = Math.Clamp(Maximum, 1, 20);
        var filled = (int)Math.Round(Math.Clamp(Value, 0, count));
        var palette = ToneResources.For(Tone);

        for (var index = 0; index < count; index++)
        {
            var dot = new Ellipse
            {
                Width = 12,
                Height = 12,
                Margin = new Thickness(2),
                StrokeThickness = 1,
            };

            if (index < filled)
            {
                dot.SetResourceReference(Shape.FillProperty, palette.Solid);
                dot.SetResourceReference(Shape.StrokeProperty, palette.Solid);
            }
            else
            {
                dot.SetResourceReference(Shape.FillProperty, "Surface.Alt");
                dot.SetResourceReference(Shape.StrokeProperty, "Border.Strong");
            }

            Host.Children.Add(dot);
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsReadOnly)
        {
            return;
        }

        var count = Math.Clamp(Maximum, 1, 20);
        var width = Host.ActualWidth;
        if (width <= 0)
        {
            return;
        }

        var position = e.GetPosition(Host).X;
        if (FlowDirection == FlowDirection.RightToLeft)
        {
            position = width - position;
        }

        var index = (int)Math.Ceiling(Math.Clamp(position, 0, width - 0.01) / (width / count));
        SetCurrentValue(ValueProperty, (double)Math.Clamp(index, 1, count));
    }
}
