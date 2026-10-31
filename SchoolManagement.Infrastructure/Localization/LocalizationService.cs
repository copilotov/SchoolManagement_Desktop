using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Localization;

namespace SchoolManagement.Infrastructure.Localization;

/// <summary>
/// JSON-backed localization. Files are copied from the Flutter project as-is;
/// no key is ever hardcoded in XAML or in C#.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private const string DefaultLanguage = "ar";
    private static readonly string[] Supported = ["ar", "en", "fr"];
    private static readonly IReadOnlyDictionary<string, string> EmptyTranslations =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly string _translationsDirectory;
    private readonly ILogger<LocalizationService> _logger;
    private readonly object _sync = new();
    private readonly HashSet<string> _missingKeys = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);

    private IReadOnlyDictionary<string, string> _translations = EmptyTranslations;
    private string _currentLanguage = DefaultLanguage;
    private bool _isLoaded;

    public LocalizationService(string translationsDirectory, ILogger<LocalizationService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(translationsDirectory);
        ArgumentNullException.ThrowIfNull(logger);

        _translationsDirectory = translationsDirectory;
        _logger = logger;
    }

    public string CurrentLanguage
    {
        get
        {
            lock (_sync)
            {
                return _currentLanguage;
            }
        }
    }

    public bool IsRightToLeft => string.Equals(CurrentLanguage, "ar", StringComparison.Ordinal);

    public IReadOnlyList<string> SupportedLanguages => Supported;

    public IReadOnlyCollection<string> MissingKeys
    {
        get
        {
            lock (_sync)
            {
                return _missingKeys.ToArray();
            }
        }
    }

    public event EventHandler? LanguageChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task<bool> InitializeAsync(string? language = null, CancellationToken cancellationToken = default)
    {
        if (_isLoaded && (language is null || string.Equals(language, CurrentLanguage, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var requested = NormalizeLanguage(language);
        var loaded = await LoadFileAsync(requested, cancellationToken).ConfigureAwait(false);

        if (!loaded && !string.Equals(requested, DefaultLanguage, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Could not load '{Requested}'; falling back to the default language '{Default}'.",
                requested,
                DefaultLanguage);

            await LoadFileAsync(DefaultLanguage, cancellationToken).ConfigureAwait(false);
        }

        lock (_sync)
        {
            return _isLoaded;
        }
    }

    public async Task<bool> SetLanguageAsync(string language, CancellationToken cancellationToken = default)
    {
        var requested = NormalizeLanguage(language);
        var loaded = await LoadFileAsync(requested, cancellationToken).ConfigureAwait(false);

        if (!loaded && !string.Equals(requested, DefaultLanguage, StringComparison.Ordinal))
        {
            // Runtime switching must not silently jump to another language; keep
            // the current one and let the UI report the failure.
            _logger.LogWarning("Language switch to '{Requested}' failed; keeping '{Current}'.", requested, CurrentLanguage);
        }

        return loaded;
    }

    public string Tr(string key, IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        if (parameters is null || parameters.Count == 0)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        IReadOnlyDictionary<string, string> translations;
        lock (_sync)
        {
            if (!_isLoaded)
            {
                return LastSegment(key);
            }

            translations = _translations;
        }

        if (!translations.TryGetValue(key, out var template))
        {
            RecordMissingKey(key);
            return LastSegment(key);
        }

        if (parameters is null || parameters.Count == 0)
        {
            _cache[key] = template;
            return template;
        }

        return ReplaceParameters(template, parameters);
    }

    public string this[string key] => Tr(key);

    public bool HasKey(string key)
    {
        lock (_sync)
        {
            return _isLoaded && _translations.ContainsKey(key);
        }
    }

    private async Task<bool> LoadFileAsync(string language, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_translationsDirectory, $"{language}.json");

        try
        {
            var json = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            var root = JsonNode.Parse(json) as JsonObject
                ?? throw new FormatException($"Translation file '{path}' is not a JSON object.");

            var flattened = new Dictionary<string, string>(StringComparer.Ordinal);
            Flatten(root, string.Empty, flattened);

            lock (_sync)
            {
                _translations = flattened;
                _missingKeys.Clear();
                _cache.Clear();
                _currentLanguage = language;
                _isLoaded = true;
            }

            _logger.LogInformation("Loaded {Count} translation keys for '{Language}'.", flattened.Count, language);
            RaiseLanguageChanged();
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load translation file '{Path}'.", path);
            return false;
        }
    }

    private void RecordMissingKey(string key)
    {
        bool added;
        lock (_sync)
        {
            added = _missingKeys.Add(key);
        }

        if (added)
        {
            _logger.LogWarning("Missing translation key: {Key}", key);
        }
    }

    private void RaiseLanguageChanged()
    {
        LanguageChanged?.Invoke(this, EventArgs.Empty);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRightToLeft)));
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return DefaultLanguage;
        }

        var normalized = language.Trim().ToLowerInvariant();
        return Supported.Contains(normalized, StringComparer.Ordinal) ? normalized : DefaultLanguage;
    }

    private static void Flatten(JsonNode? node, string prefix, IDictionary<string, string> result)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var (key, value) in jsonObject)
                {
                    var fullKey = prefix.Length == 0 ? key : $"{prefix}.{key}";
                    Flatten(value, fullKey, result);
                }

                break;

            case JsonValue jsonValue:
                result[prefix] = jsonValue.TryGetValue<string>(out var text)
                    ? text
                    : jsonValue.ToJsonString();
                break;
        }
    }

    private static string ReplaceParameters(string template, IReadOnlyDictionary<string, string> parameters)
    {
        var result = template;
        foreach (var (key, value) in parameters)
        {
            result = result.Replace($"{{{key}}}", value, StringComparison.Ordinal);
        }

        return result;
    }

    private static string LastSegment(string key)
    {
        var separator = key.LastIndexOf('.');
        return separator >= 0 && separator < key.Length - 1
            ? key[(separator + 1)..]
            : key;
    }
}
