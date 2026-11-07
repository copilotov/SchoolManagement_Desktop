using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Domain.Common;
using SchoolManagement.Tests.Academic;

namespace SchoolManagement.Tests.Students;

/// <summary>
/// Builds the Phase 5 ViewModels on top of substituted repositories and ports,
/// so every test runs the real use cases and the real ViewModel logic without a
/// WPF message loop.
/// </summary>
internal sealed class StudentVmHarness
{
    private readonly ILicenseGuard _guard;

    public StudentVmHarness(ILicenseGuard? guard = null)
    {
        _guard = guard ?? Allowing();

        Repository
            .GetListAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentListItem>>.Success(Array.Empty<StudentListItem>()));
        Repository
            .CountAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(0));
        Repository
            .CodeExistsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        Repository
            .SaveAsync(Arg.Any<StudentSaveRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<StudentSaveOutcome>.Success(new StudentSaveOutcome(1, true, "STU-2026-0001")));
        Repository
            .GetDependenciesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<StudentDependencies>.Success(new StudentDependencies(0, 0, 0)));
        Repository
            .DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        Repository
            .GetExportRowsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentExportRow>>.Success(Array.Empty<StudentExportRow>()));
        Repository
            .GetCardDataAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentCardData>>.Success(Array.Empty<StudentCardData>()));
        Repository
            .GetAbsenceSummaryAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<StudentAbsenceSummary>.Success(
                new StudentAbsenceSummary(1, 0, 5, false, false)));

        References
            .GetAcademicLevelsAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success(
                new[] { new LookupItem(1, "Level One") }));
        References
            .GetGradesAsync(Arg.Any<int?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<LookupItem>>.Success(
                new[] { new LookupItem(10, "Grade One") }));
        Groups
            .GetSummariesAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<GroupSummary>>.Success(Array.Empty<GroupSummary>()));

        Enrollments
            .GetByStudentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<EnrollmentListItem>>.Success(Array.Empty<EnrollmentListItem>()));
        Payments
            .GetByStudentAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<PaymentListItem>>.Success(Array.Empty<PaymentListItem>()));
        Attendance
            .GetByStudentAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<AttendanceListItem>>.Success(Array.Empty<AttendanceListItem>()));

        Gateway
            .WriteAsync(Arg.Any<StudentFileFormat>(), Arg.Any<IReadOnlyList<StudentExportRow>>(), Arg.Any<CancellationToken>())
            .Returns(Result<byte[]>.Success(new byte[] { 7 }));
        Gateway
            .DefaultExtension(Arg.Any<StudentFileFormat>())
            .Returns("csv");

        Cards
            .RenderQrPng(Arg.Any<string>(), Arg.Any<int>())
            .Returns(Result<byte[]>.Success(Array.Empty<byte>()));
        Cards
            .RenderCardPreviewPng(Arg.Any<StudentCardData>())
            .Returns(Result<byte[]>.Success(Array.Empty<byte>()));
        Cards
            .RenderCardPdf(Arg.Any<StudentCardData>())
            .Returns(Result<byte[]>.Success(new byte[] { 1, 2 }));
        Cards
            .RenderQrSheetPdf(Arg.Any<IReadOnlyList<StudentQrItem>>())
            .Returns(Result<byte[]>.Success(new byte[] { 3, 4 }));

        Dialogs
            .ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);

        License = BuildLicense(Localization);
    }

    public FakeLocalization Localization { get; } = new();

    public IUserDialogService Dialogs { get; } = Substitute.For<IUserDialogService>();

    public IFileDialogService Files { get; } = Substitute.For<IFileDialogService>();

    public IPdfPrintService Print { get; } = Substitute.For<IPdfPrintService>();

    public INavigationService Navigation { get; } = Substitute.For<INavigationService>();

    public IStudentRepository Repository { get; } = Substitute.For<IStudentRepository>();

    public IReferenceDataRepository References { get; } = Substitute.For<IReferenceDataRepository>();

    public IGroupRepository Groups { get; } = Substitute.For<IGroupRepository>();

    public IEnrollmentRepository Enrollments { get; } = Substitute.For<IEnrollmentRepository>();

    public IPaymentRepository Payments { get; } = Substitute.For<IPaymentRepository>();

    public IAttendanceRepository Attendance { get; } = Substitute.For<IAttendanceRepository>();

    public IStudentFileGateway Gateway { get; } = Substitute.For<IStudentFileGateway>();

    public IStudentCardService Cards { get; } = Substitute.For<IStudentCardService>();

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

    public StudentsViewModel CreateStudents() => new(
        new GetStudentsUseCase(Repository),
        new GetStudentLookupsUseCase(References, Groups),
        new GetStudentDetailsUseCase(Repository),
        new SaveStudentUseCase(Repository, _guard),
        new DeleteStudentUseCase(Repository, _guard),
        new ImportStudentsUseCase(Gateway, Repository, _guard),
        new ExportStudentsUseCase(Repository, Gateway),
        Localization,
        Dialogs,
        Files,
        Navigation,
        License);

    public StudentDetailsViewModel CreateDetails() => new(
        new GetStudentDetailsUseCase(Repository),
        new GetStudentActivityUseCase(Enrollments, Payments, Attendance, Repository),
        Localization,
        Navigation);

    public StudentCardViewModel CreateCard() => new(
        new RenderStudentCardPreviewUseCase(Repository, Cards),
        new BuildStudentCardPdfUseCase(Repository, Cards),
        Files,
        Print,
        Localization,
        Navigation);

    public StudentsQrPrintViewModel CreateQrPrint() => new(
        new GetStudentsUseCase(Repository),
        new RenderStudentsQrUseCase(Repository, Cards),
        new BuildStudentsQrSheetPdfUseCase(Repository, Cards),
        Print,
        Files,
        Localization,
        Navigation);

    public static StudentListItem Item(
        int id = 1,
        string first = "Alice",
        string last = "Anderson",
        string code = "STU-2026-0001") => new(
        id,
        code,
        first,
        last,
        "+213555000001",
        StudentStatuses.Active,
        "Guardian One",
        1,
        "Level One",
        "Grade One",
        "Group One",
        1500);

    public static StudentDetails Details(int id = 1) => new(
        id,
        "STU-2026-0001",
        "Alice",
        "Anderson",
        new DateTime(2010, 5, 4, 0, 0, 0, DateTimeKind.Utc),
        "female",
        "12 Rue des Ecoles",
        "+213555000001",
        null,
        StudentStatuses.Active,
        "Note",
        1500,
        1,
        "Guardian One",
        "+213661123456",
        "guardian@example.com",
        "mother");

    public static StudentCardData Card(int id = 1) => new(
        id,
        "STU-2026-0001",
        "Alice Anderson",
        "Level One",
        "Grade One",
        "Group One",
        "Guardian One",
        "+213661123456",
        "Test School",
        "2026-2027");

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

