using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Dashboard;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Desktop.Controls;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Tests.Dashboard;

/// <summary>
/// Phase D4 - the dashboard command center. Every test runs the real
/// <see cref="GetDashboardSnapshotUseCase"/> over substituted repositories, so
/// the aggregation rules and the display records are exercised together.
/// </summary>
public sealed class DashboardViewModelTests
{
    [Fact]
    public async Task Load_builds_the_five_stat_cards_in_priority_order()
    {
        var harness = new DashboardVmHarness().WithPopulatedSchool();

        (await harness.LoadAsync()).Should().BeTrue();

        harness.ViewModel.Stats.Should().HaveCount(5);
        harness.ViewModel.Stats.Select(stat => stat.Target).Should().ContainInOrder(
            DashboardStatTarget.Students,
            DashboardStatTarget.Groups,
            DashboardStatTarget.Sessions,
            DashboardStatTarget.Revenue,
            DashboardStatTarget.Arrears);

        harness.ViewModel.Stats[0].Value.Should().Be("24");
        harness.ViewModel.Stats[1].Value.Should().Be("3");
        harness.ViewModel.Stats[2].Value.Should().Be("3");
        harness.ViewModel.Stats[3].Value.Should().Be("350 DZD");
        harness.ViewModel.Stats[4].Value.Should().Be("150 DZD");
        harness.ViewModel.Stats[4].Caption.Should().Be("2 students in arrears");
        harness.ViewModel.Stats[3].Tone.Should().Be(StatusTone.Success);
        harness.ViewModel.Stats[4].Tone.Should().Be(StatusTone.Danger);
    }

    [Fact]
    public async Task Only_the_revenue_card_compares_the_last_two_months()
    {
        var harness = new DashboardVmHarness().WithPopulatedSchool();

        await harness.LoadAsync();

        harness.ViewModel.Stats[3].Trend.Should().Be(TrendDirection.Up);
        harness.ViewModel.Stats[3].Sparkline.Should().HaveCount(6);
        harness.ViewModel.Stats.Where(stat => stat.Target != DashboardStatTarget.Revenue)
            .Should().OnlyContain(stat => stat.Trend == TrendDirection.Flat);
    }

    [Theory]
    [InlineData(8, "Good morning")]
    [InlineData(11, "Good morning")]
    [InlineData(14, "Good afternoon")]
    [InlineData(20, "Good evening")]
    public async Task Greeting_follows_the_time_of_day_and_names_the_school(int hour, string expected)
    {
        var harness = new DashboardVmHarness(
            new DateTime(2026, 11, 4, hour, 0, 0, DateTimeKind.Utc));

        await harness.LoadAsync();

        harness.ViewModel.Greeting.Should().Be(expected);
        harness.ViewModel.GreetingTitle.Should().Be($"{expected}, Test School");
        harness.ViewModel.SchoolName.Should().Be("Test School");
    }

    [Fact]
    public async Task Date_text_uses_the_localized_weekday_and_month()
    {
        var harness = new DashboardVmHarness().WithPopulatedSchool();
        harness.Localization.Values[TranslationKeys.Days.Wednesday] = "Wednesday";
        harness.Localization.Values[TranslationKeys.Months.November] = "November";

        await harness.LoadAsync();

        harness.ViewModel.DateText.Should().Be("Wednesday 4 November 2026");
    }

    [Fact]
    public async Task Sessions_are_sorted_by_start_time_and_carry_their_own_rules()
    {
        var harness = new DashboardVmHarness().WithPopulatedSchool();
        harness.Localization.Values[TranslationKeys.Attendance.Regular] = "Regular";
        harness.Localization.Values[TranslationKeys.Attendance.Makeup] = "Makeup";
        harness.Localization.Values[TranslationKeys.Attendance.Scheduled] = "Scheduled";
        harness.Localization.Values[TranslationKeys.Attendance.Completed] = "Completed";

        await harness.LoadAsync();

        harness.ViewModel.Sessions.Select(row => row.Id).Should().ContainInOrder(1, 3, 2);
        var first = harness.ViewModel.Sessions[0];
        first.TimeText.Should().Be("09:00 - 10:00");
        first.TypeText.Should().Be("Regular");
        first.StatusText.Should().Be("Scheduled");
        first.StudentsText.Should().Be("20 students");
        first.CanTakeAttendance.Should().BeTrue();
        first.HasAttendance.Should().BeFalse();
        first.Tone.Should().Be(StatusTone.Warning);

        harness.ViewModel.Sessions[1].CanTakeAttendance.Should().BeFalse();
        harness.ViewModel.Sessions[1].HasAttendance.Should().BeTrue();
        harness.ViewModel.HasSessions.Should().BeTrue();
    }

