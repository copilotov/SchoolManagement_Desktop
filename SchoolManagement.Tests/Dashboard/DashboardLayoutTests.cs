using FluentAssertions;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Tests.Dashboard;

/// <summary>
/// Phase D4 - the adaptive grid thresholds. They are pure, so the "3/2/1
/// columns with no overflow at any size" promise is asserted without a window.
/// </summary>
public sealed class DashboardLayoutTests
{
    [Theory]
    [InlineData(1600, 3)]
    [InlineData(1200, 3)]
    [InlineData(1199, 2)]
    [InlineData(900, 2)]
    [InlineData(760, 2)]
    [InlineData(759, 1)]
    [InlineData(640, 1)]
    [InlineData(0, 1)]
    [InlineData(-100, 1)]
    public void Overview_and_charts_use_three_two_one_columns(double width, int expected)
        => DashboardLayout.ColumnsFor(width).Should().Be(expected);

    [Theory]
    [InlineData(1600, 4)]
    [InlineData(1200, 4)]
    [InlineData(1199, 2)]
    [InlineData(760, 2)]
    [InlineData(759, 1)]
    public void Quick_actions_fit_four_two_one_cards(double width, int expected)
        => DashboardLayout.QuickActionColumnsFor(width).Should().Be(expected);

    [Theory]
    [InlineData(1600, 2)]
    [InlineData(760, 2)]
    [InlineData(759, 1)]
    [InlineData(500, 1)]
    public void Attention_rows_never_exceed_two_columns(double width, int expected)
        => DashboardLayout.AttentionColumnsFor(width).Should().Be(expected);

    [Fact]
    public void The_smallest_step_is_one_column_so_nothing_can_overflow()
    {
        DashboardLayout.ColumnsFor(320).Should().Be(1);
        DashboardLayout.QuickActionColumnsFor(320).Should().Be(1);
        DashboardLayout.AttentionColumnsFor(320).Should().Be(1);
    }
}
