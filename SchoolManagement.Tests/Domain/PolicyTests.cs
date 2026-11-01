using FluentAssertions;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Tests.Domain;

public sealed class PolicyTests
{
    [Theory]
    [InlineData(30, 29, false, false, 1)]
    [InlineData(30, 30, true, false, 0)]
    [InlineData(2, 3, true, true, 0)]
    [InlineData(0, 0, true, false, 0)]
    public void Group_capacity_warns_at_or_above_the_maximum_but_never_blocks(
        int maxStudents,
        int activeEnrollments,
        bool isFull,
        bool isOverCapacity,
        int remainingSeats)
    {
        var capacity = GroupCapacityPolicy.Evaluate(maxStudents, activeEnrollments);

        capacity.IsFull.Should().Be(isFull);
        capacity.IsOverCapacity.Should().Be(isOverCapacity);
        capacity.ShouldWarn.Should().Be(isFull);
        capacity.CanEnroll.Should().BeTrue();
        capacity.RemainingSeats.Should().Be(remainingSeats);
    }

    [Fact]
    public void Group_capacity_rejects_negative_values()
    {
        var act = () => GroupCapacityPolicy.Evaluate(-1, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Unknown_group_produces_no_capacity_warning()
    {
        GroupCapacityPolicy.EvaluateForKnownGroup(null, 10).Should().BeNull();
        GroupCapacityPolicy.EvaluateForKnownGroup(30, 30)!.Value.IsFull.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 5, false, false, 5)]
    [InlineData(4, 5, false, false, 1)]
    [InlineData(5, 5, true, false, 0)]
    [InlineData(6, 5, true, true, 0)]
    public void Absence_limit_is_reached_at_the_limit_and_exceeded_above_it(
        int absenceCount,
        int limit,
        bool hasReached,
        bool isExceeded,
        int remaining)
    {
        var status = AbsenceLimitPolicy.Evaluate(absenceCount, limit);

        status.HasReachedLimit.Should().Be(hasReached);
        status.IsExceeded.Should().Be(isExceeded);
        status.RemainingBeforeLimit.Should().Be(remaining);
    }

    [Fact]
    public void Absence_limit_parses_stored_values_tolerantly()
    {
        AbsenceLimitPolicy.ParseLimit(null).Should().Be(5);
        AbsenceLimitPolicy.ParseLimit("").Should().Be(5);
        AbsenceLimitPolicy.ParseLimit("not a number").Should().Be(5);
        AbsenceLimitPolicy.ParseLimit("0").Should().Be(5);
        AbsenceLimitPolicy.ParseLimit("-2").Should().Be(5);
        AbsenceLimitPolicy.ParseLimit("3").Should().Be(3);
    }

    [Fact]
    public void Absence_limit_rejects_invalid_inputs()
    {
        var negativeCount = () => AbsenceLimitPolicy.Evaluate(-1, 5);
        var invalidLimit = () => AbsenceLimitPolicy.Evaluate(0, 0);

        negativeCount.Should().Throw<ArgumentOutOfRangeException>();
        invalidLimit.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Monthly_dues_subtract_partial_payments_and_partial_exemptions()
    {
        var dues = MonthlyDuesCalculator.Calculate(
            monthlyFee: 1000,
            paid: 400,
            partialExemption: 100,
            hasFullExemption: false);

        dues.Expected.Should().Be(1000);
        dues.Paid.Should().Be(400);
        dues.Exempted.Should().Be(100);
        dues.Outstanding.Should().Be(500);
    }

    [Fact]
    public void A_full_exemption_covers_the_whole_month()
    {
        var dues = MonthlyDuesCalculator.Calculate(1000, 0, 250, hasFullExemption: true);

        dues.Exempted.Should().Be(1000);
        dues.Outstanding.Should().Be(0);
    }

    [Fact]
    public void Outstanding_is_never_negative_and_exemption_cannot_exceed_the_fee()
    {
        var overpaid = MonthlyDuesCalculator.Calculate(1000, 1200, 0, hasFullExemption: false);
        overpaid.Outstanding.Should().Be(0);

        var tooMuchExemption = MonthlyDuesCalculator.Calculate(1000, 0, 5000, hasFullExemption: false);
        tooMuchExemption.Exempted.Should().Be(1000);
        tooMuchExemption.Outstanding.Should().Be(0);
    }

    [Fact]
    public void Monthly_dues_reject_negative_amounts()
    {
        var negativeFee = () => MonthlyDuesCalculator.Calculate(-1, 0, 0, false);
        var negativePaid = () => MonthlyDuesCalculator.Calculate(1000, -1, 0, false);
        var negativeExemption = () => MonthlyDuesCalculator.Calculate(1000, 0, -1, false);

        negativeFee.Should().Throw<ArgumentOutOfRangeException>();
        negativePaid.Should().Throw<ArgumentOutOfRangeException>();
        negativeExemption.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Month_keys_follow_the_phone_format()
    {
        MonthlyDuesCalculator.ToMonthYear(new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc))
            .Should().Be("2026-01");
        MonthlyDuesCalculator.ToMonthYear(new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc))
            .Should().Be("2026-10");
    }
}
