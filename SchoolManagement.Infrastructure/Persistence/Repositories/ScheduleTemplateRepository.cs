using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

public sealed class ScheduleTemplateRepository
    : GenericRepository<ScheduleTemplate>, IScheduleTemplateRepository
{
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
}
