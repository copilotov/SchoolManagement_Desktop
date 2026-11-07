using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - one pill for every entity status. The text is already localized
/// by the caller; the control only paints the tone.
/// </summary>
public partial class StatusBadge : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(StatusBadge), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ToneProperty =
        DependencyProperty.Register(
            nameof(Tone), typeof(StatusTone), typeof(StatusBadge),
            new PropertyMetadata(StatusTone.Neutral, OnToneChanged));

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(
            nameof(Glyph), typeof(string), typeof(StatusBadge), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ShowDotProperty =
        DependencyProperty.Register(
            nameof(ShowDot), typeof(bool), typeof(StatusBadge), new PropertyMetadata(false));

    public StatusBadge()
    {
        InitializeComponent();
        ApplyTone();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public StatusTone Tone
    {
        get => (StatusTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public bool ShowDot
    {
        get => (bool)GetValue(ShowDotProperty);
        set => SetValue(ShowDotProperty, value);
    }

    private static void OnToneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((StatusBadge)d).ApplyTone();

    private void ApplyTone()
    {
        var palette = ToneResources.For(Tone);
        Surface.SetResourceReference(Border.BackgroundProperty, palette.Background);
        Surface.SetResourceReference(Border.BorderBrushProperty, palette.Border);
        Label.SetResourceReference(TextBlock.ForegroundProperty, palette.Foreground);
        GlyphBlock.SetResourceReference(TextBlock.ForegroundProperty, palette.Foreground);
        Dot.SetResourceReference(Shape.FillProperty, palette.Solid);
    }
}
