using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Services;

/// <summary>
/// Creates the in-app absence notification after an attendance save.
///
/// It is a deliberate exception to the "writes go through a guarded use case"
/// rule: it runs as the post-save side effect of
/// <see cref="Attendance.SaveSessionAttendanceUseCase"/>, which has already run
/// <c>ILicenseGuard.EnsureCanWrite()</c> exactly once. Wrapping a second guarded
/// use case here would run the gate twice and let an internal step fail after
/// the roster was written. The repository is therefore called directly, and the
/// rule is documented in AGENTS.md.
///
/// Only one unread absence notification per student exists at a time: the same
/// duplicate check the phone app used.
/// </summary>
public sealed class AutoNotificationService
{
    private readonly IStudentRepository _students;
    private readonly INotificationRepository _notifications;
    private readonly ILocalizationService _localization;
    private readonly ITrustedClock _clock;

    public AutoNotificationService(
        IStudentRepository students,
        INotificationRepository notifications,
        ILocalizationService localization,
        ITrustedClock clock)
    {
        _students = students ?? throw new ArgumentNullException(nameof(students));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Evaluates the absence limit for each distinct student. Returns the count
    /// of notifications actually created. A repository failure stops the sweep
    /// and is returned to the caller, which treats it as best-effort.
    /// </summary>
    public async Task<Result<int>> CheckAttendanceLimitAsync(
        IReadOnlyCollection<int> studentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(studentIds);

        var created = 0;
        foreach (var studentId in studentIds.Distinct())
        {
            if (studentId <= 0)
            {
                continue;
            }

            var summary = await _students
                .GetAbsenceSummaryAsync(studentId, cancellationToken)
                .ConfigureAwait(false);

            if (summary.IsFailure)
            {
                return Result<int>.Fail(summary.Failure!);
            }

            if (!summary.Value.HasReachedLimit)
            {
                continue;
            }

            var hasUnread = await _notifications
                .HasUnreadAsync(NotificationTypes.Attendance, studentId, cancellationToken)
                .ConfigureAwait(false);

            if (hasUnread.IsFailure)
            {
                return Result<int>.Fail(hasUnread.Failure!);
            }

            if (hasUnread.Value)
            {
                continue;
            }

            var details = await _students
                .GetDetailsAsync(studentId, cancellationToken)
                .ConfigureAwait(false);

            if (details.IsFailure)
            {
                return Result<int>.Fail(details.Failure!);
            }

            var name = details.Value is null
                ? studentId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : details.Value.FirstName + " " + details.Value.LastName;

            var notification = new Notification
            {
                Title = _localization.Tr(
                    TranslationKeys.Notifications.AbsenceAlertTitle,
                    ("name", name)),
                Body = _localization.Tr(
                    TranslationKeys.Notifications.AbsenceBodyDetailed,
                    ("name", name),
                    ("absent", summary.Value.AbsenceCount.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                    ("threshold", summary.Value.Limit.ToString(
                        System.Globalization.CultureInfo.InvariantCulture))),
                Type = NotificationTypes.Attendance,
                RecipientType = RecipientTypes.Student,
                RecipientId = studentId,
                Channel = NotificationChannels.InApp,
                IsRead = false,
                CreatedAt = _clock.LocalNow,
            };

            var saved = await _notifications
                .AddAsync(notification, cancellationToken)
                .ConfigureAwait(false);

            if (saved.IsFailure)
            {
                return Result<int>.Fail(saved.Failure!);
            }

            created++;
        }

        return Result<int>.Success(created);
    }
}
