using System.Windows;
using System.Windows.Media;

namespace SchoolManagement.Desktop.Behaviors;

/// <summary>
/// Mirrors a directional glyph when the element is laid out right-to-left.
///
/// A back arrow must point the other way in Arabic. WPF mirrors layout
/// automatically but a glyph is a picture, not a layout rule, so the icon has
/// to be flipped explicitly. Attach it only to directional icons (back,
/// forward, chevrons, undo/redo, import/export); a symmetric glyph such as a
/// plus or a trash can must never opt in.
///
/// Usage:
/// <code>
/// &lt;TextBlock behaviors:RtlMirrorBehavior.IsEnabled="True" Text="&#xE72B;" /&gt;
/// </code>
/// </summary>
public static class RtlMirrorBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(RtlMirrorBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty IsHookedProperty =
        DependencyProperty.RegisterAttached(
            "IsHooked",
            typeof(bool),
            typeof(RtlMirrorBehavior),
            new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if (!(bool)element.GetValue(IsHookedProperty))
        {
            element.SetValue(IsHookedProperty, true);
            element.Loaded += OnElementLoaded;
            element.IsVisibleChanged += OnIsVisibleChanged;
        }

        Apply(element);
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            Apply(element);
        }
    }

    private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            Apply(element);
        }
    }

    private static void Apply(FrameworkElement element)
    {
        if (!GetIsEnabled(element))
        {
            return;
        }

        var isRtl = element.FlowDirection == FlowDirection.RightToLeft;

        if (element.RenderTransform is ScaleTransform scale)
        {
            var target = isRtl ? -1d : 1d;
            if (!scale.ScaleX.Equals(target) || !scale.ScaleY.Equals(1d))
            {
                scale.ScaleX = target;
                scale.ScaleY = 1;
            }

            return;
        }

        if (element.RenderTransform is not null && !ReferenceEquals(element.RenderTransform, Transform.Identity))
        {
            // A different transform is already in use (a motion animation, for
            // example). Leave it alone rather than silently replacing it.
            return;
        }

        if (isRtl)
        {
            element.RenderTransform = new ScaleTransform(-1, 1);
            element.RenderTransformOrigin = new Point(0.5, 0.5);
        }
    }
}
