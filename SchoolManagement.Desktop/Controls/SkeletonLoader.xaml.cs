using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - shimmering loading placeholder. Bars are generated from
/// <see cref="Rows"/>; the highlight is a single translated gradient, so the
/// cost stays constant no matter how many rows are shown.
/// </summary>
public partial class SkeletonLoader : UserControl
{
    private static readonly double[] WidthPattern = [1.0, 0.82, 0.92, 0.74, 0.88, 0.68];

    public static readonly DependencyProperty RowsProperty =
        DependencyProperty.Register(
            nameof(Rows), typeof(int), typeof(SkeletonLoader),
            new PropertyMetadata(4, OnLayoutChanged));

    public static readonly DependencyProperty RowHeightProperty =
        DependencyProperty.Register(
            nameof(RowHeight), typeof(double), typeof(SkeletonLoader),
            new PropertyMetadata(14d, OnLayoutChanged));

    public static readonly DependencyProperty RowSpacingProperty =
        DependencyProperty.Register(
            nameof(RowSpacing), typeof(double), typeof(SkeletonLoader),
            new PropertyMetadata(12d, OnLayoutChanged));

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(
            nameof(IsActive), typeof(bool), typeof(SkeletonLoader),
            new PropertyMetadata(true, OnAnimationStateChanged));

    private Storyboard? _storyboard;

    public SkeletonLoader()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
        BuildBars();
    }

    public int Rows
    {
        get => (int)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public double RowHeight
    {
        get => (double)GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SkeletonLoader)d).BuildBars();

    private static void OnAnimationStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SkeletonLoader)d).SyncAnimation();

    private void BuildBars()
    {
        if (BarHost is null)
        {
            return;
        }

        BarHost.Children.Clear();
        var count = Math.Max(1, Rows);
        for (var index = 0; index < count; index++)
        {
            var ratio = WidthPattern[index % WidthPattern.Length];

            var row = new Grid { Margin = new Thickness(0, 0, 0, RowSpacing) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ratio, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - ratio, GridUnitType.Star) });

            var bar = new Border
            {
                Height = Math.Max(4, RowHeight),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            bar.SetResourceReference(Border.BackgroundProperty, "Surface.Alt");
            bar.SetResourceReference(Border.CornerRadiusProperty, "Radius.Small");
            Grid.SetColumn(bar, 0);
            row.Children.Add(bar);

            BarHost.Children.Add(row);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => SyncAnimation();

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopAnimation();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => SyncAnimation();

    private void SyncAnimation()
    {
        if (!IsLoaded || !IsActive || !MotionSettings.IsMotionEnabled || ActualWidth <= 0)
        {
            StopAnimation();
            Shimmer.Visibility = Visibility.Collapsed;
            return;
        }

        Shimmer.Visibility = Visibility.Visible;
        if (_storyboard is not null)
        {
            return;
        }

        var width = Math.Max(200, ActualWidth);
        ShimmerTransform.X = -width;

        var animation = new DoubleAnimation(-width, width, new Duration(TimeSpan.FromMilliseconds(1200)))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };

        Storyboard.SetTarget(animation, ShimmerTransform);
        Storyboard.SetTargetProperty(animation, new PropertyPath(TranslateTransform.XProperty));

        _storyboard = new Storyboard();
        _storyboard.Children.Add(animation);
        _storyboard.Begin(this, isControllable: true);
    }

    private void StopAnimation()
    {
        if (_storyboard is null)
        {
            return;
        }

        _storyboard.Stop(this);
        _storyboard.Remove(this);
        _storyboard = null;
        ShimmerTransform.X = -600;
    }
}
