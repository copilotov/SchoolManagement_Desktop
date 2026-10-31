using System.ComponentModel;

namespace SchoolManagement.Application.Localization;

/// <summary>
/// Loads ar/en/fr JSON files and exposes translated strings.
/// Implementations must raise <see cref="INotifyPropertyChanged.PropertyChanged"/>
/// for the indexer ("Item[]") when the language changes, so {loc:Tr ...}
/// bindings update instantly without recreating windows.
/// </summary>
public interface ILocalizationService : INotifyPropertyChanged
{
    string CurrentLanguage { get; }

    bool IsRightToLeft { get; }

    IReadOnlyList<string> SupportedLanguages { get; }

    IReadOnlyCollection<string> MissingKeys { get; }

    event EventHandler? LanguageChanged;

    /// <summary>Loads the language (or the default) for the first time.</summary>
    Task<bool> InitializeAsync(string? language = null, CancellationToken cancellationToken = default);

    /// <summary>Switches language at runtime. Returns false when the file cannot be loaded.</summary>
    Task<bool> SetLanguageAsync(string language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a nested key. A missing key returns its last segment and is
    /// recorded in <see cref="MissingKeys"/>; it never returns an empty string.
    /// </summary>
    string Tr(string key, IReadOnlyDictionary<string, string>? parameters = null);

    /// <summary>Indexer used by the WPF markup extension.</summary>
    string this[string key] { get; }

    bool HasKey(string key);
}
