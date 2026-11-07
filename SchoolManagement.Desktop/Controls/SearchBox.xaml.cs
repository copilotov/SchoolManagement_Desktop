using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - instant search field. Every keystroke raises the two-way
/// <see cref="Text"/> change, the clear button empties it, and Enter runs the
/// optional <see cref="SearchCommand"/>.
/// </summary>
public partial class SearchBox : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(SearchBox),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(
            nameof(Placeholder), typeof(string), typeof(SearchBox), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SearchCommandProperty =
        DependencyProperty.Register(
            nameof(SearchCommand), typeof(ICommand), typeof(SearchBox), new PropertyMetadata(null));

    public SearchBox() => InitializeComponent();

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public ICommand? SearchCommand
    {
        get => (ICommand?)GetValue(SearchCommandProperty);
        set => SetValue(SearchCommandProperty, value);
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(TextProperty, string.Empty);
        Input.Focus();
    }

    private void OnSurfaceMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!Input.IsKeyboardFocusWithin)
        {
            Input.Focus();
        }
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (SearchCommand is { } command && command.CanExecute(Text))
        {
            command.Execute(Text);
        }
    }
}
