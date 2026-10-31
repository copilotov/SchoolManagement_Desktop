using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Paths;
using SchoolManagement.Application.Persistence;
using SchoolManagement.Application.Settings;
using SchoolManagement.Infrastructure.Licensing;
using SchoolManagement.Infrastructure.Persistence;

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
        }.ToString();

        services.AddDbContextFactory<SchoolManagementDbContext>(
            options => options.UseSqlite(connectionString));

        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<ILicenseBootstrapper, LicenseBootstrapper>();

        return services;
    }
}
