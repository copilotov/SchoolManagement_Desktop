using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Domain.Common;
using SchoolManagement.Infrastructure.Persistence.Converters;
using SchoolManagement.Infrastructure.Persistence.Repositories;

namespace SchoolManagement.Tests.Persistence;

/// <summary>
/// Real SQLite coverage of the Phase 6 repositories: deliberate group
/// projections, grade-derived level saving, idempotent enrollment, weekly slot
/// round-trip/conflicts and duplicate-free session generation.
/// </summary>
public sealed class GroupRepositoryTests
{
    private const string SeedScript = """
        INSERT INTO "academic_levels" ("id", "name", "order_index") VALUES
            (1, 'Level One', 1), (2, 'Level Two', 2);
        INSERT INTO "grades" ("id", "academic_level_id", "name", "order_index") VALUES
            (1, 1, 'Grade One', 1), (2, 2, 'Grade Two', 2);
        INSERT INTO "subjects" ("id", "name") VALUES
            (1, 'Mathematics'), (2, 'Physics');
        INSERT INTO "teachers" ("id", "full_name") VALUES
            (1, 'Teacher One'), (2, 'Teacher Two');
        INSERT INTO "groups"
            ("id", "subject_id", "teacher_id", "academic_level_id", "grade_id", "name",
             "max_students", "monthly_fee", "is_active")
            VALUES
            (1, 1, 1, 1, 1, 'Group Alpha', 2, 1000, 1),
            (2, 2, 2, 2, 2, 'Group Beta', 30, 2000, 1),
            (3, 1, 1, 1, 1, 'Group Gamma', 30, 500, 0);
        INSERT INTO "students" ("id", "student_code", "first_name", "last_name", "status") VALUES
            (1, 'STU-2026-0001', 'Alice', 'Anderson', 'active'),
            (2, 'STU-2026-0002', 'Bob', 'Brown', 'active'),
            (3, 'STU-2026-0003', 'Carol', 'Clark', 'active');
        INSERT INTO "guardians" ("id", "full_name") VALUES (1, 'Guardian One');
        INSERT INTO "enrollments" ("id", "student_id", "group_id", "status") VALUES
            (1, 1, 1, 'active'),
            (2, 2, 1, 'active'),
            (3, 3, 1, 'withdrawn');
        """;

    [Fact]
    public async Task List_projects_the_joined_names_capacity_and_slot_count()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateGroupRepository(host);

        var result = await repository.GetListAsync(new GroupListQuery());

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Select(group => group.Name).Should().ContainInOrder("Group Alpha", "Group Beta");

        var alpha = result.Value[0];
        alpha.SubjectName.Should().Be("Mathematics");
        alpha.TeacherName.Should().Be("Teacher One");
        alpha.AcademicLevelId.Should().Be(1);
        alpha.AcademicLevelName.Should().Be("Level One");
        alpha.GradeName.Should().Be("Grade One");
        alpha.ActiveEnrollments.Should().Be(2);
        alpha.WeeklySlots.Should().Be(2);
        alpha.IsFull.Should().BeTrue();
        alpha.CapacityText.Should().Be("2/2");

