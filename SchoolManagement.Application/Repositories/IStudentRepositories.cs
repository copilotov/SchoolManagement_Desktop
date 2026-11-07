using SchoolManagement.Application.Common;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Students;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Repositories;

public interface IStudentRepository : IGenericRepository<Student>
{
    Task<Result<IReadOnlyList<StudentListItem>>> SearchAsync(
        string? searchTerm,
        bool onlyActive,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Filtered, sorted and paged list. Every filter runs in SQL.</summary>
    Task<Result<IReadOnlyList<StudentListItem>>> GetListAsync(
        StudentListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Count for the same filters as <see cref="GetListAsync"/>.</summary>
    Task<Result<int>> CountAsync(
        StudentListQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<StudentDetails?>> GetDetailsAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<int>> CountAsync(bool onlyActive, CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive check excluding the edited row.</summary>
    Task<Result<bool>> CodeExistsAsync(
        string studentCode,
        int? excludeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates the student and its guardian in one transaction.
    /// A blank code is generated from the highest sequence of the current year
    /// inside that transaction, so two concurrent saves cannot collide.
    /// </summary>
    Task<Result<StudentSaveOutcome>> SaveAsync(
        StudentSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<StudentDependencies>> GetDependenciesAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<StudentExportRow>>> GetExportRowsAsync(
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<StudentCardData>>> GetCardDataAsync(
        IReadOnlyCollection<int> studentIds,
        CancellationToken cancellationToken = default);

    Task<Result<StudentAbsenceSummary>> GetAbsenceSummaryAsync(
        int studentId,
        CancellationToken cancellationToken = default);
}

public interface IGroupRepository : IGenericRepository<Group>
{
    Task<Result<IReadOnlyList<GroupSummary>>> GetSummariesAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default);

    /// <summary>Filtered and sorted group table; every filter runs in SQL.</summary>
    Task<Result<IReadOnlyList<GroupSummary>>> GetListAsync(
        SchoolManagement.Application.Groups.GroupListQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<GroupSummary?>> GetSummaryAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates the group. An update keeps the stored level when the
    /// request does not carry one, and the subject/teacher ids are always
    /// written so the list and the form stay in sync.
    /// </summary>
    Task<Result<SchoolManagement.Application.Groups.GroupSaveOutcome>> SaveAsync(
        SchoolManagement.Application.Groups.GroupSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<SchoolManagement.Application.Groups.GroupDependencies>> GetDependenciesAsync(
        int groupId,
        CancellationToken cancellationToken = default);

    Task<Result<int>> GetActiveEnrollmentCountAsync(int groupId, CancellationToken cancellationToken = default);
}

public interface IEnrollmentRepository : IGenericRepository<Enrollment>
{
    Task<Result<IReadOnlyList<EnrollmentListItem>>> GetByStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<EnrollmentListItem>>> GetByGroupAsync(
        int groupId,
        CancellationToken cancellationToken = default);

    Task<Result<int>> CountActiveByGroupAsync(int groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A student may hold at most one active enrollment per group. A previous
    /// withdrawn/inactive row does not count, so re-enrolling is allowed.
    /// </summary>
    Task<Result<bool>> ExistsActiveAsync(
        int groupId,
        int studentId,
        CancellationToken cancellationToken = default);

    Task<Result<SchoolManagement.Application.Groups.EnrollmentSaveOutcome>> EnrollAsync(
        SchoolManagement.Application.Groups.EnrollmentSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> SetStatusAsync(
        int enrollmentId,
        string status,
        CancellationToken cancellationToken = default);
}
