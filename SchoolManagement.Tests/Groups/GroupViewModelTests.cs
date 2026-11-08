using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.Time;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Tests.Academic;

namespace SchoolManagement.Tests.Groups;

/// <summary>
/// Builds the six Phase 6 ViewModels on top of substituted repositories and
/// ports, so every test runs the real use cases and the real ViewModel logic
/// without a WPF message loop.
/// </summary>
internal sealed class GroupVmHarness
{
    private readonly ILicenseGuard _guard;

    public GroupVmHarness(ILicenseGuard? guard = null)
    {
        _guard = guard ?? Allowing();

        Groups
            .GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success(Array.Empty<GroupSummary>()));
        Groups
            .GetSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(Summary()));
        Groups
            .SaveAsync(Arg.Any<GroupSaveRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<GroupSaveOutcome>.Success(new GroupSaveOutcome(1, true)));
        Groups
            .GetDependenciesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<GroupDependencies>.Success(new GroupDependencies(0, 0, 0)));
        Groups
            .DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        References
            .GetAcademicLevelsAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success(
                new[] { new LookupItem(1, "Level One") }));
        References
            .GetGradesAsync(Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success(
                new[] { new LookupItem(1, "Grade One"), new LookupItem(2, "Grade Two") }));
        References
            .GetSubjectsAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success(
                new[] { new LookupItem(1, "Mathematics"), new LookupItem(2, "Physics") }));
        References
            .GetTeachersAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success(
                new[] { new LookupItem(1, "Teacher One") }));
        References
            .GetSchoolNameAsync(Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("School"));

        GradeRepository
            .GetSubjectIdsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<int>>.Success(new[] { 1, 2 }));

        Enrollments
            .GetByGroupAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<EnrollmentListItem>>.Success(Array.Empty<EnrollmentListItem>()));
        Enrollments
            .GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<Enrollment?>.Success(new Enrollment
            {
                Id = 1,
                StudentId = 1,
                GroupId = 1,
                EnrollmentDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                Status = EnrollmentStatuses.Active,
            }));
        Enrollments
            .EnrollAsync(Arg.Any<EnrollmentSaveRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<EnrollmentSaveOutcome>.Success(new EnrollmentSaveOutcome(1, true, false)));
        Enrollments
            .SetStatusAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        Students
            .GetListAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentListItem>>.Success(Array.Empty<StudentListItem>()));

        ScheduleRepository
            .GetSlotsAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Success(Array.Empty<ScheduleSlotItem>()));
        ScheduleRepository
            .GetConflictsAsync(
                Arg.Any<int>(),
                Arg.Any<int?>(),
                Arg.Any<int>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Success(Array.Empty<ScheduleSlotItem>()));
        ScheduleRepository
            .SaveSlotAsync(Arg.Any<ScheduleSaveRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<ScheduleSaveOutcome>.Success(new ScheduleSaveOutcome(1, true)));
        ScheduleRepository
            .DeleteSlotAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        Sessions
            .GenerateForDateAsync(Arg.Any<DateTime>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SessionGenerationResult>.Success(new SessionGenerationResult(1, 0)));

        Attendance
            .GetGroupStatisticsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<GroupAttendanceStats>.Success(new GroupAttendanceStats(1, 3, 1, 1, 1)));

        Payments
            .GetMonthlyDuesAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<MonthlyDue>>.Success(Array.Empty<MonthlyDue>()));

        Report
            .BuildFullReportAsync(Arg.Any<GroupReportData>(), Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Success(new byte[] { 1, 2 }));

        Files
            .PickSavePathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns("C:\\temp\\group-report-1.pdf");

        Dialogs
            .ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        Clock.LocalNow.Returns(new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc));
        Clock.UtcNow.Returns(new DateTime(2026, 11, 4, 8, 0, 0, DateTimeKind.Utc));

        License = BuildLicense(Localization);
    }

    public FakeLocalization Localization { get; } = new();

    public IUserDialogService Dialogs { get; } = Substitute.For<IUserDialogService>();

    public IFileDialogService Files { get; } = Substitute.For<IFileDialogService>();

    public IPdfPrintService Print { get; } = Substitute.For<IPdfPrintService>();

    public INavigationService Navigation { get; } = Substitute.For<INavigationService>();

    public IGroupRepository Groups { get; } = Substitute.For<IGroupRepository>();

    public IReferenceDataRepository References { get; } = Substitute.For<IReferenceDataRepository>();

    public IGradeRepository GradeRepository { get; } = Substitute.For<IGradeRepository>();

    public IEnrollmentRepository Enrollments { get; } = Substitute.For<IEnrollmentRepository>();

    public IStudentRepository Students { get; } = Substitute.For<IStudentRepository>();

    public IScheduleTemplateRepository ScheduleRepository { get; } = Substitute.For<IScheduleTemplateRepository>();

    public ISessionRepository Sessions { get; } = Substitute.For<ISessionRepository>();

    public IAttendanceRepository Attendance { get; } = Substitute.For<IAttendanceRepository>();

    public IPaymentRepository Payments { get; } = Substitute.For<IPaymentRepository>();

    public IGroupReportService Report { get; } = Substitute.For<IGroupReportService>();

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

    public GroupsViewModel CreateGroups() => new(
        new GetGroupsUseCase(Groups),
        new GetGroupLookupsUseCase(References),
        new GetGroupSubjectsUseCase(GradeRepository, References),
        new GetGroupSummaryUseCase(Groups),
        new SaveGroupUseCase(Groups, _guard),
        new RemoveGroupUseCase(Groups, _guard),
        new GenerateSessionsUseCase(Sessions, _guard),
        Localization,
        Dialogs,
        Navigation,
        Clock,
        License);

    public GroupScheduleViewModel CreateSchedule() => new(
        new GetGroupSummaryUseCase(Groups),
        new GetGroupScheduleUseCase(ScheduleRepository),
        new GetScheduleConflictsUseCase(ScheduleRepository, Groups),
        new SaveScheduleSlotUseCase(ScheduleRepository, _guard),
        new DeleteScheduleSlotUseCase(ScheduleRepository, _guard),
        new GenerateSessionsUseCase(Sessions, _guard),
        Localization,
        Dialogs,
        Navigation,
        Clock,
        License);

    public EnrollmentViewModel CreateEnrollment() => new(
        new GetEnrollmentBoardUseCase(Groups, Enrollments, Students),
        new EnrollStudentInGroupUseCase(Enrollments, _guard),
        new WithdrawEnrollmentUseCase(Enrollments, _guard),
        Localization,
        Dialogs,
        Navigation,
        License);

    public TimetableViewModel CreateTimetable() => new(
        new GetTimetableUseCase(ScheduleRepository),
        new GetGroupsUseCase(Groups),
        new GetGroupLookupsUseCase(References),
        Localization,
        Navigation);

    public GroupDetailsViewModel CreateDetails() => new(
        new GetGroupDetailsUseCase(Groups, Enrollments, ScheduleRepository, Attendance),
        Localization,
        Navigation);

    public GroupFullReportViewModel CreateReport() => new(
        new BuildGroupFullReportUseCase(
            Groups,
            Enrollments,
            Attendance,
            Payments,
            References,
            Clock,
            Report),
        Files,
        Print,
        Localization,
        Navigation);

    public static GroupSummary Summary(
        int id = 1,
        int active = 2,
        int max = 30,
        int slots = 2,
        bool isActive = true) => new(
        id,
        "Group Alpha",
        1,
        "Mathematics",
        1,
        "Teacher One",
        1,
        "Level One",
        1,
        "Grade One",
        max,
        1500,
        isActive,
        active,
        slots);

    public static ScheduleSlotItem Slot(
        int id = 1,
        int day = 0,
        int hour = 8,
        string? location = "Room 1") => new(
        id,
        1,
        "Group Alpha",
        1,
        "Teacher One",
        day,
        new DateTime(2000, 1, 1, hour, 0, 0, DateTimeKind.Utc),
        new DateTime(2000, 1, 1, hour + 1, 0, 0, DateTimeKind.Utc),
        location,
        true);

    public static EnrollmentListItem EnrollmentRow(
        int id = 1,
        int studentId = 1,
        string status = EnrollmentStatuses.Active) => new(
        id,
        studentId,
        $"STU-2026-{studentId:0000}",
        $"Student {studentId}",
        1,
        "Group Alpha",
        new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        status,
        0);

    public static StudentListItem Student(int id = 3) => new(
        id,
        $"STU-2026-{id:0000}",
        "Carol",
        "Clark",
        "+213555000003",
        StudentStatuses.Active,
        null,
        null);

    private static LicenseViewModel BuildLicense(ILocalizationService localization)
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

