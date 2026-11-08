using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SchoolManagement.Desktop.Controls;

/// <summary>Raised when a slot is dropped on a new day/hour.</summary>
public sealed class WeekScheduleMovedEventArgs(
    WeekScheduleItem item,
    int dayIndex,
    TimeSpan start,
    TimeSpan end) : EventArgs
{
    public WeekScheduleItem Item { get; } = item;

    public int DayIndex { get; } = dayIndex;

    public TimeSpan Start { get; } = start;

    public TimeSpan End { get; } = end;
}

/// <summary>Raised when the user clicks an empty cell while the grid is editable.</summary>
public sealed class WeekScheduleSlotRequestedEventArgs(int dayIndex, TimeSpan start) : EventArgs
{
    public int DayIndex { get; } = dayIndex;

    public TimeSpan Start { get; } = start;
}

/// <summary>Raised when a slot is double-clicked, so the caller can edit it.</summary>
public sealed class WeekScheduleSlotActivatedEventArgs(WeekScheduleItem item) : EventArgs
{
    public WeekScheduleItem Item { get; } = item;
}

/// <summary>
/// Phase D3 - the weekly timetable, days x hours.
///
/// Day 0 is Saturday and day 6 is Friday, the same contract as
/// <c>WeekDayCalculator</c>. Cells and slots share one <see cref="Grid"/>, so a
/// slot spans the hours it covers and the layout costs one element per cell
/// rather than a canvas with manual math. Dragging is opt-in: with
/// <see cref="IsEditable"/> on, dropping a slot on another cell moves it and
/// raises <see cref="SlotMoved"/>; the caller decides whether to persist.
///
/// <see cref="WeekScheduleConflictPolicy"/> marks overlaps in danger, so a
/// double booking is visible without reading the labels.
/// </summary>
public partial class WeekScheduleGrid : UserControl
{
    private static readonly DependencyPropertyKey HasConflictsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(HasConflicts), typeof(bool), typeof(WeekScheduleGrid), new PropertyMetadata(false));

    private static readonly DependencyPropertyKey IsEmptyPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsEmpty), typeof(bool), typeof(WeekScheduleGrid), new PropertyMetadata(true));

    public static readonly DependencyProperty HasConflictsProperty = HasConflictsPropertyKey.DependencyProperty;
    public static readonly DependencyProperty IsEmptyProperty = IsEmptyPropertyKey.DependencyProperty;

    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(
            nameof(Items), typeof(IEnumerable), typeof(WeekScheduleGrid),
            new PropertyMetadata(null, OnLayoutChanged));

    public static readonly DependencyProperty DayLabelsProperty =
        DependencyProperty.Register(
            nameof(DayLabels), typeof(IEnumerable), typeof(WeekScheduleGrid),
            new PropertyMetadata(null, OnLayoutChanged));

    public static readonly DependencyProperty StartHourProperty =
        DependencyProperty.Register(
            nameof(StartHour), typeof(int), typeof(WeekScheduleGrid),
            new PropertyMetadata(8, OnLayoutChanged));

    public static readonly DependencyProperty EndHourProperty =
        DependencyProperty.Register(
            nameof(EndHour), typeof(int), typeof(WeekScheduleGrid),
            new PropertyMetadata(19, OnLayoutChanged));

    public static readonly DependencyProperty HourHeightProperty =
        DependencyProperty.Register(
            nameof(HourHeight), typeof(double), typeof(WeekScheduleGrid),
            new PropertyMetadata(44d, OnLayoutChanged));

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(
            nameof(IsCompact), typeof(bool), typeof(WeekScheduleGrid),
            new PropertyMetadata(false, OnLayoutChanged));

    public static readonly DependencyProperty IsEditableProperty =
        DependencyProperty.Register(
            nameof(IsEditable), typeof(bool), typeof(WeekScheduleGrid), new PropertyMetadata(false));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(WeekScheduleGrid), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(WeekScheduleGrid), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty EmptyTextProperty =
        DependencyProperty.Register(
            nameof(EmptyText), typeof(string), typeof(WeekScheduleGrid), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionsContentProperty =
        DependencyProperty.Register(
            nameof(ActionsContent), typeof(object), typeof(WeekScheduleGrid), new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem), typeof(WeekScheduleItem), typeof(WeekScheduleGrid),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    private WeekScheduleItem? _dragItem;
    private Point _dragOrigin;
    private bool _isDragging;

    public WeekScheduleGrid()
    {
        InitializeComponent();
        Loaded += (_, _) => Rebuild();
    }

    public event EventHandler<WeekScheduleMovedEventArgs>? SlotMoved;

    /// <summary>Raised by a click on an empty cell when <see cref="IsEditable"/> is on.</summary>
    public event EventHandler<WeekScheduleSlotRequestedEventArgs>? SlotRequested;

    /// <summary>Raised by a double-click on a slot, whatever the edit mode.</summary>
    public event EventHandler<WeekScheduleSlotActivatedEventArgs>? SlotActivated;

    public bool HasConflicts => (bool)GetValue(HasConflictsProperty);

    public bool IsEmpty => (bool)GetValue(IsEmptyProperty);

    public IEnumerable? Items
    {
        get => (IEnumerable?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public IEnumerable? DayLabels
    {
        get => (IEnumerable?)GetValue(DayLabelsProperty);
        set => SetValue(DayLabelsProperty, value);
    }

    public int StartHour
    {
        get => (int)GetValue(StartHourProperty);
        set => SetValue(StartHourProperty, value);
    }

    public int EndHour
    {
        get => (int)GetValue(EndHourProperty);
        set => SetValue(EndHourProperty, value);
    }

    public double HourHeight
    {
        get => (double)GetValue(HourHeightProperty);
        set => SetValue(HourHeightProperty, value);
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public bool IsEditable
    {
        get => (bool)GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
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

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public object? ActionsContent
    {
        get => GetValue(ActionsContentProperty);
        set => SetValue(ActionsContentProperty, value);
    }

    public WeekScheduleItem? SelectedItem
    {
        get => (WeekScheduleItem?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((WeekScheduleGrid)d).Rebuild();

    /// <summary>Rebuilds the grid and re-evaluates conflicts. Cheap enough to call on a change.</summary>
    public void Rebuild()
    {
        if (GridHost is null)
        {
            return;
        }

        GridHost.Children.Clear();
        GridHost.RowDefinitions.Clear();
        GridHost.ColumnDefinitions.Clear();

        var start = Math.Clamp(StartHour, 0, 23);
        var end = Math.Clamp(EndHour, start + 1, 24);
        var hours = end - start;
        var hourColumnWidth = IsCompact ? 40d : 56d;
        var rowHeight = IsCompact ? 28d : Math.Max(28d, HourHeight);

        GridHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(hourColumnWidth) });
        for (var day = 0; day < 7; day++)
        {
            GridHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        GridHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(IsCompact ? 24d : 30d) });
        for (var index = 0; index < hours; index++)
        {
            GridHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowHeight) });
        }

        GridHost.Children.Add(BuildCell("Surface.Alt", isHeader: true, 0, 0));

        var labels = DayLabels?.Cast<object?>().Select(label => label?.ToString() ?? string.Empty).ToList()
            ?? new List<string>();

        for (var day = 0; day < 7; day++)
        {
            var header = BuildCell("Surface.Alt", isHeader: true, 0, day + 1);
            var text = new TextBlock
            {
                Text = day < labels.Count ? labels[day] : string.Empty,
                FontSize = IsCompact ? 10 : 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            text.SetResourceReference(TextBlock.ForegroundProperty, "Text.Secondary");
            header.Child = text;
            GridHost.Children.Add(header);
        }

        for (var row = 0; row < hours; row++)
        {
            var hour = start + row;
            var label = new TextBlock
            {
                Text = hour.ToString("00", CultureInfo.InvariantCulture) + ":00",
                FontSize = IsCompact ? 9 : 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Text.Secondary");
            Grid.SetRow(label, row + 1);
            Grid.SetColumn(label, 0);
            GridHost.Children.Add(label);

            for (var day = 0; day < 7; day++)
            {
                var cell = BuildCell(row % 2 == 0 ? "Surface.Card" : "Surface.RowAlt", isHeader: false, row + 1, day + 1);
                cell.Tag = (day, hour);

                if (IsEditable)
                {
                    cell.Cursor = Cursors.Cross;
                    cell.MouseLeftButtonDown += OnCellMouseDown;
                }

                GridHost.Children.Add(cell);
            }
        }

        var items = Items?.OfType<WeekScheduleItem>().ToList() ?? new List<WeekScheduleItem>();
        WeekScheduleConflictPolicy.MarkConflicts(items);

        SetValue(HasConflictsPropertyKey, items.Any(item => item.IsConflict));
        SetValue(IsEmptyPropertyKey, items.Count == 0);

        foreach (var item in items)
        {
            AddSlot(item, start, end);
        }
    }

    private static Border BuildCell(string backgroundKey, bool isHeader, int row, int column)
    {
        var cell = new Border
        {
            BorderThickness = new Thickness(0, 0, 1, 1),
            SnapsToDevicePixels = true,
        };
        cell.SetResourceReference(Border.BackgroundProperty, backgroundKey);
        cell.SetResourceReference(Border.BorderBrushProperty, "Border.Subtle");

        if (isHeader)
        {
            cell.BorderThickness = new Thickness(0, 0, 1, 1);
        }

        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        return cell;
    }

    private void AddSlot(WeekScheduleItem item, int startHour, int endHour)
    {
        var day = Math.Clamp(item.DayIndex, 0, 6);
        var begin = Math.Max(startHour, (int)Math.Floor(item.Start.TotalHours));
        var finish = Math.Min(endHour, (int)Math.Ceiling(item.End.TotalHours));

        if (finish <= begin)
        {
            return;
        }

        var palette = ToneResources.For(item.IsConflict ? StatusTone.Danger : item.Tone);

        var slot = new Border
        {
            Margin = new Thickness(2),
            Padding = IsCompact ? new Thickness(4, 2, 4, 2) : new Thickness(6, 4, 6, 4),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Cursor = IsEditable ? Cursors.Hand : Cursors.Arrow,
            Tag = item,
            ToolTip = string.IsNullOrWhiteSpace(item.Subtitle) ? item.Title : item.Title + " — " + item.Subtitle,
        };
        slot.SetResourceReference(Border.BackgroundProperty, palette.Background);
        slot.SetResourceReference(Border.BorderBrushProperty, palette.Border);
        slot.MouseLeftButtonDown += OnSlotMouseDown;
        slot.MouseMove += OnSlotMouseMove;
        slot.MouseLeftButtonUp += OnSlotMouseUp;

        var panel = new StackPanel();
        var title = new TextBlock
        {
            Text = item.Title,
            FontSize = IsCompact ? 9 : 11,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, palette.Foreground);
        panel.Children.Add(title);

        if (!IsCompact && !string.IsNullOrWhiteSpace(item.Subtitle))
        {
            var subtitle = new TextBlock
            {
                Text = item.Subtitle,
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 0),
                Opacity = 0.9,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
            };
            subtitle.SetResourceReference(TextBlock.ForegroundProperty, palette.Foreground);
            panel.Children.Add(subtitle);
        }

        slot.Child = panel;

        Grid.SetRow(slot, begin - startHour + 1);
        Grid.SetColumn(slot, day + 1);
        Grid.SetRowSpan(slot, Math.Max(1, finish - begin));
        GridHost.Children.Add(slot);
    }

    private void OnSlotMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: WeekScheduleItem item } slot)
        {
            return;
        }

        SetCurrentValue(SelectedItemProperty, item);

        if (e.ClickCount >= 2)
        {
            _dragItem = null;
            slot.ReleaseMouseCapture();
            SlotActivated?.Invoke(this, new WeekScheduleSlotActivatedEventArgs(item));
            e.Handled = true;
            return;
        }

        if (!IsEditable)
        {
            return;
        }

        _dragItem = item;
        _dragOrigin = e.GetPosition(this);
        _isDragging = false;
        slot.CaptureMouse();
        e.Handled = true;
    }

    private void OnSlotMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragItem is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var delta = e.GetPosition(this) - _dragOrigin;
        if (Math.Abs(delta.X) > 4 || Math.Abs(delta.Y) > 4)
        {
            _isDragging = true;
            if (sender is UIElement element)
            {
                element.Opacity = 0.6;
                element.IsHitTestVisible = false;
            }
        }
    }

    private void OnSlotMouseUp(object sender, MouseButtonEventArgs e)
    {
        var item = _dragItem;
        var wasDragging = _isDragging;

        _dragItem = null;
        _isDragging = false;

        if (sender is UIElement element)
        {
            element.Opacity = 1;
            element.IsHitTestVisible = true;
            element.ReleaseMouseCapture();
        }

        if (item is null || !wasDragging)
        {
            return;
        }

        var target = FindCell(e.GetPosition(GridHost));
        if (target is null)
        {
            return;
        }

        var (day, hour) = target.Value;
        if (day == item.DayIndex && hour == (int)Math.Floor(item.Start.TotalHours))
        {
            return;
        }

        var duration = item.Duration > TimeSpan.Zero ? item.Duration : TimeSpan.FromHours(1);
        item.DayIndex = day;
        item.Start = TimeSpan.FromHours(hour);
        item.End = item.Start + duration;

        Rebuild();
        SlotMoved?.Invoke(this, new WeekScheduleMovedEventArgs(item, day, item.Start, item.End));
    }

    private void OnCellMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsEditable || sender is not Border { Tag: ValueTuple<int, int> cell })
        {
            return;
        }

        SlotRequested?.Invoke(this, new WeekScheduleSlotRequestedEventArgs(cell.Item1, TimeSpan.FromHours(cell.Item2)));
        e.Handled = true;
    }

    private (int Day, int Hour)? FindCell(Point position)
    {
        var hit = GridHost.InputHitTest(position) as DependencyObject;
        while (hit is not null)
        {
            if (hit is Border { Tag: ValueTuple<int, int> cell })
            {
                return cell;
            }

            hit = VisualTreeHelper.GetParent(hit);
        }

        return null;
    }
}
