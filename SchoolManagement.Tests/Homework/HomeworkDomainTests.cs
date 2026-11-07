using FluentAssertions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Tests.Homework;

/// <summary>
/// Pure homework rules: submitted vs late vs missing vs pending, and the
/// "due for this attendance session" link.
/// </summary>
public sealed class HomeworkDomainTests
{
    private static readonly DateTime Due = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void An_end_of_day_instant_is_utc_and_keeps_the_date()
    {
        var end = HomeworkPolicy.EndOfDay(Due);

        end.Kind.Should().Be(DateTimeKind.Utc);
        end.Date.Should().Be(Due.Date);
        end.Hour.Should().Be(23);
        end.Minute.Should().Be(59);
        end.Second.Should().Be(59);
    }

    [Theory]
    [InlineData(2026, 11, 9, false)]
    [InlineData(2026, 11, 10, false)]
    [InlineData(2026, 11, 11, true)]
    public void Overdue_starts_the_day_after_the_due_date(int year, int month, int day, bool expected)
        => HomeworkPolicy.IsOverdue(Due, new DateTime(year, month, day, 8, 0, 0, DateTimeKind.Utc))
            .Should().Be(expected);

    [Fact]
    public void A_submission_on_the_due_day_is_submitted()
        => HomeworkPolicy.Resolve(
                new DateTime(2026, 11, 10, 22, 0, 0, DateTimeKind.Utc),
                Due,
                new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc))
            .Should().Be(HomeworkStatuses.Submitted);

    [Fact]
    public void A_late_submission_is_late()
        => HomeworkPolicy.Resolve(
                new DateTime(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc),
                Due,
                new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc))
            .Should().Be(HomeworkStatuses.Late);

    [Fact]
    public void No_submission_after_the_due_date_is_missing()
        => HomeworkPolicy.Resolve(
                null,
                Due,
                new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc))
            .Should().Be(HomeworkStatuses.Missing);

    [Fact]
    public void No_submission_before_the_due_date_is_pending()
        => HomeworkPolicy.Resolve(
                null,
                Due,
                new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc))
            .Should().Be(HomeworkStatuses.Pending);

    [Theory]
    [InlineData(HomeworkStatuses.Submitted, true)]
    [InlineData(HomeworkStatuses.Late, true)]
    [InlineData(HomeworkStatuses.Pending, false)]
    [InlineData(HomeworkStatuses.Missing, false)]
    [InlineData(null, false)]
    public void Only_submitted_and_late_count_as_done(string? status, bool expected)
        => HomeworkPolicy.CountsAsSubmitted(status).Should().Be(expected);

    [Fact]
    public void An_existing_late_row_stays_late()
        => HomeworkPolicy.ResolveExisting(
                HomeworkStatuses.Late,
                new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc),
                Due,
                new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc))
            .Should().Be(HomeworkStatuses.Late);

    [Fact]
    public void A_session_matches_its_due_homeworks_only_while_unfinished()
    {
        HomeworkPolicy.IsDueForSession(12, 12, false).Should().BeTrue();
        HomeworkPolicy.IsDueForSession(12, 12, true).Should().BeFalse();
        HomeworkPolicy.IsDueForSession(12, 13, false).Should().BeFalse();
        HomeworkPolicy.IsDueForSession(null, 12, false).Should().BeFalse();
    }
}
