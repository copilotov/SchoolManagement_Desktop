using System.Windows;
using System.Windows.Controls;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - a titled block that groups related form fields on one card.
/// </summary>
public partial class FormSection : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(FormSection), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(FormSection), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SectionContentProperty =
        DependencyProperty.Register(
            nameof(SectionContent), typeof(object), typeof(FormSection), new PropertyMetadata(null));

    public FormSection() => InitializeComponent();

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

    public object? SectionContent
    {
        get => GetValue(SectionContentProperty);
        set => SetValue(SectionContentProperty, value);
    }
}
