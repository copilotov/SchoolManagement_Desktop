using FluentAssertions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Tests.Attendance;

/// <summary>
/// Pure Phase 7 rules that have no database or UI dependency: the session
/// status state machine and the attendance QR payload format.
/// </summary>
public sealed class AttendanceDomainTests
{
    [Theory]
    [InlineData(SessionStatuses.Scheduled, SessionStatuses.InProgress, true)]
    [InlineData(SessionStatuses.Scheduled, SessionStatuses.Completed, true)]
    [InlineData(SessionStatuses.Scheduled, SessionStatuses.Cancelled, true)]
    [InlineData(SessionStatuses.Scheduled, SessionStatuses.Postponed, true)]
    [InlineData(SessionStatuses.InProgress, SessionStatuses.Completed, true)]
    [InlineData(SessionStatuses.Postponed, SessionStatuses.Scheduled, true)]
    [InlineData(SessionStatuses.Completed, SessionStatuses.Scheduled, false)]
    [InlineData(SessionStatuses.Completed, SessionStatuses.Cancelled, false)]
    [InlineData(SessionStatuses.Cancelled, SessionStatuses.Scheduled, false)]
    public void Only_open_sessions_may_move_to_another_status(string from, string to, bool allowed)
        => SessionStatusPolicy.CanTransition(from, to).Should().Be(allowed);

    [Fact]
    public void A_transition_to_the_same_status_is_a_no_op()
        => SessionStatusPolicy.CanTransition(
                SessionStatuses.Cancelled,
                SessionStatuses.Cancelled)
            .Should().BeTrue();

    [Fact]
    public void An_unknown_stored_status_is_treated_tolerantly()
        => SessionStatusPolicy.CanTransition("legacy_value", SessionStatuses.Completed)
            .Should().BeTrue();

    [Theory]
    [InlineData("nonsense", false)]
    [InlineData(null, false)]
    [InlineData(SessionStatuses.Scheduled, true)]
    [InlineData("COMPLETED", true)]
    public void Only_the_five_stored_statuses_are_valid(string? status, bool expected)
        => SessionStatusPolicy.IsValidStatus(status).Should().Be(expected);

    [Theory]
    [InlineData(SessionStatuses.Scheduled, true)]
    [InlineData(SessionStatuses.InProgress, true)]
    [InlineData(SessionStatuses.Completed, false)]
    [InlineData(SessionStatuses.Cancelled, false)]
    [InlineData(SessionStatuses.Postponed, false)]
    public void Attendance_is_only_taken_for_an_open_session(string status, bool expected)
        => SessionStatusPolicy.CanTakeAttendance(status).Should().Be(expected);

    [Theory]
    [InlineData(SessionStatuses.Cancelled, true)]
    [InlineData(SessionStatuses.Postponed, true)]
    [InlineData(SessionStatuses.Scheduled, false)]
    [InlineData(SessionStatuses.Completed, false)]
    public void Cancelling_or_postponing_suggests_an_sms(string status, bool expected)
        => SessionStatusPolicy.SuggestsSms(status).Should().Be(expected);

    [Fact]
    public void A_completed_or_cancelled_session_is_terminal()
    {
        SessionStatusPolicy.IsTerminal(SessionStatuses.Completed).Should().BeTrue();
        SessionStatusPolicy.IsTerminal(SessionStatuses.Cancelled).Should().BeTrue();
        SessionStatusPolicy.IsTerminal(SessionStatuses.Scheduled).Should().BeFalse();
    }

    [Fact]
    public void The_qr_payload_is_formatted_with_the_code_prefix()
        => QrAttendanceCode.Format("STU-2026-0007").Should().Be("CODE:STU-2026-0007");

    [Theory]
    [InlineData("CODE:STU-2026-0007", "STU-2026-0007")]
    [InlineData("  code:STU-2026-0007  ", "STU-2026-0007")]
    [InlineData("Code: STU-2026-0007", "STU-2026-0007")]
    public void The_qr_parser_accepts_the_prefix_in_any_case(string payload, string expected)
    {
        QrAttendanceCode.TryParse(payload, out var studentCode).Should().BeTrue();
        studentCode.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("STU-2026-0007")]
    [InlineData("CODE:")]
    [InlineData("CODE:bad code")]
    [InlineData("CODE:bad?code")]
    public void The_qr_parser_rejects_an_invalid_payload(string payload)
        => QrAttendanceCode.TryParse(payload, out _).Should().BeFalse();

    [Fact]
    public void The_manual_parser_accepts_a_bare_student_code()
    {
        QrAttendanceCode.TryParseInput("STU-2026-0007", out var studentCode).Should().BeTrue();
        studentCode.Should().Be("STU-2026-0007");
    }

    [Fact]
    public void The_manual_parser_accepts_a_full_qr_payload()
    {
        QrAttendanceCode.TryParseInput("CODE:STU-2026-0007", out var studentCode).Should().BeTrue();
        studentCode.Should().Be("STU-2026-0007");
    }

    [Fact]
    public void The_manual_parser_rejects_whitespace_and_punctuation()
    {
        QrAttendanceCode.TryParseInput("   ", out _).Should().BeFalse();
        QrAttendanceCode.TryParseInput("STU 2026", out _).Should().BeFalse();
    }
}
