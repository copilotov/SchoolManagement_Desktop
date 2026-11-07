using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SchoolManagement.Desktop.Controls;

/// <summary>Collapses an element when its boolean value is true.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

/// <summary>Visible when the value is non-null and non-blank.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Visible when the value is null or blank (placeholder use).</summary>
public sealed class InverseStringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Visible when the value is not null.</summary>
public sealed class ObjectToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Visible when the value is null (empty-state / placeholder use).</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Inverts a boolean, for IsEnabled bindings.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;
}

/// <summary>
/// Chooses the cell text alignment from the live flow direction. A numeric cell
/// aligns to the trailing edge (right in LTR, left in RTL) and a text cell to
/// the leading edge, so a column reads correctly after an Arabic switch without
/// rebuilding the table.
/// </summary>
public sealed class FlowDirectionToAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var rightToLeft = value is FlowDirection.RightToLeft;
        var trailing = string.Equals(parameter as string, "Trailing", StringComparison.OrdinalIgnoreCase);

        if (trailing)
        {
            return rightToLeft ? TextAlignment.Left : TextAlignment.Right;
        }

        return rightToLeft ? TextAlignment.Right : TextAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Formats a count with a caller-supplied localized pattern.</summary>
public sealed class CountFormatConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[1] is not string format || string.IsNullOrWhiteSpace(format))
        {
            return values.FirstOrDefault()?.ToString() ?? string.Empty;
        }

        return string.Format(CultureInfo.CurrentCulture, format, values[0]);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
