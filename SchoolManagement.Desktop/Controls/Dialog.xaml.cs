using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - the single dialog surface for confirmations and small forms.
/// Danger tone swaps the primary button to the danger role, so a destructive
/// confirmation reads correctly without a second control.
/// </summary>
public partial class Dialog : UserControl
{
    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(
            nameof(IsOpen), typeof(bool), typeof(Dialog),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIsOpenChanged));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(Dialog), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(Dialog), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DialogContentProperty =
        DependencyProperty.Register(
            nameof(DialogContent), typeof(object), typeof(Dialog), new PropertyMetadata(null));

    public static readonly DependencyProperty PrimaryTextProperty =
        DependencyProperty.Register(
            nameof(PrimaryText), typeof(string), typeof(Dialog), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PrimaryCommandProperty =
        DependencyProperty.Register(
            nameof(PrimaryCommand), typeof(ICommand), typeof(Dialog), new PropertyMetadata(null));

    public static readonly DependencyProperty SecondaryTextProperty =
        DependencyProperty.Register(
            nameof(SecondaryText), typeof(string), typeof(Dialog), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SecondaryCommandProperty =
        DependencyProperty.Register(
            nameof(SecondaryCommand), typeof(ICommand), typeof(Dialog), new PropertyMetadata(null));

    public static readonly DependencyProperty ToneProperty =
        DependencyProperty.Register(
            nameof(Tone), typeof(StatusTone), typeof(Dialog),
            new PropertyMetadata(StatusTone.Accent, OnToneChanged));

    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(
            nameof(CloseCommand), typeof(ICommand), typeof(Dialog), new PropertyMetadata(null));

    public Dialog()
    {
        InitializeComponent();
        ApplyTone();
    }

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

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? DialogContent
    {
        get => GetValue(DialogContentProperty);
        set => SetValue(DialogContentProperty, value);
    }

    public string PrimaryText
    {
        get => (string)GetValue(PrimaryTextProperty);
        set => SetValue(PrimaryTextProperty, value);
    }

    public ICommand? PrimaryCommand
    {
        get => (ICommand?)GetValue(PrimaryCommandProperty);
        set => SetValue(PrimaryCommandProperty, value);
    }

    public string SecondaryText
    {
        get => (string)GetValue(SecondaryTextProperty);
        set => SetValue(SecondaryTextProperty, value);
    }

    public ICommand? SecondaryCommand
    {
        get => (ICommand?)GetValue(SecondaryCommandProperty);
        set => SetValue(SecondaryCommandProperty, value);
    }

    public StatusTone Tone
    {
        get => (StatusTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    private static void OnToneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Dialog)d).ApplyTone();

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Dialog)d).AnimateOpen();

    private void ApplyTone()
    {
        if (PrimaryButton is null)
        {
            return;
        }

        PrimaryButton.SetResourceReference(
            FrameworkElement.StyleProperty,
            Tone == StatusTone.Danger ? "Button.Danger" : "Button.Primary");
    }

    private void AnimateOpen()
    {
        if (Card is null)
        {
            return;
        }

        if (!IsOpen || !MotionSettings.IsMotionEnabled)
        {
            Card.BeginAnimation(OpacityProperty, null);
            Card.Opacity = 1;
            return;
        }

        Card.Opacity = 0;
        Card.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(150)))
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
