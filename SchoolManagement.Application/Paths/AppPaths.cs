namespace SchoolManagement.Application.Paths;

/// <summary>All filesystem locations used by the desktop application.</summary>
public sealed record AppPaths(string DataDirectory, string TranslationsDirectory)
{
    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public string DatabaseFile => Path.Combine(DataDirectory, "school_management_db.sqlite");

    public string LicenseFile => Path.Combine(DataDirectory, "license.dat");

    public string BackupsDirectory => Path.Combine(DataDirectory, "backups");

    public string LogsDirectory => Path.Combine(DataDirectory, "logs");

    public static AppPaths CreateDefault()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = AppContext.BaseDirectory;
        }

        return new AppPaths(
            Path.Combine(localAppData, "SchoolManagement"),
            Path.Combine(AppContext.BaseDirectory, "Resources", "Translations"));
    }
}
