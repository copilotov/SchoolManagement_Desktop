using System.Windows;
using System.Windows.Controls;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - label + editor + helper + error, so every form in the app shows a
/// required marker and an inline translated message in the same place.
/// </summary>
public partial class FormField : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(FormField), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty FieldContentProperty =
        DependencyProperty.Register(
            nameof(FieldContent), typeof(object), typeof(FormField), new PropertyMetadata(null));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(FormField), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ErrorTextProperty =
        DependencyProperty.Register(
            nameof(ErrorText), typeof(string), typeof(FormField), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsRequiredProperty =
        DependencyProperty.Register(
            nameof(IsRequired), typeof(bool), typeof(FormField), new PropertyMetadata(false));

    public FormField() => InitializeComponent();

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public object? FieldContent
    {
        get => GetValue(FieldContentProperty);
        set => SetValue(FieldContentProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string ErrorText
    {
        get => (string)GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }

    public bool IsRequired
    {
        get => (bool)GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }
}
