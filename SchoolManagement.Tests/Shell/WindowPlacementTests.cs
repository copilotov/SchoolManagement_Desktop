using FluentAssertions;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Tests.Shell;

public sealed class WindowPlacementTests
{
    private const double VirtualLeft = 0;
    private const double VirtualTop = 0;
    private const double VirtualWidth = 1920;
    private const double VirtualHeight = 1080;
    private const double MinWidth = 1000;
    private const double MinHeight = 650;

    [Fact]
    public void A_placement_inside_the_desktop_is_restored_unchanged()
    {
        var placement = new WindowPlacement(100, 120, 1280, 820, false);

        var clamped = Clamp(placement);

        clamped.Should().Be(placement);
    }

    [Fact]
    public void A_missing_position_is_centred_on_the_desktop()
    {
        var clamped = Clamp(WindowPlacement.Default);

        clamped.Left.Should().Be((VirtualWidth - WindowPlacement.DefaultWidth) / 2);
        clamped.Top.Should().Be((VirtualHeight - WindowPlacement.DefaultHeight) / 2);
        clamped.Width.Should().Be(WindowPlacement.DefaultWidth);
        clamped.Height.Should().Be(WindowPlacement.DefaultHeight);
    }

    [Fact]
    public void A_window_saved_on_an_unplugged_monitor_stays_reachable()
    {
        var placement = new WindowPlacement(5000, -5000, 1280, 820, false);

        var clamped = Clamp(placement);

        // At least 80 units of the title bar remain on the virtual desktop.
        clamped.Left.Should().Be(VirtualWidth - 80);
        clamped.Top.Should().Be(-820 + 80);
        clamped.Width.Should().Be(1280);
        clamped.Height.Should().Be(820);
    }

    [Fact]
    public void The_minimum_size_wins_over_a_too_small_saved_size()
    {
        var clamped = Clamp(new WindowPlacement(10, 10, 320, 200, false));

        clamped.Width.Should().Be(MinWidth);
        clamped.Height.Should().Be(MinHeight);
    }

    [Fact]
    public void A_size_larger_than_the_desktop_is_capped()
    {
        var clamped = Clamp(new WindowPlacement(0, 0, 4000, 3000, false));

        clamped.Width.Should().Be(VirtualWidth);
        clamped.Height.Should().Be(VirtualHeight);
    }

    [Fact]
    public void A_corrupt_value_falls_back_to_the_default_size()
    {
        var clamped = Clamp(new WindowPlacement(double.NaN, double.PositiveInfinity, -10, 0, false));

        clamped.Width.Should().Be(WindowPlacement.DefaultWidth);
        clamped.Height.Should().Be(WindowPlacement.DefaultHeight);
        clamped.Left.Should().Be((VirtualWidth - WindowPlacement.DefaultWidth) / 2);
        clamped.Top.Should().Be((VirtualHeight - WindowPlacement.DefaultHeight) / 2);
    }

    [Fact]
    public void The_maximized_state_survives_clamping()
    {
        var clamped = Clamp(new WindowPlacement(100, 100, 1280, 820, true));

        clamped.IsMaximized.Should().BeTrue();
    }

    [Fact]
    public void A_negative_origin_multi_monitor_desktop_keeps_the_window_on_screen()
    {
        var clamped = new WindowPlacement(-500, -200, 1280, 820, false).Clamp(
            -1920,
            0,
            3840,
            1080,
            MinWidth,
            MinHeight);

        clamped.Left.Should().Be(-500);
        clamped.Top.Should().Be(-200);
    }

    private static WindowPlacement Clamp(WindowPlacement placement) => placement.Clamp(
        VirtualLeft,
        VirtualTop,
        VirtualWidth,
        VirtualHeight,
        MinWidth,
        MinHeight);
}
