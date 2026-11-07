using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Homeworks;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;
using HomeworkEntity = SchoolManagement.Domain.Entities.Homework;

namespace SchoolManagement.Tests.Homework;

/// <summary>
/// Phase 7 homework use cases: the license gate, validation, normalization, the
/// late/submitted resolution of a submission and the report bundle.
/// </summary>
public sealed class HomeworkUseCaseTests
{
    private static readonly DateTime DueDate = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Today = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

    // SaveHomework ----------------------------------------------------------

    [Fact]
    public async Task Saving_homework_checks_the_license_gate_first()
    {
        var repository = Substitute.For<IHomeworkRepository>();
        var guard = Blocking();

        var result = await new SaveHomeworkUseCase(repository, Clock(), guard).ExecuteAsync(Request());

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await repository.DidNotReceive().AddAsync(Arg.Any<HomeworkEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_homework_without_a_session_is_rejected_before_the_repository()
    {
        var repository = Substitute.For<IHomeworkRepository>();

        var result = await new SaveHomeworkUseCase(repository, Clock(), Allowing())
            .ExecuteAsync(Request() with { SessionId = 0 });

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.Required);
        result.Failure.Parameters["field"].Should().Be("sessionId");
        await repository.DidNotReceive().AddAsync(Arg.Any<HomeworkEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_homework_with_a_non_utc_due_date_is_rejected()
    {
        var repository = Substitute.For<IHomeworkRepository>();

        var result = await new SaveHomeworkUseCase(repository, Clock(), Allowing())
            .ExecuteAsync(Request() with { DueDate = new DateTime(2026, 11, 10) });

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidDate);
    }

    [Fact]
    public async Task Saving_homework_normalizes_the_form_before_the_insert()
    {
        var repository = Substitute.For<IHomeworkRepository>();
        HomeworkEntity? stored = null;
        repository.AddAsync(Arg.Do<HomeworkEntity>(homework => stored = homework), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(12));
        var guard = Allowing();

        var result = await new SaveHomeworkUseCase(repository, Clock(), guard)
            .ExecuteAsync(new HomeworkSaveRequest(
                3,
                7,
                0,
                "  Exercise  ",
                "  Page 10  ",
                new DateTime(2026, 11, 10, 18, 30, 0, DateTimeKind.Utc),
                "   "));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.HomeworkId.Should().Be(12);
        result.Value.Created.Should().BeTrue();
        guard.Received(1).EnsureCanWrite();
        stored.Should().NotBeNull();
        stored!.GroupId.Should().Be(3);
        stored.SessionId.Should().Be(7);
        stored.TargetSessionId.Should().BeNull();
        stored.Title.Should().Be("Exercise");
        stored.Description.Should().Be("Page 10");
        stored.Notes.Should().BeNull();
        stored.DueDate.Should().Be(DueDate);
        stored.IsCompleted.Should().BeFalse();
        stored.CreatedAt.Should().Be(DateTime.SpecifyKind(Today, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Updating_homework_keeps_its_id_and_reports_created_false()
    {
        var repository = Substitute.For<IHomeworkRepository>();
        var existing = new HomeworkEntity
        {
            Id = 9,
            GroupId = 3,
            SessionId = 7,
            Title = "Old",
            Description = "Old body",
            DueDate = DueDate,
        };
        repository.GetByIdAsync(9, Arg.Any<CancellationToken>())
            .Returns(Result<HomeworkEntity?>.Success(existing));
        HomeworkEntity? stored = null;
        repository.UpdateAsync(Arg.Do<HomeworkEntity>(homework => stored = homework), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await new SaveHomeworkUseCase(repository, Clock(), Allowing())
            .ExecuteAsync(Request() with { HomeworkId = 9 });

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.HomeworkId.Should().Be(9);
        result.Value.Created.Should().BeFalse();
        stored!.Title.Should().Be("Exercise");
        stored.UpdatedAt.Should().Be(DateTime.SpecifyKind(Today, DateTimeKind.Utc));
        await repository.DidNotReceive().AddAsync(Arg.Any<HomeworkEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Updating_a_missing_homework_reports_not_found()
    {
        var repository = Substitute.For<IHomeworkRepository>();
        repository.GetByIdAsync(9, Arg.Any<CancellationToken>())
            .Returns(Result<HomeworkEntity?>.Success(null));

        var result = await new SaveHomeworkUseCase(repository, Clock(), Allowing())
            .ExecuteAsync(Request() with { HomeworkId = 9 });

        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
        result.Failure.MessageKey.Should().Be(TranslationKeys.Homework.NotFound);
    }

    // SetHomeworkSubmissionStatus ------------------------------------------

    [Fact]
    public async Task Changing_a_submission_status_checks_the_license_gate_first()
    {
        var repository = Substitute.For<IHomeworkRepository>();
        var guard = Blocking();

        var result = await new SetHomeworkSubmissionStatusUseCase(repository, Clock(), guard)
            .ExecuteAsync(new HomeworkSubmissionRequest(1, 2, HomeworkStatuses.Submitted));

        result.Failure!.Kind.Should().Be(FailureKind.License);
        guard.Received(1).EnsureCanWrite();
        await repository.DidNotReceive().SetSubmissionStatusAsync(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<DateTime?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_submission_status_is_rejected()
    {
        var repository = Substitute.For<IHomeworkRepository>();

        var result = await new SetHomeworkSubmissionStatusUseCase(repository, Clock(), Allowing())
            .ExecuteAsync(new HomeworkSubmissionRequest(1, 2, "done"));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidField);
        await repository.DidNotReceive().SetSubmissionStatusAsync(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<DateTime?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Marking_pending_clears_the_submitted_timestamp()
    {
        var repository = RepoWithHomework();
        string? status = null;
        DateTime? submittedAt = Today;
        repository.SetSubmissionStatusAsync(
                9,
                2,
                Arg.Do<string>(value => status = value),
                Arg.Do<DateTime?>(value => submittedAt = value),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await new SetHomeworkSubmissionStatusUseCase(repository, Clock(), Allowing())
            .ExecuteAsync(new HomeworkSubmissionRequest(9, 2, HomeworkStatuses.Pending));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        status.Should().Be(HomeworkStatuses.Pending);
        submittedAt.Should().BeNull();
    }

    [Fact]
    public async Task Marking_missing_clears_the_submitted_timestamp()
    {
        var repository = RepoWithHomework();
        DateTime? submittedAt = Today;
        repository.SetSubmissionStatusAsync(
                9,
                2,
                HomeworkStatuses.Missing,
                Arg.Do<DateTime?>(value => submittedAt = value),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        await new SetHomeworkSubmissionStatusUseCase(repository, Clock(), Allowing())
            .ExecuteAsync(new HomeworkSubmissionRequest(9, 2, HomeworkStatuses.Missing));

        submittedAt.Should().BeNull();
    }

    [Fact]
    public async Task A_submission_after_the_due_date_is_late()
    {
        var repository = RepoWithHomework();
        string? status = null;
        repository.SetSubmissionStatusAsync(
                9,
                2,
                Arg.Do<string>(value => status = value),
                Arg.Any<DateTime?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        var clock = Clock(new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc));

        var result = await new SetHomeworkSubmissionStatusUseCase(repository, clock, Allowing())
            .ExecuteAsync(new HomeworkSubmissionRequest(9, 2, HomeworkStatuses.Submitted));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        status.Should().Be(HomeworkStatuses.Late);
    }

    [Fact]
    public async Task A_submission_on_the_due_day_is_submitted()
    {
        var repository = RepoWithHomework();
        string? status = null;
        repository.SetSubmissionStatusAsync(
                9,
                2,
                Arg.Do<string>(value => status = value),
                Arg.Any<DateTime?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        var clock = Clock(new DateTime(2026, 11, 10, 22, 0, 0, DateTimeKind.Utc));

        await new SetHomeworkSubmissionStatusUseCase(repository, clock, Allowing())
            .ExecuteAsync(new HomeworkSubmissionRequest(9, 2, HomeworkStatuses.Submitted));

        status.Should().Be(HomeworkStatuses.Submitted);
    }

    [Fact]
    public async Task An_explicit_submitted_date_wins_over_the_clock()
    {
        var repository = RepoWithHomework();
        string? status = null;
        DateTime? submittedAt = null;
        repository.SetSubmissionStatusAsync(
                9,
                2,
                Arg.Do<string>(value => status = value),
                Arg.Do<DateTime?>(value => submittedAt = value),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        await new SetHomeworkSubmissionStatusUseCase(repository, Clock(), Allowing())
            .ExecuteAsync(new HomeworkSubmissionRequest(
                9,
                2,
                HomeworkStatuses.Submitted,
                new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)));

        status.Should().Be(HomeworkStatuses.Submitted);
        submittedAt.Should().Be(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task A_missing_homework_reports_not_found()
    {
        var repository = Substitute.For<IHomeworkRepository>();
        repository.GetSummaryAsync(9, Arg.Any<CancellationToken>())
            .Returns(Result<HomeworkListItem?>.Success(null));

        var result = await new SetHomeworkSubmissionStatusUseCase(repository, Clock(), Allowing())
            .ExecuteAsync(new HomeworkSubmissionRequest(9, 2, HomeworkStatuses.Submitted));

        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
    }

    // Read use cases --------------------------------------------------------

    [Fact]
    public async Task The_submission_board_counts_submitted_and_pending_rows()
    {
        var repository = Substitute.For<IHomeworkRepository>();
        repository.GetSummaryAsync(9, Arg.Any<CancellationToken>())
            .Returns(Result<HomeworkListItem?>.Success(Item()));
        repository.GetSubmissionBoardAsync(9, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkSubmissionItem>>.Success(
            [
                Submission(1, HomeworkStatuses.Submitted),
                Submission(2, HomeworkStatuses.Late),
                Submission(3, HomeworkStatuses.Pending),
                Submission(4, HomeworkStatuses.Missing),
            ]));

        var result = await new GetHomeworkSubmissionBoardUseCase(repository).ExecuteAsync(9);

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.SubmittedCount.Should().Be(2);
        result.Value.PendingCount.Should().Be(2);
        result.Value.Submissions.Should().HaveCount(4);
    }

    [Fact]
    public async Task The_submission_board_reports_a_missing_homework()
    {
        var repository = Substitute.For<IHomeworkRepository>();
        repository.GetSummaryAsync(9, Arg.Any<CancellationToken>())
            .Returns(Result<HomeworkListItem?>.Success(null));

        var result = await new GetHomeworkSubmissionBoardUseCase(repository).ExecuteAsync(9);

        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
        result.Failure.MessageKey.Should().Be(TranslationKeys.Homework.NotFound);
    }

    [Fact]
    public async Task The_homework_report_rejects_an_inverted_period()
    {
        var repository = Substitute.For<IHomeworkRepository>();

        var result = await new GetHomeworkReportUseCase(
                repository,
                Substitute.For<IGroupRepository>(),
                Substitute.For<IReferenceDataRepository>(),
                Clock())
            .ExecuteAsync(new HomeworkReportQuery(FromDate: Today, ToDate: Today.AddDays(-1)));

        result.Failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidDate);
        await repository.DidNotReceive().GetListAsync(
            Arg.Any<HomeworkReportQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_homework_report_builds_the_bundle()
    {
        var repository = Substitute.For<IHomeworkRepository>();
        repository.GetListAsync(Arg.Any<HomeworkReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkListItem>>.Success([Item()]));
        var groups = Substitute.For<IGroupRepository>();
        groups.GetSummaryAsync(3, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Success(
                new GroupSummary(3, "Group One", 1, "Math", 10, "Teacher", 2, "Level", 3, "Grade", 30, 100, true, 2)));
        var references = Substitute.For<IReferenceDataRepository>();
        references.GetSchoolNameAsync(Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("School One"));

        var result = await new GetHomeworkReportUseCase(repository, groups, references, Clock())
            .ExecuteAsync(new HomeworkReportQuery(GroupId: 3, FromDate: Today, ToDate: Today.AddDays(30)));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.SchoolName.Should().Be("School One");
        result.Value.GroupName.Should().Be("Group One");
        result.Value.TotalCount.Should().Be(1);
        result.Value.CompletedCount.Should().Be(0);
        result.Value.SubmittedTotal.Should().Be(1);
        result.Value.PendingTotal.Should().Be(2);
        result.Value.GeneratedAt.Should().Be(Today);
    }

    [Fact]
    public async Task The_homework_report_propagates_a_group_failure()
    {
        var repository = Substitute.For<IHomeworkRepository>();
        repository.GetListAsync(Arg.Any<HomeworkReportQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<HomeworkListItem>>.Success([]));
        var groups = Substitute.For<IGroupRepository>();
        groups.GetSummaryAsync(3, Arg.Any<CancellationToken>())
            .Returns(Result<GroupSummary?>.Fail(Failure.Database()));
        var references = Substitute.For<IReferenceDataRepository>();
        references.GetSchoolNameAsync(Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("School One"));

        var result = await new GetHomeworkReportUseCase(repository, groups, references, Clock())
            .ExecuteAsync(new HomeworkReportQuery(GroupId: 3));

        result.Failure!.Kind.Should().Be(FailureKind.Database);
    }

    // Helpers ---------------------------------------------------------------

    private static HomeworkSaveRequest Request() => new(
        3,
        7,
        8,
        "Exercise",
        "Page 10",
        DueDate);

    private static IHomeworkRepository RepoWithHomework()
    {
        var repository = Substitute.For<IHomeworkRepository>();
        repository.GetSummaryAsync(9, Arg.Any<CancellationToken>())
            .Returns(Result<HomeworkListItem?>.Success(Item()));
        return repository;
    }

    private static HomeworkListItem Item() => new(
        9,
        3,
        7,
        7,
        "Exercise",
        "Page 10",
        DueDate,
        false,
        3,
        1,
        2);

    private static HomeworkSubmissionItem Submission(int studentId, string status) => new(
        studentId,
        9,
        studentId,
        $"STU-2026-{studentId:0000}",
        $"Student {studentId}",
        status,
        null,
        null);

    private static ITrustedClock Clock(DateTime? now = null)
    {
        var clock = Substitute.For<ITrustedClock>();
        var value = now ?? Today;
        clock.LocalNow.Returns(value);
        clock.UtcNow.Returns(value);
        return clock;
    }

    private static ILicenseGuard Allowing()
    {
        var guard = Substitute.For<ILicenseGuard>();
        guard.EnsureCanWrite().Returns(Result<bool>.Success(true));
        return guard;
    }

    private static ILicenseGuard Blocking()
    {
        var guard = Substitute.For<ILicenseGuard>();
        guard.EnsureCanWrite().Returns(Result<bool>.Fail(
            Failure.License(TranslationKeys.License.ErrorWriteBlocked)));
        return guard;
    }
}
