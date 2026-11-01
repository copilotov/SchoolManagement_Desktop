using SchoolManagement.Application.Common;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Repositories;

public interface IPaymentRepository : IGenericRepository<Payment>
{
    Task<Result<IReadOnlyList<PaymentListItem>>> GetByStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    Task<Result<double>> GetPaidTotalAsync(
        int studentId,
        string monthYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dues for one "YYYY-MM" month across active enrollments. The expected
    /// amount is the group monthly fee; payments and exemptions are aggregated
    /// in SQL and only the final subtraction happens in memory.
    /// </summary>
    Task<Result<IReadOnlyList<MonthlyDue>>> GetMonthlyDuesAsync(
        string monthYear,
        CancellationToken cancellationToken = default);

    Task<Result<MonthlyDue?>> GetStudentMonthlyDueAsync(
        int studentId,
        int groupId,
        string monthYear,
        CancellationToken cancellationToken = default);
}

public interface IHomeworkRepository : IGenericRepository<Homework>
{
    Task<Result<IReadOnlyList<HomeworkListItem>>> GetByGroupAsync(
        int groupId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<HomeworkSubmissionItem>>> GetSubmissionsAsync(
        int homeworkId,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> SetSubmissionStatusAsync(
        int homeworkId,
        int studentId,
        string status,
        DateTime? submittedAt,
        string? notes,
        CancellationToken cancellationToken = default);
}
