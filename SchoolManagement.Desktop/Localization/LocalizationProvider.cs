using SchoolManagement.Application.Localization;

namespace SchoolManagement.Desktop.Localization;

/// <summary>
/// Runtime access point for XAML markup extensions. The App sets it after the
/// translation step and before the Shell is created. It is intentionally a
/// tiny static bridge; services should inject ILocalizationService instead.
/// </summary>
public static class LocalizationProvider
{
    public static ILocalizationService? Current { get; private set; }

    public static void Initialize(ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);
        Current = localizationService;
    }

    /// <summary>Tr() helper for code paths that cannot use constructor injection.</summary>
    public static string Tr(string key, IReadOnlyDictionary<string, string>? parameters = null)
    {
        var localization = Current;
        if (localization is null)
        {
            var separator = key.LastIndexOf('.');
            return separator >= 0 && separator < key.Length - 1 ? key[(separator + 1)..] : key;
        }

        return localization.Tr(key, parameters);
    }
}
