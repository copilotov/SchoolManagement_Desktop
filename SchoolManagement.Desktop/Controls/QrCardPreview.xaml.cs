using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - a QR card preview that scales inside its host. The printable PDF
/// and this preview share the same proportions, so what the user sees is what
/// comes out of the printer.
/// </summary>
public partial class QrCardPreview : UserControl
{
    public static readonly DependencyProperty QrImageProperty =
        DependencyProperty.Register(
            nameof(QrImage), typeof(ImageSource), typeof(QrCardPreview), new PropertyMetadata(null));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(QrCardPreview), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(
            nameof(Subtitle), typeof(string), typeof(QrCardPreview), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CodeProperty =
        DependencyProperty.Register(
            nameof(Code), typeof(string), typeof(QrCardPreview), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty FooterProperty =
        DependencyProperty.Register(
            nameof(Footer), typeof(string), typeof(QrCardPreview), new PropertyMetadata(string.Empty));

    public QrCardPreview() => InitializeComponent();

    public ImageSource? QrImage
    {
        get => (ImageSource?)GetValue(QrImageProperty);
        set => SetValue(QrImageProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    public string Footer
    {
        get => (string)GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }
}
