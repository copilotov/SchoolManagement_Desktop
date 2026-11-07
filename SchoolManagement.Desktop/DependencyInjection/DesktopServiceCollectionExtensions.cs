using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Desktop.Views;

namespace SchoolManagement.Desktop.DependencyInjection;

public static class DesktopServiceCollectionExtensions
{
    /// <summary>
    /// Registers WPF services and ViewModels. ViewModels are transient; the
    /// NavigationService caches one instance per tab for state preservation.
    /// Shared services (theme, navigation, localization, settings) are singletons.
    /// </summary>
    public static IServiceCollection AddDesktop(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IActivationDialogService, ActivationDialogService>();
        services.AddSingleton<IUserDialogService, UserDialogService>();

        // Phase D2 - shell chrome. The toast queue is shared by the whole window;
        // the window geometry lives in its own file so settings.json stays
        // limited to language and theme.
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<IWindowStateStore>(provider =>
            new WindowStateStore(
                WindowStateStore.DefaultPath(),
                provider.GetRequiredService<ILogger<WindowStateStore>>()));

        // Phase 7 seams: the SMS launcher opens the messaging app and the QR
        // scanner window captures webcam frames; both stay in the WPF layer.
        services.AddSingleton<ISmsLauncher, SmsLauncher>();
        services.AddSingleton<IQrScannerDialogService, QrScannerDialogService>();

        // Phase 5 seams: file dialogs, PDF printing and image decoding stay in
        // the WPF layer so the ViewModels remain testable without a message loop.
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<IPdfPrintService, PdfPrintService>();

        services.AddSingleton<LicenseViewModel>();
        services.AddTransient<ActivationViewModel>();

        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<CommandPaletteViewModel>();
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
        services.AddTransient<AttendanceViewModel>();
        services.AddTransient<TakeAttendanceViewModel>();
        services.AddTransient<HomeworkViewModel>();
        services.AddTransient<AttendanceReportViewModel>();
        services.AddTransient<AcademicsViewModel>();
        services.AddTransient<AcademicLevelsViewModel>();
        services.AddTransient<AcademicGradesViewModel>();
        services.AddTransient<AcademicSubjectsViewModel>();
        services.AddTransient<AcademicTeachersViewModel>();
        services.AddTransient<FinanceViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<NotificationsViewModel>();
        services.AddTransient<MessagesViewModel>();

        services.AddTransient<ActivationWindow>();
        services.AddTransient<ShellWindow>();
        services.AddTransient<ShortcutsWindow>();

#if DEBUG
        // Phase D1 - the design gallery is a development tool only. The F12
        // hook in ShellWindow is compiled out of release builds.
        services.AddTransient<DesignGalleryViewModel>();
        services.AddTransient<DesignGalleryWindow>();
#endif

        return services;
    }
}
