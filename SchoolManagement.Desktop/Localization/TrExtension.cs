using System.Windows.Data;
using System.Windows.Markup;

namespace SchoolManagement.Desktop.Localization;

/// <summary>
/// {loc:Tr students.title}
/// Returns a binding to the localization service indexer, so a language switch
/// updates every visible string instantly (no window recreation).
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var localization = LocalizationProvider.Current;
        if (localization is null || string.IsNullOrWhiteSpace(Key))
        {
            var separator = Key.LastIndexOf('.');
            return separator >= 0 && separator < Key.Length - 1 ? Key[(separator + 1)..] : Key;
        }

        var binding = new Binding($"[{Key}]")
        {
            Source = localization,
            Mode = BindingMode.OneWay,
        };

        return binding.ProvideValue(serviceProvider);
    }
}