    [Fact]
    public async Task Alerts_follow_the_documented_priority_order()
    {
        var harness = new DashboardVmHarness(
            license: new LicenseInfo(LicenseStatus.ExpiringSoon, "fp", DaysRemaining: 12))
            .WithPopulatedSchool();
        harness.Dashboard
            .GetAbsenceAlertsAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ResultOf(new DashboardAbsenceAlert(9, "STU-2026-0009", "Dina", 6, 5)));

        await harness.LoadAsync();

        harness.ViewModel.Alerts.Select(alert => alert.Target).Should().ContainInOrder(
            DashboardAlertTarget.Finance,
            DashboardAlertTarget.Students,
            DashboardAlertTarget.Attendance,
            DashboardAlertTarget.License);

        harness.ViewModel.Alerts[0].Body.Should().Be("2 students · 150 DZD");
        harness.ViewModel.Alerts[0].Tone.Should().Be(StatusTone.Warning);
        harness.ViewModel.Alerts[1].Body.Should().Be("1 students over 5");
        harness.ViewModel.Alerts[1].Tone.Should().Be(StatusTone.Danger);
        harness.ViewModel.Alerts[2].Body.Should().Be("2 sessions");
        harness.ViewModel.Alerts[3].Body.Should().Be("12 days left");
        harness.ViewModel.HasAlerts.Should().BeTrue();
        harness.ViewModel.HasNoAlerts.Should().BeFalse();
    }

    [Fact]
    public async Task A_quiet_day_shows_the_all_clear_state()
    {
        var harness = new DashboardVmHarness();

        await harness.LoadAsync();

        harness.ViewModel.Alerts.Should().BeEmpty();
        harness.ViewModel.HasNoAlerts.Should().BeTrue();
        harness.ViewModel.HasAlerts.Should().BeFalse();
        harness.ViewModel.HasSessions.Should().BeFalse();
    }

    [Fact]
    public async Task A_new_school_shows_the_guided_setup_instead_of_the_overview()
    {
        var harness = new DashboardVmHarness();

        await harness.LoadAsync();

        harness.ViewModel.IsNewSchool.Should().BeTrue();
        harness.ViewModel.ShowSetup.Should().BeTrue();
        harness.ViewModel.ShowContent.Should().BeFalse();
        harness.ViewModel.SetupSteps.Should().HaveCount(5);
        harness.ViewModel.SetupSteps.Select(step => step.Number).Should().ContainInOrder(1, 2, 3, 4, 5);
        harness.ViewModel.SetupSteps.Select(step => step.Target).Should().ContainInOrder(
            DashboardSetupTarget.Levels,
            DashboardSetupTarget.Subjects,
            DashboardSetupTarget.Teachers,
            DashboardSetupTarget.Groups,
            DashboardSetupTarget.Students);
        harness.ViewModel.SetupSteps.Should().OnlyContain(step => !step.IsComplete);
        harness.ViewModel.SetupSteps.Should().OnlyContain(step => step.Tone == StatusTone.Neutral);
        harness.ViewModel.SetupSteps[0].CountText.Should().Be("0 items");
        harness.ViewModel.SetupSteps[0].StateText.Should().Be("To do");
    }

    [Fact]
    public async Task A_populated_school_hides_the_guide_and_marks_completed_steps()
    {
        var harness = new DashboardVmHarness().WithPopulatedSchool();

        await harness.LoadAsync();

        harness.ViewModel.IsNewSchool.Should().BeFalse();
        harness.ViewModel.ShowContent.Should().BeTrue();
        harness.ViewModel.ShowSetup.Should().BeFalse();
        harness.ViewModel.SetupSteps.Should().OnlyContain(step => step.IsComplete);
        harness.ViewModel.SetupSteps.Should().OnlyContain(step => step.Tone == StatusTone.Success);
        harness.ViewModel.SetupSteps[4].CountText.Should().Be("24 items");
        harness.ViewModel.SetupSteps[4].StateText.Should().Be("Done");
    }

    [Fact]
    public async Task Charts_are_built_from_the_snapshot()
    {
        var harness = new DashboardVmHarness().WithPopulatedSchool();
        harness.Localization.Values[TranslationKeys.Months.June] = "June";
        harness.Localization.Values[TranslationKeys.Months.July] = "July";
        harness.Localization.Values[TranslationKeys.Months.August] = "August";
        harness.Localization.Values[TranslationKeys.Months.September] = "September";
        harness.Localization.Values[TranslationKeys.Months.October] = "October";
        harness.Localization.Values[TranslationKeys.Months.November] = "November";
        harness.Localization.Values[TranslationKeys.Days.Saturday] = "Saturday";
        harness.Localization.Values[TranslationKeys.Days.Sunday] = "Sunday";

        await harness.LoadAsync();

        harness.ViewModel.RevenueValues.Should().HaveCount(6);
        harness.ViewModel.RevenueValues[^1].Should().Be(250);
        harness.ViewModel.RevenueLabels.Should().ContainInOrder(
            "June", "July", "August", "September", "October", "November");
        harness.ViewModel.RevenueSummary.Should().Be("6-month total: 350 DZD");

        // The use case always fills all seven school days, so the two populated
        // days keep their rate and the other five stay at zero.
        harness.ViewModel.WeekAttendanceValues.Should().Equal(90, 50, 0, 0, 0, 0, 0);
        harness.ViewModel.WeekAttendanceLabels.Should().ContainInOrder("Saturday", "Sunday");
        harness.ViewModel.WeekAttendanceSummary.Should().Be("Attendance rate: 72.2%");

        harness.ViewModel.LevelValues.Should().Equal(15, 3);
        harness.ViewModel.LevelLabels.Should().ContainInOrder("Level One", "Unplaced");
        harness.ViewModel.LevelSummary.Should().Be("2 levels");
    }

    [Fact]
    public async Task Empty_charts_report_no_data_instead_of_a_zero_line()
    {
        var harness = new DashboardVmHarness();

        await harness.LoadAsync();

        harness.ViewModel.RevenueValues.Should().BeEmpty();
        harness.ViewModel.RevenueLabels.Should().BeEmpty();
        harness.ViewModel.RevenueSummary.Should().BeEmpty();
        harness.ViewModel.WeekAttendanceValues.Should().BeEmpty();
        harness.ViewModel.WeekAttendanceSummary.Should().BeEmpty();
        harness.ViewModel.LevelValues.Should().BeEmpty();
        harness.ViewModel.LevelSummary.Should().BeEmpty();
    }

    [Fact]
    public async Task A_failure_shows_the_localized_error_and_keeps_retry_enabled()
    {
        var harness = new DashboardVmHarness();
        harness.Localization.Values[TranslationKeys.Errors.DatabaseFailure] = "Database unavailable";
        harness.Dashboard
            .GetTodaySessionsAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<DashboardSessionItem>>.Fail(Failure.Database()));

        (await harness.LoadAsync()).Should().BeFalse();

        harness.ViewModel.HasError.Should().BeTrue();
        harness.ViewModel.ErrorMessage.Should().Be("Database unavailable");
        harness.ViewModel.IsLoading.Should().BeFalse();
        harness.ViewModel.ShowContent.Should().BeFalse();
        harness.ViewModel.ShowSetup.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureLoaded_loads_the_snapshot_only_once()
    {
        var harness = new DashboardVmHarness().WithPopulatedSchool();

        await harness.ViewModel.EnsureLoadedAsync();
        await harness.ViewModel.EnsureLoadedAsync();

        await harness.Dashboard.Received(1)
            .GetTodaySessionsAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_reloads_the_snapshot_after_the_first_load()
    {
        var harness = new DashboardVmHarness().WithPopulatedSchool();
        await harness.LoadAsync();

        await harness.ViewModel.RefreshCommand.ExecuteAsync(null);

        await harness.Dashboard.Received(2)
            .GetTodaySessionsAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Update_width_rearranges_every_grid()
    {
        var harness = new DashboardVmHarness();

        harness.ViewModel.UpdateWidth(1300);
        harness.ViewModel.StatColumns.Should().Be(3);
        harness.ViewModel.ChartColumns.Should().Be(3);
        harness.ViewModel.QuickColumns.Should().Be(4);
        harness.ViewModel.AttentionColumns.Should().Be(2);

        harness.ViewModel.UpdateWidth(900);
        harness.ViewModel.StatColumns.Should().Be(2);
        harness.ViewModel.ChartColumns.Should().Be(2);
        harness.ViewModel.QuickColumns.Should().Be(2);
        harness.ViewModel.AttentionColumns.Should().Be(2);

        harness.ViewModel.UpdateWidth(600);
        harness.ViewModel.StatColumns.Should().Be(1);
        harness.ViewModel.ChartColumns.Should().Be(1);
        harness.ViewModel.QuickColumns.Should().Be(1);
        harness.ViewModel.AttentionColumns.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-20)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void An_unusable_width_leaves_the_layout_untouched(double width)
    {
        var harness = new DashboardVmHarness();
        harness.ViewModel.UpdateWidth(1300);

        harness.ViewModel.UpdateWidth(width);

        harness.ViewModel.StatColumns.Should().Be(3);
        harness.ViewModel.QuickColumns.Should().Be(4);
    }

    [Fact]
    public async Task A_language_change_rebuilds_every_label_immediately()
    {
        var harness = new DashboardVmHarness().WithPopulatedSchool();
        harness.Localization.Values[TranslationKeys.Dashboard.StudentsCount] = "Students";
        await harness.LoadAsync();
        harness.ViewModel.Stats[0].Title.Should().Be("Students");

        harness.Localization.Values[TranslationKeys.Dashboard.StudentsCount] = "Élèves";
        harness.Localization.Values[TranslationKeys.Dashboard.GreetingMorning] = "Bonjour";
        harness.Localization.RaiseLanguageChanged();

        harness.ViewModel.Stats[0].Title.Should().Be("Élèves");
        harness.ViewModel.Greeting.Should().Be("Bonjour");
        harness.ViewModel.GreetingTitle.Should().Be("Bonjour, Test School");
    }

    [Fact]
    public async Task Stat_cards_open_their_screen_with_a_ready_made_filter()
    {
        var harness = new DashboardVmHarness().WithPopulatedSchool();
        await harness.LoadAsync();

        await harness.ViewModel.OpenStatCommand.ExecuteAsync(harness.ViewModel.Stats[0]);
        harness.Navigation.Received(1).NavigateToTab(ShellTab.Students);

        await harness.ViewModel.OpenStatCommand.ExecuteAsync(harness.ViewModel.Stats[4]);
        harness.Navigation.Received(1).NavigateToTab(ShellTab.Finance);
    }

    [Fact]
    public async Task Quick_actions_are_four_and_route_to_their_screens()
    {
        var harness = new DashboardVmHarness().WithPopulatedSchool();
        await harness.LoadAsync();

        harness.ViewModel.QuickActions.Select(action => action.Target).Should().ContainInOrder(
            DashboardQuickTarget.NewStudent,
            DashboardQuickTarget.Payment,
            DashboardQuickTarget.Attendance,
            DashboardQuickTarget.Report);

        await harness.ViewModel.OpenQuickActionCommand.ExecuteAsync(harness.ViewModel.QuickActions[0]);
        harness.Navigation.Received(1).NavigateToTab(ShellTab.Students);

        await harness.ViewModel.OpenQuickActionCommand.ExecuteAsync(harness.ViewModel.QuickActions[1]);
        harness.Navigation.Received(1).NavigateToTab(ShellTab.Finance);

        await harness.ViewModel.OpenQuickActionCommand.ExecuteAsync(harness.ViewModel.QuickActions[2]);
        harness.Navigation.Received(1).NavigateToTab(ShellTab.Attendance);
    }

    [Fact]
    public async Task A_setup_step_opens_the_matching_academics_tab()
    {
        var harness = new DashboardVmHarness();
        await harness.LoadAsync();

        await harness.ViewModel.OpenSetupStepCommand.ExecuteAsync(harness.ViewModel.SetupSteps[0]);

        harness.Navigation.Received(1).NavigateToTab(ShellTab.Academics);
    }

    [Fact]
    public async Task Take_attendance_opens_the_roster_of_an_open_session()
    {
        var harness = new DashboardVmHarness().WithPopulatedSchool();
        await harness.LoadAsync();

        await harness.ViewModel.TakeAttendanceCommand.ExecuteAsync(harness.ViewModel.Sessions[0]);

        harness.Navigation.Received(1).NavigateTo<TakeAttendanceViewModel>();
    }

    [Fact]
    public async Task Take_attendance_is_refused_for_a_closed_session()
    {
        var harness = new DashboardVmHarness().WithPopulatedSchool();
        await harness.LoadAsync();
        var closed = harness.ViewModel.Sessions.Single(row => !row.CanTakeAttendance);

        await harness.ViewModel.TakeAttendanceCommand.ExecuteAsync(closed);

        harness.Navigation.DidNotReceive().NavigateTo<TakeAttendanceViewModel>();
    }

    [Fact]
    public async Task A_license_alert_opens_the_activation_dialog()
    {
        var harness = new DashboardVmHarness(
            license: new LicenseInfo(LicenseStatus.ExpiringSoon, "fp", DaysRemaining: 3));
        await harness.LoadAsync();
        var alert = harness.ViewModel.Alerts.Single(row => row.Target == DashboardAlertTarget.License);

        await harness.ViewModel.OpenAlertCommand.ExecuteAsync(alert);

        await harness.ActivationDialog.Received(1).ShowAsync();
    }

    [Fact]
    public async Task A_payment_alert_opens_the_finance_screen()
    {
        var harness = new DashboardVmHarness().WithPopulatedSchool();
        await harness.LoadAsync();
        var alert = harness.ViewModel.Alerts.Single(row => row.Target == DashboardAlertTarget.Finance);

        await harness.ViewModel.OpenAlertCommand.ExecuteAsync(alert);

        harness.Navigation.Received(1).NavigateToTab(ShellTab.Finance);
    }

    [Fact]
    public async Task A_null_command_parameter_is_ignored()
    {
        var harness = new DashboardVmHarness().WithPopulatedSchool();
        await harness.LoadAsync();

        await harness.ViewModel.OpenStatCommand.ExecuteAsync(null);
        await harness.ViewModel.OpenAlertCommand.ExecuteAsync(null);
        await harness.ViewModel.OpenQuickActionCommand.ExecuteAsync(null);
        await harness.ViewModel.OpenSetupStepCommand.ExecuteAsync(null);
        await harness.ViewModel.TakeAttendanceCommand.ExecuteAsync(null);

        harness.Navigation.DidNotReceiveWithAnyArgs().NavigateToTab(default);
        harness.Navigation.DidNotReceive().NavigateTo<TakeAttendanceViewModel>();
    }

    [Fact]
    public void Dispose_unsubscribes_from_the_language_and_license_pulses()
    {
        var harness = new DashboardVmHarness();

        harness.ViewModel.Dispose();
        harness.ViewModel.Dispose();

        harness.Localization.RaiseLanguageChanged();
        harness.License.Apply(new LicenseInfo(LicenseStatus.ExpiringSoon, "fp", DaysRemaining: 2));
    }

    private static Result<IReadOnlyList<DashboardAbsenceAlert>> ResultOf(DashboardAbsenceAlert alert)
        => Result<IReadOnlyList<DashboardAbsenceAlert>>.Success([alert]);
}
