using System.Windows;
using System.Windows.Controls;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - title, description, breadcrumb and an action host for a page.
/// Text is always supplied by the caller (already localized), never baked in.
/// </summary>
public partial class PageHeader : UserControl
{
    public static readonly DependencyProperty BreadcrumbProperty =
        DependencyProperty.Register(
            nameof(Breadcrumb), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionsContentProperty =
        DependencyProperty.Register(
            nameof(ActionsContent), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    public PageHeader() => InitializeComponent();

    public string Breadcrumb
    {
        get => (string)GetValue(BreadcrumbProperty);
        set => SetValue(BreadcrumbProperty, value);
    }

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

    public object? ActionsContent
    {
        get => GetValue(ActionsContentProperty);
        set => SetValue(ActionsContentProperty, value);
    }
}
