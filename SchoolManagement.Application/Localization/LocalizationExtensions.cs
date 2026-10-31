using SchoolManagement.Application.Common;

namespace SchoolManagement.Application.Localization;

public static class LocalizationExtensions
{
    /// <summary>
    /// Ergonomic Tr() for services:
    /// <c>localization.Tr("errors.fetch", ("error", ex.Message))</c>.
    /// </summary>
    public static string Tr(this ILocalizationService localization, string key, params (string Key, string Value)[] parameters)
    {
        ArgumentNullException.ThrowIfNull(localization);

        if (parameters.Length == 0)
        {
            return localization.Tr(key);
        }

        var values = new Dictionary<string, string>(parameters.Length, StringComparer.Ordinal);
        foreach (var (parameterKey, value) in parameters)
        {
            values[parameterKey] = value;
        }

        return localization.Tr(key, values);
    }

    /// <summary>Localizes a Failure for display without exposing its diagnostic detail.</summary>
    public static string ToDisplayMessage(this Failure failure, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(localization);

        return localization.Tr(failure.MessageKey, failure.Parameters);
    }
}
