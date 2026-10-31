using FluentAssertions;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Tests.Domain;

public sealed class WeekDayTests
{
    [Theory]
    [InlineData(2024, 1, 6, WeekDay.Saturday)]
    [InlineData(2024, 1, 7, WeekDay.Sunday)]
    [InlineData(2024, 1, 8, WeekDay.Monday)]
    [InlineData(2024, 1, 9, WeekDay.Tuesday)]
    [InlineData(2024, 1, 10, WeekDay.Wednesday)]
    [InlineData(2024, 1, 11, WeekDay.Thursday)]
    [InlineData(2024, 1, 12, WeekDay.Friday)]
    public void FromDate_maps_the_school_week(int year, int month, int day, WeekDay expected)
    {
        var date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);

        WeekDayCalculator.FromDate(date).Should().Be(expected);
        WeekDayCalculator.FromDate(DateOnly.FromDateTime(date)).Should().Be(expected);
    }

    [Fact]
    public void The_broken_modulo_formula_is_detected_on_saturday()
    {
        var saturday = new DateTime(2024, 1, 6, 0, 0, 0, DateTimeKind.Utc);

        var correct = WeekDayCalculator.FromDate(saturday);
        var broken = (WeekDay)((int)saturday.DayOfWeek % 7);

        correct.Should().Be(WeekDay.Saturday);
        broken.Should().Be(WeekDay.Friday);
        broken.Should().NotBe(correct);
    }

    [Fact]
    public void ToIndex_and_FromIndex_are_symmetric()
    {
        foreach (var day in Enum.GetValues<WeekDay>())
        {
            WeekDayCalculator.FromIndex(WeekDayCalculator.ToIndex(day)).Should().Be(day);
        }
    }

    [Fact]
    public void FromIndex_rejects_values_outside_the_week()
    {
        var action = () => WeekDayCalculator.FromIndex(7);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ToTranslationKey_returns_keys_and_not_display_text()
    {
        WeekDayCalculator.ToTranslationKey(WeekDay.Saturday).Should().Be("days.saturday");
        WeekDayCalculator.ToTranslationKey(WeekDay.Friday).Should().Be("days.friday");
    }
}
