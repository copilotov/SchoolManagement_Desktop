using SchoolManagement.Application.Academic;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Repositories;

/// <summary>
/// Academic structure repositories. Each list method takes a deliberate query
/// (search + include inactive + sort) and returns a projection so SQLite only
/// returns the columns the screen displays.
/// </summary>
public interface IAcademicLevelRepository : IGenericRepository<AcademicLevel>
{
    Task<Result<IReadOnlyList<AcademicLevelListItem>>> GetListAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<int>> CountGradesAsync(int levelId, CancellationToken cancellationToken = default);

    Task<Result<int>> CountGroupsAsync(int levelId, CancellationToken cancellationToken = default);
}

public interface IGradeRepository : IGenericRepository<Grade>
{
    Task<Result<IReadOnlyList<GradeListItem>>> GetListAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<int>>> GetSubjectIdsAsync(
        int gradeId,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> SetSubjectsAsync(
        int gradeId,
        IReadOnlyCollection<int> subjectIds,
        CancellationToken cancellationToken = default);

    Task<Result<int>> CountGroupsAsync(int gradeId, CancellationToken cancellationToken = default);
}

public interface ISubjectRepository : IGenericRepository<Subject>
{
    Task<Result<IReadOnlyList<SubjectListItem>>> GetListAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> CodeExistsAsync(
        string code,
        int? excludeId = null,
        CancellationToken cancellationToken = default);

    Task<Result<int>> CountGroupsAsync(int subjectId, CancellationToken cancellationToken = default);

    Task<Result<int>> CountGradesAsync(int subjectId, CancellationToken cancellationToken = default);
}

public interface ITeacherRepository : IGenericRepository<Teacher>
{
    Task<Result<IReadOnlyList<TeacherListItem>>> GetListAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<int>> CountGroupsAsync(int teacherId, CancellationToken cancellationToken = default);
}
