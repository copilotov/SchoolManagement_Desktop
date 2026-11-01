namespace SchoolManagement.Application.Localization;

/// <summary>
/// Compile-time translation keys used from C#. XAML uses {loc:Tr ...} directly.
/// A test reflects over every constant here and verifies it exists in all three
/// language files, so a typo becomes a failing build instead of a runtime
/// "missing key" in front of the user.
/// </summary>
public static class TranslationKeys
{
    public static class App
    {
        public const string Name = "app.name";
        public const string Subtitle = "app.subtitle";
    }

    public static class Common
    {
        public const string Retry = "common.retry";
        public const string Error = "common.error";
        public const string Loading = "common.loading";
        public const string NoData = "common.noData";
        public const string Save = "common.save";
        public const string Cancel = "common.cancel";
        public const string Currency = "common.currency";
    }

    public static class Menu
    {
        public const string Dashboard = "menu.dashboard";
        public const string Students = "menu.students";
        public const string Groups = "menu.groups";
        public const string Finance = "menu.finance";
        public const string Settings = "menu.settings";
    }

    public static class Dashboard
    {
        public const string Title = "dashboard.title";
        public const string Welcome = "dashboard.welcome";
        public const string Statistics = "dashboard.statistics";
        public const string StudentsCount = "dashboard.studentsCount";
        public const string GroupsCount = "dashboard.groupsCount";
        public const string TodaySessions = "dashboard.todaySessions";
        public const string Outstanding = "dashboard.outstanding";
        public const string QuickActions = "dashboard.quickActions";
    }

    public static class Students
    {
        public const string Title = "students.title";
    }

    public static class Groups
    {
        public const string Title = "groups.title";
    }

    public static class Finance
    {
        public const string Title = "finance.title";
    }

    public static class Settings
    {
        public const string Title = "settings.title";
        public const string Appearance = "settings.appearance";
        public const string DarkMode = "settings.darkMode";
        public const string Language = "settings.language";
        public const string Arabic = "settings.arabic";
        public const string English = "settings.english";
        public const string French = "settings.french";
        public const string AppVersion = "settings.appVersion";
        public const string AboutDescription = "settings.aboutDescription";
    }

    public static class Messages
    {
        public const string SavedSuccessfully = "messages.savedSuccessfully";
    }

    public static class Errors
    {
        public const string Unexpected = "errors.unexpected";
        public const string SaveError = "errors.saveError";
        public const string DatabaseFailure = "errors.databaseFailure";
        public const string NotFound = "errors.notFound";
        public const string RequiredFields = "errors.requiredFields";
    }

    public static class Days
    {
        public const string Saturday = "days.saturday";
        public const string Sunday = "days.sunday";
        public const string Monday = "days.monday";
        public const string Tuesday = "days.tuesday";
        public const string Wednesday = "days.wednesday";
        public const string Thursday = "days.thursday";
        public const string Friday = "days.friday";
    }
}