public sealed class GroupsViewModelTests
{
    [Fact]
    public async Task Load_projects_the_rows_and_sends_the_default_query()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success(
                new[] { GroupVmHarness.Summary(), GroupVmHarness.Summary(2) }));

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        vm.Items.Should().HaveCount(2);
        vm.IsEmpty.Should().BeFalse();
        vm.SortOptions.Should().HaveCount(6);
        vm.Grades.Should().HaveCount(2);
        vm.Teachers.Should().HaveCount(2);
        vm.Teachers[0].Id.Should().Be(0);

        await harness.Groups.Received(1).GetListAsync(
            Arg.Is<GroupListQuery>(query =>
                query.SearchTerm == null
                && !query.IncludeInactive
                && query.SortBy == GroupSortField.Name
                && !query.Descending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_list_is_loaded_only_once()
    {
        var harness = new GroupVmHarness();
        using var vm = harness.CreateGroups();

        await vm.EnsureLoadedAsync();
        await vm.EnsureLoadedAsync();

        await harness.Groups.Received(1).GetListAsync(
            Arg.Any<GroupListQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_filter_bar_builds_the_deliberate_query()
    {
        var harness = new GroupVmHarness();
        using var vm = harness.CreateGroups();

        vm.SearchText = "alpha";
        vm.IncludeInactive = true;
        vm.SortDescending = true;
        vm.SelectedSort = vm.SortOptions.Single(option => option.Field == GroupSortField.Fee);

        await vm.EnsureLoadedAsync();

        await harness.Groups.Received(1).GetListAsync(
            Arg.Is<GroupListQuery>(query =>
                query.SearchTerm == "alpha"
                && query.IncludeInactive
                && query.SortBy == GroupSortField.Fee
                && query.Descending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_read_failure_shows_the_error_without_the_renew_button()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Fail(Failure.Database()));

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be(TranslationKeys.Errors.DatabaseFailure);
        vm.IsRenewVisible.Should().BeFalse();
        vm.State.Should().BeOfType<UiState<IReadOnlyList<GroupSummary>>.Error>();
    }

    [Fact]
    public async Task An_empty_result_sets_the_empty_state()
    {
        var harness = new GroupVmHarness();
        using var vm = harness.CreateGroups();

        await vm.EnsureLoadedAsync();

        vm.IsEmpty.Should().BeTrue();
        vm.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_opens_the_shared_form_with_defaults()
    {
        var harness = new GroupVmHarness();
        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        vm.AddCommand.Execute(null);

        vm.IsEditorOpen.Should().BeTrue();
        vm.EditingId.Should().BeNull();
        vm.EditorName.Should().BeEmpty();
        vm.EditorMaxStudents.Should().Be("30");
        vm.EditorTitle.Should().Be(TranslationKeys.Groups.Add);
        vm.SelectedEditorGrade.Should().NotBeNull();
        vm.Subjects.Should().HaveCount(2);
    }

    [Fact]
    public async Task Edit_loads_the_group_and_offers_only_the_subjects_of_its_grade()
    {
        var harness = new GroupVmHarness();
        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        await vm.BeginEditAsync(1);

        vm.IsEditorOpen.Should().BeTrue();
        vm.EditingId.Should().Be(1);
        vm.EditorName.Should().Be("Group Alpha");
        vm.EditorMaxStudents.Should().Be("30");
        vm.EditorMonthlyFee.Should().Be("1500");
        vm.EditorTitle.Should().Be(TranslationKeys.Groups.Edit);
        vm.SelectedEditorGrade!.Id.Should().Be(1);
        vm.Subjects.Select(subject => subject.Id).Should().BeEquivalentTo(new[] { 1, 2 });
        vm.SelectedEditorSubject!.Id.Should().Be(1);
    }

    [Fact]
    public async Task Edit_reports_a_missing_group()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetSummaryAsync(404, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(null));

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        await vm.BeginEditAsync(404);

        vm.IsEditorOpen.Should().BeFalse();
        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be(TranslationKeys.Errors.NotFound);
    }

    [Fact]
    public async Task Save_creates_the_group_with_the_trimmed_name_and_the_selected_links()
    {
        var harness = new GroupVmHarness();
        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        vm.AddCommand.Execute(null);
        vm.SelectedEditorGrade = vm.Grades[0];
        vm.SelectedEditorSubject = vm.Subjects[0];
        vm.SelectedEditorTeacher = vm.Teachers[1];
        vm.EditorName = "  New Group  ";
        vm.EditorMaxStudents = "12";
        vm.EditorMonthlyFee = "900.5";

        await vm.SaveEditorCommand.ExecuteAsync(null);

        await harness.Groups.Received(1).SaveAsync(
            Arg.Is<GroupSaveRequest>(request =>
                request.Name == "New Group"
                && request.GradeId == 1
                && request.SubjectId == 1
                && request.TeacherId == 1
                && request.MaxStudents == 12
                && request.MonthlyFee == 900.5
                && request.GroupId == null),
            Arg.Any<CancellationToken>());
        vm.IsEditorOpen.Should().BeFalse();
        vm.StatusMessage.Should().Be(TranslationKeys.Groups.Saved);
    }

    [Fact]
    public async Task A_license_blocked_save_keeps_the_form_open_and_shows_renew()
    {
        var harness = new GroupVmHarness(GroupVmHarness.Blocking());
        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        vm.AddCommand.Execute(null);
        vm.SelectedEditorGrade = vm.Grades[0];
        vm.SelectedEditorSubject = vm.Subjects[0];
        vm.EditorName = "Blocked Group";

        await vm.SaveEditorCommand.ExecuteAsync(null);

        vm.IsEditorOpen.Should().BeTrue();
        vm.IsRenewVisible.Should().BeTrue();
        vm.ErrorMessage.Should().Be(TranslationKeys.License.ErrorWriteBlocked);
        await harness.Groups.DidNotReceive().SaveAsync(
            Arg.Any<GroupSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_confirmed_removes_the_group_and_reports_it()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => Result<IReadOnlyList<GroupSummary>>.Success(
                    new[] { GroupVmHarness.Summary() }),
                _ => Result<IReadOnlyList<GroupSummary>>.Success(
                    Array.Empty<GroupSummary>()));

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();
        vm.Items.Should().ContainSingle();

        await vm.DeleteCommand.ExecuteAsync(vm.Items[0]);

        await harness.Groups.Received(1).DeleteAsync(1, Arg.Any<CancellationToken>());
        vm.StatusMessage.Should().Be(TranslationKeys.Groups.Deleted);
        vm.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_declined_keeps_the_group()
    {
        var harness = new GroupVmHarness();
        harness.Dialogs
            .ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);
        harness.Groups
            .GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success(
                new[] { GroupVmHarness.Summary() }));

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        await vm.DeleteCommand.ExecuteAsync(vm.Items[0]);

        await harness.Groups.DidNotReceive().DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        vm.HasStatus.Should().BeFalse();
    }

    [Fact]
    public async Task A_linked_group_cannot_be_deleted()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetDependenciesAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupDependencies>.Success(new GroupDependencies(2, 1, 3)));
        harness.Groups
            .GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success(
                new[] { GroupVmHarness.Summary() }));

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        await vm.DeleteCommand.ExecuteAsync(vm.Items[0]);

        vm.ErrorMessage.Should().Be(TranslationKeys.Groups.DeleteBlocked);
        await harness.Groups.DidNotReceive().DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(3, 0, TranslationKeys.Groups.GenerationCreated)]
    [InlineData(0, 2, TranslationKeys.Groups.GenerationAlreadyExists)]
    [InlineData(0, 0, TranslationKeys.Groups.GenerationNone)]
    public async Task Session_generation_reports_the_created_or_existing_count(
        int created,
        int existing,
        string expectedKey)
    {
        var harness = new GroupVmHarness();
        harness.Sessions
            .GenerateForDateAsync(Arg.Any<DateTime>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SessionGenerationResult>.Success(
                new SessionGenerationResult(created, existing)));

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        await vm.GenerateAllCommand.ExecuteAsync(null);

        await harness.Sessions.Received(1).GenerateForDateAsync(
            new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc),
            null,
            Arg.Any<CancellationToken>());
        vm.StatusMessage.Should().Be(expectedKey);
    }

    [Fact]
    public async Task A_generation_failure_shows_the_error()
    {
        var harness = new GroupVmHarness();
        harness.Sessions
            .GenerateForDateAsync(Arg.Any<DateTime>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result<SessionGenerationResult>.Fail(
                Failure.Validation(TranslationKeys.Errors.RequiredFields, "date")));

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        await vm.GenerateAllCommand.ExecuteAsync(null);

        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be(TranslationKeys.Errors.RequiredFields);
    }

    [Fact]
    public async Task Details_opens_the_group_file()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success(
                new[] { GroupVmHarness.Summary() }));

        using var details = harness.CreateDetails();
        harness.Navigation.NavigateTo<GroupDetailsViewModel>().Returns(details);

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        await vm.DetailsCommand.ExecuteAsync(vm.Items[0]);

        harness.Navigation.Received(1).NavigateTo<GroupDetailsViewModel>();
        details.GroupId.Should().Be(1);
    }

    [Fact]
    public async Task Enroll_and_schedule_shortcuts_open_their_pages()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success(
                new[] { GroupVmHarness.Summary() }));

        using var enrollment = harness.CreateEnrollment();
        using var schedule = harness.CreateSchedule();
        harness.Navigation.NavigateTo<EnrollmentViewModel>().Returns(enrollment);
        harness.Navigation.NavigateTo<GroupScheduleViewModel>().Returns(schedule);

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        await vm.EnrollCommand.ExecuteAsync(vm.Items[0]);
        await vm.ScheduleCommand.ExecuteAsync(vm.Items[0]);

        harness.Navigation.Received(1).NavigateTo<EnrollmentViewModel>();
        harness.Navigation.Received(1).NavigateTo<GroupScheduleViewModel>();
        enrollment.GroupId.Should().Be(1);
        schedule.GroupId.Should().Be(1);
        schedule.Days.Should().HaveCount(7);
    }

    [Fact]
    public async Task The_timetable_shortcut_opens_the_weekly_grid()
    {
        var harness = new GroupVmHarness();
        using var timetable = harness.CreateTimetable();
        harness.Navigation.NavigateTo<TimetableViewModel>().Returns(timetable);

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();

        await vm.OpenTimetableCommand.ExecuteAsync(null);

        harness.Navigation.Received(1).NavigateTo<TimetableViewModel>();
        timetable.Cells.Should().HaveCount(104);
    }

    [Fact]
    public async Task A_language_change_refreshes_the_sort_labels_and_the_open_form_title()
    {
        var harness = new GroupVmHarness();
        harness.Localization.Values[TranslationKeys.Groups.SortName] = "Name";
        harness.Localization.Values[TranslationKeys.Groups.Add] = "Add group";

        using var vm = harness.CreateGroups();
        await vm.EnsureLoadedAsync();
        vm.AddCommand.Execute(null);

        vm.SortOptions[0].Label.Should().Be("Name");
        vm.EditorTitle.Should().Be("Add group");

        harness.Localization.Values[TranslationKeys.Groups.SortName] = "Nom";
        harness.Localization.Values[TranslationKeys.Groups.Add] = "Ajouter";
        harness.Localization.RaiseLanguageChanged();

        vm.SortOptions[0].Label.Should().Be("Nom");
        vm.EditorTitle.Should().Be("Ajouter");
    }
}

