using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - initials avatar with an optional photo. When no tone is forced
/// the container colour is derived from the name, so a roster gets a stable
/// colour per person without storing one.
/// </summary>
public partial class Avatar : UserControl
{
    private static readonly StatusTone[] AutoTones =
    [
        StatusTone.Accent,
        StatusTone.Info,
        StatusTone.Success,
        StatusTone.Warning,
        StatusTone.Danger,
    ];

    public static readonly DependencyProperty DisplayNameProperty =
        DependencyProperty.Register(
            nameof(DisplayName), typeof(string), typeof(Avatar), new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty InitialsProperty =
        DependencyProperty.Register(
            nameof(Initials), typeof(string), typeof(Avatar), new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty ImageSourceProperty =
        DependencyProperty.Register(
            nameof(ImageSource), typeof(ImageSource), typeof(Avatar), new PropertyMetadata(null, OnContentChanged));

    public static readonly DependencyProperty SizeProperty =
        DependencyProperty.Register(
            nameof(Size), typeof(double), typeof(Avatar), new PropertyMetadata(40d, OnContentChanged));

    public static readonly DependencyProperty ToneProperty =
        DependencyProperty.Register(
            nameof(Tone), typeof(StatusTone), typeof(Avatar),
            new PropertyMetadata(StatusTone.Accent, OnContentChanged));

    public static readonly DependencyProperty AutoToneProperty =
        DependencyProperty.Register(
            nameof(AutoTone), typeof(bool), typeof(Avatar), new PropertyMetadata(true, OnContentChanged));

    public Avatar()
    {
        InitializeComponent();
        UpdateVisuals();
    }

    public string DisplayName
    {
        get => (string)GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    public string Initials
    {
        get => (string)GetValue(InitialsProperty);
        set => SetValue(InitialsProperty, value);
    }

    public ImageSource? ImageSource
    {
        get => (ImageSource?)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public StatusTone Tone
    {
        get => (StatusTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public bool AutoTone
    {
        get => (bool)GetValue(AutoToneProperty);
        set => SetValue(AutoToneProperty, value);
    }

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Avatar)d).UpdateVisuals();

    private void UpdateVisuals()
    {
        if (Surface is null)
        {
            return;
        }

        var size = Math.Max(16, Size);
        Surface.Width = size;
        Surface.Height = size;
        Surface.CornerRadius = new CornerRadius(size / 2);

        if (ImageSource is { } image)
        {
            Surface.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
            InitialsText.Visibility = Visibility.Collapsed;
            return;
        }

        var text = string.IsNullOrWhiteSpace(Initials) ? DeriveInitials(DisplayName) : Initials.Trim();
        InitialsText.Text = text;
        InitialsText.FontSize = Math.Max(10, size * 0.4);
        InitialsText.Visibility = Visibility.Visible;

        var palette = ToneResources.For(ResolveTone(text));
        Surface.SetResourceReference(Border.BackgroundProperty, palette.Background);
        InitialsText.SetResourceReference(TextBlock.ForegroundProperty, palette.Foreground);
    }

    private StatusTone ResolveTone(string seed)
    {
        if (!AutoTone)
        {
            return Tone;
        }

        var hash = 0;
        foreach (var character in seed)
        {
            hash = unchecked((hash * 31) + character);
        }

        var index = (hash & int.MaxValue) % AutoTones.Length;
        return AutoTones[index];
    }

    private static string DeriveInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var words = name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .ToArray();

        if (words.Length == 0)
        {
            return "?";
        }

        return string.Concat(words.Select(word => char.ToUpperInvariant(word[0])));
    }
}
