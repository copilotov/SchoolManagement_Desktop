using System.ComponentModel;
using NSubstitute;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Dashboard;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.Time;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Tests.Dashboard;

/// <summary>
/// In-memory localization double that also substitutes <c>{name}</c> placeholders,
/// so the dashboard tests can assert the final sentence a manager reads instead
/// of only the raw translation key.
/// </summary>
internal sealed class DashboardLocalization : ILocalizationService
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    public string CurrentLanguage => "en";

    public bool IsRightToLeft => false;

    public IReadOnlyList<string> SupportedLanguages { get; } = new[] { "en" };

    public IReadOnlyCollection<string> MissingKeys { get; } = Array.Empty<string>();

    public event EventHandler? LanguageChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Task<bool> InitializeAsync(string? language = null, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> SetLanguageAsync(string language, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public string Tr(string key, IReadOnlyDictionary<string, string>? parameters = null)
    {
        var template = Values.TryGetValue(key, out var value) ? value : key;

        if (parameters is null || parameters.Count == 0)
        {
            return template;
        }

        foreach (var (name, replacement) in parameters)
        {
            template = template.Replace(
                string.Concat("{", name, "}"),
                replacement,
                StringComparison.Ordinal);
        }

        return template;
    }

    public string this[string key] => Tr(key);

    public bool HasKey(string key) => Values.ContainsKey(key);

    public void RaiseLanguageChanged()
    {
        LanguageChanged?.Invoke(this, EventArgs.Empty);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}

/// <summary>
/// Builds the real <see cref="GetDashboardSnapshotUseCase"/> on top of substituted
/// repositories, so every dashboard test exercises the real aggregation rules and
/// the real ViewModel display logic without a message loop or a database.
/// </summary>
internal sealed class DashboardVmHarness
{
    /// <summary>Wednesday 4 November 2026, morning.</summary>
    public static readonly DateTime Day = new(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc);

    public IDashboardRepository Dashboard { get; } = Substitute.For<IDashboardRepository>();

    public IStudentRepository Students { get; } = Substitute.For<IStudentRepository>();

    public IGroupRepository Groups { get; } = Substitute.For<IGroupRepository>();

    public IPaymentRepository Payments { get; } = Substitute.For<IPaymentRepository>();

    public IReferenceDataRepository Reference { get; } = Substitute.For<IReferenceDataRepository>();

    public ISettingsRepository Settings { get; } = Substitute.For<ISettingsRepository>();

    public ITrustedClock Clock { get; } = Substitute.For<ITrustedClock>();

    public DashboardLocalization Localization { get; } = new();

    public INavigationService Navigation { get; } = Substitute.For<INavigationService>();

    public ILicenseService LicenseService { get; } = Substitute.For<ILicenseService>();

    public IActivationDialogService ActivationDialog { get; } = Substitute.For<IActivationDialogService>();

    public LicenseViewModel License { get; }

    public DashboardViewModel ViewModel { get; }

    public DashboardVmHarness(DateTime? now = null, LicenseInfo? license = null)
    {
        var instant = now ?? Day;
        Clock.LocalNow.Returns(instant);
        Clock.UtcNow.Returns(instant);

        Localization.Values[TranslationKeys.Common.Currency] = "DZD";
        Localization.Values[TranslationKeys.Common.Active] = "Active";
        Localization.Values[TranslationKeys.Dashboard.Today] = "Today";
        Localization.Values[TranslationKeys.Dashboard.GreetingWithName] = "{greeting}, {name}";
        Localization.Values[TranslationKeys.Dashboard.GreetingMorning] = "Good morning";
        Localization.Values[TranslationKeys.Dashboard.GreetingAfternoon] = "Good afternoon";
        Localization.Values[TranslationKeys.Dashboard.GreetingEvening] = "Good evening";
        Localization.Values[TranslationKeys.Dashboard.ArrearsCount] = "{count} students in arrears";
        Localization.Values[TranslationKeys.Dashboard.SessionStudents] = "{count} students";
        Localization.Values[TranslationKeys.Dashboard.RevenueSummary] = "6-month total: {amount}";
        Localization.Values[TranslationKeys.Dashboard.AttendanceSummary] = "Attendance rate: {rate}%";
        Localization.Values[TranslationKeys.Dashboard.LevelSummary] = "{count} levels";
        Localization.Values[TranslationKeys.Dashboard.SetupCount] = "{count} items";
        Localization.Values[TranslationKeys.Dashboard.SetupDone] = "Done";
        Localization.Values[TranslationKeys.Dashboard.SetupPending] = "To do";
        Localization.Values[TranslationKeys.Dashboard.AlertArrearsBody] = "{count} students · {amount}";
        Localization.Values[TranslationKeys.Dashboard.AlertAbsenceBody] = "{count} students over {limit}";
        Localization.Values[TranslationKeys.Dashboard.AlertMissingAttendanceBody] = "{count} sessions";
        Localization.Values[TranslationKeys.Dashboard.AlertLicenseBody] = "{days} days left";
        Localization.Values[TranslationKeys.Dashboard.NoLevel] = "Unplaced";

        Reference.GetSchoolNameAsync(Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("Test School"));
        Reference.GetAcademicLevelsAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success(Array.Empty<LookupItem>()));
        Reference.GetSubjectsAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success(Array.Empty<LookupItem>()));
        Reference.GetTeachersAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success(Array.Empty<LookupItem>()));

        Students.CountAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(0));
        Groups.GetSummariesAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success(Array.Empty<GroupSummary>()));
        Payments.GetMonthlyDuesAsync(
                Arg.Any<string>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MonthlyDue>>.Success(Array.Empty<MonthlyDue>()));
        Settings.GetAbsenceLimitAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(5));

        Dashboard.GetTodaySessionsAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DashboardSessionItem>>.Success(Array.Empty<DashboardSessionItem>()));
        Dashboard.GetAbsenceAlertsAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DashboardAbsenceAlert>>.Success(Array.Empty<DashboardAbsenceAlert>()));
        Dashboard.GetRevenueTrendAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DashboardMonthPoint>>.Success(Array.Empty<DashboardMonthPoint>()));
        Dashboard.GetWeekAttendanceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DashboardDayPoint>>.Success(Array.Empty<DashboardDayPoint>()));
        Dashboard.GetLevelDistributionAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DashboardLevelSlice>>.Success(Array.Empty<DashboardLevelSlice>()));

        LicenseService.LoadAndVerifyAsync(Arg.Any<CancellationToken>())
            .Returns(LicenseInfo.Unlicensed("fingerprint"));

        License = new LicenseViewModel(LicenseService, ActivationDialog, Localization);
        License.Apply(license ?? LicenseInfo.Unlicensed("fingerprint"));

        ViewModel = new DashboardViewModel(
            new GetDashboardSnapshotUseCase(
                Dashboard,
                Students,
                Groups,
                Payments,
                Reference,
                Settings,
                Clock),
            Localization,
            Navigation,
            License,
            Clock);
    }

    /// <summary>A populated school: students, groups, dues, sessions and charts.</summary>
    public DashboardVmHarness WithPopulatedSchool()
    {
        Students.CountAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(24));
        Groups.GetSummariesAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success(
            [
                Summary(1, "Group Alpha"),
                Summary(2, "Group Beta"),
                Summary(3, "Group Gamma"),
            ]));
        Payments.GetMonthlyDuesAsync(
                Arg.Any<string>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MonthlyDue>>.Success(
            [
                Due(1, paid: 200, outstanding: 100),
                Due(2, paid: 50, outstanding: 50),
                Due(3, paid: 100, outstanding: 0),
            ]));

        Reference.GetAcademicLevelsAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success([new LookupItem(1, "Level One")]));
        Reference.GetSubjectsAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success([new LookupItem(1, "Mathematics")]));
        Reference.GetTeachersAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success([new LookupItem(1, "Teacher One")]));

        Dashboard.GetTodaySessionsAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DashboardSessionItem>>.Success(
            [
                Session(2, "Group Beta", 14, "scheduled", "regular", students: 18, attendance: 0),
                Session(1, "Group Alpha", 9, "scheduled", "regular", students: 20, attendance: 0),
                Session(3, "Group Gamma", 11, "completed", "makeup", students: 12, attendance: 12),
            ]));

        Dashboard.GetRevenueTrendAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DashboardMonthPoint>>.Success(
            [
                new DashboardMonthPoint("2026-10", 100),
                new DashboardMonthPoint("2026-11", 250),
            ]));

        Dashboard.GetWeekAttendanceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DashboardDayPoint>>.Success(
            [
                new DashboardDayPoint(0, 8, 1, 1),
                new DashboardDayPoint(1, 4, 4, 0),
            ]));

        Dashboard.GetLevelDistributionAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DashboardLevelSlice>>.Success(
            [
                new DashboardLevelSlice("Level One", 15),
                new DashboardLevelSlice(null, 3),
            ]));

        return this;
    }

    public Task<bool> LoadAsync() => ViewModel.LoadAsync();

    public static GroupSummary Summary(int id, string name) => new(
        id,
        name,
        SubjectId: 1,
        SubjectName: "Mathematics",
        TeacherId: 1,
        TeacherName: "Teacher One",
        AcademicLevelId: 1,
        AcademicLevelName: "Level One",
        GradeId: 1,
        GradeName: "Grade One",
        MaxStudents: 30,
        MonthlyFee: 1000,
        IsActive: true,
        ActiveEnrollments: 10);

    public static MonthlyDue Due(int studentId, double paid, double outstanding) => new(
        studentId,
        $"STU-2026-{studentId:0000}",
        $"Student {studentId}",
        1,
        "Group Alpha",
        "2026-11",
        Expected: paid + outstanding,
        Paid: paid,
        Exempted: 0,
        Outstanding: outstanding);

    public static DashboardSessionItem Session(
        int id,
        string groupName,
        int startHour,
        string status,
        string type,
        int students,
        int attendance) => new(
        id,
        1,
        groupName,
        new DateTime(2026, 11, 4, startHour, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 11, 4, startHour + 1, 0, 0, DateTimeKind.Utc),
        status,
        type,
        students,
        attendance);
}