public sealed class GroupScheduleViewModelTests
{
    private static GroupVmHarness HarnessWithSlots()
    {
        var harness = new GroupVmHarness();
        harness.ScheduleRepository
            .GetSlotsAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Success(
                new[] { GroupVmHarness.Slot(1, 0, 8), GroupVmHarness.Slot(2, 2, 10) }));
        return harness;
    }

    [Fact]
    public async Task Load_groups_the_slots_under_their_weekday()
    {
        var harness = HarnessWithSlots();
        using var vm = harness.CreateSchedule();

        await vm.InitializeAsync(1);

        vm.Days.Should().HaveCount(7);
        vm.Days[0].Slots.Should().ContainSingle();
        vm.Days[1].Slots.Should().BeEmpty();
        vm.Days[2].Slots.Should().ContainSingle();
        vm.HasAnySlot.Should().BeTrue();
        vm.GroupName.Should().Be("Group Alpha");
        vm.TeacherText.Should().Be("Teacher One");
        vm.CapacityText.Should().Be("2/30");
    }

    [Fact]
    public async Task A_missing_group_shows_not_found()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetSummaryAsync(404, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(null));

        using var vm = harness.CreateSchedule();
        await vm.InitializeAsync(404);

        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be(TranslationKeys.Errors.NotFound);
        vm.Days.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_opens_the_dialog_with_the_default_range()
    {
        var harness = HarnessWithSlots();
        using var vm = harness.CreateSchedule();
        await vm.InitializeAsync(1);

        vm.AddSlotCommand.Execute(null);

        vm.IsEditorOpen.Should().BeTrue();
        vm.EditingSlotId.Should().BeNull();
        vm.SelectedDay!.Value.Should().Be(0);
        vm.EditorStartTime.Should().Be("08:00");
        vm.EditorEndTime.Should().Be("09:00");
        vm.EditorTitle.Should().Be(TranslationKeys.Groups.AddTimeSlot);
    }

    [Fact]
    public async Task Edit_fills_the_dialog_from_the_selected_slot()
    {
        var harness = HarnessWithSlots();
        using var vm = harness.CreateSchedule();
        await vm.InitializeAsync(1);

        vm.EditSlotCommand.Execute(vm.Days[2].Slots[0]);

        vm.IsEditorOpen.Should().BeTrue();
        vm.EditingSlotId.Should().Be(2);
        vm.SelectedDay!.Value.Should().Be(2);
        vm.EditorStartTime.Should().Be("10:00");
        vm.EditorEndTime.Should().Be("11:00");
        vm.EditorLocation.Should().Be("Room 1");
        vm.EditorTitle.Should().Be(TranslationKeys.Groups.EditTimeSlot);
    }

    [Fact]
    public async Task Saving_a_conflict_free_slot_persists_and_reloads()
    {
        var harness = HarnessWithSlots();
        using var vm = harness.CreateSchedule();
        await vm.InitializeAsync(1);

        vm.AddSlotCommand.Execute(null);
        vm.SelectedDay = vm.WeekDays[1];
        vm.EditorStartTime = "10:00";
        vm.EditorEndTime = "11:00";
        vm.EditorLocation = "  Room 5  ";

        await vm.SaveEditorCommand.ExecuteAsync(null);

        await harness.ScheduleRepository.Received(1).SaveSlotAsync(
            Arg.Is<ScheduleSaveRequest>(request =>
                request.GroupId == 1
                && request.DayOfWeek == 1
                && request.StartTime == new TimeSpan(10, 0, 0)
                && request.EndTime == new TimeSpan(11, 0, 0)
                && request.Location == "Room 5"
                && request.IsActive),
            Arg.Any<CancellationToken>());
        vm.IsEditorOpen.Should().BeFalse();
        vm.StatusMessage.Should().Be(TranslationKeys.Groups.ScheduleSaved);
    }

    [Fact]
    public async Task A_conflict_requires_a_confirmation_before_the_save()
    {
        var harness = HarnessWithSlots();
        harness.ScheduleRepository
            .GetConflictsAsync(
                Arg.Any<int>(),
                Arg.Any<int?>(),
                Arg.Any<int>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Success(
                new[] { GroupVmHarness.Slot(9, 1, 10) }));

        using var vm = harness.CreateSchedule();
        await vm.InitializeAsync(1);
        vm.AddSlotCommand.Execute(null);
        vm.SelectedDay = vm.WeekDays[1];
        vm.EditorStartTime = "10:30";
        vm.EditorEndTime = "11:30";

        await vm.SaveEditorCommand.ExecuteAsync(null);

        vm.IsConflictOpen.Should().BeTrue();
        vm.Conflicts.Should().ContainSingle();
        vm.Conflicts[0].Text.Should().Contain("Group Alpha").And.Contain("10:00-11:00");
        await harness.ScheduleRepository.DidNotReceive().SaveSlotAsync(
            Arg.Any<ScheduleSaveRequest>(),
            Arg.Any<CancellationToken>());

        await vm.ConfirmConflictCommand.ExecuteAsync(null);

        await harness.ScheduleRepository.Received(1).SaveSlotAsync(
            Arg.Any<ScheduleSaveRequest>(),
            Arg.Any<CancellationToken>());
        vm.IsConflictOpen.Should().BeFalse();
        vm.Conflicts.Should().BeEmpty();
        vm.StatusMessage.Should().Be(TranslationKeys.Groups.ScheduleSaved);
    }

    [Fact]
    public async Task Cancelling_the_conflict_never_saves()
    {
        var harness = HarnessWithSlots();
        harness.ScheduleRepository
            .GetConflictsAsync(
                Arg.Any<int>(),
                Arg.Any<int?>(),
                Arg.Any<int>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<int?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Success(
                new[] { GroupVmHarness.Slot(9, 1, 10) }));

        using var vm = harness.CreateSchedule();
        await vm.InitializeAsync(1);
        vm.AddSlotCommand.Execute(null);
        vm.SelectedDay = vm.WeekDays[1];
        vm.EditorStartTime = "10:30";
        vm.EditorEndTime = "11:30";

        await vm.SaveEditorCommand.ExecuteAsync(null);
        vm.CancelConflictCommand.Execute(null);

        vm.IsConflictOpen.Should().BeFalse();
        vm.Conflicts.Should().BeEmpty();
        await harness.ScheduleRepository.DidNotReceive().SaveSlotAsync(
            Arg.Any<ScheduleSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("25:00", "26:00")]
    [InlineData("10:00", "09:00")]
    [InlineData("", "09:00")]
    public async Task An_invalid_range_is_rejected_before_any_query(string start, string end)
    {
        var harness = HarnessWithSlots();
        using var vm = harness.CreateSchedule();
        await vm.InitializeAsync(1);
        vm.AddSlotCommand.Execute(null);
        vm.EditorStartTime = start;
        vm.EditorEndTime = end;

        await vm.SaveEditorCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be(TranslationKeys.Groups.InvalidTimeRange);
        await harness.ScheduleRepository.DidNotReceive().GetConflictsAsync(
            Arg.Any<int>(),
            Arg.Any<int?>(),
            Arg.Any<int>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
        await harness.ScheduleRepository.DidNotReceive().SaveSlotAsync(
            Arg.Any<ScheduleSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deleting_a_slot_asks_for_confirmation_then_reloads()
    {
        var harness = HarnessWithSlots();
        using var vm = harness.CreateSchedule();
        await vm.InitializeAsync(1);

        await vm.DeleteSlotCommand.ExecuteAsync(vm.Days[0].Slots[0]);

        await harness.Dialogs.Received(1).ConfirmAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
        await harness.ScheduleRepository.Received(1).DeleteSlotAsync(1, Arg.Any<CancellationToken>());
        vm.StatusMessage.Should().Be(TranslationKeys.Groups.ScheduleDeleted);
    }

    [Fact]
    public async Task Declining_the_delete_keeps_the_slot()
    {
        var harness = HarnessWithSlots();
        harness.Dialogs
            .ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        using var vm = harness.CreateSchedule();
        await vm.InitializeAsync(1);

        await vm.DeleteSlotCommand.ExecuteAsync(vm.Days[0].Slots[0]);

        await harness.ScheduleRepository.DidNotReceive().DeleteSlotAsync(
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_blocked_save_keeps_the_dialog_open_and_shows_renew()
    {
        var harness = new GroupVmHarness(GroupVmHarness.Blocking());
        using var vm = harness.CreateSchedule();
        await vm.InitializeAsync(1);

        vm.AddSlotCommand.Execute(null);
        vm.EditorStartTime = "10:00";
        vm.EditorEndTime = "11:00";

        await vm.SaveEditorCommand.ExecuteAsync(null);

        vm.IsEditorOpen.Should().BeTrue();
        vm.IsRenewVisible.Should().BeTrue();
        vm.ErrorMessage.Should().Be(TranslationKeys.License.ErrorWriteBlocked);
        await harness.ScheduleRepository.DidNotReceive().SaveSlotAsync(
            Arg.Any<ScheduleSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void A_language_change_rebuilds_the_weekday_labels()
    {
        var harness = HarnessWithSlots();
        harness.Localization.Values["days.saturday"] = "Saturday";
        using var vm = harness.CreateSchedule();

        vm.WeekDays[0].Label.Should().Be("Saturday");

        harness.Localization.Values["days.saturday"] = "Samedi";
        harness.Localization.RaiseLanguageChanged();

        vm.WeekDays[0].Label.Should().Be("Samedi");
    }
}

public sealed class EnrollmentViewModelTests
{
    private static GroupVmHarness HarnessWithBoard(bool full = false)
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetSummaryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(
                GroupVmHarness.Summary(active: full ? 30 : 2, max: 30)));
        harness.Enrollments
            .GetByGroupAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<EnrollmentListItem>>.Success(new[]
            {
                GroupVmHarness.EnrollmentRow(1, 1),
                GroupVmHarness.EnrollmentRow(2, 2, EnrollmentStatuses.Withdrawn),
            }));
        harness.Students
            .GetListAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentListItem>>.Success(
                new[] { GroupVmHarness.Student(1), GroupVmHarness.Student(3) }));
        return harness;
    }

    [Fact]
    public async Task The_board_lists_only_active_enrollments_and_the_students_that_can_join()
    {
        var harness = HarnessWithBoard();
        using var vm = harness.CreateEnrollment();

        await vm.InitializeAsync(1);

        vm.Enrolled.Should().ContainSingle().Which.StudentId.Should().Be(1);
        vm.Available.Should().ContainSingle().Which.Id.Should().Be(3);
        vm.GroupName.Should().Be("Group Alpha");
        vm.SubjectText.Should().Be("Mathematics");
        vm.TeacherText.Should().Be("Teacher One");
        vm.IsFull.Should().BeFalse();
        vm.CapacityText.Should().Be(TranslationKeys.Enrollment.CapacityLabel);
        vm.CapacityPercent.Should().Be(7);
        vm.HasAvailable.Should().BeTrue();
        vm.HasEnrolled.Should().BeTrue();
    }

    [Fact]
    public async Task Enrolling_an_available_student_shows_the_success_message()
    {
        var harness = HarnessWithBoard();
        using var vm = harness.CreateEnrollment();
        await vm.InitializeAsync(1);

        await vm.EnrollCommand.ExecuteAsync(vm.Available[0]);

        await harness.Enrollments.Received(1).EnrollAsync(
            Arg.Is<EnrollmentSaveRequest>(request => request.GroupId == 1 && request.StudentId == 3),
            Arg.Any<CancellationToken>());
        vm.StatusMessage.Should().Be(TranslationKeys.Enrollment.EnrolledSuccess);
    }

    [Fact]
    public async Task A_full_group_asks_for_confirmation_and_warns_after_the_enrollment()
    {
        var harness = HarnessWithBoard(full: true);
        using var vm = harness.CreateEnrollment();
        await vm.InitializeAsync(1);

        await vm.EnrollCommand.ExecuteAsync(vm.Available[0]);

        await harness.Dialogs.Received(1).ConfirmAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
        await harness.Enrollments.Received(1).EnrollAsync(
            Arg.Any<EnrollmentSaveRequest>(),
            Arg.Any<CancellationToken>());
        vm.StatusMessage.Should().Be(TranslationKeys.Enrollment.CapacityExceededWarning);
    }

    [Fact]
    public async Task Declining_the_full_group_confirmation_does_not_enroll()
    {
        var harness = HarnessWithBoard(full: true);
        harness.Dialogs
            .ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        using var vm = harness.CreateEnrollment();
        await vm.InitializeAsync(1);

        await vm.EnrollCommand.ExecuteAsync(vm.Available[0]);

        await harness.Enrollments.DidNotReceive().EnrollAsync(
            Arg.Any<EnrollmentSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_already_active_enrollment_reports_the_dedicated_message()
    {
        var harness = HarnessWithBoard();
        harness.Enrollments
            .EnrollAsync(Arg.Any<EnrollmentSaveRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<EnrollmentSaveOutcome>.Success(new EnrollmentSaveOutcome(1, false, true)));

        using var vm = harness.CreateEnrollment();
        await vm.InitializeAsync(1);

        await vm.EnrollCommand.ExecuteAsync(vm.Available[0]);

        vm.StatusMessage.Should().Be(TranslationKeys.Enrollment.AlreadyEnrolled);
    }

    [Fact]
    public async Task Withdrawing_asks_for_confirmation_and_changes_only_the_status()
    {
        var harness = HarnessWithBoard();
        using var vm = harness.CreateEnrollment();
        await vm.InitializeAsync(1);

        await vm.WithdrawCommand.ExecuteAsync(vm.Enrolled[0]);

        await harness.Enrollments.Received(1).SetStatusAsync(
            1,
            EnrollmentStatuses.Withdrawn,
            Arg.Any<CancellationToken>());
        vm.StatusMessage.Should().Be(TranslationKeys.Enrollment.UnenrolledSuccess);
    }

    [Fact]
    public async Task Declining_the_withdraw_keeps_the_row_active()
    {
        var harness = HarnessWithBoard();
        harness.Dialogs
            .ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        using var vm = harness.CreateEnrollment();
        await vm.InitializeAsync(1);

        await vm.WithdrawCommand.ExecuteAsync(vm.Enrolled[0]);

        await harness.Enrollments.DidNotReceive().SetStatusAsync(
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_search_box_filters_the_available_students()
    {
        var harness = HarnessWithBoard();
        using var vm = harness.CreateEnrollment();
        await vm.InitializeAsync(1);

        vm.SearchText = "carol";

        await harness.Students.Received(1).GetListAsync(
            Arg.Is<StudentListQuery>(query => query.SearchTerm == "carol"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Back_returns_to_the_group_list()
    {
        var harness = new GroupVmHarness();
        using var groups = harness.CreateGroups();
        harness.Navigation.NavigateTo<GroupsViewModel>().Returns(groups);

        using var vm = harness.CreateEnrollment();

        vm.BackCommand.Execute(null);

        harness.Navigation.Received(1).NavigateTo<GroupsViewModel>();
    }
}

public sealed class TimetableViewModelTests
{
    [Fact]
    public async Task The_grid_has_one_header_row_plus_seven_days_and_one_column_per_hour()
    {
        var harness = new GroupVmHarness();
        using var vm = harness.CreateTimetable();

        await vm.InitializeAsync(null, null);

        vm.ColumnCount.Should().Be(13);
        vm.RowCount.Should().Be(8);
        vm.Cells.Should().HaveCount(104);
        vm.Cells[0].IsHeader.Should().BeTrue();
        vm.Cells[0].Text.Should().BeEmpty();
        vm.Cells[1].Text.Should().Be("08:00");
        vm.Cells[12].Text.Should().Be("19:00");
        vm.Cells[13].IsDayLabel.Should().BeTrue();
        vm.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task A_slot_lands_in_the_cell_of_its_day_and_start_hour()
    {
        var harness = new GroupVmHarness();
        harness.ScheduleRepository
            .GetSlotsAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Success(
                new[] { GroupVmHarness.Slot(1, 0, 8, "Room 7") }));

        using var vm = harness.CreateTimetable();
        await vm.InitializeAsync(null, null);

        // Index 13 is the Saturday label; 08:00 is the first hour cell.
        vm.Cells[14].HasSlot.Should().BeTrue();
        vm.Cells[14].Text.Should().Contain("Group Alpha")
            .And.Contain("08:00-09:00")
            .And.Contain("Room 7");
        vm.Cells[15].HasSlot.Should().BeFalse();
        vm.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task The_filters_are_sent_to_the_timetable_query()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetListAsync(Arg.Any<GroupListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success(
                new[] { GroupVmHarness.Summary() }));

        using var vm = harness.CreateTimetable();
        await vm.InitializeAsync(1, 1);

        await harness.ScheduleRepository.Received(1).GetSlotsAsync(
            1,
            1,
            false,
            Arg.Any<CancellationToken>());
        vm.GroupOptions.Should().HaveCount(2);
        vm.GroupOptions[0].Id.Should().BeNull();
        vm.TeacherOptions.Should().HaveCount(2);
        vm.TeacherOptions[0].Id.Should().BeNull();
    }

    [Fact]
    public async Task A_read_failure_shows_the_error()
    {
        var harness = new GroupVmHarness();
        harness.ScheduleRepository
            .GetSlotsAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Fail(Failure.Database()));

        using var vm = harness.CreateTimetable();
        await vm.InitializeAsync(null, null);

        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be(TranslationKeys.Errors.DatabaseFailure);
    }

    [Fact]
    public async Task A_language_change_rebuilds_the_day_headers_and_the_all_filter()
    {
        var harness = new GroupVmHarness();
        harness.Localization.Values["days.saturday"] = "Saturday";
        harness.Localization.Values[TranslationKeys.Timetable.AllGroups] = "All groups";

        using var vm = harness.CreateTimetable();
        await vm.InitializeAsync(null, null);

        vm.Cells[13].Text.Should().Be("Saturday");
        vm.GroupOptions[0].Label.Should().Be("All groups");

        harness.Localization.Values["days.saturday"] = "Samedi";
        harness.Localization.Values[TranslationKeys.Timetable.AllGroups] = "Tous";
        harness.Localization.RaiseLanguageChanged();

        vm.Cells[13].Text.Should().Be("Samedi");
        vm.GroupOptions[0].Label.Should().Be("Tous");
    }
}

public sealed class GroupDetailsViewModelTests
{
    [Fact]
    public async Task The_group_file_joins_students_slots_and_attendance()
    {
        var harness = new GroupVmHarness();
        harness.Enrollments
            .GetByGroupAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<EnrollmentListItem>>.Success(
                new[] { GroupVmHarness.EnrollmentRow() }));
        harness.ScheduleRepository
            .GetSlotsAsync(Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<ScheduleSlotItem>>.Success(
                new[] { GroupVmHarness.Slot(2, 2, 10), GroupVmHarness.Slot(1, 0, 8) }));

        using var vm = harness.CreateDetails();
        await vm.InitializeAsync(1);

        vm.Students.Should().ContainSingle();
        vm.Slots.Should().HaveCount(2);
        vm.Slots[0].DayOfWeek.Should().Be(0);
        vm.Slots[1].DayOfWeek.Should().Be(2);
        vm.GroupName.Should().Be("Group Alpha");
        vm.SubjectText.Should().Be("Mathematics");
        vm.TeacherText.Should().Be("Teacher One");
        vm.GradeText.Should().Be("Grade One");
        vm.LevelText.Should().Be("Level One");
        vm.FeeText.Should().Be("1500");
        vm.CapacityText.Should().Be("2/30");
        vm.CapacityPercent.Should().Be(7);
        vm.IsFull.Should().BeFalse();
        vm.AttendanceText.Should().Be("2 / 3 (66.7%)");
        vm.HasStudents.Should().BeTrue();
        vm.HasSlots.Should().BeTrue();
    }

    [Fact]
    public async Task A_missing_group_shows_not_found()
    {
        var harness = new GroupVmHarness();
        harness.Groups
            .GetSummaryAsync(404, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(null));

        using var vm = harness.CreateDetails();
        await vm.InitializeAsync(404);

        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be(TranslationKeys.Errors.NotFound);
        vm.Students.Should().BeEmpty();
    }

    [Fact]
    public async Task The_shortcuts_open_enrollment_schedule_and_the_report()
    {
        var harness = new GroupVmHarness();
        using var enrollment = harness.CreateEnrollment();
        using var schedule = harness.CreateSchedule();
        using var report = harness.CreateReport();
        harness.Navigation.NavigateTo<EnrollmentViewModel>().Returns(enrollment);
        harness.Navigation.NavigateTo<GroupScheduleViewModel>().Returns(schedule);
        harness.Navigation.NavigateTo<GroupFullReportViewModel>().Returns(report);

        using var vm = harness.CreateDetails();
        await vm.InitializeAsync(1);

        await vm.EnrollCommand.ExecuteAsync(null);
        await vm.ScheduleCommand.ExecuteAsync(null);
        await vm.ReportCommand.ExecuteAsync(null);

        harness.Navigation.Received(1).NavigateTo<EnrollmentViewModel>();
        harness.Navigation.Received(1).NavigateTo<GroupScheduleViewModel>();
        harness.Navigation.Received(1).NavigateTo<GroupFullReportViewModel>();
        enrollment.GroupId.Should().Be(1);
        schedule.GroupId.Should().Be(1);
        report.GroupId.Should().Be(1);
        report.IsReady.Should().BeTrue();
    }

    [Fact]
    public async Task Edit_opens_the_shared_form_on_the_cached_list()
    {
        var harness = new GroupVmHarness();
        using var groups = harness.CreateGroups();
        harness.Navigation.NavigateTo<GroupsViewModel>().Returns(groups);

        using var vm = harness.CreateDetails();
        await vm.InitializeAsync(1);

        await vm.EditCommand.ExecuteAsync(null);

        harness.Navigation.Received(1).NavigateTo<GroupsViewModel>();
        groups.EditingId.Should().Be(1);
        groups.IsEditorOpen.Should().BeTrue();
        groups.EditorName.Should().Be("Group Alpha");
    }

    [Fact]
    public void Back_returns_to_the_group_list()
    {
        var harness = new GroupVmHarness();
        using var groups = harness.CreateGroups();
        harness.Navigation.NavigateTo<GroupsViewModel>().Returns(groups);

        using var vm = harness.CreateDetails();

        vm.BackCommand.Execute(null);

        harness.Navigation.Received(1).NavigateTo<GroupsViewModel>();
    }
}

public sealed class GroupFullReportViewModelTests
{
    [Fact]
    public async Task The_report_is_built_when_the_page_opens()
    {
        var harness = new GroupVmHarness();
        using var vm = harness.CreateReport();

        await vm.InitializeAsync(1);

        vm.IsReady.Should().BeTrue();
        vm.HasError.Should().BeFalse();
        await harness.Report.Received(1).BuildFullReportAsync(
            Arg.Is<GroupReportData>(data => data.Group.Id == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_report_failure_shows_the_error_and_clears_the_ready_state()
    {
        var harness = new GroupVmHarness();
        harness.Report
            .BuildFullReportAsync(Arg.Any<GroupReportData>(), Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound)));

        using var vm = harness.CreateReport();
        await vm.InitializeAsync(1);

        vm.IsReady.Should().BeFalse();
        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be(TranslationKeys.Errors.NotFound);
    }

    [Fact]
    public async Task Print_and_open_reuse_the_same_pdf_bytes()
    {
        var harness = new GroupVmHarness();
        using var vm = harness.CreateReport();
        await vm.InitializeAsync(1);

        await vm.PrintCommand.ExecuteAsync(null);
        await vm.OpenCommand.ExecuteAsync(null);

        await harness.Print.Received(1).PrintAsync(
            Arg.Is<byte[]>(bytes => bytes.SequenceEqual(new byte[] { 1, 2 })),
            "group-report-1.pdf");
        await harness.Print.Received(1).OpenAsync(
            Arg.Is<byte[]>(bytes => bytes.SequenceEqual(new byte[] { 1, 2 })),
            "group-report-1.pdf");
    }

    [Fact]
    public async Task Save_writes_the_picked_path()
    {
        var harness = new GroupVmHarness();
        harness.Files
            .PickSavePathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns("C:\\temp\\group.pdf");

        using var vm = harness.CreateReport();
        await vm.InitializeAsync(1);

        await vm.SavePdfCommand.ExecuteAsync(null);

        await harness.Files.Received(1).WriteAllBytesAsync(
            "C:\\temp\\group.pdf",
            Arg.Is<byte[]>(bytes => bytes.SequenceEqual(new byte[] { 1, 2 })));
    }

    [Fact]
    public async Task A_cancelled_save_writes_nothing()
    {
        var harness = new GroupVmHarness();
        harness.Files
            .PickSavePathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns((string?)null);

        using var vm = harness.CreateReport();
        await vm.InitializeAsync(1);

        await vm.SavePdfCommand.ExecuteAsync(null);

        await harness.Files.DidNotReceive().WriteAllBytesAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>());
    }

    [Fact]
    public async Task Print_save_and_open_do_nothing_before_the_pdf_exists()
    {
        var harness = new GroupVmHarness();
        using var vm = harness.CreateReport();

        await vm.PrintCommand.ExecuteAsync(null);
        await vm.OpenCommand.ExecuteAsync(null);
        await vm.SavePdfCommand.ExecuteAsync(null);

        await harness.Print.DidNotReceive().PrintAsync(Arg.Any<byte[]>(), Arg.Any<string>());
        await harness.Print.DidNotReceive().OpenAsync(Arg.Any<byte[]>(), Arg.Any<string>());
        await harness.Files.DidNotReceive().WriteAllBytesAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>());
    }

    [Fact]
    public void Back_and_close_return_to_the_group_list()
    {
        var harness = new GroupVmHarness();
        using var groups = harness.CreateGroups();
        harness.Navigation.NavigateTo<GroupsViewModel>().Returns(groups);

        using var vm = harness.CreateReport();

        vm.BackCommand.Execute(null);
        vm.CloseCommand.Execute(null);

        harness.Navigation.Received(2).NavigateTo<GroupsViewModel>();
    }
}