public sealed class StudentsViewModelTests
{
    [Fact]
    public async Task Load_projects_rows_and_sends_the_default_query()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetListAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentListItem>>.Success(
                new[] { StudentVmHarness.Item() }));

        using var vm = harness.CreateStudents();
        await vm.EnsureLoadedAsync();

        vm.Items.Should().ContainSingle();
        vm.Items[0].LevelName.Should().Be("Level One");
        vm.Levels.Should().HaveCount(2);
        vm.Levels[0].Id.Should().BeNull();
        vm.Grades.Should().HaveCount(2);
        vm.HasError.Should().BeFalse();

        await harness.Repository.Received(1).GetListAsync(
            Arg.Is<StudentListQuery>(query =>
                query.SearchTerm == null
                && query.Status == null
                && query.SortBy == StudentSortField.Name
                && !query.Descending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ensure_loaded_only_queries_the_repository_once()
    {
        var harness = new StudentVmHarness();
        using var vm = harness.CreateStudents();

        await vm.EnsureLoadedAsync();
        await vm.EnsureLoadedAsync();

        await harness.Repository.Received(1).GetListAsync(
            Arg.Any<StudentListQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Filters_sort_and_search_are_sent_to_sql()
    {
        var harness = new StudentVmHarness();
        using var vm = harness.CreateStudents();
        await vm.EnsureLoadedAsync();

        vm.SearchText = "ali";
        vm.SelectedLevel = vm.Levels.Single(option => option.Id == 1);
        vm.SelectedStatus = vm.Statuses.Single(option => option.Id == 2);
        vm.SelectedSort = vm.SortOptions.Single(option => option.Field == StudentSortField.Code);
        vm.SortDescending = true;

        harness.Repository.ClearReceivedCalls();
        await vm.SearchCommand.ExecuteAsync(null);

        await harness.Repository.Received(1).GetListAsync(
            Arg.Is<StudentListQuery>(query =>
                query.SearchTerm == "ali"
                && query.Status == StudentStatuses.Inactive
                && query.AcademicLevelId == 1
                && query.SortBy == StudentSortField.Code
                && query.Descending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_read_failure_is_shown_without_the_renew_button()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetListAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentListItem>>.Fail(Failure.Database()));

        using var vm = harness.CreateStudents();
        await vm.EnsureLoadedAsync();

        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be(TranslationKeys.Errors.DatabaseFailure);
        vm.IsRenewVisible.Should().BeFalse();
    }

    [Fact]
    public async Task An_empty_result_sets_the_empty_state()
    {
        var harness = new StudentVmHarness();
        using var vm = harness.CreateStudents();

        await vm.RefreshAsync();

        vm.IsEmpty.Should().BeTrue();
        vm.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_opens_the_form_and_save_writes_and_closes_it()
    {
        var harness = new StudentVmHarness();
        StudentSaveRequest? saved = null;
        harness.Repository
            .SaveAsync(Arg.Do<StudentSaveRequest>(request => saved = request), Arg.Any<CancellationToken>())
            .Returns(Result<StudentSaveOutcome>.Success(new StudentSaveOutcome(3, true, "STU-2026-0003")));

        using var vm = harness.CreateStudents();
        await vm.EnsureLoadedAsync();

        vm.AddCommand.Execute(null);
        vm.IsEditorOpen.Should().BeTrue();
        vm.EditorTitle.Should().Be(TranslationKeys.Students.Add);
        vm.EditorFirstName = "  Dina  ";
        vm.EditorLastName = " Diallo ";
        vm.EditorGuardianName = " Guardian Two ";
        vm.EditorGuardianPhone = "0661000000";
        vm.SelectedEditorStatus = vm.Statuses.Single(option => option.Id == 1);

        await vm.SaveEditorCommand.ExecuteAsync(null);

        vm.IsEditorOpen.Should().BeFalse();
        vm.HasError.Should().BeFalse();
        vm.StatusMessage.Should().Be(TranslationKeys.Messages.SavedSuccessfully);

        saved.Should().NotBeNull();
        saved!.FirstName.Should().Be("Dina");
        saved.LastName.Should().Be("Diallo");
        saved.GuardianName.Should().Be("Guardian Two");
        saved.GuardianPhone.Should().Be("+213661000000");
        saved.Status.Should().Be(StudentStatuses.Active);
        saved.StudentId.Should().BeNull();
    }

    [Fact]
    public async Task A_blocked_save_shows_the_renew_button_and_keeps_the_form_open()
    {
        var harness = new StudentVmHarness(StudentVmHarness.Blocking());
        using var vm = harness.CreateStudents();
        await vm.EnsureLoadedAsync();

        vm.AddCommand.Execute(null);
        vm.EditorFirstName = "Dina";
        vm.EditorLastName = "Diallo";

        await vm.SaveEditorCommand.ExecuteAsync(null);

        vm.IsEditorOpen.Should().BeTrue();
        vm.IsRenewVisible.Should().BeTrue();
        vm.ErrorMessage.Should().Be(TranslationKeys.License.ErrorWriteBlocked);
        await harness.Repository.DidNotReceive().SaveAsync(
            Arg.Any<StudentSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Editing_loads_the_details_into_the_form()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<StudentDetails?>.Success(StudentVmHarness.Details()));
        harness.Repository
            .GetListAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentListItem>>.Success(
                new[] { StudentVmHarness.Item() }));

        using var vm = harness.CreateStudents();
        await vm.EnsureLoadedAsync();

        await vm.EditCommand.ExecuteAsync(vm.Items[0]);

        vm.IsEditorOpen.Should().BeTrue();
        vm.EditingId.Should().Be(1);
        vm.EditorTitle.Should().Be(TranslationKeys.Students.Edit);
        vm.EditorFirstName.Should().Be("Alice");
        vm.EditorDateOfBirth.Should().Be("2010-05-04");
        vm.SelectedEditorGender!.Value.Should().Be("female");
        vm.SelectedEditorStatus!.Id.Should().Be(1);
        vm.EditorGuardianEmail.Should().Be("guardian@example.com");
        vm.CreateSaveRequest().StudentId.Should().Be(1);
    }

    [Fact]
    public async Task A_declined_confirmation_does_not_delete()
    {
        var harness = new StudentVmHarness();
        harness.Dialogs
            .ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);

        using var vm = harness.CreateStudents();
        await vm.EnsureLoadedAsync();

        await vm.DeleteCommand.ExecuteAsync(StudentVmHarness.Item());

        await harness.Repository.DidNotReceive().DeleteAsync(
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_confirmed_delete_removes_the_student_and_refreshes()
    {
        var harness = new StudentVmHarness();
        using var vm = harness.CreateStudents();
        await vm.EnsureLoadedAsync();

        await vm.DeleteCommand.ExecuteAsync(StudentVmHarness.Item());

        await harness.Repository.Received(1).DeleteAsync(1, Arg.Any<CancellationToken>());
        vm.StatusMessage.Should().Be(TranslationKeys.Messages.DeleteSuccess);
    }

    [Fact]
    public async Task A_linked_student_reports_the_counted_delete_block()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetDependenciesAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<StudentDependencies>.Success(new StudentDependencies(2, 1, 0)));

        using var vm = harness.CreateStudents();
        await vm.EnsureLoadedAsync();

        await vm.DeleteCommand.ExecuteAsync(StudentVmHarness.Item());

        vm.ErrorMessage.Should().Be(TranslationKeys.Students.DeleteBlocked);
        vm.IsRenewVisible.Should().BeFalse();
        await harness.Repository.DidNotReceive().DeleteAsync(
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Import_opens_the_per_row_report()
    {
        var harness = new StudentVmHarness();
        harness.Files
            .OpenAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new PickedFile("students.csv", new byte[] { 1 }));
        harness.Gateway
            .ReadAsync(StudentFileFormat.Csv, Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(Result<ParsedStudentFile>.Success(new ParsedStudentFile(
                new[]
                {
                    new StudentImportRow(2, "Dina", "Diallo", null, null, null, null, null, 0),
                    new StudentImportRow(3, "Bob", " ", null, null, null, null, null, 0),
                },
                Array.Empty<StudentImportError>())));

        using var vm = harness.CreateStudents();
        await vm.EnsureLoadedAsync();

        await vm.ImportCsvCommand.ExecuteAsync(null);

        vm.IsImportReportOpen.Should().BeTrue();
        vm.ImportErrors.Should().ContainSingle();
        vm.ImportErrors[0].Row.Should().Be(TranslationKeys.Students.ImportRow);
        vm.ImportErrors[0].Reference.Should().Be("Bob");
        vm.ImportErrors[0].Message.Should().Be(TranslationKeys.Validators.Required);
        vm.ImportReportSummary.Should().Be(TranslationKeys.Students.ImportSuccess);
        vm.StatusMessage.Should().Be(TranslationKeys.Students.ImportSuccess);
        await harness.Repository.Received(1).SaveAsync(
            Arg.Any<StudentSaveRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Export_writes_the_picked_path()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetExportRowsAsync(Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentExportRow>>.Success(new[]
            {
                new StudentExportRow(
                    1, "STU-2026-0001", "Alice", "Anderson", null, null, null, null,
                    StudentStatuses.Active, null, 0, null, null, null, null, null, null, null),
            }));
        harness.Files
            .PickSavePathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns("C:\\temp\\students.csv");

        using var vm = harness.CreateStudents();
        await vm.EnsureLoadedAsync();

        await vm.ExportCsvCommand.ExecuteAsync(null);

        await harness.Files.Received(1).WriteAllBytesAsync(
            "C:\\temp\\students.csv",
            Arg.Any<byte[]>());
        vm.StatusMessage.Should().Be(TranslationKeys.Students.ExportSuccess);
    }

    [Fact]
    public async Task A_language_change_refreshes_the_sort_labels_and_the_editor_title()
    {
        var harness = new StudentVmHarness();
        harness.Localization.Values[TranslationKeys.Students.SortName] = "Name";
        harness.Localization.Values[TranslationKeys.Students.Add] = "Add student";

        using var vm = harness.CreateStudents();
        await vm.EnsureLoadedAsync();
        vm.AddCommand.Execute(null);

        vm.SortOptions[0].Label.Should().Be("Name");
        vm.EditorTitle.Should().Be("Add student");

        harness.Localization.Values[TranslationKeys.Students.SortName] = "Nom";
        harness.Localization.Values[TranslationKeys.Students.Add] = "Ajouter";
        harness.Localization.RaiseLanguageChanged();

        vm.SortOptions[0].Label.Should().Be("Nom");
        vm.EditorTitle.Should().Be("Ajouter");
    }

    [Fact]
    public async Task Opening_the_details_page_navigates_to_it()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<StudentDetails?>.Success(StudentVmHarness.Details()));
        harness.Repository
            .GetListAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentListItem>>.Success(
                new[] { StudentVmHarness.Item() }));

        using var details = harness.CreateDetails();
        harness.Navigation.NavigateTo<StudentDetailsViewModel>().Returns(details);

        using var vm = harness.CreateStudents();
        await vm.EnsureLoadedAsync();

        await vm.DetailsCommand.ExecuteAsync(vm.Items[0]);

        harness.Navigation.Received(1).NavigateTo<StudentDetailsViewModel>();
        details.StudentId.Should().Be(1);
        details.Details!.FirstName.Should().Be("Alice");
    }
}

public sealed class StudentDetailsViewModelTests
{
    [Fact]
    public async Task Details_and_the_three_tabs_are_loaded_by_the_activity_use_case()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<StudentDetails?>.Success(StudentVmHarness.Details()));
        harness.Enrollments
            .GetByStudentAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<EnrollmentListItem>>.Success(new[]
            {
                new EnrollmentListItem(
                    1, 1, "STU-2026-0001", "Alice Anderson", 5, "Group One",
                    new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), StudentStatuses.Active, 0),
            }));
        harness.Repository
            .GetAbsenceSummaryAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<StudentAbsenceSummary>.Success(
                new StudentAbsenceSummary(1, 2, 5, false, false)));

        using var vm = harness.CreateDetails();
        await vm.InitializeAsync(1);

        vm.Details.Should().NotBeNull();
        vm.FullName.Should().Be("Alice Anderson");
        vm.DateOfBirthText.Should().Be("2010-05-04");
        vm.GenderText.Should().Be(TranslationKeys.Students.Female);
        vm.StatusText.Should().Be(TranslationKeys.Students.Active);
        vm.RegistrationFeeText.Should().Be("1500");
        vm.Enrollments.Should().ContainSingle();
        vm.HasEnrollments.Should().BeTrue();
        vm.HasPayments.Should().BeFalse();
        vm.HasAttendance.Should().BeFalse();
        vm.AbsenceText.Should().Be("2 / 5");
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task A_missing_student_shows_an_error()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetDetailsAsync(9, Arg.Any<CancellationToken>())
            .Returns(Result<StudentDetails?>.Success(null));

        using var vm = harness.CreateDetails();
        await vm.InitializeAsync(9);

        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be(TranslationKeys.Errors.NotFound);
    }

    [Fact]
    public async Task Details_edit_opens_the_shared_form_on_the_list()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetDetailsAsync(1, Arg.Any<CancellationToken>())
            .Returns(Result<StudentDetails?>.Success(StudentVmHarness.Details()));
        harness.Repository
            .GetListAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentListItem>>.Success(
                new[] { StudentVmHarness.Item() }));

        using var students = harness.CreateStudents();
        harness.Navigation.NavigateTo<StudentsViewModel>().Returns(students);

        using var vm = harness.CreateDetails();
        await vm.InitializeAsync(1);
        await vm.EditCommand.ExecuteAsync(null);

        students.IsEditorOpen.Should().BeTrue();
        students.EditingId.Should().Be(1);
    }
}

