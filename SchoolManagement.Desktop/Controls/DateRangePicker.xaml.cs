using System.Windows;
using System.Windows.Controls;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - a designed from/to range. Both ends are nullable, so an open
/// range is represented without a sentinel date.
/// </summary>
public partial class DateRangePicker : UserControl
{
    public static readonly DependencyProperty StartDateProperty =
        DependencyProperty.Register(
            nameof(StartDate), typeof(DateTime?), typeof(DateRangePicker),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty EndDateProperty =
        DependencyProperty.Register(
            nameof(EndDate), typeof(DateTime?), typeof(DateRangePicker),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty StartLabelProperty =
        DependencyProperty.Register(
            nameof(StartLabel), typeof(string), typeof(DateRangePicker), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty EndLabelProperty =
        DependencyProperty.Register(
            nameof(EndLabel), typeof(string), typeof(DateRangePicker), new PropertyMetadata(string.Empty));

    public DateRangePicker() => InitializeComponent();

    public DateTime? StartDate
    {
        get => (DateTime?)GetValue(StartDateProperty);
        set => SetValue(StartDateProperty, value);
    }

    public DateTime? EndDate
    {
        get => (DateTime?)GetValue(EndDateProperty);
        set => SetValue(EndDateProperty, value);
    }

    public string StartLabel
    {
        get => (string)GetValue(StartLabelProperty);
        set => SetValue(StartLabelProperty, value);
    }

    public string EndLabel
    {
        get => (string)GetValue(EndLabelProperty);
        set => SetValue(EndLabelProperty, value);
    }
}
