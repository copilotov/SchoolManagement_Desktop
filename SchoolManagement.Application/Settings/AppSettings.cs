namespace SchoolManagement.Application.Settings;

public enum AppTheme
{
    Light = 0,
    Dark = 1,
}

/// <summary>User preferences persisted in settings.json (never in the database).</summary>
public sealed record AppSettings
{
    public AppSettings()
    {
    }

    public AppSettings(string language, AppTheme theme)
    {
        Language = language;
        Theme = theme;
    }

    public string Language { get; init; } = "ar";

    public AppTheme Theme { get; init; } = AppTheme.Light;
}
