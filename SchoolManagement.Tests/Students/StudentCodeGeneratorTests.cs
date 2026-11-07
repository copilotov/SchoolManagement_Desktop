using FluentAssertions;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Tests.Students;

/// <summary>
/// The student code is both the readable identifier and the QR payload key, so
/// its format, parsing and sequence rules are pure and covered here.
/// </summary>
public sealed class StudentCodeGeneratorTests
{
    [Theory]
    [InlineData(2026, 1, "STU-2026-0001")]
    [InlineData(2026, 42, "STU-2026-0042")]
    [InlineData(1999, 9999, "STU-1999-9999")]
    public void Build_pads_the_year_and_the_sequence(int year, int sequence, string expected)
    {
        StudentCodeGenerator.Build(year, sequence).Should().Be(expected);
    }

    [Theory]
    [InlineData(1899, 1)]
    [InlineData(10000, 1)]
    public void Build_rejects_a_year_outside_four_digits(int year, int sequence)
    {
        var act = () => StudentCodeGenerator.Build(year, sequence);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10000)]
    public void Build_rejects_a_sequence_outside_the_reserved_range(int sequence)
    {
        var act = () => StudentCodeGenerator.Build(2026, sequence);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Parse_sequence_round_trips_a_generated_code()
    {
        var code = StudentCodeGenerator.Build(2026, 7);

        StudentCodeGenerator.ParseSequence(code, 2026).Should().Be(7);
        StudentCodeGenerator.IsCodeForYear(code, 2026).Should().BeTrue();
    }

    [Theory]
    [InlineData("STU-2025-0007", 2026)]
    [InlineData("STU-2026-0000", 2026)]
    [InlineData("STU-2026-10000", 2026)]
    [InlineData("STU-2026-ABC", 2026)]
    [InlineData("STU-2026-", 2026)]
    [InlineData("", 2026)]
    [InlineData(null, 2026)]
    public void Parse_sequence_rejects_another_year_or_a_malformed_code(string? code, int year)
    {
        StudentCodeGenerator.ParseSequence(code, year).Should().Be(0);
        StudentCodeGenerator.IsCodeForYear(code, year).Should().BeFalse();
    }

    [Fact]
    public void Parse_sequence_ignores_the_code_case()
    {
        StudentCodeGenerator.ParseSequence(" stu-2026-0012 ", 2026).Should().Be(12);
    }

    [Fact]
    public void Next_sequence_is_the_highest_of_the_requested_year_plus_one()
    {
        var codes = new[]
        {
            "STU-2025-0099",
            "STU-2026-0001",
            "STU-2026-0040",
            "STU-2026-0040",
            "CUSTOM-CODE",
            null,
        };

        StudentCodeGenerator.NextSequence(codes, 2026).Should().Be(41);
    }

    [Fact]
    public void Next_sequence_starts_at_one_for_a_new_year()
    {
        StudentCodeGenerator.NextSequence(new[] { "STU-2025-0099" }, 2026).Should().Be(1);
        StudentCodeGenerator.NextSequence(Array.Empty<string?>(), 2026).Should().Be(1);
    }

    [Fact]
    public void Next_sequence_refuses_to_exhaust_the_reserved_range()
    {
        var act = () => StudentCodeGenerator.NextSequence(new[] { "STU-2026-9999" }, 2026);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Next_sequence_requires_the_existing_codes()
    {
        var act = () => StudentCodeGenerator.NextSequence(null!, 2026);

        act.Should().Throw<ArgumentNullException>();
    }
}
