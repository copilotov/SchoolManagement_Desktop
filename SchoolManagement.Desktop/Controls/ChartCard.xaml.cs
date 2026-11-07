using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SchoolManagement.Desktop.Services;
using SkiaSharp;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - a chart tile (line, area or bar) wrapped in the app card.
///
/// LiveCharts2 needs concrete <see cref="SKColor"/> values, so the series and
/// the axes are rebuilt from the current palette whenever the theme changes.
/// <see cref="ThemeMonitor"/> is the pulse for that, because a
/// <c>DynamicResource</c> cannot reach inside a Skia paint. The rebuild is a
/// handful of objects, so it is cheap enough to run on a theme switch.
/// </summary>
public partial class ChartCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(ChartCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(ChartCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SummaryProperty =
        DependencyProperty.Register(
            nameof(Summary), typeof(string), typeof(ChartCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty EmptyTextProperty =
        DependencyProperty.Register(
            nameof(EmptyText), typeof(string), typeof(ChartCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(
            nameof(Kind), typeof(ChartKind), typeof(ChartCard),
            new PropertyMetadata(ChartKind.Line, OnChartChanged));

    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(
            nameof(Values), typeof(IEnumerable), typeof(ChartCard),
            new PropertyMetadata(null, OnChartChanged));

    public static readonly DependencyProperty LabelsProperty =
        DependencyProperty.Register(
            nameof(Labels), typeof(IEnumerable), typeof(ChartCard),
            new PropertyMetadata(null, OnChartChanged));

    public static readonly DependencyProperty ToneProperty =
        DependencyProperty.Register(
            nameof(Tone), typeof(StatusTone), typeof(ChartCard),
            new PropertyMetadata(StatusTone.Accent, OnChartChanged));

    public static readonly DependencyProperty ActionsContentProperty =
        DependencyProperty.Register(
            nameof(ActionsContent), typeof(object), typeof(ChartCard), new PropertyMetadata(null));

    public static readonly DependencyProperty ShowEmptyStateProperty =
        DependencyProperty.Register(
            nameof(ShowEmptyState), typeof(bool), typeof(ChartCard), new PropertyMetadata(false));

    public ChartCard()
    {
        InitializeComponent();
        Loaded += (_, _) => Rebuild();
        Unloaded += (_, _) => ThemeMonitor.ThemeChanged -= OnThemeChanged;
        ThemeMonitor.ThemeChanged += OnThemeChanged;
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

    public string Summary
    {
        get => (string)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public ChartKind Kind
    {
        get => (ChartKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public IEnumerable? Values
    {
        get => (IEnumerable?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IEnumerable? Labels
    {
        get => (IEnumerable?)GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    public StatusTone Tone
    {
        get => (StatusTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public object? ActionsContent
    {
        get => GetValue(ActionsContentProperty);
        set => SetValue(ActionsContentProperty, value);
    }

    public bool ShowEmptyState
    {
        get => (bool)GetValue(ShowEmptyStateProperty);
        set => SetValue(ShowEmptyStateProperty, value);
    }

    private static void OnChartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ChartCard)d).Rebuild();

    private void OnThemeChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        if (Chart is null)
        {
            return;
        }

        var values = ReadDoubles(Values);
        var labels = ReadStrings(Labels);
        var hasData = values.Count > 0;

        SetCurrentValue(ShowEmptyStateProperty, !hasData);

        if (!hasData)
        {
            Chart.Series = Array.Empty<ISeries>();
            return;
        }

        var solid = ResolveColor(ToneResources.For(Tone).Solid, SKColors.Teal);
        var container = ResolveColor(ToneResources.For(Tone).Background, SKColors.Transparent);
        var text = ResolveColor("Text.Secondary", new SKColor(0x63, 0x5E, 0x58));
        var grid = ResolveColor("Border.Subtle", new SKColor(0xE8, 0xE2, 0xD8));

        Chart.Series = [CreateSeries(values, solid, container)];

        Chart.XAxes =
        [
            new Axis
            {
                Labels = labels.Count > 0 ? labels : null,
                LabelsPaint = new SolidColorPaint(text) { SKTypeface = SKTypeface.Default },
                SeparatorsPaint = null,
                TextSize = 11,
                TicksPaint = null,
                LabelsRotation = labels.Count > 6 ? 30 : 0,
            },
        ];

        Chart.YAxes =
        [
            new Axis
            {
                LabelsPaint = new SolidColorPaint(text) { SKTypeface = SKTypeface.Default },
                SeparatorsPaint = new SolidColorPaint(grid) { StrokeThickness = 1 },
                TextSize = 11,
                TicksPaint = null,
                MinLimit = null,
            },
        ];
    }

    private ISeries CreateSeries(List<double> values, SKColor solid, SKColor container)
    {
        var stroke = new SolidColorPaint(solid) { StrokeThickness = 2 };
        var fill = Kind == ChartKind.Line
            ? null
            : new SolidColorPaint(container);

        return Kind switch
        {
            ChartKind.Bar => new ColumnSeries<double>
            {
                Values = values,
                Fill = new SolidColorPaint(solid),
                Stroke = null,
                MaxBarWidth = 28,
            },
            ChartKind.Area => new LineSeries<double>
            {
                Values = values,
                Fill = fill,
                Stroke = stroke,
                GeometrySize = 0,
                LineSmoothness = 0.4,
            },
            _ => new LineSeries<double>
            {
                Values = values,
                Fill = null,
                Stroke = stroke,
                GeometrySize = 6,
                GeometryStroke = stroke,
                GeometryFill = new SolidColorPaint(ResolveColor("Surface.Card", SKColors.White)),
                LineSmoothness = 0.4,
            },
        };
    }

    private static List<double> ReadDoubles(IEnumerable? source)
    {
        var list = new List<double>();
        if (source is null)
        {
            return list;
        }

        foreach (var item in source)
        {
            if (item is null)
            {
                continue;
            }

            try
            {
                list.Add(Convert.ToDouble(item, CultureInfo.InvariantCulture));
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                // A non-numeric sample is skipped instead of failing the whole chart.
            }
        }

        return list;
    }

    private static List<string> ReadStrings(IEnumerable? source)
        => source?.Cast<object?>().Select(item => item?.ToString() ?? string.Empty).ToList() ?? new List<string>();

    /// <summary>Reads a palette brush and converts it to the SKColor LiveCharts needs.</summary>
    private static SKColor ResolveColor(string resourceKey, SKColor fallback)
    {
        if (System.Windows.Application.Current?.TryFindResource(resourceKey) is SolidColorBrush brush)
        {
            var color = brush.Color;
            return new SKColor(color.R, color.G, color.B, color.A);
        }

        return fallback;
    }
}