public sealed class StudentCardViewModelTests
{
    [Fact]
    public async Task A_preview_failure_is_displayed()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetCardDataAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentCardData>>.Success(Array.Empty<StudentCardData>()));

        var vm = harness.CreateCard();
        await vm.InitializeAsync(1);

        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be(TranslationKeys.Errors.NotFound);
        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task A_preview_success_clears_the_error()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetCardDataAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentCardData>>.Success(
                new[] { StudentVmHarness.Card() }));

        var vm = harness.CreateCard();
        await vm.InitializeAsync(1);

        vm.HasError.Should().BeFalse();
        vm.StudentId.Should().Be(1);
        harness.Cards.Received(1).RenderCardPreviewPng(Arg.Any<StudentCardData>());
    }

    [Fact]
    public async Task Printing_uses_the_pdf_builder_and_the_print_service()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetCardDataAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentCardData>>.Success(
                new[] { StudentVmHarness.Card() }));

        var vm = harness.CreateCard();
        await vm.InitializeAsync(1);
        await vm.PrintCommand.ExecuteAsync(null);

        await harness.Print.Received(1).PrintAsync(
            Arg.Is<byte[]>(pdf => pdf.Length == 2),
            "student-card-1.pdf");
    }
}

public sealed class StudentsQrPrintViewModelTests
{
    [Fact]
    public async Task The_filtered_students_are_rendered_as_tiles()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetListAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentListItem>>.Success(new[]
            {
                StudentVmHarness.Item(),
                StudentVmHarness.Item(2, "Bob", "Brown", "STU-2026-0002"),
            }));
        harness.Repository
            .GetCardDataAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentCardData>>.Success(new[]
            {
                StudentVmHarness.Card(),
                StudentVmHarness.Card(2),
            }));

        var vm = harness.CreateQrPrint();
        await vm.InitializeAsync(new StudentListQuery(Status: StudentStatuses.Active));

        vm.Tiles.Should().HaveCount(2);
        vm.Tiles[0].StudentCode.Should().Be("STU-2026-0001");
        vm.Summary.Should().Be(TranslationKeys.Students.QrPageOf);
        vm.IsEmpty.Should().BeFalse();
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task Printing_without_tiles_reports_the_empty_key()
    {
        var harness = new StudentVmHarness();
        var vm = harness.CreateQrPrint();
        await vm.InitializeAsync(new StudentListQuery());

        await vm.PrintAllCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be(TranslationKeys.Students.QrEmpty);
        await harness.Print.DidNotReceive().PrintAsync(Arg.Any<byte[]>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Saving_the_sheet_writes_the_picked_pdf()
    {
        var harness = new StudentVmHarness();
        harness.Repository
            .GetListAsync(Arg.Any<StudentListQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentListItem>>.Success(
                new[] { StudentVmHarness.Item() }));
        harness.Repository
            .GetCardDataAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<StudentCardData>>.Success(
                new[] { StudentVmHarness.Card() }));
        harness.Files
            .PickSavePathAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns("C:\\temp\\qr.pdf");

        var vm = harness.CreateQrPrint();
        await vm.InitializeAsync(new StudentListQuery());
        await vm.SavePdfCommand.ExecuteAsync(null);

        await harness.Files.Received(1).WriteAllBytesAsync(
            "C:\\temp\\qr.pdf",
            Arg.Any<byte[]>());
    }

    [Fact]
    public async Task Closing_returns_to_the_students_tab()
    {
        var harness = new StudentVmHarness();
        var vm = harness.CreateQrPrint();

        await vm.CloseCommand.ExecuteAsync(null);

        harness.Navigation.Received(1).NavigateToTab(ShellTab.Students);
    }
}
