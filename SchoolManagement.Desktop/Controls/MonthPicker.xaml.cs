using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - month + year picker for a dues month. It stores a UTC date on the
/// first day of the month, matching the app date convention, and never depends
/// on the machine culture for the month names: the caller passes them.
/// </summary>
public partial class MonthPicker : UserControl
{
    public static readonly DependencyProperty SelectedMonthProperty =
        DependencyProperty.Register(
            nameof(SelectedMonth), typeof(DateTime?), typeof(MonthPicker),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedMonthChanged));

    public static readonly DependencyProperty MonthNamesProperty =
        DependencyProperty.Register(
            nameof(MonthNames), typeof(IEnumerable), typeof(MonthPicker),
            new PropertyMetadata(null, OnItemsChanged));

    public static readonly DependencyProperty StartYearProperty =
        DependencyProperty.Register(
            nameof(StartYear), typeof(int), typeof(MonthPicker),
            new PropertyMetadata(DateTime.UtcNow.Year - 5, OnItemsChanged));

    public static readonly DependencyProperty EndYearProperty =
        DependencyProperty.Register(
            nameof(EndYear), typeof(int), typeof(MonthPicker),
            new PropertyMetadata(DateTime.UtcNow.Year + 5, OnItemsChanged));

    public static readonly DependencyProperty ShowLabelsProperty =
        DependencyProperty.Register(
            nameof(ShowLabels), typeof(bool), typeof(MonthPicker), new PropertyMetadata(true));

    private bool _isSyncing;

    public MonthPicker()
    {
        InitializeComponent();
        BuildItems();
    }

    public DateTime? SelectedMonth
    {
        get => (DateTime?)GetValue(SelectedMonthProperty);
        set => SetValue(SelectedMonthProperty, value);
    }

    public IEnumerable? MonthNames
    {
        get => (IEnumerable?)GetValue(MonthNamesProperty);
        set => SetValue(MonthNamesProperty, value);
    }

    public int StartYear
    {
        get => (int)GetValue(StartYearProperty);
        set => SetValue(StartYearProperty, value);
    }

    public int EndYear
    {
        get => (int)GetValue(EndYearProperty);
        set => SetValue(EndYearProperty, value);
    }

    public bool ShowLabels
    {
        get => (bool)GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MonthPicker)d).BuildItems();

    private static void OnSelectedMonthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MonthPicker)d).SyncFromValue();

    private void BuildItems()
    {
        if (MonthBox is null || YearBox is null)
        {
            return;
        }

        var names = MonthNames?.Cast<object?>().Select(item => item?.ToString() ?? string.Empty).ToList();
        if (names is null || names.Count < 12)
        {
            names = CultureInfo.CurrentCulture.DateTimeFormat.MonthNames
                .Take(12)
                .ToList();
        }

        var months = new List<MonthOption>(12);
        for (var month = 1; month <= 12; month++)
        {
            months.Add(new MonthOption(month, names[month - 1]));
        }

        var start = Math.Min(StartYear, EndYear);
        var end = Math.Max(StartYear, EndYear);
        var years = new List<YearOption>();
        for (var year = start; year <= end; year++)
        {
            years.Add(new YearOption(year));
        }

        _isSyncing = true;
        try
        {
            MonthBox.ItemsSource = months;
            YearBox.ItemsSource = years;
        }
        finally
        {
            _isSyncing = false;
        }

        SyncFromValue();
    }

    private void SyncFromValue()
    {
        if (MonthBox is null || YearBox is null || _isSyncing)
        {
            return;
        }

        var month = SelectedMonth ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        _isSyncing = true;
        try
        {
            MonthBox.SelectedIndex = Math.Clamp(month.Month - 1, 0, 11);

            var years = YearBox.ItemsSource?.Cast<YearOption>().ToList() ?? new List<YearOption>();
            var index = years.FindIndex(item => item.Year == month.Year);
            if (index >= 0)
            {
                YearBox.SelectedIndex = index;
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncing || MonthBox?.SelectedItem is not MonthOption monthOption || YearBox?.SelectedItem is not YearOption yearOption)
        {
            return;
        }

        var value = new DateTime(yearOption.Year, monthOption.Number, 1, 0, 0, 0, DateTimeKind.Utc);
        SetCurrentValue(SelectedMonthProperty, value);
    }
}
