using FluentAssertions;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Tests.Attendance;

/// <summary>
/// Duplicate prevention and group validation of the attendance scanner. The
/// rules live in <see cref="QrScanSession"/>, away from the webcam window, so
/// they can be proven without a camera: the same code is accepted once, a code
/// from another group is refused and malformed input never reaches the roster.
/// </summary>
public sealed class QrScanSessionTests
{
    private static readonly QrScanStudent[] Roster =
    [
        new(1, "STU-2026-0001", "Alice Anderson"),
        new(2, "STU-2026-0002", "Bob Brown"),
        new(3, "STU-2026-0003", "Carol Clark"),
    ];

    [Fact]
    public void A_full_qr_payload_accepts_the_student()
    {
        var session = new QrScanSession(Roster);

        var outcome = session.Scan("CODE:STU-2026-0002", out var student);

        outcome.Should().Be(QrScanOutcome.Accepted);
        student!.StudentId.Should().Be(2);
        student.StudentName.Should().Be("Bob Brown");
        session.Count.Should().Be(1);
        session.AcceptedStudentIds.Should().Equal(2);
    }

    [Theory]
    [InlineData("STU-2026-0001")]
    [InlineData("  STU-2026-0001  ")]
    [InlineData("CODE:STU-2026-0001")]
    [InlineData("code:stu-2026-0001")]
    public void A_webcam_read_a_usb_reader_and_a_bare_code_all_resolve_the_same_student(string payload)
    {
        var session = new QrScanSession(Roster);

        session.Scan(payload, out var student).Should().Be(QrScanOutcome.Accepted);

        student!.StudentId.Should().Be(1);
    }

    [Fact]
    public void The_same_student_is_never_accepted_twice()
    {
        var session = new QrScanSession(Roster);

        session.Scan("CODE:STU-2026-0001", out _).Should().Be(QrScanOutcome.Accepted);
        var second = session.Scan("CODE:STU-2026-0001", out var student);
        var third = session.Scan("STU-2026-0001", out _);

        second.Should().Be(QrScanOutcome.Duplicate);
        third.Should().Be(QrScanOutcome.Duplicate);
        student!.StudentId.Should().Be(1);
        session.Count.Should().Be(1);
        session.AcceptedStudentIds.Should().Equal(1);
    }

    [Fact]
    public void A_code_from_another_group_is_refused_without_a_student()
    {
        var session = new QrScanSession(Roster);

        var outcome = session.Scan("CODE:STU-2026-0099", out var student);

        outcome.Should().Be(QrScanOutcome.NotInGroup);
        student.Should().BeNull();
        session.Count.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("CODE:")]
    [InlineData("CODE:bad code")]
    [InlineData("not a student code")]
    public void Malformed_input_never_reaches_the_roster(string? payload)
    {
        var session = new QrScanSession(Roster);

        var outcome = session.Scan(payload, out var student);

        outcome.Should().Be(QrScanOutcome.Invalid);
        student.Should().BeNull();
        session.AcceptedStudentIds.Should().BeEmpty();
    }

    [Fact]
    public void Two_students_are_counted_independently()
    {
        var session = new QrScanSession(Roster);

        session.Scan("CODE:STU-2026-0003", out _).Should().Be(QrScanOutcome.Accepted);
        session.Scan("CODE:STU-2026-0001", out _).Should().Be(QrScanOutcome.Accepted);

        session.Count.Should().Be(2);
        session.AcceptedStudentIds.Should().BeEquivalentTo(new[] { 1, 3 });
    }

    [Fact]
    public void Students_without_a_usable_code_are_ignored()
    {
        var session = new QrScanSession(
        [
            new QrScanStudent(0, "STU-2026-0001", "No Id"),
            new QrScanStudent(4, " ", "No Code"),
            new QrScanStudent(5, "STU-2026-0005", "Valid"),
        ]);

        session.Scan("CODE:STU-2026-0001", out _).Should().Be(QrScanOutcome.NotInGroup);
        session.Scan("CODE:STU-2026-0005", out var student).Should().Be(QrScanOutcome.Accepted);
        student!.StudentId.Should().Be(5);
    }

    [Fact]
    public void A_duplicate_code_inside_the_roster_keeps_the_last_row()
    {
        var session = new QrScanSession(
        [
            new QrScanStudent(1, "STU-2026-0001", "First"),
            new QrScanStudent(2, "STU-2026-0001", "Second"),
        ]);

        session.Scan("CODE:STU-2026-0001", out var student).Should().Be(QrScanOutcome.Accepted);

        student!.StudentId.Should().Be(2);
    }

    [Fact]
    public void A_null_roster_is_rejected()
        => FluentActions.Invoking(() => new QrScanSession(null!))
            .Should().Throw<ArgumentNullException>();
}
