using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Settings;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.Time;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Tests.Navigation;

public sealed class NavigationServiceTests
{
    [Theory]
    [InlineData(ShellTab.Dashboard, typeof(DashboardViewModel))]
    [InlineData(ShellTab.Students, typeof(StudentsViewModel))]
    [InlineData(ShellTab.Groups, typeof(GroupsViewModel))]
    [InlineData(ShellTab.Academics, typeof(AcademicsViewModel))]
    [InlineData(ShellTab.Finance, typeof(FinanceViewModel))]
    [InlineData(ShellTab.Settings, typeof(SettingsViewModel))]
    public void NavigateToTab_resolves_the_expected_view_model(ShellTab tab, Type expectedType)
    {
        using var provider = CreateProvider();
        var navigation = provider.GetRequiredService<INavigationService>();

        navigation.NavigateToTab(tab);

        navigation.CurrentTab.Should().Be(tab);
        navigation.CurrentViewModel.Should().BeOfType(expectedType);
    }

    [Fact]
    public void Navigating_back_reuses_the_same_view_model_instance()
    {
        using var provider = CreateProvider();
        var navigation = provider.GetRequiredService<INavigationService>();

        navigation.NavigateToTab(ShellTab.Dashboard);
        var dashboard = navigation.CurrentViewModel;

        navigation.NavigateTo(ShellTab.Students);
        navigation.NavigateToTab(ShellTab.Dashboard);

        navigation.CurrentViewModel.Should().BeSameAs(dashboard);
        navigation.CurrentTab.Should().Be(ShellTab.Dashboard);
    }

    [Fact]
    public void Navigated_event_carries_the_tab_and_the_current_view_model()
    {
        using var provider = CreateProvider();
        var navigation = provider.GetRequiredService<INavigationService>();
        var events = new List<NavigationChangedEventArgs>();
        navigation.Navigated += (_, args) => events.Add(args);

        navigation.NavigateTo(ShellTab.Dashboard);
        navigation.NavigateToTab(ShellTab.Groups);

        events.Should().HaveCount(2);
        events[0].Tab.Should().Be(ShellTab.Dashboard);
        events[1].Tab.Should().Be(ShellTab.Groups);
        events[1].ViewModel.Should().BeSameAs(navigation.CurrentViewModel);
    }

