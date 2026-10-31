using SchoolManagement.Application.Localization;

namespace SchoolManagement.Application.Common;

/// <summary>The five failure categories agreed for the whole application.</summary>
public enum FailureKind
{
    Validation,
    NotFound,
    Database,
    License,
    Unexpected,
}

/// <summary>
/// An expected failure. It carries a translation key, never a hardcoded
/// user-visible message. The UI (or an application service) localizes it with
/// <see cref="LocalizationExtensions.ToDisplayMessage"/>.
/// </summary>
public sealed record Failure
{
    private static readonly IReadOnlyDictionary<string, string> EmptyParameters =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public Failure(
        FailureKind kind,
        string messageKey,
        string? detail = null,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(messageKey))
        {
            throw new ArgumentException("A failure must carry a translation key.", nameof(messageKey));
        }

        Kind = kind;
        MessageKey = messageKey;
        Detail = detail;
        Parameters = parameters ?? EmptyParameters;
    }

    public FailureKind Kind { get; }

    public string MessageKey { get; }

    /// <summary>Diagnostic only. Never displayed directly to the user.</summary>
    public string? Detail { get; }

    public IReadOnlyDictionary<string, string> Parameters { get; }

    public static Failure Validation(
        string messageKey,
        string? field = null,
        string? detail = null,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        var values = parameters is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(parameters, StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(field))
        {
            values["field"] = field;
        }

        return new Failure(FailureKind.Validation, messageKey, detail, values);
    }

    public static Failure NotFound(
        string messageKey,
        string? detail = null,
        IReadOnlyDictionary<string, string>? parameters = null)
        => new(FailureKind.NotFound, messageKey, detail, parameters);

    public static Failure Database(
        string messageKey = TranslationKeys.Errors.DatabaseFailure,
        string? detail = null,
        IReadOnlyDictionary<string, string>? parameters = null)
        => new(FailureKind.Database, messageKey, detail, parameters);

    public static Failure License(
        string messageKey,
        string? detail = null,
        IReadOnlyDictionary<string, string>? parameters = null)
        => new(FailureKind.License, messageKey, detail, parameters);

    public static Failure Unexpected(
        string messageKey = TranslationKeys.Errors.Unexpected,
        string? detail = null,
        IReadOnlyDictionary<string, string>? parameters = null)
        => new(FailureKind.Unexpected, messageKey, detail, parameters);

    public override string ToString() => $"{Kind}: {MessageKey}";
}
