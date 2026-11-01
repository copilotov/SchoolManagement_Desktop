using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Paths;
using SchoolManagement.Application.Persistence;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Settings;
using SchoolManagement.Application.Time;
using SchoolManagement.Infrastructure.Licensing;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Repositories;
using SchoolManagement.Infrastructure.Time;

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
        services.AddSingleton<ITrustedClock, SystemTrustedClock>();

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
        services.AddSingleton<ILicenseBootstrapper, LicenseBootstrapper>();

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

        return services;
    }
}