    [Fact]
    public void Opening_a_sub_screen_keeps_the_current_tab_selected()
    {
        using var provider = CreateProvider();
        var navigation = provider.GetRequiredService<INavigationService>();
        navigation.NavigateTo(ShellTab.Students);

        var opened = navigation.NavigateTo<DashboardViewModel>();

        navigation.CurrentTab.Should().Be(ShellTab.Students);
        navigation.CurrentViewModel.Should().BeSameAs(opened);
        opened.Should().BeOfType<DashboardViewModel>();
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton(Substitute.For<ILocalizationService>());
        services.AddSingleton(Substitute.For<IThemeService>());
        services.AddSingleton(Substitute.For<IAppSettingsStore>());

        services.AddSingleton(Substitute.For<ILicenseService>());
        services.AddSingleton(Substitute.For<IActivationDialogService>());
        services.AddSingleton(Substitute.For<IUserDialogService>());
        services.AddSingleton(Substitute.For<ILicenseGuard>());
        services.AddSingleton<LicenseViewModel>();

        services.AddSingleton(Substitute.For<IAcademicLevelRepository>());
        services.AddSingleton(Substitute.For<IGradeRepository>());
        services.AddSingleton(Substitute.For<ISubjectRepository>());
        services.AddSingleton(Substitute.For<ITeacherRepository>());

        services.AddSingleton(Substitute.For<IStudentRepository>());
        services.AddSingleton(Substitute.For<IGroupRepository>());
        services.AddSingleton(Substitute.For<IEnrollmentRepository>());
        services.AddSingleton(Substitute.For<IPaymentRepository>());
        services.AddSingleton(Substitute.For<IAttendanceRepository>());
        services.AddSingleton(Substitute.For<IReferenceDataRepository>());
        services.AddSingleton(Substitute.For<IScheduleTemplateRepository>());
        services.AddSingleton(Substitute.For<ISessionRepository>());
        services.AddSingleton(Substitute.For<IGroupReportService>());
        services.AddSingleton(Substitute.For<ITrustedClock>());
        services.AddSingleton(Substitute.For<IStudentFileGateway>());
        services.AddSingleton(Substitute.For<IStudentCardService>());
        services.AddSingleton(Substitute.For<IFileDialogService>());
        services.AddSingleton(Substitute.For<IPdfPrintService>());

        services.AddTransient<GetAcademicLevelsUseCase>();
        services.AddTransient<GetGradesUseCase>();
        services.AddTransient<GetSubjectsUseCase>();
        services.AddTransient<GetTeachersUseCase>();
        services.AddTransient<GetGradeSubjectsUseCase>();
        services.AddTransient<AddAcademicLevelUseCase>();
        services.AddTransient<UpdateAcademicLevelUseCase>();
        services.AddTransient<DeleteAcademicLevelUseCase>();
        services.AddTransient<AddGradeUseCase>();
        services.AddTransient<UpdateGradeUseCase>();
        services.AddTransient<DeleteGradeUseCase>();
        services.AddTransient<SetGradeSubjectsUseCase>();
        services.AddTransient<AddSubjectUseCase>();
        services.AddTransient<UpdateSubjectUseCase>();
        services.AddTransient<DeleteSubjectUseCase>();
        services.AddTransient<AddTeacherUseCase>();
        services.AddTransient<UpdateTeacherUseCase>();
        services.AddTransient<DeleteTeacherUseCase>();

        services.AddTransient<GetStudentLookupsUseCase>();
        services.AddTransient<GetStudentsUseCase>();
        services.AddTransient<GetStudentDetailsUseCase>();
        services.AddTransient<GetStudentActivityUseCase>();
        services.AddTransient<GetStudentCardDataUseCase>();
        services.AddTransient<SaveStudentUseCase>();
        services.AddTransient<DeleteStudentUseCase>();
        services.AddTransient<ImportStudentsUseCase>();
        services.AddTransient<ExportStudentsUseCase>();
        services.AddTransient<RenderStudentQrUseCase>();
        services.AddTransient<RenderStudentCardPreviewUseCase>();
        services.AddTransient<BuildStudentCardPdfUseCase>();
        services.AddTransient<BuildStudentsCardsPdfUseCase>();
        services.AddTransient<RenderStudentsQrUseCase>();
        services.AddTransient<BuildStudentsQrSheetPdfUseCase>();

        services.AddTransient<GetGroupLookupsUseCase>();
        services.AddTransient<GetGroupSubjectsUseCase>();
        services.AddTransient<GetGroupsUseCase>();
        services.AddTransient<GetGroupSummaryUseCase>();
        services.AddTransient<GetGroupDetailsUseCase>();
        services.AddTransient<GetGroupScheduleUseCase>();
        services.AddTransient<GetScheduleConflictsUseCase>();
        services.AddTransient<GetTimetableUseCase>();
        services.AddTransient<GetEnrollmentBoardUseCase>();
        services.AddTransient<BuildGroupFullReportUseCase>();
        services.AddTransient<SaveGroupUseCase>();
        services.AddTransient<RemoveGroupUseCase>();
        services.AddTransient<EnrollStudentInGroupUseCase>();
        services.AddTransient<WithdrawEnrollmentUseCase>();
        services.AddTransient<SaveScheduleSlotUseCase>();
        services.AddTransient<DeleteScheduleSlotUseCase>();
        services.AddTransient<GenerateSessionsUseCase>();

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<StudentsViewModel>();
        services.AddTransient<StudentDetailsViewModel>();
        services.AddTransient<StudentCardViewModel>();
        services.AddTransient<StudentsQrPrintViewModel>();
        services.AddTransient<GroupsViewModel>();
        services.AddTransient<GroupDetailsViewModel>();
        services.AddTransient<GroupScheduleViewModel>();
        services.AddTransient<EnrollmentViewModel>();
        services.AddTransient<TimetableViewModel>();
        services.AddTransient<GroupFullReportViewModel>();
        services.AddTransient<AcademicLevelsViewModel>();
        services.AddTransient<AcademicGradesViewModel>();
        services.AddTransient<AcademicSubjectsViewModel>();
        services.AddTransient<AcademicTeachersViewModel>();
        services.AddTransient<AcademicsViewModel>();
        services.AddTransient<FinanceViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }
}
