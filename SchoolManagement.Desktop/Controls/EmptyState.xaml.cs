using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - empty state shown by a list or a report when there is no data.
/// The illustration is a vector, so it stays crisp at 100-200% DPI and follows
/// the palette without an image asset.
/// </summary>
public partial class EmptyState : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(EmptyState), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(EmptyState), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionTextProperty =
        DependencyProperty.Register(
            nameof(ActionText), typeof(string), typeof(EmptyState), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionCommandProperty =
        DependencyProperty.Register(
            nameof(ActionCommand), typeof(ICommand), typeof(EmptyState), new PropertyMetadata(null));

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(
            nameof(Glyph), typeof(string), typeof(EmptyState), new PropertyMetadata(string.Empty));

    public EmptyState() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }
}
