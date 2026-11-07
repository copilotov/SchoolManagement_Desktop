using System.Threading;
using System.Windows;
using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Localization;
using SchoolManagement.Desktop.Controls;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Desktop.Views;

namespace SchoolManagement.Tests.DesignSystem;

/// <summary>
/// Phase D1 - the styles are only ever resolved at runtime, so a missing
/// <c>StaticResource</c> key or a broken template is invisible to a pure unit
/// test. This test loads the real <c>App.xaml</c> resource tree (WPF-UI
/// dictionaries, theme shim, design tokens, component styles) on an STA thread
/// and instantiates the design gallery, which references every style the
/// foundation defines.
///
/// Phase D3 - the same window also hosts one live sample of every reusable
/// component, so a component that stops resolving its resources or disappears
/// from the gallery fails here.
/// </summary>
public sealed class DesignGalleryWindowTests
{
    [Fact]
    public void Gallery_window_loads_with_the_real_application_resources()
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                // App.InitializeComponent is the generated loader for App.xaml:
                // it builds the exact MergedDictionaries list the shipped app
                // uses, so this test cannot drift from the real merge order.
                var application = new SchoolManagement.Desktop.App();
                application.InitializeComponent();

                var viewModel = new DesignGalleryViewModel(
                    Substitute.For<IThemeService>(),
                    Substitute.For<ILocalizationService>());

                var window = new DesignGalleryWindow(viewModel);

                window.DataContext.Should().BeSameAs(viewModel);

                // Phase D3 - every reusable component has a live sample in the
                // gallery. FindName walks the real name scope built by the XAML
                // parser, so a deleted sample is a test failure, not a manual
                // review note.
                var expected = new Dictionary<string, Type>(StringComparer.Ordinal)
                {
                    ["HeaderSample"] = typeof(PageHeader),
                    ["StatStudents"] = typeof(StatCard),
                    ["StatGroups"] = typeof(StatCard),
                    ["StatAttendance"] = typeof(StatCard),
                    ["StatOutstanding"] = typeof(StatCard),
                    ["TableSample"] = typeof(DataTable),
                    ["EmptySample"] = typeof(EmptyState),
                    ["ErrorSample"] = typeof(ErrorState),
                    ["SkeletonSample"] = typeof(SkeletonLoader),
                    ["SectionSample"] = typeof(FormSection),
                    ["WizardSample"] = typeof(Wizard),
                    ["PanelSample"] = typeof(SidePanel),
                    ["DialogSample"] = typeof(Dialog),
                    ["BadgeSample"] = typeof(StatusBadge),
                    ["ChipSample"] = typeof(Chip),
                    ["CapacitySample"] = typeof(CapacityBar),
                    ["CapacityFullSample"] = typeof(CapacityBar),
                    ["ProgressSample"] = typeof(ProgressStat),
                    ["ProgressSampleB"] = typeof(ProgressStat),
                    ["AvatarSampleA"] = typeof(Avatar),
                    ["AvatarSampleD"] = typeof(Avatar),
                    ["RatingSample"] = typeof(RatingDots),
                    ["RangeSample"] = typeof(DateRangePicker),
                    ["MonthSample"] = typeof(MonthPicker),
                    ["SearchSample"] = typeof(SearchBox),
                    ["TagSample"] = typeof(TagInput),
                    ["ScheduleSample"] = typeof(WeekScheduleGrid),
                    ["ScheduleCompactSample"] = typeof(WeekScheduleGrid),
                    ["QrSample"] = typeof(QrCardPreview),
                    ["ChartLineSample"] = typeof(ChartCard),
                    ["ChartAreaSample"] = typeof(ChartCard),
                    ["ChartBarSample"] = typeof(ChartCard),
                };

                foreach (var (name, type) in expected)
                {
                    var element = window.FindName(name);
                    element.Should().NotBeNull($"{name} must stay in the design gallery");
                    element.Should().BeAssignableTo(type, $"{name} must stay a {type.Name}");
                }

                // The DataTable sample proves the column collection is live and
                // that a screen binds real columns, not a placeholder grid.
                var table = window.FindName("TableSample") as DataTable;
                table.Should().NotBeNull();
                table!.Columns.Should().HaveCount(3);

                // The master-detail panel only opens with a template, so the
                // gallery must supply one.
                table.DetailTemplate.Should().NotBeNull();

                window.Close();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(30)).Should().BeTrue("the WPF thread must finish");

        failure.Should().BeNull(
            "every StaticResource used by the design system must resolve from App.xaml");
    }
}
