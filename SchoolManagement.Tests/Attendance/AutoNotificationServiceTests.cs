using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Services;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Tests.Attendance;

/// <summary>
/// The absence-limit notification after an attendance save. The two rules from
/// the phone app: only students who reached the limit are notified, and only
/// while no unread absence notification already exists for that student.
/// </summary>
public sealed class AutoNotificationServiceTests
{
    [Fact]
    public async Task A_student_below_the_limit_gets_no_notification()
    {
        var (service, notifications) = Build(1, Summary(1, absences: 2, limit: 5, reached: false));

        var result = await service.CheckAttendanceLimitAsync([1]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        await notifications.DidNotReceive().AddAsync(
            Arg.Any<Notification>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_student_at_the_limit_gets_one_absence_notification()
    {
        var (service, notifications) = Build(1, Summary(1, absences: 5, limit: 5, reached: true));
        Notification? stored = null;
        notifications.HasUnreadAsync(
                NotificationTypes.Attendance,
                1,
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        notifications.AddAsync(Arg.Do<Notification>(item => stored = item), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(10));

        var result = await service.CheckAttendanceLimitAsync([1]);

        result.Value.Should().Be(1);
        stored.Should().NotBeNull();
        stored!.Type.Should().Be(NotificationTypes.Attendance);
        stored.RecipientType.Should().Be(RecipientTypes.Student);
        stored.RecipientId.Should().Be(1);
        stored.Channel.Should().Be(NotificationChannels.InApp);
        stored.IsRead.Should().BeFalse();
        stored.Title.Should().Contain("First1 Last1");
        stored.Body.Should().Contain("First1 Last1");
        stored.Body.Should().Contain("5");
    }

    [Fact]
    public async Task An_unread_absence_notification_prevents_a_duplicate()
    {
        var (service, notifications) = Build(1, Summary(1, absences: 6, limit: 5, reached: true));
        notifications.HasUnreadAsync(
                NotificationTypes.Attendance,
                1,
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await service.CheckAttendanceLimitAsync([1]);

        result.Value.Should().Be(0);
        await notifications.DidNotReceive().AddAsync(
            Arg.Any<Notification>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Repeated_student_ids_are_evaluated_once()
    {
        var (service, notifications) = Build(1, Summary(1, absences: 0, limit: 5, reached: false));

        var result = await service.CheckAttendanceLimitAsync([1, 1, 1]);

        result.Value.Should().Be(0);
        await notifications.Received(0).AddAsync(
            Arg.Any<Notification>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalid_student_ids_are_skipped()
    {
        var students = Substitute.For<IStudentRepository>();
        var notifications = Substitute.For<INotificationRepository>();
        var service = new AutoNotificationService(students, notifications, Localization(), Clock());

        var result = await service.CheckAttendanceLimitAsync([0, -3]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        await students.DidNotReceive().GetAbsenceSummaryAsync(
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_student_detail_falls_back_to_the_student_id()
    {
        var (service, notifications) = Build(1, Summary(1, absences: 5, limit: 5, reached: true), hasDetails: false);
        notifications.HasUnreadAsync(
                NotificationTypes.Attendance,
                1,
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        Notification? stored = null;
        notifications.AddAsync(Arg.Do<Notification>(item => stored = item), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(10));

        await service.CheckAttendanceLimitAsync([1]);

        stored!.Title.Should().Contain("1");
        stored.Title.Should().NotContain("First");
    }

    [Fact]
    public async Task An_absence_summary_failure_stops_the_sweep()
    {
        var (service, _) = Build(1, Result<StudentAbsenceSummary>.Fail(Failure.Database()));

        var result = await service.CheckAttendanceLimitAsync([1]);

        result.Failure!.Kind.Should().Be(FailureKind.Database);
    }

    [Fact]
    public async Task A_failed_notification_insert_is_returned()
    {
        var (service, notifications) = Build(1, Summary(1, absences: 5, limit: 5, reached: true));
        notifications.HasUnreadAsync(
                NotificationTypes.Attendance,
                1,
                Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(false));
        notifications.AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Fail(Failure.Database()));

        var result = await service.CheckAttendanceLimitAsync([1]);

        result.Failure!.Kind.Should().Be(FailureKind.Database);
    }

    private static (AutoNotificationService Service, INotificationRepository Notifications) Build(
        int studentId,
        Result<StudentAbsenceSummary> summary,
        bool hasDetails = true)
    {
        var students = Substitute.For<IStudentRepository>();
        students.GetAbsenceSummaryAsync(studentId, Arg.Any<CancellationToken>())
            .Returns(summary);
        students.GetDetailsAsync(studentId, Arg.Any<CancellationToken>())
            .Returns(Result<StudentDetails?>.Success(hasDetails
                ? new StudentDetails(
                    studentId,
                    "STU-2026-0001",
                    "First1",
                    "Last1",
                    null,
                    null,
                    null,
                    null,
                    null,
                    StudentStatuses.Active,
                    null,
                    0,
                    null,
                    null,
                    null)
                : null));

        var notifications = Substitute.For<INotificationRepository>();
        return (new AutoNotificationService(students, notifications, Localization(), Clock()), notifications);
    }

    private static Result<StudentAbsenceSummary> Summary(
        int studentId,
        int absences,
        int limit,
        bool reached)
        => Result<StudentAbsenceSummary>.Success(
            new StudentAbsenceSummary(studentId, absences, limit, reached, absences > limit));

    private static ILocalizationService Localization()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.Tr(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
            .Returns(call =>
            {
                var key = call.Arg<string>();
                var parameters = call.Arg<IReadOnlyDictionary<string, string>>();
                return key + "|" + string.Join(
                    ",",
                    parameters.Select(pair => pair.Key + "=" + pair.Value));
            });
        return localization;
    }

    private static ITrustedClock Clock()
    {
        var clock = Substitute.For<ITrustedClock>();
        clock.LocalNow.Returns(new DateTime(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc));
        clock.UtcNow.Returns(new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc));
        return clock;
    }
}
