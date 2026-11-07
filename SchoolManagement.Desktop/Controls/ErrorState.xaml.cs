using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - failure surface. It carries the localized message and an optional
/// retry command; the ViewModel never has to know how the card looks.
/// </summary>
public partial class ErrorState : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(ErrorState), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(
            nameof(Message), typeof(string), typeof(ErrorState), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DetailsProperty =
        DependencyProperty.Register(
            nameof(Details), typeof(string), typeof(ErrorState), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RetryTextProperty =
        DependencyProperty.Register(
            nameof(RetryText), typeof(string), typeof(ErrorState), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RetryCommandProperty =
        DependencyProperty.Register(
            nameof(RetryCommand), typeof(ICommand), typeof(ErrorState), new PropertyMetadata(null));

    public ErrorState() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string Details
    {
        get => (string)GetValue(DetailsProperty);
        set => SetValue(DetailsProperty, value);
    }

    public string RetryText
    {
        get => (string)GetValue(RetryTextProperty);
        set => SetValue(RetryTextProperty, value);
    }

    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }
}
