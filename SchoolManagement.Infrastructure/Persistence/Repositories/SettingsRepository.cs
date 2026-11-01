using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Key/value settings plus the single-row fee settings. The original schema
/// seeds both; reads stay tolerant when a row is missing.
/// </summary>
public sealed class SettingsRepository : RepositoryBase, ISettingsRepository
{
    public const int FeeSettingsId = 1;

    public SettingsRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<SettingsRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<SystemSetting?>> GetSystemSettingAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult(
                Result<SystemSetting?>.Fail(
                    Failure.Validation(TranslationKeys.Errors.RequiredFields, nameof(key))));
        }

        var settingKey = key.Trim();

        return RunAsync<SystemSetting?>(
            "Settings.GetSystemSetting",
            async context => await context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(setting => setting.Key == settingKey, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
    }

    public Task<Result<bool>> SetSystemSettingAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult(
                Result<bool>.Fail(
                    Failure.Validation(TranslationKeys.Errors.RequiredFields, nameof(key))));
        }

        var settingKey = key.Trim();

        return RunResultAsync(
            "Settings.SetSystemSetting",
            async context =>
            {
                var setting = await context.SystemSettings
                    .FirstOrDefaultAsync(x => x.Key == settingKey, cancellationToken)
                    .ConfigureAwait(false);

                if (setting is null)
                {
                    setting = new SystemSetting
                    {
                        Key = settingKey,
                        Value = value,
                        UpdatedAt = Clock.LocalNow,
                    };
                    context.SystemSettings.Add(setting);
                }
                else
                {
                    setting.Value = value;
                    setting.UpdatedAt = Clock.LocalNow;
                }

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
    }

    public Task<Result<int>> GetAbsenceLimitAsync(CancellationToken cancellationToken = default)
        => RunAsync(
            "Settings.GetAbsenceLimit",
            async context =>
            {
                var storedValue = await context.SystemSettings
                    .AsNoTracking()
                    .Where(setting => setting.Key == AbsenceLimitPolicy.SettingKey)
                    .Select(setting => setting.Value)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                return AbsenceLimitPolicy.ParseLimit(storedValue);
            },
            cancellationToken);

    public Task<Result<FeeSetting>> GetFeeSettingsAsync(CancellationToken cancellationToken = default)
        => RunAsync(
            "Settings.GetFeeSettings",
            async context => await context.FeeSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
                ?? new FeeSetting
                {
                    Id = FeeSettingsId,
                    UpdatedAt = Clock.LocalNow,
                },
            cancellationToken);

    public Task<Result<bool>> UpdateFeeSettingsAsync(
        FeeSetting settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return RunResultAsync(
            "Settings.UpdateFeeSettings",
            async context =>
            {
                var stored = await context.FeeSettings
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (stored is null)
                {
                    stored = new FeeSetting { Id = FeeSettingsId };
                    context.FeeSettings.Add(stored);
                }

                stored.RegistrationFeeEnabled = settings.RegistrationFeeEnabled;
                stored.RegistrationFeeAmount = settings.RegistrationFeeAmount;
                stored.Discount3Months = settings.Discount3Months;
                stored.Discount6Months = settings.Discount6Months;
                stored.Discount12Months = settings.Discount12Months;
                stored.UpdatedAt = Clock.LocalNow;

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
    }
}