        var beta = result.Value[1];
        beta.SubjectName.Should().Be("Physics");
        beta.ActiveEnrollments.Should().Be(0);
        beta.WeeklySlots.Should().Be(1);
    }

    [Fact]
    public async Task List_searches_every_joined_name_and_honours_include_inactive()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateGroupRepository(host);

        (await repository.GetListAsync(new GroupListQuery(SearchTerm: "alpha"))).Value
            .Should().ContainSingle().Which.Id.Should().Be(1);
        (await repository.GetListAsync(new GroupListQuery(SearchTerm: "phys"))).Value
            .Should().ContainSingle().Which.Id.Should().Be(2);
        (await repository.GetListAsync(new GroupListQuery(SearchTerm: "Teacher Two"))).Value
            .Should().ContainSingle().Which.Id.Should().Be(2);
        (await repository.GetListAsync(new GroupListQuery(SearchTerm: "Grade One"))).Value
            .Should().ContainSingle().Which.Id.Should().Be(1);

        (await repository.GetListAsync(new GroupListQuery())).Value.Should().HaveCount(2);
        (await repository.GetListAsync(new GroupListQuery(IncludeInactive: true))).Value
            .Should().HaveCount(3);
    }

    [Fact]
    public async Task List_sorts_by_every_column_in_both_directions()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateGroupRepository(host);

        (await repository.GetListAsync(new GroupListQuery(SortBy: GroupSortField.Name, Descending: true))).Value
            .Select(group => group.Name).Should().ContainInOrder("Group Beta", "Group Alpha");
        (await repository.GetListAsync(new GroupListQuery(SortBy: GroupSortField.Subject))).Value
            .Select(group => group.SubjectName).Should().ContainInOrder("Mathematics", "Physics");
        (await repository.GetListAsync(new GroupListQuery(SortBy: GroupSortField.Teacher))).Value
            .Select(group => group.TeacherName).Should().ContainInOrder("Teacher One", "Teacher Two");
        (await repository.GetListAsync(new GroupListQuery(SortBy: GroupSortField.Grade))).Value
            .Select(group => group.GradeName).Should().ContainInOrder("Grade One", "Grade Two");
        (await repository.GetListAsync(new GroupListQuery(SortBy: GroupSortField.Capacity))).Value
            .Select(group => group.MaxStudents).Should().ContainInOrder(2, 30);
        (await repository.GetListAsync(new GroupListQuery(SortBy: GroupSortField.Capacity, Descending: true))).Value
            .Select(group => group.MaxStudents).Should().ContainInOrder(30, 2);
        (await repository.GetListAsync(new GroupListQuery(SortBy: GroupSortField.Fee))).Value
            .Select(group => group.MonthlyFee).Should().ContainInOrder(1000, 2000);
        (await repository.GetListAsync(new GroupListQuery(SortBy: GroupSortField.Fee, Descending: true))).Value
            .Select(group => group.MonthlyFee).Should().ContainInOrder(2000, 1000);
    }

    [Fact]
    public async Task Summary_reads_one_group_with_its_counters()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateGroupRepository(host);

        var result = await repository.GetSummaryAsync(1);

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Group Alpha");
        result.Value.ActiveEnrollments.Should().Be(2);
        result.Value.WeeklySlots.Should().Be(2);

        var missing = await repository.GetSummaryAsync(99);
        missing.IsSuccess.Should().BeTrue();
        missing.Value.Should().BeNull();
    }

    [Fact]
    public async Task Save_derives_the_level_from_the_selected_grade()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateGroupRepository(host);

        var result = await repository.SaveAsync(new GroupSaveRequest(
            "Group Delta",
            AcademicLevelId: null,
            GradeId: 2,
            SubjectId: 1,
            TeacherId: 2,
            MaxStudents: 25,
            MonthlyFee: 1500,
            IsActive: true));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Created.Should().BeTrue();

        var summary = await repository.GetSummaryAsync(result.Value.GroupId);
        summary.Value!.AcademicLevelId.Should().Be(2);
        summary.Value.AcademicLevelName.Should().Be("Level Two");
        summary.Value.GradeName.Should().Be("Grade Two");
        summary.Value.SubjectName.Should().Be("Mathematics");
    }

    [Fact]
    public async Task Save_uses_an_explicit_level_when_the_request_carries_one()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateGroupRepository(host);

        var result = await repository.SaveAsync(new GroupSaveRequest(
            "Group Delta",
            AcademicLevelId: 1,
            GradeId: 2,
            SubjectId: 1,
            TeacherId: 2,
            MaxStudents: 25,
            MonthlyFee: 1500,
            IsActive: true));

        (await repository.GetSummaryAsync(result.Value.GroupId)).Value!.AcademicLevelId.Should().Be(1);
    }

    [Fact]
    public async Task Save_updates_a_group_and_keeps_its_stored_level()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateGroupRepository(host);

        var result = await repository.SaveAsync(new GroupSaveRequest(
            "  Alpha Renamed  ",
            AcademicLevelId: null,
            GradeId: 1,
            SubjectId: 2,
            TeacherId: 2,
            MaxStudents: 40,
            MonthlyFee: 1250,
            IsActive: false,
            GroupId: 1));

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Created.Should().BeFalse();
        result.Value.GroupId.Should().Be(1);

        var summary = await repository.GetSummaryAsync(1);
        summary.Value!.Name.Should().Be("Alpha Renamed");
        summary.Value.AcademicLevelId.Should().Be(1);
        summary.Value.SubjectId.Should().Be(2);
        summary.Value.TeacherId.Should().Be(2);
        summary.Value.MaxStudents.Should().Be(40);
        summary.Value.MonthlyFee.Should().Be(1250);
        summary.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Save_reports_not_found_for_a_missing_group()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateGroupRepository(host);

        var result = await repository.SaveAsync(new GroupSaveRequest(
            "Ghost",
            null,
            1,
            1,
            1,
            30,
            0,
            true,
            GroupId: 404));

        result.IsFailure.Should().BeTrue();
        result.Failure!.Kind.Should().Be(FailureKind.NotFound);
    }

    [Fact]
    public async Task Enrollment_is_idempotent_while_the_row_stays_active()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateEnrollmentRepository(host);

        var first = await repository.EnrollAsync(new EnrollmentSaveRequest(2, 1));
        first.IsSuccess.Should().BeTrue(first.Failure?.Detail);
        first.Value.Created.Should().BeTrue();
        first.Value.AlreadyEnrolled.Should().BeFalse();

        var second = await repository.EnrollAsync(new EnrollmentSaveRequest(2, 1));
        second.IsSuccess.Should().BeTrue();
        second.Value.Created.Should().BeFalse();
        second.Value.AlreadyEnrolled.Should().BeTrue();
        second.Value.EnrollmentId.Should().Be(first.Value.EnrollmentId);

        (await repository.CountActiveByGroupAsync(2)).Value.Should().Be(1);
    }

    [Fact]
    public async Task A_withdrawn_enrollment_can_be_created_again()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateEnrollmentRepository(host);

        // Enrollment 3 is withdrawn, so student 3 may join group 1 again.
        var reEnroll = await repository.EnrollAsync(new EnrollmentSaveRequest(1, 3));
        reEnroll.IsSuccess.Should().BeTrue(reEnroll.Failure?.Detail);
        reEnroll.Value.Created.Should().BeTrue();
        reEnroll.Value.EnrollmentId.Should().NotBe(3);

        (await repository.CountActiveByGroupAsync(1)).Value.Should().Be(3);
    }

    [Fact]
    public async Task Concurrent_enrollments_never_create_two_active_rows()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateEnrollmentRepository(host);

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => repository.EnrollAsync(new EnrollmentSaveRequest(2, 2)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(result => result.IsSuccess);
        results.Count(result => result.Value.Created).Should().Be(1);
        results.Count(result => result.Value.AlreadyEnrolled).Should().Be(7);
        (await repository.CountActiveByGroupAsync(2)).Value.Should().Be(1);
    }

    [Fact]
    public async Task Exists_active_ignores_withdrawn_rows()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateEnrollmentRepository(host);

        (await repository.ExistsActiveAsync(1, 1)).Value.Should().BeTrue();
        (await repository.ExistsActiveAsync(1, 3)).Value.Should().BeFalse();
    }

    [Fact]
    public async Task Withdraw_changes_only_the_status()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateEnrollmentRepository(host);

        var result = await repository.SetStatusAsync(1, EnrollmentStatuses.Withdrawn);

        result.IsSuccess.Should().BeTrue();
        (await repository.CountActiveByGroupAsync(1)).Value.Should().Be(1);
        var rows = await repository.GetByGroupAsync(1);
        rows.Value.Should().HaveCount(3);
        rows.Value.Single(row => row.Id == 1).Status.Should().Be(EnrollmentStatuses.Withdrawn);
    }

    [Fact]
    public async Task Slot_round_trips_its_day_time_location_and_teacher()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateScheduleRepository(host);

        var created = await repository.SaveSlotAsync(new ScheduleSaveRequest(
            1,
            3,
            new TimeSpan(11, 30, 0),
            new TimeSpan(12, 45, 0),
            "Room A"));

        created.IsSuccess.Should().BeTrue(created.Failure?.Detail);
        created.Value.Created.Should().BeTrue();

        var slot = await repository.GetSlotAsync(created.Value.SlotId);
        slot.IsSuccess.Should().BeTrue();
        slot.Value.Should().NotBeNull();
        slot.Value!.GroupName.Should().Be("Group Alpha");
        slot.Value.TeacherId.Should().Be(1);
        slot.Value.TeacherName.Should().Be("Teacher One");
        slot.Value.DayOfWeek.Should().Be(3);
        slot.Value.StartTime.TimeOfDay.Should().Be(new TimeSpan(11, 30, 0));
        slot.Value.EndTime.TimeOfDay.Should().Be(new TimeSpan(12, 45, 0));
        slot.Value.Location.Should().Be("Room A");

        var updated = await repository.SaveSlotAsync(new ScheduleSaveRequest(
            1,
            4,
            new TimeSpan(13, 0, 0),
            new TimeSpan(14, 0, 0),
            "Room B",
            SlotId: created.Value.SlotId));
        updated.Value.Created.Should().BeFalse();

        var reloaded = (await repository.GetSlotAsync(created.Value.SlotId)).Value!;
        reloaded.DayOfWeek.Should().Be(4);
        reloaded.Location.Should().Be("Room B");
    }

    [Fact]
    public async Task Slot_save_reports_a_missing_group_or_slot()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateScheduleRepository(host);

        var missingGroup = await repository.SaveSlotAsync(new ScheduleSaveRequest(
            404, 0, new TimeSpan(8, 0, 0), new TimeSpan(9, 0, 0), null));
        missingGroup.Failure!.Kind.Should().Be(FailureKind.NotFound);

        var missingSlot = await repository.SaveSlotAsync(new ScheduleSaveRequest(
            1, 0, new TimeSpan(8, 0, 0), new TimeSpan(9, 0, 0), null, SlotId: 404));
        missingSlot.Failure!.Kind.Should().Be(FailureKind.NotFound);
    }

    [Fact]
    public async Task Conflicts_select_overlapping_slots_of_the_same_group_or_teacher()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateScheduleRepository(host);

        // Group 2 shares no teacher with group 1: give it teacher 1 so the
        // cross-group teacher rule can be checked too.
        await host.ExecuteAsync("UPDATE \"groups\" SET \"teacher_id\" = 1 WHERE \"id\" = 2;");
        await repository.SaveSlotAsync(new ScheduleSaveRequest(
            2, 5, new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0), "Room X"));
        await repository.SaveSlotAsync(new ScheduleSaveRequest(
            1, 5, new TimeSpan(10, 30, 0), new TimeSpan(11, 30, 0), "Room Y"));

        var conflicts = await repository.GetConflictsAsync(
            1,
            1,
            5,
            new TimeSpan(10, 15, 0),
            new TimeSpan(10, 45, 0));

        conflicts.IsSuccess.Should().BeTrue(conflicts.Failure?.Detail);
        conflicts.Value.Should().HaveCount(2);
        conflicts.Value.Select(slot => slot.GroupId).Should().BeEquivalentTo(new[] { 1, 2 });

        var otherDay = await repository.GetConflictsAsync(
            1, 1, 6, new TimeSpan(10, 15, 0), new TimeSpan(10, 45, 0));
        otherDay.Value.Should().BeEmpty();

        var excluded = await repository.GetConflictsAsync(
            1,
            1,
            5,
            new TimeSpan(10, 0, 0),
            new TimeSpan(11, 0, 0),
            excludeSlotId: conflicts.Value.Single(slot => slot.GroupId == 2).Id);
        excluded.Value.Should().ContainSingle().Which.GroupId.Should().Be(1);
    }

    [Fact]
    public async Task Slots_can_be_filtered_by_group_or_teacher()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateScheduleRepository(host);

        var byGroup = await repository.GetSlotsAsync(1, null, false);
        byGroup.IsSuccess.Should().BeTrue(byGroup.Failure?.Detail);
        byGroup.Value.Should().HaveCount(2);
        var byTeacher = await repository.GetSlotsAsync(null, 2, false);
        byTeacher.IsSuccess.Should().BeTrue(byTeacher.Failure?.Detail);
        byTeacher.Value.Should().HaveCount(1);
        var all = await repository.GetSlotsAsync(null, null, false);
        all.IsSuccess.Should().BeTrue(all.Failure?.Detail);
        all.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task Delete_slot_removes_the_row_or_reports_not_found()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateScheduleRepository(host);

        var deleted = await repository.DeleteSlotAsync(1);
        deleted.IsSuccess.Should().BeTrue();
        deleted.Value.Should().BeTrue();

        (await repository.GetSlotAsync(1)).Value.Should().BeNull();

        var missing = await repository.DeleteSlotAsync(404);
        missing.Failure!.Kind.Should().Be(FailureKind.NotFound);
    }

    [Fact]
    public async Task Session_generation_creates_one_row_per_template_and_never_duplicates()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateSessionRepository(host);

        // 2026-11-07 is a Saturday (school day 0), which holds group 1 08:00
        // and group 2 09:00.
        var saturday = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);

        var first = await repository.GenerateForDateAsync(saturday);
        first.IsSuccess.Should().BeTrue(first.Failure?.Detail);
        first.Value.CreatedCount.Should().Be(2);
        first.Value.AlreadyExisted.Should().Be(0);
        first.Value.Total.Should().Be(2);

        var second = await repository.GenerateForDateAsync(saturday);
        second.Value.CreatedCount.Should().Be(0);
        second.Value.AlreadyExisted.Should().Be(2);

        var sessions = await repository.GetByDateAsync(saturday);
        sessions.Value.Should().HaveCount(2);
        sessions.Value.Select(session => session.StartTime.TimeOfDay)
            .Should().BeEquivalentTo(new[] { new TimeSpan(8, 0, 0), new TimeSpan(9, 0, 0) });
        sessions.Value.Should().OnlyContain(session => session.Type == SessionTypes.Regular);
        sessions.Value.Should().OnlyContain(session => session.Status == SessionStatuses.Scheduled);
    }

    [Fact]
    public async Task Session_generation_honours_the_group_filter()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateSessionRepository(host);

        var saturday = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);

        var result = await repository.GenerateForDateAsync(saturday, groupId: 2);
        result.Value.CreatedCount.Should().Be(1);
        result.Value.AlreadyExisted.Should().Be(0);

        var again = await repository.GenerateForDateAsync(saturday, groupId: 2);
        again.Value.CreatedCount.Should().Be(0);
        again.Value.AlreadyExisted.Should().Be(1);

        var otherGroup = await repository.GenerateForDateAsync(saturday, groupId: 1);
        otherGroup.Value.CreatedCount.Should().Be(1);
    }

    [Fact]
    public async Task Session_generation_on_a_day_without_templates_creates_nothing()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateSessionRepository(host);

        // 2026-11-05 is a Thursday (school day 5), which has no template.
        var thursday = new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc);

        var result = await repository.GenerateForDateAsync(thursday);

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.CreatedCount.Should().Be(0);
        result.Value.AlreadyExisted.Should().Be(0);
    }

    [Fact]
    public async Task Dependencies_and_active_count_feed_the_delete_guard()
    {
        await using var host = await SeededHostAsync();
        var groups = CreateGroupRepository(host);
        var enrollments = CreateEnrollmentRepository(host);

        var alpha = await groups.GetDependenciesAsync(1);
        alpha.IsSuccess.Should().BeTrue();
        alpha.Value.Enrollments.Should().Be(3);
        alpha.Value.ScheduleSlots.Should().Be(2);
        alpha.Value.HasLinks.Should().BeTrue();

        var beta = await groups.GetDependenciesAsync(2);
        beta.Value.Enrollments.Should().Be(0);
        beta.Value.ScheduleSlots.Should().Be(1);

        (await groups.GetActiveEnrollmentCountAsync(1)).Value.Should().Be(2);
        (await groups.GetActiveEnrollmentCountAsync(2)).Value.Should().Be(0);
        (await enrollments.CountActiveByGroupAsync(1)).Value.Should().Be(2);
    }

    [Fact]
    public async Task Delete_removes_a_group_and_reports_not_found_afterwards()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateGroupRepository(host);

        (await repository.DeleteAsync(2)).Value.Should().BeTrue();
        (await repository.GetSummaryAsync(2)).Value.Should().BeNull();

        var missing = await repository.DeleteAsync(404);
        missing.Failure!.Kind.Should().Be(FailureKind.NotFound);
    }

    [Fact]
    public async Task Group_statistics_count_present_absent_and_late_records()
    {
        await using var host = await SeededHostAsync();
        var attendance = CreateAttendanceRepository(host);

        var sessionStart = UnixSecondsDateTimeConverter.ToUnixSeconds(
            new DateTime(2026, 11, 7, 8, 0, 0, DateTimeKind.Utc));
        var sessionEnd = UnixSecondsDateTimeConverter.ToUnixSeconds(
            new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc));

        await host.ExecuteAsync($"""
            INSERT INTO "sessions"
                ("id", "group_id", "session_date", "start_time", "end_time")
                VALUES (1, 1, {sessionStart}, {sessionStart}, {sessionEnd});
            INSERT INTO "attendance" ("id", "session_id", "student_id", "status") VALUES
                (1, 1, 1, 'present'),
                (2, 1, 2, 'absent'),
                (3, 1, 3, 'late');
            """);

        var stats = await attendance.GetGroupStatisticsAsync(1);

        stats.IsSuccess.Should().BeTrue(stats.Failure?.Detail);
        stats.Value.SessionCount.Should().Be(1);
        stats.Value.RecordCount.Should().Be(3);
        stats.Value.PresentCount.Should().Be(1);
        stats.Value.AbsentCount.Should().Be(1);
        stats.Value.LateCount.Should().Be(1);
        stats.Value.AttendanceRate.Should().BeApproximately(66.67, 0.01);
    }

    [Fact]
    public async Task Get_by_group_lists_active_and_withdrawn_students_with_their_codes()
    {
        await using var host = await SeededHostAsync();
        var repository = CreateEnrollmentRepository(host);

        var result = await repository.GetByGroupAsync(1);

        result.IsSuccess.Should().BeTrue(result.Failure?.Detail);
        result.Value.Should().HaveCount(3);
        result.Value.Select(row => row.StudentName)
            .Should().ContainInOrder("Alice Anderson", "Bob Brown", "Carol Clark");
        result.Value.Should().OnlyContain(row => row.GroupName == "Group Alpha");
        result.Value.Single(row => row.StudentId == 3).Status.Should().Be(EnrollmentStatuses.Withdrawn);
    }

    private static GroupRepository CreateGroupRepository(DatabaseTestHost host) => new(
        host.CreateFactory(),
        host.Clock,
        NullLogger<GroupRepository>.Instance);

    private static EnrollmentRepository CreateEnrollmentRepository(DatabaseTestHost host) => new(
        host.CreateFactory(),
        host.Clock,
        NullLogger<EnrollmentRepository>.Instance);

    private static ScheduleTemplateRepository CreateScheduleRepository(DatabaseTestHost host) => new(
        host.CreateFactory(),
        host.Clock,
        NullLogger<ScheduleTemplateRepository>.Instance);

    private static SessionRepository CreateSessionRepository(DatabaseTestHost host) => new(
        host.CreateFactory(),
        host.Clock,
        NullLogger<SessionRepository>.Instance);

    private static AttendanceRepository CreateAttendanceRepository(DatabaseTestHost host) => new(
        host.CreateFactory(),
        host.Clock,
        NullLogger<AttendanceRepository>.Instance);

    private static async Task<DatabaseTestHost> SeededHostAsync()
    {
        var host = DatabaseTestHost.Create();
        await host.InitializeAsync();
        await host.ExecuteAsync(SeedScript);
        await host.ExecuteAsync(SlotSeed());
        return host;
    }

    // Seeded weekly slots: group 1 on Saturday 08:00-09:00 and Monday
    // 10:00-11:00, group 2 on Saturday 09:00-10:00.
    private static string SlotSeed()
    {
        var saturdayEight = UnixSecondsDateTimeConverter.ToUnixSeconds(
            new DateTime(2000, 1, 1, 8, 0, 0, DateTimeKind.Utc));
        var saturdayNine = UnixSecondsDateTimeConverter.ToUnixSeconds(
            new DateTime(2000, 1, 1, 9, 0, 0, DateTimeKind.Utc));
        var mondayTen = UnixSecondsDateTimeConverter.ToUnixSeconds(
            new DateTime(2000, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        var mondayEleven = UnixSecondsDateTimeConverter.ToUnixSeconds(
            new DateTime(2000, 1, 1, 11, 0, 0, DateTimeKind.Utc));

        return $"""
            INSERT INTO "schedule_templates"
                ("id", "group_id", "day_of_week", "start_time", "end_time", "location", "is_active")
                VALUES
                (1, 1, 0, {saturdayEight}, {saturdayNine}, 'Room 1', 1),
                (2, 1, 2, {mondayTen}, {mondayEleven}, 'Room 2', 1),
                (3, 2, 0, {saturdayNine}, {saturdayNine + 3600}, 'Room 3', 1);
            """;
    }
}
