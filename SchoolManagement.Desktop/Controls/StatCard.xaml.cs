using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - dashboard stat tile. The trend arrow is chosen by
/// <see cref="Trend"/> and always uses a semantic brush, so a positive delta is
/// green in both themes and a negative one is red.
/// </summary>
public partial class StatCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TrendProperty =
        DependencyProperty.Register(
            nameof(Trend), typeof(TrendDirection), typeof(StatCard),
            new PropertyMetadata(TrendDirection.Flat, OnTrendChanged));

    public static readonly DependencyProperty TrendTextProperty =
        DependencyProperty.Register(
            nameof(TrendText), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SparklineValuesProperty =
        DependencyProperty.Register(
            nameof(SparklineValues), typeof(IEnumerable), typeof(StatCard), new PropertyMetadata(null));

    public StatCard()
    {
        InitializeComponent();
        UpdateTrend();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public TrendDirection Trend
    {
        get => (TrendDirection)GetValue(TrendProperty);
        set => SetValue(TrendProperty, value);
    }

    public string TrendText
    {
        get => (string)GetValue(TrendTextProperty);
        set => SetValue(TrendTextProperty, value);
    }

    public IEnumerable? SparklineValues
    {
        get => (IEnumerable?)GetValue(SparklineValuesProperty);
        set => SetValue(SparklineValuesProperty, value);
    }

    private static void OnTrendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((StatCard)d).UpdateTrend();

    private void UpdateTrend()
    {
        var direction = Trend;
        TrendUpIcon.Visibility = direction == TrendDirection.Up ? Visibility.Visible : Visibility.Collapsed;
        TrendDownIcon.Visibility = direction == TrendDirection.Down ? Visibility.Visible : Visibility.Collapsed;
        TrendFlatIcon.Visibility = direction == TrendDirection.Flat ? Visibility.Visible : Visibility.Collapsed;

        var key = direction switch
        {
            TrendDirection.Up => "Success",
            TrendDirection.Down => "Danger",
            _ => "Text.Secondary",
        };
        TrendLabel.SetResourceReference(TextBlock.ForegroundProperty, key);
    }
}
