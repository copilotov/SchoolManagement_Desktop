using FluentAssertions;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Tests.Groups;

/// <summary>
/// Pure Phase 6 rules: the half-open overlap test, the warning-only conflict
/// detection, the date/time combination used by session generation and the
/// computed values of the group projections.
/// </summary>
public sealed class GroupsDomainTests
{
    private static readonly DateTime Day = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(8, 0, 9, 0, 8, 30, 9, 30, true)]
    [InlineData(8, 0, 9, 0, 7, 0, 8, 0, false)]
    [InlineData(8, 0, 9, 0, 9, 0, 10, 0, false)]
    [InlineData(8, 0, 10, 0, 9, 0, 11, 0, true)]
    [InlineData(8, 0, 9, 0, 8, 0, 9, 0, true)]
    [InlineData(8, 0, 9, 0, 9, 0, 9, 0, false)]
    public void Overlaps_uses_half_open_intervals(
        int firstStartHour,
        int firstStartMinute,
        int firstEndHour,
        int firstEndMinute,
        int secondStartHour,
        int secondStartMinute,
        int secondEndHour,
        int secondEndMinute,
        bool expected)
    {
        var firstStart = new TimeSpan(firstStartHour, firstStartMinute, 0);
        var firstEnd = new TimeSpan(firstEndHour, firstEndMinute, 0);
        var secondStart = new TimeSpan(secondStartHour, secondStartMinute, 0);
        var secondEnd = new TimeSpan(secondEndHour, secondEndMinute, 0);

        ScheduleConflictPolicy.Overlaps(firstStart, firstEnd, secondStart, secondEnd)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(8, 0, 9, 0, true)]
    [InlineData(8, 0, 8, 0, false)]
    [InlineData(9, 0, 8, 0, false)]
    public void A_valid_range_ends_after_it_starts(int startHour, int startMinute, int endHour, int endMinute, bool expected)
    {
        ScheduleConflictPolicy.IsValidRange(
                new TimeSpan(startHour, startMinute, 0),
                new TimeSpan(endHour, endMinute, 0))
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    public void The_school_week_only_accepts_seven_days(int dayOfWeek, bool expected)
        => ScheduleConflictPolicy.IsValidDay(dayOfWeek).Should().Be(expected);

    [Fact]
    public void Detect_reports_the_same_group_conflict()
    {
        var candidate = new ScheduleSlotCandidate(0, 1, 10, 0, At(8), At(9));
        var existing = new[]
        {
            new ScheduleConflictSource(5, 1, "Group One", 10, 0, At(8, 30), At(9, 30)),
        };

        var conflicts = ScheduleConflictPolicy.Detect(candidate, existing);

        conflicts.Should().ContainSingle();
        conflicts[0].Kind.Should().Be(ScheduleConflictKind.SameGroup);
        conflicts[0].SlotId.Should().Be(5);
        conflicts[0].GroupName.Should().Be("Group One");
    }

    [Fact]
    public void Detect_reports_the_same_teacher_across_groups()
    {
        var candidate = new ScheduleSlotCandidate(0, 1, 10, 2, At(10), At(11));
        var existing = new[]
        {
            new ScheduleConflictSource(7, 2, "Group Two", 10, 2, At(10, 30), At(11, 30)),
        };

        var conflicts = ScheduleConflictPolicy.Detect(candidate, existing);

        conflicts.Should().ContainSingle();
        conflicts[0].Kind.Should().Be(ScheduleConflictKind.SameTeacher);
        conflicts[0].GroupName.Should().Be("Group Two");
    }

    [Fact]
    public void Detect_ignores_other_days_other_teachers_and_self()
    {
        var candidate = new ScheduleSlotCandidate(3, 1, 10, 1, At(8), At(9));
        var existing = new[]
        {
            // Same group but another day.
            new ScheduleConflictSource(1, 1, "Group One", 10, 2, At(8), At(9)),
            // Same day and overlapping but another group and another teacher.
            new ScheduleConflictSource(2, 2, "Group Two", 20, 1, At(8), At(9)),
            // The edited slot itself never conflicts with itself.
            new ScheduleConflictSource(3, 1, "Group One", 10, 1, At(8), At(9)),
            // Same group, same day, no time overlap.
            new ScheduleConflictSource(4, 1, "Group One", 10, 1, At(9), At(10)),
        };

        ScheduleConflictPolicy.Detect(candidate, existing).Should().BeEmpty();
    }

    [Fact]
    public void A_zero_slot_id_is_never_excluded_as_self()
    {
        var candidate = new ScheduleSlotCandidate(0, 1, null, 0, At(8), At(9));
        var existing = new[]
        {
            new ScheduleConflictSource(0, 1, "Group One", null, 0, At(8), At(9)),
        };

        ScheduleConflictPolicy.Detect(candidate, existing).Should().ContainSingle();
    }

    [Fact]
    public void A_null_teacher_never_matches_another_null_teacher()
    {
        var candidate = new ScheduleSlotCandidate(0, 1, null, 0, At(8), At(9));
        var existing = new[]
        {
            new ScheduleConflictSource(9, 2, "Group Two", null, 0, At(8), At(9)),
        };

        ScheduleConflictPolicy.Detect(candidate, existing).Should().BeEmpty();
    }

    [Fact]
    public void Combine_merges_a_date_and_a_time_of_day_as_utc()
    {
        var combined = SessionSchedulePolicy.Combine(Day, new TimeSpan(14, 30, 0));

        combined.Kind.Should().Be(DateTimeKind.Utc);
        combined.Year.Should().Be(2026);
        combined.Month.Should().Be(11);
        combined.Day.Should().Be(2);
        combined.Hour.Should().Be(14);
        combined.Minute.Should().Be(30);
    }

    [Fact]
    public void Combine_accepts_a_stored_time_of_day_date()
    {
        var stored = new DateTime(2000, 1, 1, 9, 15, 0, DateTimeKind.Utc);

        var combined = SessionSchedulePolicy.Combine(Day, stored);

        combined.Should().Be(new DateTime(2026, 11, 2, 9, 15, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Combine_keeps_the_date_components_of_the_input()
    {
        var combined = SessionSchedulePolicy.Combine(
            new DateTime(2027, 1, 31, 23, 59, 0, DateTimeKind.Utc),
            new TimeSpan(7, 5, 0));

        combined.Should().Be(new DateTime(2027, 1, 31, 7, 5, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Session_ranges_must_end_after_they_start()
    {
        var start = new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc);

        SessionSchedulePolicy.IsValidRange(start, start.AddHours(1)).Should().BeTrue();
        SessionSchedulePolicy.IsValidRange(start, start).Should().BeFalse();
        SessionSchedulePolicy.IsValidRange(start, start.AddHours(-1)).Should().BeFalse();
    }

    [Theory]
    [InlineData(30, 12, false, false, 18, "12/30")]
    [InlineData(30, 30, true, false, 0, "30/30")]
    [InlineData(30, 31, true, true, 0, "31/30")]
    [InlineData(0, 0, true, false, 0, "0/0")]
    public void Group_summary_exposes_the_capacity_badges(
        int maxStudents,
        int active,
        bool isFull,
        bool isOverCapacity,
        int remaining,
        string capacityText)
    {
        var summary = Summary(maxStudents: maxStudents, active: active);

        summary.IsFull.Should().Be(isFull);
        summary.IsOverCapacity.Should().Be(isOverCapacity);
        summary.RemainingSeats.Should().Be(remaining);
        summary.CapacityText.Should().Be(capacityText);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(4, true)]
    public void Group_summary_reports_whether_it_has_a_schedule(int slots, bool hasSchedule)
        => Summary(weeklySlots: slots).HasSchedule.Should().Be(hasSchedule);

    [Fact]
    public void Attendance_rate_counts_present_and_late_over_records()
    {
        new GroupAttendanceStats(4, 10, 6, 3, 1).AttendanceRate.Should().Be(70);
        new GroupAttendanceStats(0, 0, 0, 0, 0).AttendanceRate.Should().Be(0);
    }

    [Fact]
    public void Session_generation_total_adds_created_and_existing()
    {
        new SessionGenerationResult(3, 2).Total.Should().Be(5);
        new SessionGenerationResult(0, 0).Total.Should().Be(0);
    }

    [Fact]
    public void Group_dependencies_are_linked_when_any_counter_is_positive()
    {
        new GroupDependencies(0, 0, 0).HasLinks.Should().BeFalse();
        new GroupDependencies(1, 0, 0).HasLinks.Should().BeTrue();
        new GroupDependencies(0, 2, 0).HasLinks.Should().BeTrue();
        new GroupDependencies(0, 0, 3).HasLinks.Should().BeTrue();
    }

    [Fact]
    public void A_report_totals_its_dues()
    {
        var data = new GroupReportData(
            "Test School",
            null,
            Summary(),
            Array.Empty<GroupReportStudent>(),
            new GroupAttendanceStats(1, 1, 1, 0, 0),
            new[]
            {
                Due(expected: 1000, paid: 400, outstanding: 600),
                Due(expected: 1000, paid: 1000, outstanding: 0),
            },
            "2026-11",
            Day);

        data.TotalExpected.Should().Be(2000);
        data.TotalPaid.Should().Be(1400);
        data.TotalOutstanding.Should().Be(600);
    }

    [Fact]
    public void FromIndex_is_the_inverse_of_ToIndex_and_rejects_an_invalid_index()
    {
        for (var index = 0; index < 7; index++)
        {
            WeekDayCalculator.ToIndex(WeekDayCalculator.FromIndex(index)).Should().Be(index);
        }

        var act = () => WeekDayCalculator.FromIndex(7);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(WeekDay.Saturday, "days.saturday")]
    [InlineData(WeekDay.Sunday, "days.sunday")]
    [InlineData(WeekDay.Monday, "days.monday")]
    [InlineData(WeekDay.Tuesday, "days.tuesday")]
    [InlineData(WeekDay.Wednesday, "days.wednesday")]
    [InlineData(WeekDay.Thursday, "days.thursday")]
    [InlineData(WeekDay.Friday, "days.friday")]
    public void Every_school_day_has_its_translation_key(WeekDay day, string key)
        => WeekDayCalculator.ToTranslationKey(day).Should().Be(key);

    private static TimeSpan At(int hour, int minute = 0) => new(hour, minute, 0);

    private static GroupSummary Summary(
        int maxStudents = 30,
        int active = 0,
        int weeklySlots = 0) => new(
        1,
        "Group One",
        1,
        "Mathematics",
        1,
        "Teacher One",
        1,
        "Level One",
        1,
        "Grade One",
        maxStudents,
        1000,
        true,
        active,
        weeklySlots);

    private static MonthlyDue Due(double expected, double paid, double outstanding) => new(
        1,
        "STU-2026-0001",
        "Alice Anderson",
        1,
        "Group One",
        "2026-11",
        expected,
        paid,
        0,
        outstanding);
}
