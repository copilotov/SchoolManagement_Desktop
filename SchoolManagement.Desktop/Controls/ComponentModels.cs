using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - the semantic tone of a badge, chip, capacity bar or progress
/// stat. The palette owns the actual colours; a component only names the role,
/// so a theme switch repaints every tone without touching a screen.
/// </summary>
public enum StatusTone
{
    Neutral = 0,
    Accent = 1,
    Success = 2,
    Warning = 3,
    Danger = 4,
    Info = 5,
}

/// <summary>Direction of a stat-card trend.</summary>
public enum TrendDirection
{
    Flat = 0,
    Up = 1,
    Down = 2,
}

/// <summary>DataTable row density. Compact is the 32px Fluent density.</summary>
public enum DataTableDensity
{
    Comfortable = 0,
    Compact = 1,
}

/// <summary>Chart family rendered by <see cref="ChartCard"/>.</summary>
public enum ChartKind
{
    Line = 0,
    Area = 1,
    Bar = 2,
}

/// <summary>
/// One column of a <see cref="DataTable"/>. It is a DependencyObject so a
/// screen can write <c>Header="{loc:Tr students.name}"</c> and have the header
/// follow a language switch, while the table rebuilds its columns from the
/// real values.
/// </summary>
public sealed class DataTableColumn : DependencyObject
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(DataTableColumn),
        new PropertyMetadata(string.Empty, OnColumnChanged));

    public static readonly DependencyProperty BindingPathProperty = DependencyProperty.Register(
        nameof(BindingPath), typeof(string), typeof(DataTableColumn),
        new PropertyMetadata(string.Empty, OnColumnChanged));

    public static readonly DependencyProperty SortMemberPathProperty = DependencyProperty.Register(
        nameof(SortMemberPath), typeof(string), typeof(DataTableColumn),
        new PropertyMetadata(null, OnColumnChanged));

    public static readonly DependencyProperty WidthProperty = DependencyProperty.Register(
        nameof(Width), typeof(DataGridLength), typeof(DataTableColumn),
        new PropertyMetadata(new DataGridLength(1, DataGridLengthUnitType.Star), OnColumnChanged));

    public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register(
        nameof(IsVisible), typeof(bool), typeof(DataTableColumn),
        new PropertyMetadata(true, OnColumnChanged));

    public static readonly DependencyProperty FilterTextProperty = DependencyProperty.Register(
        nameof(FilterText), typeof(string), typeof(DataTableColumn),
        new PropertyMetadata(string.Empty, OnColumnChanged));

    public static readonly DependencyProperty CellTemplateProperty = DependencyProperty.Register(
        nameof(CellTemplate), typeof(DataTemplate), typeof(DataTableColumn),
        new PropertyMetadata(null, OnColumnChanged));

    public static readonly DependencyProperty StringFormatProperty = DependencyProperty.Register(
        nameof(StringFormat), typeof(string), typeof(DataTableColumn),
        new PropertyMetadata(null, OnColumnChanged));

    public static readonly DependencyProperty IsNumericProperty = DependencyProperty.Register(
        nameof(IsNumeric), typeof(bool), typeof(DataTableColumn),
        new PropertyMetadata(false, OnColumnChanged));

    /// <summary>Raised when any column value changes, so the table can react.</summary>
    public event EventHandler<DataTableColumnChangedEventArgs>? Changed;

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string BindingPath
    {
        get => (string)GetValue(BindingPathProperty);
        set => SetValue(BindingPathProperty, value);
    }

    public string? SortMemberPath
    {
        get => (string?)GetValue(SortMemberPathProperty);
        set => SetValue(SortMemberPathProperty, value);
    }

    public DataGridLength Width
    {
        get => (DataGridLength)GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    public bool IsVisible
    {
        get => (bool)GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    public string? FilterText
    {
        get => (string?)GetValue(FilterTextProperty);
        set => SetValue(FilterTextProperty, value);
    }

    public DataTemplate? CellTemplate
    {
        get => (DataTemplate?)GetValue(CellTemplateProperty);
        set => SetValue(CellTemplateProperty, value);
    }

    public string? StringFormat
    {
        get => (string?)GetValue(StringFormatProperty);
        set => SetValue(StringFormatProperty, value);
    }

    public bool IsNumeric
    {
        get => (bool)GetValue(IsNumericProperty);
        set => SetValue(IsNumericProperty, value);
    }

    private static void OnColumnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DataTableColumn)d).Changed?.Invoke(d, new DataTableColumnChangedEventArgs(e.Property.Name));
}

