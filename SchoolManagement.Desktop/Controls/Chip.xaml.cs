using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - a pill for a tag, a filter or a selected value. The optional
/// close button either runs <see cref="CloseCommand"/> or raises
/// <see cref="Closed"/> so the caller can remove the item.
/// </summary>
public partial class Chip : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(Chip), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(
            nameof(Glyph), typeof(string), typeof(Chip), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ToneProperty =
        DependencyProperty.Register(
            nameof(Tone), typeof(StatusTone), typeof(Chip),
            new PropertyMetadata(StatusTone.Neutral, OnToneChanged));

    public static readonly DependencyProperty ShowCloseProperty =
        DependencyProperty.Register(
            nameof(ShowClose), typeof(bool), typeof(Chip), new PropertyMetadata(false));

    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(
            nameof(CloseCommand), typeof(ICommand), typeof(Chip), new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(
            nameof(CommandParameter), typeof(object), typeof(Chip), new PropertyMetadata(null));

    public Chip()
    {
        InitializeComponent();
        ApplyTone();
    }

    public event EventHandler? Closed;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public StatusTone Tone
    {
        get => (StatusTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public bool ShowClose
    {
        get => (bool)GetValue(ShowCloseProperty);
        set => SetValue(ShowCloseProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private static void OnToneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Chip)d).ApplyTone();

    private void ApplyTone()
    {
        var palette = ToneResources.For(Tone);
        Surface.SetResourceReference(Border.BackgroundProperty, palette.Background);
        Surface.SetResourceReference(Border.BorderBrushProperty, palette.Border);
        Label.SetResourceReference(TextBlock.ForegroundProperty, palette.Foreground);
        GlyphBlock.SetResourceReference(TextBlock.ForegroundProperty, palette.Foreground);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        if (CloseCommand is { } command && command.CanExecute(CommandParameter))
        {
            command.Execute(CommandParameter);
            return;
        }

        Closed?.Invoke(this, EventArgs.Empty);
    }
}
