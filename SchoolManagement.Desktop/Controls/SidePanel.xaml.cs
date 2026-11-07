using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - a drawer that slides a form in from the trailing edge without
/// leaving the page. Closing either runs <see cref="CloseCommand"/> or sets
/// <see cref="IsOpen"/> to false.
/// </summary>
public partial class SidePanel : UserControl
{
    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(
            nameof(IsOpen), typeof(bool), typeof(SidePanel),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIsOpenChanged));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(SidePanel), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PanelContentProperty =
        DependencyProperty.Register(
            nameof(PanelContent), typeof(object), typeof(SidePanel), new PropertyMetadata(null));

    public static readonly DependencyProperty ActionsContentProperty =
        DependencyProperty.Register(
            nameof(ActionsContent), typeof(object), typeof(SidePanel), new PropertyMetadata(null));

    public static readonly DependencyProperty PanelWidthProperty =
        DependencyProperty.Register(
            nameof(PanelWidth), typeof(double), typeof(SidePanel), new PropertyMetadata(420d));

    public static readonly DependencyProperty ShowScrimProperty =
        DependencyProperty.Register(
            nameof(ShowScrim), typeof(bool), typeof(SidePanel), new PropertyMetadata(true));

    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(
            nameof(CloseCommand), typeof(ICommand), typeof(SidePanel), new PropertyMetadata(null));

    public SidePanel() => InitializeComponent();

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? PanelContent
    {
        get => GetValue(PanelContentProperty);
        set => SetValue(PanelContentProperty, value);
    }

    public object? ActionsContent
    {
        get => GetValue(ActionsContentProperty);
        set => SetValue(ActionsContentProperty, value);
    }

    public double PanelWidth
    {
        get => (double)GetValue(PanelWidthProperty);
        set => SetValue(PanelWidthProperty, value);
    }

    public bool ShowScrim
    {
        get => (bool)GetValue(ShowScrimProperty);
        set => SetValue(ShowScrimProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((SidePanel)d).AnimateOpen();

    private void AnimateOpen()
    {
        if (Panel is null)
        {
            return;
        }

        if (!IsOpen || !MotionSettings.IsMotionEnabled)
        {
            Panel.BeginAnimation(OpacityProperty, null);
            Panel.Opacity = 1;
            return;
        }

        Panel.Opacity = 0;
        Panel.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(160)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnScrimClick(object sender, MouseButtonEventArgs e) => Close();

    private void Close()
    {
        if (CloseCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
            return;
        }

        IsOpen = false;
    }
}
