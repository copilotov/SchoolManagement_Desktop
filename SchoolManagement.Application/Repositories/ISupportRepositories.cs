using SchoolManagement.Application.Common;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Repositories;

public interface INotificationRepository : IGenericRepository<Notification>
{
    Task<Result<IReadOnlyList<NotificationListItem>>> GetRecentAsync(
        int take,
        CancellationToken cancellationToken = default);

    Task<Result<int>> CountUnreadAsync(CancellationToken cancellationToken = default);

    Task<Result<bool>> MarkReadAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The absence notification is only created when there is no unread one
    /// for the same type and student, exactly like the phone app.
    /// </summary>
    Task<Result<bool>> HasUnreadAsync(
        string type,
        int recipientId,
        CancellationToken cancellationToken = default);
}

public interface ISettingsRepository
{
    Task<Result<SystemSetting?>> GetSystemSettingAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> SetSystemSettingAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default);

    Task<Result<int>> GetAbsenceLimitAsync(CancellationToken cancellationToken = default);

    Task<Result<FeeSetting>> GetFeeSettingsAsync(CancellationToken cancellationToken = default);

    Task<Result<bool>> UpdateFeeSettingsAsync(
        FeeSetting settings,
        CancellationToken cancellationToken = default);
}

public interface IReferenceDataRepository
{
    Task<Result<IReadOnlyList<LookupItem>>> GetSubjectsAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LookupItem>>> GetTeachersAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LookupItem>>> GetGuardiansAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LookupItem>>> GetAcademicLevelsAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LookupItem>>> GetGradesAsync(
        int? academicLevelId,
        bool onlyActive,
        CancellationToken cancellationToken = default);
}
