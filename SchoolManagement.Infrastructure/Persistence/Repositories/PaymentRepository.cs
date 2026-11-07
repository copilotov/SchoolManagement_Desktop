using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>
/// Finance reads. Monthly dues are built from active enrollments, the monthly
/// payments already recorded for the same "YYYY-MM" and the exemptions of that
/// month. Payments are summed in SQL; the final subtraction is delegated to
/// <see cref="MonthlyDuesCalculator"/> so the rule stays unit-tested.
/// </summary>
public sealed class PaymentRepository : GenericRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<PaymentRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<IReadOnlyList<PaymentListItem>>> GetByStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<PaymentListItem>>(
            "Payments.GetByStudent",
            async context => await context.Payments
                .AsNoTracking()
                .Where(payment => payment.StudentId == studentId)
                .OrderByDescending(payment => payment.PaymentDate)
                .ThenByDescending(payment => payment.Id)
                .Select(payment => new PaymentListItem(
                    payment.Id,
                    payment.StudentId,
                    payment.GroupId,
                    payment.SessionId,
                    payment.Type,
                    payment.MonthYear,
                    payment.Amount,
                    payment.OriginalAmount,
                    payment.RemainingAmount,
                    payment.PaymentDate,
                    payment.Method,
                    payment.ReceiptNumber,
                    payment.Notes))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<double>> GetPaidTotalAsync(
        int studentId,
        string monthYear,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(monthYear))
        {
            return Task.FromResult(
                Result<double>.Fail(
                    Failure.Validation(TranslationKeys.Errors.RequiredFields, nameof(monthYear))));
        }

        var month = monthYear.Trim();

        return RunAsync(
            "Payments.GetPaidTotal",
            async context => await context.Payments
                .AsNoTracking()
                .Where(payment => payment.StudentId == studentId
                    && payment.Type == PaymentTypes.Monthly
                    && payment.MonthYear == month)
                .SumAsync(payment => payment.Amount, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<MonthlyDue>>> GetMonthlyDuesAsync(
        string monthYear,
        int? groupId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(monthYear))
        {
            return Task.FromResult(
                Result<IReadOnlyList<MonthlyDue>>.Fail(
                    Failure.Validation(TranslationKeys.Errors.RequiredFields, nameof(monthYear))));
        }

        var month = monthYear.Trim();

        return RunAsync<IReadOnlyList<MonthlyDue>>(
            "Payments.GetMonthlyDues",
            async context =>
            {
                var enrollmentQuery = context.Enrollments.AsNoTracking()
                    .Where(enrollment => enrollment.Status == EnrollmentStatuses.Active);
                if (groupId is { } groupFilter)
                {
                    enrollmentQuery = enrollmentQuery.Where(enrollment => enrollment.GroupId == groupFilter);
                }

                var enrollments = await (
                        from enrollment in enrollmentQuery
                        join student in context.Students.AsNoTracking()
                            on enrollment.StudentId equals student.Id
                        join groupEntity in context.Groups.AsNoTracking()
                            on enrollment.GroupId equals groupEntity.Id
                        orderby student.LastName, student.FirstName, groupEntity.Name
                        select new
                        {
                            enrollment.StudentId,
                            student.StudentCode,
                            student.FirstName,
                            student.LastName,
                            enrollment.GroupId,
                            GroupName = groupEntity.Name,
                            groupEntity.MonthlyFee,
                        })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (enrollments.Count == 0)
                {
                    return Array.Empty<MonthlyDue>();
                }

                var paidByStudentGroup = await context.Payments
                    .AsNoTracking()
                    .Where(payment => payment.Type == PaymentTypes.Monthly && payment.MonthYear == month)
                    .GroupBy(payment => new { payment.StudentId, payment.GroupId })
                    .Select(group => new
                    {
                        group.Key.StudentId,
                        group.Key.GroupId,
                        Paid = group.Sum(payment => payment.Amount),
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var exemptionRows = await context.Exemptions
                    .AsNoTracking()
                    .Where(exemption => exemption.MonthYear == month && exemption.ExtraFeeId == null)
                    .Select(exemption => new { exemption.StudentId, exemption.Type, exemption.Amount })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var paidLookup = paidByStudentGroup
                    .GroupBy(row => (row.StudentId, row.GroupId))
                    .ToDictionary(group => group.Key, group => group.Sum(row => row.Paid));

                var exemptionLookup = exemptionRows
                    .GroupBy(row => row.StudentId)
                    .ToDictionary(
                        group => group.Key,
                        group => (
                            HasFull: group.Any(row => string.Equals(
                                row.Type,
                                ExemptionTypes.Full,
                                StringComparison.OrdinalIgnoreCase)),
                            Partial: group
                                .Where(row => string.Equals(
                                    row.Type,
                                    ExemptionTypes.Partial,
                                    StringComparison.OrdinalIgnoreCase))
                                .Sum(row => row.Amount)));

                var dues = new List<MonthlyDue>(enrollments.Count);

                foreach (var enrollment in enrollments)
                {
                    paidLookup.TryGetValue((enrollment.StudentId, (int?)enrollment.GroupId), out var paid);
                    exemptionLookup.TryGetValue(
                        enrollment.StudentId,
                        out var exemption);

                    var calculated = MonthlyDuesCalculator.Calculate(
                        enrollment.MonthlyFee,
                        paid,
                        exemption.Partial,
                        exemption.HasFull);

                    dues.Add(new MonthlyDue(
                        enrollment.StudentId,
                        enrollment.StudentCode,
                        $"{enrollment.FirstName} {enrollment.LastName}".Trim(),
                        enrollment.GroupId,
                        enrollment.GroupName,
                        month,
                        calculated.Expected,
                        calculated.Paid,
                        calculated.Exempted,
                        calculated.Outstanding));
                }

                return dues;
            },
            cancellationToken);
    }

    public async Task<Result<MonthlyDue?>> GetStudentMonthlyDueAsync(
        int studentId,
        int groupId,
        string monthYear,
        CancellationToken cancellationToken = default)
    {
        var all = await GetMonthlyDuesAsync(monthYear, null, cancellationToken).ConfigureAwait(false);

        if (all.IsFailure)
        {
            return Result<MonthlyDue?>.Fail(all.Failure!);
        }

        return Result<MonthlyDue?>.Success(
            all.Value.FirstOrDefault(due => due.StudentId == studentId && due.GroupId == groupId));
    }
}
