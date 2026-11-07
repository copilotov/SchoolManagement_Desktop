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
    /// in SQL and only the final subtraction happens in memory. A
    /// <paramref name="groupId"/> restricts the result to one group report.
    /// </summary>
    Task<Result<IReadOnlyList<MonthlyDue>>> GetMonthlyDuesAsync(
        string monthYear,
        int? groupId = null,
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

    /// <summary>Homeworks whose <c>target_session_id</c> is this session and still open.</summary>
    Task<Result<IReadOnlyList<HomeworkListItem>>> GetDueForSessionAsync(
        int sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Homework report list filtered by group and due-date range.</summary>
    Task<Result<IReadOnlyList<HomeworkListItem>>> GetListAsync(
        SchoolManagement.Application.Homeworks.HomeworkReportQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<HomeworkListItem?>> GetSummaryAsync(
        int homeworkId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Active students of the homework group left-joined with the submissions,
    /// so a student without a row is listed as pending instead of disappearing.
    /// </summary>
    Task<Result<IReadOnlyList<HomeworkSubmissionItem>>> GetSubmissionBoardAsync(
        int homeworkId,
        CancellationToken cancellationToken = default);
}
