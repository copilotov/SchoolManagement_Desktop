using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.Behaviors;

/// <summary>Named entry animations available to any element.</summary>
public enum MotionKind
{
    None = 0,
    FadeIn = 1,
    SlideUp = 2,
    ScaleIn = 3,
}

/// <summary>
/// Attached behaviour that plays one of the shared storyboards from
/// <c>Resources/Design/Animations.xaml</c> when an element is loaded.
///
/// Usage:
/// <code>
/// &lt;Border behaviors:MotionBehavior.Play="SlideUp" /&gt;
/// </code>
///
/// The behaviour is a no-op when Windows "Animation effects" is off
/// (<see cref="MotionSettings.IsMotionEnabled"/>), when the storyboard resource
/// is missing, or when the element already owns a different render transform.
/// Nothing here mutates layout: only opacity and a render transform animate, so
/// a running animation can never trigger a re-measure of the page.
/// </summary>
public static class MotionBehavior
{
    public static readonly DependencyProperty PlayProperty =
        DependencyProperty.RegisterAttached(
            "Play",
            typeof(MotionKind),
            typeof(MotionBehavior),
            new PropertyMetadata(MotionKind.None, OnPlayChanged));

    /// <summary>
    /// Lifts the element by 2px while the pointer is over it. Only a card or a
    /// tile should opt in; a table row must not move under the cursor.
    /// </summary>
    public static readonly DependencyProperty HoverLiftProperty =
        DependencyProperty.RegisterAttached(
            "HoverLift",
            typeof(bool),
            typeof(MotionBehavior),
            new PropertyMetadata(false, OnHoverLiftChanged));

    private static readonly DependencyProperty IsLoadedHookProperty =
        DependencyProperty.RegisterAttached(
            "IsLoadedHook",
            typeof(bool),
            typeof(MotionBehavior),
            new PropertyMetadata(false));

    private static readonly DependencyProperty IsHoverHookProperty =
        DependencyProperty.RegisterAttached(
            "IsHoverHook",
            typeof(bool),
            typeof(MotionBehavior),
            new PropertyMetadata(false));

    public static MotionKind GetPlay(DependencyObject element) =>
        (MotionKind)element.GetValue(PlayProperty);

    public static void SetPlay(DependencyObject element, MotionKind value) =>
        element.SetValue(PlayProperty, value);

    public static bool GetHoverLift(DependencyObject element) =>
        (bool)element.GetValue(HoverLiftProperty);

    public static void SetHoverLift(DependencyObject element, bool value) =>
        element.SetValue(HoverLiftProperty, value);

    private static void OnPlayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if (GetPlay(element) == MotionKind.None)
        {
            return;
        }

        if (!(bool)element.GetValue(IsLoadedHookProperty))
        {
            element.SetValue(IsLoadedHookProperty, true);
            element.Loaded += OnElementLoaded;
        }
    }

    private static void OnHoverLiftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if (!GetHoverLift(element))
        {
            return;
        }

        if ((bool)element.GetValue(IsHoverHookProperty))
        {
            return;
        }

        element.SetValue(IsHoverHookProperty, true);
        element.MouseEnter += OnMouseEnter;
        element.MouseLeave += OnMouseLeave;
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        element.Loaded -= OnElementLoaded;
        element.SetValue(IsLoadedHookProperty, false);

        if (!MotionSettings.IsMotionEnabled)
        {
            return;
        }

        var kind = GetPlay(element);
        if (kind == MotionKind.None)
        {
            return;
        }

        var resourceKey = kind switch
        {
            MotionKind.FadeIn => "Anim.FadeIn",
            MotionKind.SlideUp => "Anim.SlideUp",
            MotionKind.ScaleIn => "Anim.ScaleIn",
            _ => null,
        };

        if (resourceKey is null || !PrepareTransform(element, kind))
        {
            return;
        }

        if (System.Windows.Application.Current?.TryFindResource(resourceKey) is Storyboard storyboard)
        {
            storyboard.Begin(element);
        }
    }

    private static bool PrepareTransform(FrameworkElement element, MotionKind kind)
    {
        switch (kind)
        {
            case MotionKind.SlideUp:
                if (element.RenderTransform is null || ReferenceEquals(element.RenderTransform, Transform.Identity))
                {
                    element.RenderTransform = new TranslateTransform();
                }

                return element.RenderTransform is TranslateTransform;

            case MotionKind.ScaleIn:
                if (element.RenderTransform is null || ReferenceEquals(element.RenderTransform, Transform.Identity))
                {
                    element.RenderTransform = new ScaleTransform(1, 1);
                    element.RenderTransformOrigin = new Point(0.5, 0.5);
                }

                return element.RenderTransform is ScaleTransform;

            default:
                return true;
        }
    }

    private static void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!MotionSettings.IsMotionEnabled || sender is not FrameworkElement element)
        {
            return;
        }

        if (element.RenderTransform is null || ReferenceEquals(element.RenderTransform, Transform.Identity))
        {
            element.RenderTransform = new TranslateTransform();
        }

        if (element.RenderTransform is not TranslateTransform)
        {
            return;
        }

        if (System.Windows.Application.Current?.TryFindResource("Anim.HoverLift.Up") is Storyboard storyboard)
        {
            storyboard.Begin(element);
        }
    }

    private static void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.RenderTransform is not TranslateTransform transform)
        {
            return;
        }

        if (!MotionSettings.IsMotionEnabled)
        {
            transform.Y = 0;
            return;
        }

        if (System.Windows.Application.Current?.TryFindResource("Anim.HoverLift.Down") is Storyboard storyboard)
        {
            storyboard.Begin(element);
        }
    }
}
