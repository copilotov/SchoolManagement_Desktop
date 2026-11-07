using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - a small stepper. Navigation is internal (Back / Next / Finish)
/// and the final step runs the caller's <see cref="FinishCommand"/>, so the
/// control stays presentational.
/// </summary>
public partial class Wizard : UserControl
{
    public static readonly DependencyProperty StepsProperty =
        DependencyProperty.Register(
            nameof(Steps), typeof(IEnumerable), typeof(Wizard),
            new PropertyMetadata(null, OnStepsChanged));

    public static readonly DependencyProperty CurrentIndexProperty =
        DependencyProperty.Register(
            nameof(CurrentIndex), typeof(int), typeof(Wizard),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnCurrentIndexChanged));

    public static readonly DependencyProperty FinishCommandProperty =
        DependencyProperty.Register(
            nameof(FinishCommand), typeof(ICommand), typeof(Wizard), new PropertyMetadata(null));

    public static readonly DependencyProperty ShowNavigationProperty =
        DependencyProperty.Register(
            nameof(ShowNavigation), typeof(bool), typeof(Wizard), new PropertyMetadata(true));

    public Wizard()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    public IEnumerable? Steps
    {
        get => (IEnumerable?)GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    public int CurrentIndex
    {
        get => (int)GetValue(CurrentIndexProperty);
        set => SetValue(CurrentIndexProperty, value);
    }

    public ICommand? FinishCommand
    {
        get => (ICommand?)GetValue(FinishCommandProperty);
        set => SetValue(FinishCommandProperty, value);
    }

    public bool ShowNavigation
    {
        get => (bool)GetValue(ShowNavigationProperty);
        set => SetValue(ShowNavigationProperty, value);
    }

    private static void OnStepsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Wizard)d).Refresh();

    private static void OnCurrentIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Wizard)d).Refresh();

    private List<WizardStep> ReadSteps()
        => Steps?.OfType<WizardStep>().ToList() ?? new List<WizardStep>();

    private void Refresh()
    {
        if (StepHost is null)
        {
            return;
        }

        var steps = ReadSteps();
        var count = steps.Count;
        var index = count == 0 ? 0 : Math.Clamp(CurrentIndex, 0, count - 1);
        if (index != CurrentIndex)
        {
            SetCurrentValue(CurrentIndexProperty, index);
        }

        BuildMarkers(steps, index);

        ContentHost.Content = count == 0 ? null : steps[index].Content;

        NavigationHost.Visibility = ShowNavigation ? Visibility.Visible : Visibility.Collapsed;
        BackButton.Visibility = index > 0 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Visibility = index < count - 1 ? Visibility.Visible : Visibility.Collapsed;
        FinishButton.Visibility = count > 0 && index == count - 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BuildMarkers(IReadOnlyList<WizardStep> steps, int current)
    {
        StepHost.Children.Clear();

        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var isCurrent = index == current;
            var isCompleted = index < current;

            var marker = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1),
            };
            marker.SetResourceReference(Border.BackgroundProperty, isCurrent ? "Accent" : isCompleted ? "Accent.Container" : "Surface.Alt");
            marker.SetResourceReference(Border.BorderBrushProperty, isCurrent || isCompleted ? "Accent" : "Border.Strong");

            var number = new TextBlock
            {
                Text = (index + 1).ToString(CultureInfo.InvariantCulture),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            number.SetResourceReference(TextBlock.ForegroundProperty, isCurrent ? "Text.OnAccent" : isCompleted ? "Accent.OnContainer" : "Text.Secondary");
            marker.Child = number;

            var label = new TextBlock
            {
                Text = step.Title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, isCurrent ? "Text.Primary" : "Text.Secondary");

            var group = new StackPanel { Orientation = Orientation.Horizontal };
            group.Children.Add(marker);
            group.Children.Add(label);
            StepHost.Children.Add(group);

            if (index < steps.Count - 1)
            {
                var connector = new Border
                {
                    Width = 32,
                    Height = 1,
                    Margin = new Thickness(8, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                connector.SetResourceReference(Border.BackgroundProperty, isCompleted ? "Accent" : "Border.Subtle");
                StepHost.Children.Add(connector);
            }
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (CurrentIndex > 0)
        {
            CurrentIndex--;
        }
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        var count = ReadSteps().Count;
        if (CurrentIndex < count - 1)
        {
            CurrentIndex++;
        }
    }

    private void OnFinishClick(object sender, RoutedEventArgs e)
    {
        if (FinishCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }
}
