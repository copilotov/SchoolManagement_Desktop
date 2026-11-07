using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

public sealed class ScheduleTemplateRepository
    : GenericRepository<ScheduleTemplate>, IScheduleTemplateRepository
{
    // The phone schema stores a wall-clock DateTime. A fixed reference date
    // keeps the time-of-day stable while still round-tripping through the
    // Unix-seconds converter.
    private static readonly DateTime ReferenceDate = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public ScheduleTemplateRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<ScheduleTemplateRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<IReadOnlyList<ScheduleTemplate>>> GetByGroupAsync(
        int groupId,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<ScheduleTemplate>>(
            "ScheduleTemplates.GetByGroup",
            async context => await context.ScheduleTemplates
                .AsNoTracking()
                .Where(template => template.GroupId == groupId)
                .OrderBy(template => template.DayOfWeek)
                .ThenBy(template => template.StartTime)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<IReadOnlyList<ScheduleTemplate>>> GetActiveByWeekDayAsync(
        WeekDay day,
        CancellationToken cancellationToken = default)
    {
        var dayIndex = (int)day;

        return RunAsync<IReadOnlyList<ScheduleTemplate>>(
            "ScheduleTemplates.GetActiveByWeekDay",
            async context => await context.ScheduleTemplates
                .AsNoTracking()
                .Where(template => template.IsActive && template.DayOfWeek == dayIndex)
                .OrderBy(template => template.StartTime)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<ScheduleSlotItem>>> GetSlotsAsync(
        int? groupId,
        int? teacherId,
        bool includeInactive,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<ScheduleSlotItem>>(
            "ScheduleTemplates.GetSlots",
            async context =>
            {
                // Filter on the entity columns before the projection: EF cannot
                // translate a predicate over the projected record once the
                // projection contains correlated subqueries.
                var slots = context.ScheduleTemplates.AsNoTracking();

                if (groupId is { } groupFilter)
                {
                    slots = slots.Where(slot => slot.GroupId == groupFilter);
                }

                if (teacherId is { } teacherFilter)
                {
                    slots = slots.Where(slot => context.Groups.Any(
                        group => group.Id == slot.GroupId && group.TeacherId == teacherFilter));
                }

                if (!includeInactive)
                {
                    slots = slots.Where(slot => slot.IsActive);
                }

                return await Project(
                        context,
                        slots.OrderBy(slot => slot.DayOfWeek).ThenBy(slot => slot.StartTime))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);

    public Task<Result<ScheduleSlotItem?>> GetSlotAsync(
        int slotId,
        CancellationToken cancellationToken = default)
        => RunAsync<ScheduleSlotItem?>(
            "ScheduleTemplates.GetSlot",
            async context => await Project(
                    context,
                    context.ScheduleTemplates.AsNoTracking().Where(slot => slot.Id == slotId))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<IReadOnlyList<ScheduleSlotItem>>> GetConflictsAsync(
        int groupId,
        int? teacherId,
        int dayOfWeek,
        TimeSpan startTime,
        TimeSpan endTime,
        int? excludeSlotId = null,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<ScheduleSlotItem>>(
            "ScheduleTemplates.GetConflicts",
            async context =>
            {
                // Only the slots of that weekday are loaded. The overlap test is
                // a pure half-open interval rule on TimeSpan values, so SQLite
                // never has to translate date arithmetic.
                var sameDay = await Project(
                        context,
                        context.ScheduleTemplates
                            .AsNoTracking()
                            .Where(slot => slot.DayOfWeek == dayOfWeek && slot.IsActive))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                return sameDay
                    .Where(slot => excludeSlotId is null || slot.Id != excludeSlotId)
                    .Where(slot =>
                        slot.GroupId == groupId
                        || (teacherId is { } candidateTeacher && slot.TeacherId == candidateTeacher))
                    .Where(slot => ScheduleConflictPolicy.Overlaps(
                        startTime,
                        endTime,
                        slot.StartTime.TimeOfDay,
                        slot.EndTime.TimeOfDay))
                    .OrderBy(slot => slot.StartTime)
                    .ToList();
            },
            cancellationToken);

    public Task<Result<ScheduleSaveOutcome>> SaveSlotAsync(
        ScheduleSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RunResultAsync(
            "ScheduleTemplates.SaveSlot",
            async context =>
            {
                var group = await context.Groups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == request.GroupId, cancellationToken)
                    .ConfigureAwait(false);
                if (group is null)
                {
                    return Result<ScheduleSaveOutcome>.Fail(
                        Failure.NotFound(TranslationKeys.Errors.NotFound));
                }

                var now = Clock.LocalNow;
                ScheduleTemplate slot;
                var created = request.SlotId is null or <= 0;

                if (request.SlotId is { } slotId and > 0)
                {
                    var existing = await context.ScheduleTemplates
                        .FirstOrDefaultAsync(x => x.Id == slotId, cancellationToken)
                        .ConfigureAwait(false);
                    if (existing is null)
                    {
                        return Result<ScheduleSaveOutcome>.Fail(
                            Failure.NotFound(TranslationKeys.Errors.NotFound));
                    }

                    slot = existing;
                }
                else
                {
                    slot = new ScheduleTemplate
                    {
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    context.ScheduleTemplates.Add(slot);
                }

                slot.GroupId = request.GroupId;
                slot.DayOfWeek = request.DayOfWeek;
                slot.StartTime = ReferenceDate.Add(request.StartTime);
                slot.EndTime = ReferenceDate.Add(request.EndTime);
                slot.Location = request.Location;
                slot.IsActive = request.IsActive;
                slot.UpdatedAt = now;

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<ScheduleSaveOutcome>.Success(
                    new ScheduleSaveOutcome(slot.Id, created));
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
    }

    public Task<Result<bool>> DeleteSlotAsync(
        int slotId,
        CancellationToken cancellationToken = default)
        => RunResultAsync(
            "ScheduleTemplates.DeleteSlot",
            async context =>
            {
                var slot = await context.ScheduleTemplates
                    .FirstOrDefaultAsync(x => x.Id == slotId, cancellationToken)
                    .ConfigureAwait(false);
                if (slot is null)
                {
                    return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
                }

                context.ScheduleTemplates.Remove(slot);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);

    private static IQueryable<ScheduleSlotItem> Project(
        SchoolManagementDbContext context,
        IQueryable<ScheduleTemplate> slots)
        => slots.Select(slot => new ScheduleSlotItem(
            slot.Id,
            slot.GroupId,
            context.Groups
                .Where(group => group.Id == slot.GroupId)
                .Select(group => group.Name)
                .FirstOrDefault() ?? string.Empty,
            context.Groups
                .Where(group => group.Id == slot.GroupId)
                .Select(group => group.TeacherId)
                .FirstOrDefault(),
            context.Groups
                .Where(group => group.Id == slot.GroupId)
                .Select(group => context.Teachers
                    .Where(teacher => teacher.Id == group.TeacherId)
                    .Select(teacher => teacher.FullName)
                    .FirstOrDefault())
                .FirstOrDefault(),
            slot.DayOfWeek,
            slot.StartTime,
            slot.EndTime,
            slot.Location,
            slot.IsActive));
}
