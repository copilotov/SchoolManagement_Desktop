using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Paths;
using SchoolManagement.Application.Persistence;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Settings;
using SchoolManagement.Application.Students;
using SchoolManagement.Infrastructure.Files;
using SchoolManagement.Infrastructure.Licensing;
using SchoolManagement.Infrastructure.Licensing.Ed25519;
using SchoolManagement.Infrastructure.Licensing.Sources;
using SchoolManagement.Infrastructure.Messaging;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Repositories;

namespace SchoolManagement.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        AppPaths paths,
        ILocalizationService localizationService,
        IAppSettingsStore settingsStore)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(localizationService);
        ArgumentNullException.ThrowIfNull(settingsStore);

        services.AddSingleton(paths);
        services.AddSingleton(localizationService);
        services.AddSingleton(settingsStore);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabaseFile,
            ForeignKeys = true,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 5,
        }.ToString();

        services.AddDbContextFactory<SchoolManagementDbContext>(
            options => options
                .UseSqlite(connectionString)
                .AddInterceptors(new SqlitePragmaInterceptor()));

        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();

        // Licensing. There is no environment-variable key override: the trust
        // root is the compile-time LicensePublicKey constant.
        services.AddSingleton<SchoolManagement.Application.Time.ILocalClock, SchoolManagement.Infrastructure.Time.SystemLocalClock>();
        services.AddSingleton<IDeviceFingerprintSource, WindowsDeviceFingerprintSource>();
        services.AddSingleton<ILicenseStorage, LicenseLocalSource>();
        services.AddSingleton<ITrustedTimeStorage, TrustedTimeSource>();
        services.AddSingleton<TrustedClockService>();
        services.AddSingleton<SchoolManagement.Application.Time.ITrustedClock>(
            sp => sp.GetRequiredService<TrustedClockService>());
        services.AddSingleton<LicenseVerifier>();
        services.AddSingleton<ILicenseRepository, LicenseRepository>();
        services.AddSingleton<ILicenseService, LicenseService>();
        services.AddSingleton<ILicenseBootstrapper, LicenseBootstrapper>();
        services.AddSingleton<ILicenseGuard, LicenseGuard>();

        services.AddSingleton(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddSingleton<IStudentRepository, StudentRepository>();
        services.AddSingleton<IGroupRepository, GroupRepository>();
        services.AddSingleton<IEnrollmentRepository, EnrollmentRepository>();
        services.AddSingleton<ISessionRepository, SessionRepository>();
        services.AddSingleton<IAttendanceRepository, AttendanceRepository>();
        services.AddSingleton<IScheduleTemplateRepository, ScheduleTemplateRepository>();
        services.AddSingleton<IPaymentRepository, PaymentRepository>();
        services.AddSingleton<IHomeworkRepository, HomeworkRepository>();
        services.AddSingleton<INotificationRepository, NotificationRepository>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IReferenceDataRepository, ReferenceDataRepository>();
        services.AddSingleton<IDashboardRepository, DashboardRepository>();
        services.AddSingleton<IAcademicLevelRepository, AcademicLevelRepository>();
        services.AddSingleton<IGradeRepository, GradeRepository>();
        services.AddSingleton<ISubjectRepository, SubjectRepository>();
        services.AddSingleton<ITeacherRepository, TeacherRepository>();

        // Phase 5: student file transfer and printable card rendering.
        services.AddSingleton<IStudentFileGateway, StudentFileGateway>();
        services.AddSingleton<IStudentCardService, StudentCardService>();

        // Phase 6: printable full group report.
        services.AddSingleton<SchoolManagement.Application.Groups.IGroupReportService, GroupReportService>();

        // Phase 7: QR decoding, SMS logging and the attendance report.
        services.AddSingleton<SchoolManagement.Application.Attendance.IQrCodeDecoder, QrCodeDecoder>();
        services.AddSingleton<SchoolManagement.Application.Attendance.ISmsService, SmsService>();
        services.AddSingleton<SchoolManagement.Application.Attendance.IAttendanceReportService, AttendanceReportService>();

        return services;
    }
}
