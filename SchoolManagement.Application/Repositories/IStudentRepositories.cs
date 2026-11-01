using SchoolManagement.Application.Common;
using SchoolManagement.Application.Repositories.Models;
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

    Task<Result<StudentDetails?>> GetDetailsAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<int>> CountAsync(bool onlyActive, CancellationToken cancellationToken = default);

    Task<Result<StudentAbsenceSummary>> GetAbsenceSummaryAsync(
        int studentId,
        CancellationToken cancellationToken = default);
}

public interface IGroupRepository : IGenericRepository<Group>
{
    Task<Result<IReadOnlyList<GroupSummary>>> GetSummariesAsync(
        bool includeInactive,
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

    Task<Result<bool>> SetStatusAsync(
        int enrollmentId,
        string status,
        CancellationToken cancellationToken = default);
}
