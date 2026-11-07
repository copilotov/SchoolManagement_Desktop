using System.Globalization;
using FluentAssertions;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Localization;

namespace SchoolManagement.Tests.Groups;

/// <summary>
/// Pure validation of the group form and the weekly slot dialog. Every failure
/// carries a translation key, never a hardcoded message.
/// </summary>
public sealed class GroupValidatorTests
{
    [Fact]
    public void A_complete_request_is_valid()
    {
        GroupValidator.Validate(Request()).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void The_name_is_required(string name)
    {
        var failure = GroupValidator.Validate(Request(name: name));

        failure.Should().NotBeNull();
        failure!.MessageKey.Should().Be(TranslationKeys.Validators.Required);
        failure.Parameters["field"].Should().Be("name");
    }

    [Fact]
    public void The_name_is_length_limited()
    {
        var failure = GroupValidator.Validate(
            Request(name: new string('A', GroupValidator.NameMaxLength + 1)));

        failure!.MessageKey.Should().Be(TranslationKeys.Validators.MaxLength);
        failure.Parameters["count"].Should().Be(
            GroupValidator.NameMaxLength.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void The_name_exactly_at_the_limit_is_accepted()
        => GroupValidator.Validate(Request(name: new string('A', GroupValidator.NameMaxLength)))
            .Should().BeNull();

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-4)]
    public void A_grade_is_required(int? gradeId)
    {
        var failure = GroupValidator.Validate(Request(gradeId: gradeId));

        failure!.MessageKey.Should().Be(TranslationKeys.Validators.Required);
        failure.Parameters["field"].Should().Be("gradeId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-4)]
    public void A_subject_is_required(int? subjectId)
    {
        var failure = GroupValidator.Validate(Request(subjectId: subjectId));

        failure!.MessageKey.Should().Be(TranslationKeys.Validators.Required);
        failure.Parameters["field"].Should().Be("subjectId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(GroupValidator.MaxStudentsLimit + 1)]
    public void The_capacity_must_be_positive_and_bounded(int maxStudents)
    {
        var failure = GroupValidator.Validate(Request(maxStudents: maxStudents));

        failure!.MessageKey.Should().Be(TranslationKeys.Validators.PositiveNumber);
        failure.Parameters["field"].Should().Be("maxStudents");
    }

    [Fact]
    public void The_capacity_at_the_upper_limit_is_accepted()
        => GroupValidator.Validate(Request(maxStudents: GroupValidator.MaxStudentsLimit))
            .Should().BeNull();

    [Fact]
    public void A_negative_monthly_fee_is_rejected()
    {
        var failure = GroupValidator.Validate(Request(monthlyFee: -1));

        failure!.MessageKey.Should().Be(TranslationKeys.Validators.PositiveNumber);
        failure.Parameters["field"].Should().Be("monthlyFee");
    }

    [Fact]
    public void A_zero_monthly_fee_is_accepted()
        => GroupValidator.Validate(Request(monthlyFee: 0)).Should().BeNull();

    [Fact]
    public void Normalize_trims_the_name_and_nulls_the_optional_ids()
    {
        var normalized = GroupValidator.Normalize(Request(
            name: "  Group One  ",
            levelId: 0,
            gradeId: -1,
            subjectId: 0,
            teacherId: -2));

        normalized.Name.Should().Be("Group One");
        normalized.AcademicLevelId.Should().BeNull();
        normalized.GradeId.Should().BeNull();
        normalized.SubjectId.Should().BeNull();
        normalized.TeacherId.Should().BeNull();
    }

    [Fact]
    public void Normalize_keeps_the_positive_ids()
    {
        var normalized = GroupValidator.Normalize(Request(
            levelId: 1,
            gradeId: 2,
            subjectId: 3,
            teacherId: 4));

        normalized.AcademicLevelId.Should().Be(1);
        normalized.GradeId.Should().Be(2);
        normalized.SubjectId.Should().Be(3);
        normalized.TeacherId.Should().Be(4);
    }

    [Fact]
    public void A_valid_slot_passes_the_schedule_validator()
        => ScheduleValidator.Validate(Slot()).Should().BeNull();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void The_slot_group_is_required(int groupId)
    {
        var failure = ScheduleValidator.Validate(Slot(groupId: groupId));

        failure!.MessageKey.Should().Be(TranslationKeys.Validators.Required);
        failure.Parameters["field"].Should().Be("groupId");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void The_day_must_be_a_school_day(int day)
    {
        var failure = ScheduleValidator.Validate(Slot(day: day));

        failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidField);
        failure.Parameters["field"].Should().Be("dayOfWeek");
    }

    [Theory]
    [InlineData(9, 0, 9, 0)]
    [InlineData(10, 0, 9, 0)]
    public void The_end_must_be_after_the_start(int startHour, int startMinute, int endHour, int endMinute)
    {
        var failure = ScheduleValidator.Validate(Slot(
            start: new TimeSpan(startHour, startMinute, 0),
            end: new TimeSpan(endHour, endMinute, 0)));

        failure!.MessageKey.Should().Be(TranslationKeys.Groups.InvalidTimeRange);
        failure.Parameters["field"].Should().Be("endTime");
    }

    [Fact]
    public void The_location_is_length_limited_only_when_supplied()
    {
        var tooLong = new string('A', GroupValidator.LocationMaxLength + 1);

        var failure = ScheduleValidator.Validate(Slot(location: tooLong));
        failure!.MessageKey.Should().Be(TranslationKeys.Validators.MaxLength);
        failure.Parameters["field"].Should().Be("location");

        ScheduleValidator.Validate(Slot(location: null)).Should().BeNull();
        ScheduleValidator.Validate(Slot(location: "  Room 1  ")).Should().BeNull();
    }

    [Fact]
    public void Normalize_trims_the_location_and_nulls_a_blank_one()
    {
        ScheduleValidator.Normalize(Slot(location: "  Room 1  ")).Location.Should().Be("Room 1");
        ScheduleValidator.Normalize(Slot(location: "   ")).Location.Should().BeNull();
        ScheduleValidator.Normalize(Slot(location: null)).Location.Should().BeNull();
    }

    [Theory]
    [InlineData("08:00", 8, 0)]
    [InlineData(" 8:00 ", 8, 0)]
    [InlineData("7:05", 7, 5)]
    [InlineData("18:45", 18, 45)]
    [InlineData("08:00:30", 8, 0)]
    public void TryParse_reads_the_dialog_time(string text, int expectedHour, int expectedMinute)
    {
        ScheduleTimeParser.TryParse(text, out var value).Should().BeTrue();
        value.Hours.Should().Be(expectedHour);
        value.Minutes.Should().Be(expectedMinute);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("8")]
    [InlineData("25:00")]
    [InlineData("08-00")]
    [InlineData("abc")]
    public void TryParse_rejects_an_invalid_time(string? text)
        => ScheduleTimeParser.TryParse(text, out _).Should().BeFalse();

    [Fact]
    public void Format_round_trips_a_time_span()
    {
        var value = new TimeSpan(14, 5, 0);

        var text = ScheduleTimeParser.Format(value);

        text.Should().Be("14:05");
        ScheduleTimeParser.TryParse(text, out var parsed).Should().BeTrue();
        parsed.Should().Be(value);
    }

    [Fact]
    public void Format_reads_a_stored_time_of_day_date()
        => ScheduleTimeParser.Format(new DateTime(2000, 1, 1, 9, 30, 0, DateTimeKind.Utc))
            .Should().Be("09:30");

    private static GroupSaveRequest Request(
        string name = "Group One",
        int? levelId = 1,
        int? gradeId = 1,
        int? subjectId = 1,
        int? teacherId = 1,
        int maxStudents = 30,
        double monthlyFee = 1000) => new(
        name,
        levelId,
        gradeId,
        subjectId,
        teacherId,
        maxStudents,
        monthlyFee,
        true);

    private static ScheduleSaveRequest Slot(
        int groupId = 1,
        int day = 0,
        TimeSpan? start = null,
        TimeSpan? end = null,
        string? location = "Room 1") => new(
        groupId,
        day,
        start ?? new TimeSpan(8, 0, 0),
        end ?? new TimeSpan(9, 0, 0),
        location);
}
