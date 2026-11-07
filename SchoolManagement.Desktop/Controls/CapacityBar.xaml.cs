using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - "12/30" capacity meter. The colour follows the fill ratio; the
/// threshold is a warning only and never blocks anything.
/// </summary>
public partial class CapacityBar : UserControl
{
    private static readonly DependencyPropertyKey CapacityTextPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CapacityText), typeof(string), typeof(CapacityBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CapacityTextProperty = CapacityTextPropertyKey.DependencyProperty;

    public static readonly DependencyProperty CurrentProperty =
        DependencyProperty.Register(
            nameof(Current), typeof(double), typeof(CapacityBar),
            new PropertyMetadata(0d, OnValueChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum), typeof(double), typeof(CapacityBar),
            new PropertyMetadata(30d, OnValueChanged));

    public static readonly DependencyProperty CaptionProperty =
        DependencyProperty.Register(
            nameof(Caption), typeof(string), typeof(CapacityBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ShowValueProperty =
        DependencyProperty.Register(
            nameof(ShowValue), typeof(bool), typeof(CapacityBar), new PropertyMetadata(true));

    public CapacityBar()
    {
        InitializeComponent();
        UpdateVisuals();
    }

    public string CapacityText => (string)GetValue(CapacityTextProperty);

    public double Current
    {
        get => (double)GetValue(CurrentProperty);
        set => SetValue(CurrentProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public bool ShowValue
    {
        get => (bool)GetValue(ShowValueProperty);
        set => SetValue(ShowValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((CapacityBar)d).UpdateVisuals();

    private void UpdateVisuals()
    {
        if (Bar is null)
        {
            return;
        }

        var maximum = Maximum <= 0 ? 0 : Maximum;
        var current = Math.Max(0, Current);
        var ratio = maximum <= 0 ? 0 : current / maximum;

        Bar.Maximum = maximum <= 0 ? 1 : maximum;
        Bar.Value = maximum <= 0 ? 0 : Math.Min(current, maximum);

        var key = ratio >= 1 ? "Danger" : ratio >= 0.8 ? "Warning" : "Success";
        Bar.SetResourceReference(ForegroundProperty, key);

        SetValue(
            CapacityTextPropertyKey,
            string.Format(
                CultureInfo.CurrentCulture,
                "{0:0}/{1:0}",
                current,
                maximum));
    }
}
