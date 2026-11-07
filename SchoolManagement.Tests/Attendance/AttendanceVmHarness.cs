using NSubstitute;
using SchoolManagement.Application.Attendance;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Homeworks;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Services;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.Time;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Domain.Common;
using SchoolManagement.Tests.Academic;

namespace SchoolManagement.Tests.Attendance;

/// <summary>
/// Builds the four Phase 7 ViewModels on top of substituted repositories and
/// ports, so every test runs the real use cases and the real ViewModel logic
/// without a WPF message loop or a database.
/// </summary>
internal sealed class AttendanceVmHarness
{
    public static readonly DateTime Day = new(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);

    private readonly ILicenseGuard _guard;

    public AttendanceVmHarness(ILicenseGuard? guard = null)
    {
        _guard = guard ?? Allowing();

        Groups
            .GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success([Summary()]));
        Groups
            .GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(Summary()));

        Sessions
            .GetListAsync(Arg.Any<SessionListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<SessionListItem>>.Success(Array.Empty<SessionListItem>()));
        Sessions
            .GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<SessionListItem?>.Success(SessionItem()));
        Sessions
            .GenerateForDateAsync(Arg.Any<DateTime>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SessionGenerationResult>.Success(new SessionGenerationResult(1, 0)));
        Sessions
            .CreateExtraAsync(Arg.Any<ExtraSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<ExtraSessionOutcome>.Success(new ExtraSessionOutcome(9, true)));
        Sessions
            .SetStatusAsync(
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        Sessions
            .UpdatePaymentAsync(
                Arg.Any<int>(),
                Arg.Any<bool>(),
                Arg.Any<string?>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        Sessions
            .GetGuardianContactsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GuardianContact>>.Success(
            [
                new GuardianContact(1, "Alice Anderson", "Parent One", "+213555000001"),
            ]));

        Attendance
            .GetRosterAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AttendanceRosterItem>>.Success(
            [
                RosterRow(1, AttendanceStatuses.Present),
                RosterRow(2, AttendanceStatuses.Present),
            ]));
        Attendance
            .UpsertManyAsync(
                Arg.Any<int>(),
                Arg.Any<IReadOnlyList<AttendanceEntry>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(2));
        Attendance
            .GetReportAsync(Arg.Any<AttendanceReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AttendanceReportRow>>.Success(
            [
                new AttendanceReportRow(1, "STU-2026-0001", "Alice Anderson", 1, "Group Alpha", 4, 3, 1, 0),
                new AttendanceReportRow(2, "STU-2026-0002", "Bob Brown", 1, "Group Alpha", 4, 2, 1, 1),
            ]));

        Homeworks
            .GetDueForSessionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkListItem>>.Success(Array.Empty<HomeworkListItem>()));
        Homeworks
            .GetListAsync(Arg.Any<HomeworkReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkListItem>>.Success(Array.Empty<HomeworkListItem>()));
        Homeworks
            .GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<HomeworkListItem?>.Success(HomeworkItem()));
        Homeworks
            .GetSubmissionBoardAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkSubmissionItem>>.Success(
            [
                SubmissionRow(1, HomeworkStatuses.Submitted),
                SubmissionRow(2, HomeworkStatuses.Pending),
            ]));
        Homeworks
            .SetSubmissionStatusAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<DateTime?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        Homeworks
            .AddAsync(Arg.Any<SchoolManagement.Domain.Entities.Homework>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(3));
        Homeworks
            .UpdateAsync(Arg.Any<SchoolManagement.Domain.Entities.Homework>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        Homeworks
            .GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<SchoolManagement.Domain.Entities.Homework?>.Success(
                new SchoolManagement.Domain.Entities.Homework
                {
                    Id = 3,
                    GroupId = 1,
                    SessionId = 4,
                    Title = "Old",
                    Description = "Old body",
                    DueDate = Day.AddDays(5),
                }));

        Payments
            .AddAsync(Arg.Any<SchoolManagement.Domain.Entities.Payment>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(50));

        Students
            .GetListAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentListItem>>.Success(
            [
                new StudentListItem(1, "STU-2026-0001", "Alice", "Anderson", null, StudentStatuses.Active, null, null),
                new StudentListItem(2, "STU-2026-0002", "Bob", "Brown", null, StudentStatuses.Active, null, null),
            ]));
        Students
            .GetAbsenceSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<StudentAbsenceSummary>.Success(new StudentAbsenceSummary(0, 0, 5, false, false)));

        References
            .GetSchoolNameAsync(Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("School One"));

        Notifications
            .HasUnreadAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        Notifications
            .AddAsync(Arg.Any<SchoolManagement.Domain.Entities.Notification>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(1));

        Sms
            .SendAsync(Arg.Any<SmsSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Result<SmsSendResult>.Success(
                new SmsSendResult(1, call.Arg<SmsSendRequest>().Phone, call.Arg<SmsSendRequest>().Body)));
        SmsLauncher.OpenAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>()).Returns(true);

        Scanner
            .ScanAsync(Arg.Any<QrScanRequest>())
            .Returns(new QrScanResult(new[] { 1 }));

        Dialogs
            .ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        Files
            .PickSavePathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns("C:\\temp\\attendance-report.pdf");
        ReportService
            .BuildPdfAsync(Arg.Any<AttendanceReportData>(), Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Success(new byte[] { 1, 2 }));
        ReportService
            .BuildExcelAsync(Arg.Any<AttendanceReportData>(), Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Success(new byte[] { 3, 4 }));

        Clock.LocalNow.Returns(new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc));
        Clock.UtcNow.Returns(new DateTime(2026, 11, 4, 8, 0, 0, DateTimeKind.Utc));

        License = BuildLicense(Localization);
    }

    public FakeLocalization Localization { get; } = new();

    public IUserDialogService Dialogs { get; } = Substitute.For<IUserDialogService>();

    public IFileDialogService Files { get; } = Substitute.For<IFileDialogService>();

    public IPdfPrintService Print { get; } = Substitute.For<IPdfPrintService>();

    public INavigationService Navigation { get; } = Substitute.For<INavigationService>();

    public IQrScannerDialogService Scanner { get; } = Substitute.For<IQrScannerDialogService>();

    public ISmsLauncher SmsLauncher { get; } = Substitute.For<ISmsLauncher>();

    public IGroupRepository Groups { get; } = Substitute.For<IGroupRepository>();

    public ISessionRepository Sessions { get; } = Substitute.For<ISessionRepository>();

    public IAttendanceRepository Attendance { get; } = Substitute.For<IAttendanceRepository>();

    public IHomeworkRepository Homeworks { get; } = Substitute.For<IHomeworkRepository>();

    public IStudentRepository Students { get; } = Substitute.For<IStudentRepository>();

    public INotificationRepository Notifications { get; } = Substitute.For<INotificationRepository>();

    public IPaymentRepository Payments { get; } = Substitute.For<IPaymentRepository>();

    public IReferenceDataRepository References { get; } = Substitute.For<IReferenceDataRepository>();

    public ISmsService Sms { get; } = Substitute.For<ISmsService>();

    public IAttendanceReportService ReportService { get; } = Substitute.For<IAttendanceReportService>();

    public ITrustedClock Clock { get; } = Substitute.For<ITrustedClock>();

    public LicenseViewModel License { get; }

    public static ILicenseGuard Allowing()
    {
        var guard = Substitute.For<ILicenseGuard>();
        guard.EnsureCanWrite().Returns(Result<bool>.Success(true));
        return guard;
    }

    public static ILicenseGuard Blocking()
    {
        var guard = Substitute.For<ILicenseGuard>();
        guard.EnsureCanWrite().Returns(Result<bool>.Fail(
            Failure.License(TranslationKeys.License.ErrorWriteBlocked)));
        return guard;
    }

    public AttendanceViewModel CreateAttendance() => new(
        new GetSessionListUseCase(Sessions),
        new GetGroupsUseCase(Groups),
        new GenerateSessionsUseCase(Sessions, _guard),
        new CreateExtraSessionUseCase(Sessions, _guard),
        new ChangeSessionStatusUseCase(Sessions, _guard),
        new SetSessionPaymentUseCase(Sessions, Payments, Clock, _guard),
        new SendSessionSmsUseCase(Sessions, Sms, Localization, _guard),
        Localization,
        Dialogs,
        SmsLauncher,
        Navigation,
        Clock,
        License);

    public TakeAttendanceViewModel CreateTakeAttendance() => new(
        new GetSessionAttendanceBoardUseCase(Sessions, Attendance, Homeworks),
        new SaveSessionAttendanceUseCase(
            Sessions,
            Attendance,
            new AutoNotificationService(Students, Notifications, Localization, Clock),
            _guard),
        Localization,
        Scanner,
        Navigation,
        License);

    public HomeworkViewModel CreateHomework() => new(
        new GetHomeworkReportUseCase(Homeworks, Groups, References, Clock),
        new GetHomeworkSubmissionBoardUseCase(Homeworks),
        new SaveHomeworkUseCase(Homeworks, Clock, _guard),
        new SetHomeworkSubmissionStatusUseCase(Homeworks, Clock, _guard),
        new GetGroupsUseCase(Groups),
        new GetSessionListUseCase(Sessions),
        Localization,
        Navigation,
        Clock,
        License);

    public AttendanceReportViewModel CreateReport() => new(
        new GetAttendanceReportUseCase(Attendance, References, Groups, Clock),
        ReportService,
        new GetGroupsUseCase(Groups),
        new GetStudentsUseCase(Students),
        Files,
        Print,
        Localization,
        Navigation,
        Clock);

    // Shared sample rows ---------------------------------------------------

    public static GroupSummary Summary(int id = 1, string name = "Group Alpha") => new(
        id,
        name,
        1,
        "Mathematics",
        1,
        "Teacher One",
        1,
        "Level One",
        1,
        "Grade One",
        30,
        1000,
        true,
        2);

    public static SessionListItem SessionItem(
        int id = 4,
        string status = SessionStatuses.Scheduled,
        string type = SessionTypes.Regular,
        string? cancelReason = null) => new(
        id,
        1,
        "Group Alpha",
        Day,
        new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc),
        type,
        status,
        "Algebra",
        false,
        null,
        null,
        0,
        0,
        cancelReason);

    public static AttendanceRosterItem RosterRow(int studentId, string status) => new(
        studentId,
        $"STU-2026-{studentId:0000}",
        studentId == 1 ? "Alice Anderson" : "Bob Brown",
        status,
        null,
        null);

    public static HomeworkListItem HomeworkItem(int id = 3) => new(
        id,
        1,
        4,
        4,
        "Exercise 1",
        "Page 10",
        Day.AddDays(5),
        false,
        2,
        1,
        1);

    public static HomeworkSubmissionItem SubmissionRow(int studentId, string status) => new(
        0,
        3,
        studentId,
        $"STU-2026-{studentId:0000}",
        studentId == 1 ? "Alice Anderson" : "Bob Brown",
        status,
        null,
        null);

    private static LicenseViewModel BuildLicense(FakeLocalization localization)
    {
        var service = Substitute.For<ILicenseService>();
        service.LoadAndVerifyAsync(Arg.Any<CancellationToken>())
            .Returns(LicenseInfo.Unlicensed("FINGERPRINT"));

        return new LicenseViewModel(
            service,
            Substitute.For<IActivationDialogService>(),
            localization);
    }
}
