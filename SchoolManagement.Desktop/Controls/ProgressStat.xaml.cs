using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - circular progress with a title and a secondary line, used for a
/// rate or a paid ratio. The ring colour is a semantic tone, never a literal.
/// </summary>
public partial class ProgressStat : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(ProgressStat), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value), typeof(double), typeof(ProgressStat),
            new PropertyMetadata(0d, OnValueChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum), typeof(double), typeof(ProgressStat),
            new PropertyMetadata(100d, OnValueChanged));

    public static readonly DependencyProperty DisplayTextProperty =
        DependencyProperty.Register(
            nameof(DisplayText), typeof(string), typeof(ProgressStat), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ToneProperty =
        DependencyProperty.Register(
            nameof(Tone), typeof(StatusTone), typeof(ProgressStat),
            new PropertyMetadata(StatusTone.Accent, OnToneChanged));

    public static readonly DependencyProperty ShowPercentProperty =
        DependencyProperty.Register(
            nameof(ShowPercent), typeof(bool), typeof(ProgressStat), new PropertyMetadata(true));

    public ProgressStat()
    {
        InitializeComponent();
        UpdateVisuals();
        ApplyTone();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
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

    public string DisplayText
    {
        get => (string)GetValue(DisplayTextProperty);
        set => SetValue(DisplayTextProperty, value);
    }

    public StatusTone Tone
    {
        get => (StatusTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public bool ShowPercent
    {
        get => (bool)GetValue(ShowPercentProperty);
        set => SetValue(ShowPercentProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ProgressStat)d).UpdateVisuals();

    private static void OnToneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ProgressStat)d).ApplyTone();

    private void UpdateVisuals()
    {
        if (Ring is null)
        {
            return;
        }

        var maximum = Maximum <= 0 ? 100 : Maximum;
        var ratio = Math.Clamp(Value / maximum, 0, 1);

        Ring.Maximum = maximum;
        Ring.Value = Math.Clamp(Value, 0, maximum);
        PercentText.Text = ratio.ToString("P1", CultureInfo.CurrentCulture);
        PercentText.Visibility = ShowPercent ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyTone()
    {
        if (Ring is null)
        {
            return;
        }

        Ring.SetResourceReference(ProgressRing.ForegroundProperty, ToneResources.For(Tone).Solid);
    }
}