/// <summary>
/// Names the column property that changed. The table rebuilds its columns for a
/// structural change (path, template, format) but must not rebuild them while
/// the user types in a header filter, so it needs the property name.
/// </summary>
public sealed class DataTableColumnChangedEventArgs(string propertyName) : EventArgs
{
    public string PropertyName { get; } = propertyName;
}

/// <summary>
/// Pure filtering and value access used by <see cref="DataTable"/>. Kept out of
/// the control so search, column filters and sorting are testable without a
/// WPF message loop.
/// </summary>
public static class DataTableFilter
{
    private static readonly ConcurrentDictionary<(Type Type, string Path), PropertyInfo?> Cache = new();

    public static bool Matches(object? item, IReadOnlyList<DataTableColumn> columns, string? globalSearch)
    {
        if (item is null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(globalSearch))
        {
            var query = globalSearch.Trim();
            var hit = columns
                .Where(column => column.IsVisible && !string.IsNullOrWhiteSpace(column.BindingPath))
                .Any(column => Format(GetValue(item, column.BindingPath))!.Contains(query, StringComparison.OrdinalIgnoreCase));

            if (!hit)
            {
                return false;
            }
        }

        foreach (var column in columns)
        {
            if (!column.IsVisible || string.IsNullOrWhiteSpace(column.BindingPath) || string.IsNullOrWhiteSpace(column.FilterText))
            {
                continue;
            }

            var filter = column.FilterText.Trim();
            var value = Format(GetValue(item, column.BindingPath));
            if (!value.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static object? GetValue(object item, string path)
    {
        if (item is null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        object? current = item;
        foreach (var segment in path.Split('.'))
        {
            if (current is null)
            {
                return null;
            }

            var property = Cache.GetOrAdd(
                (current.GetType(), segment),
                key => key.Type.GetProperty(
                    key.Path,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase));

            current = property?.GetValue(current);
        }

        return current;
    }

    public static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
        _ => value.ToString() ?? string.Empty,
    };

    public static int Compare(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        // A name column sorts the way a reader expects: case-insensitive and
        // culture-aware, so "amina" and "Amina" do not split into two blocks.
        if (left is string leftText && right is string rightText)
        {
            return string.Compare(leftText, rightText, StringComparison.CurrentCultureIgnoreCase);
        }

        if (left is IComparable comparable && left.GetType() == right.GetType())
        {
            return comparable.CompareTo(right);
        }

        return string.Compare(
            Format(left),
            Format(right),
            StringComparison.CurrentCultureIgnoreCase);
    }
}

/// <summary>
/// Maps an entity status string to a visual tone. The text itself always comes
/// from the translation files; this type only decides which semantic colour a
/// status uses, so attendance, finance, homework, sessions and licensing share
/// one visual vocabulary.
/// </summary>
public static class StatusBadgeMapper
{
    public static StatusTone MapTone(string? status)
    {
        var normalized = Normalize(status);
        return normalized switch
        {
            "present" or "paid" or "paidfull" or "fullypaid" or "completed" or "active"
                or "submitted" or "success" or "valid" or "on_time" => StatusTone.Success,

            "late" or "partial" or "partialpaid" or "pending" or "scheduled" or "postponed"
                or "warning" or "expiringsoon" or "graceperiod" or "inprogress"
                or "in_progress" => StatusTone.Warning,

            "absent" or "unexcused" or "unpaid" or "notpaid" or "overdue" or "cancelled"
                or "canceled" or "invalid" or "error" or "danger" or "readonly"
                or "missing" => StatusTone.Danger,

            "excused" or "exempt" or "exempted" or "info" or "notstarted" or "makeup"
                or "extra" or "new" => StatusTone.Info,

            "accent" => StatusTone.Accent,
            _ => StatusTone.Neutral,
        };
    }

    private static string Normalize(string? status)
        => (status ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
}

/// <summary>
/// One draggable block on a <see cref="WeekScheduleGrid"/>. DayIndex follows
/// the school week: 0 = Saturday ... 6 = Friday.
/// </summary>
public sealed partial class WeekScheduleItem : ObservableObject
{
    public WeekScheduleItem(
        int dayIndex,
        TimeSpan start,
        TimeSpan end,
        string title,
        string? subtitle = null,
        StatusTone tone = StatusTone.Accent,
        string? resourceKey = null,
        object? tag = null)
    {
        DayIndex = dayIndex;
        Start = start;
        End = end;
        Title = title;
        Subtitle = subtitle;
        Tone = tone;
        ResourceKey = resourceKey;
        Tag = tag;
    }

    public int DayIndex { get; set; }

    public TimeSpan Start { get; set; }

    public TimeSpan End { get; set; }

    public string Title { get; set; }

    public string? Subtitle { get; set; }

    public StatusTone Tone { get; set; }

    /// <summary>Group or teacher identity; overlaps with the same key conflict.</summary>
    public string? ResourceKey { get; set; }

    public object? Tag { get; set; }

    [ObservableProperty]
    private bool _isConflict;

    public TimeSpan Duration => End > Start ? End - Start : TimeSpan.Zero;
}

/// <summary>
/// Half-open overlap detection for the weekly grid: touching ranges do not
/// conflict, and two different resources may legitimately share a slot.
/// </summary>
public static class WeekScheduleConflictPolicy
{
    public static void MarkConflicts(IEnumerable<WeekScheduleItem> items)
    {
        var list = items.Where(item => item.End > item.Start).ToList();

        foreach (var item in list)
        {
            item.IsConflict = false;
        }

        for (var i = 0; i < list.Count; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
            {
                var left = list[i];
                var right = list[j];

                if (left.DayIndex != right.DayIndex || !Overlaps(left, right) || !SameResource(left, right))
                {
                    continue;
                }

                left.IsConflict = true;
                right.IsConflict = true;
            }
        }
    }

    public static bool Overlaps(WeekScheduleItem left, WeekScheduleItem right)
        => left.Start < right.End && left.End > right.Start;

    private static bool SameResource(WeekScheduleItem left, WeekScheduleItem right)
    {
        if (string.IsNullOrWhiteSpace(left.ResourceKey) || string.IsNullOrWhiteSpace(right.ResourceKey))
        {
            return true;
        }

        return string.Equals(left.ResourceKey, right.ResourceKey, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Parses and formats the <c>YYYY-MM</c> contract of <see cref="MonthPicker"/>.
/// Dates are UTC at midnight on the first day of the month, matching the app
/// date convention.
/// </summary>
public static class MonthYearParser
{
    private static readonly string[] Formats = ["yyyy-MM", "yyyy/MM", "yyyy-M", "yyyy/M"];

    public static bool TryParse(string? text, out DateTime month)
    {
        if (DateTime.TryParseExact(
                text?.Trim(),
                Formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            month = new DateTime(parsed.Year, parsed.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            return true;
        }

        month = default;
        return false;
    }

    public static string Format(DateTime month)
        => month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}

/// <summary>One step of a <see cref="Wizard"/>.</summary>
public sealed class WizardStep
{
    public WizardStep(string title, string? description = null, object? content = null)
    {
        Title = title;
        Description = description;
        Content = content;
    }

    public string Title { get; }

    public string? Description { get; }

    public object? Content { get; }
}

/// <summary>One filled/unfilled dot of a <see cref="RatingDots"/> control.</summary>
public sealed partial class RatingDot : ObservableObject
{
    [ObservableProperty]
    private bool _isFilled;
}

/// <summary>A localized month entry for <see cref="MonthPicker"/>.</summary>
public sealed record MonthOption(int Number, string Name);

/// <summary>A year entry for <see cref="MonthPicker"/>.</summary>
public sealed record YearOption(int Year)
{
    public override string ToString() => Year.ToString(CultureInfo.InvariantCulture);
}
