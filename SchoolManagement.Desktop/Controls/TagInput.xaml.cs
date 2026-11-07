using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchoolManagement.Desktop.Controls;

/// <summary>
/// Phase D3 - tag editor. The caller owns the <see cref="Tags"/> collection, so
/// a form can read it without a copy; the control only adds and removes.
/// </summary>
public partial class TagInput : UserControl
{
    public static readonly DependencyProperty TagsProperty =
        DependencyProperty.Register(
            nameof(Tags), typeof(ObservableCollection<string>), typeof(TagInput), new PropertyMetadata(null));

    public static readonly DependencyProperty NewTagTextProperty =
        DependencyProperty.Register(
            nameof(NewTagText), typeof(string), typeof(TagInput),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(
            nameof(Placeholder), typeof(string), typeof(TagInput), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MaxTagsProperty =
        DependencyProperty.Register(
            nameof(MaxTags), typeof(int), typeof(TagInput), new PropertyMetadata(10));

    public TagInput() => InitializeComponent();

    public ObservableCollection<string>? Tags
    {
        get => (ObservableCollection<string>?)GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    public string NewTagText
    {
        get => (string)GetValue(NewTagTextProperty);
        set => SetValue(NewTagTextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public int MaxTags
    {
        get => (int)GetValue(MaxTagsProperty);
        set => SetValue(MaxTagsProperty, value);
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        AddCurrentTag();
        e.Handled = true;
    }

    private void AddCurrentTag()
    {
        if (Tags is not { } tags)
        {
            return;
        }

        var value = NewTagText?.Trim();
        if (string.IsNullOrWhiteSpace(value) || tags.Count >= MaxTags)
        {
            SetCurrentValue(NewTagTextProperty, string.Empty);
            return;
        }

        if (!tags.Any(tag => string.Equals(tag, value, StringComparison.CurrentCultureIgnoreCase)))
        {
            tags.Add(value);
        }

        SetCurrentValue(NewTagTextProperty, string.Empty);
        Input.Focus();
    }

    private void OnRemoveTagClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string tag })
        {
            Tags?.Remove(tag);
        }
    }
}
