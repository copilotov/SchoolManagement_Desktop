using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.Time;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

public sealed class StudentRepository : RepositoryBase, IStudentRepository
{
    /// <summary>
    /// Serializes student-code generation inside this process. The unique
    /// SQLite index on students.student_code remains the final safety net.
    /// </summary>
    private static readonly SemaphoreSlim CodeGate = new(1, 1);

    public StudentRepository(
        IDbContextFactory<SchoolManagementDbContext> contextFactory,
        ITrustedClock clock,
        ILogger<StudentRepository> logger)
        : base(contextFactory, clock, logger)
    {
    }

    public Task<Result<Student?>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => RunAsync<Student?>(
            "Students.GetById",
            async context => await context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(student => student.Id == id, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<int>> AddAsync(Student entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return RunAsync(
            "Students.Add",
            async context =>
            {
                RepositoryHelper.ApplyCreatedTimestamps(entity, Clock.LocalNow);
                context.Students.Add(entity);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return entity.Id;
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
    }

    public Task<Result<bool>> UpdateAsync(Student entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return RunResultAsync(
            "Students.Update",
            async context =>
            {
                RepositoryHelper.ApplyUpdatedTimestamp(entity, Clock.LocalNow);
                context.Entry(entity).State = EntityState.Modified;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
    }

    public Task<Result<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default)
        => RunResultAsync(
            "Students.Delete",
            async context =>
            {
                var student = await context.Students
                    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    .ConfigureAwait(false);

                if (student is null)
                {
                    return Result<bool>.Fail(Failure.NotFound(TranslationKeys.Errors.NotFound));
                }

                context.Students.Remove(student);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result<bool>.Success(true);
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);

    public Task<Result<IReadOnlyList<StudentListItem>>> SearchAsync(
        string? searchTerm,
        bool onlyActive,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
        => GetListAsync(
            new StudentListQuery(
                searchTerm,
                onlyActive ? StudentStatuses.Active : null,
                Skip: skip,
                Take: take),
            cancellationToken);

    public Task<Result<IReadOnlyList<StudentListItem>>> GetListAsync(
        StudentListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var pageSize = Math.Clamp(query.Take, 0, 500);
        var offset = Math.Max(0, query.Skip);

        return RunAsync<IReadOnlyList<StudentListItem>>(
            "Students.GetList",
            async context =>
            {
                var students = ApplyFilters(context.Students.AsNoTracking(), context, query);

                students = query.SortBy switch
                {
                    StudentSortField.Code => query.Descending
                        ? students.OrderByDescending(student => student.StudentCode)
                        : students.OrderBy(student => student.StudentCode),
                    StudentSortField.Status => query.Descending
                        ? students.OrderByDescending(student => student.Status).ThenByDescending(student => student.LastName)
                        : students.OrderBy(student => student.Status).ThenBy(student => student.LastName),
                    StudentSortField.Level => query.Descending
                        ? students.OrderByDescending(student => context.Groups
                            .Where(groupRow => groupRow.Id == context.Enrollments
                                .Where(enrollment => enrollment.StudentId == student.Id
                                    && enrollment.Status == EnrollmentStatuses.Active)
                                .OrderBy(enrollment => enrollment.GroupId)
                                .Select(enrollment => enrollment.GroupId)
                                .FirstOrDefault())
                            .Select(groupRow => context.AcademicLevels
                                .Where(level => level.Id == groupRow.AcademicLevelId)
                                .Select(level => level.Name)
                                .FirstOrDefault())
                            .FirstOrDefault())
                        : students.OrderBy(student => context.Groups
                            .Where(groupRow => groupRow.Id == context.Enrollments
                                .Where(enrollment => enrollment.StudentId == student.Id
                                    && enrollment.Status == EnrollmentStatuses.Active)
                                .OrderBy(enrollment => enrollment.GroupId)
                                .Select(enrollment => enrollment.GroupId)
                                .FirstOrDefault())
                            .Select(groupRow => context.AcademicLevels
                                .Where(level => level.Id == groupRow.AcademicLevelId)
                                .Select(level => level.Name)
                                .FirstOrDefault())
                            .FirstOrDefault()),
                    StudentSortField.Group => query.Descending
                        ? students.OrderByDescending(student => context.Groups
                            .Where(groupRow => groupRow.Id == context.Enrollments
                                .Where(enrollment => enrollment.StudentId == student.Id
                                    && enrollment.Status == EnrollmentStatuses.Active)
                                .OrderBy(enrollment => enrollment.GroupId)
                                .Select(enrollment => enrollment.GroupId)
                                .FirstOrDefault())
                            .Select(groupRow => groupRow.Name)
                            .FirstOrDefault())
                        : students.OrderBy(student => context.Groups
                            .Where(groupRow => groupRow.Id == context.Enrollments
                                .Where(enrollment => enrollment.StudentId == student.Id
                                    && enrollment.Status == EnrollmentStatuses.Active)
                                .OrderBy(enrollment => enrollment.GroupId)
                                .Select(enrollment => enrollment.GroupId)
                                .FirstOrDefault())
                            .Select(groupRow => groupRow.Name)
                            .FirstOrDefault()),
                    _ => query.Descending
                        ? students.OrderByDescending(student => student.LastName).ThenByDescending(student => student.FirstName)
                        : students.OrderBy(student => student.LastName).ThenBy(student => student.FirstName),
                };

                return await students
                    .Skip(offset)
                    .Take(pageSize)
                    .Select(student => new StudentListItem(
                        student.Id,
                        student.StudentCode,
                        student.FirstName,
                        student.LastName,
                        student.Phone,
                        student.Status,
                        context.Guardians
                            .Where(guardian => guardian.Id == student.GuardianId)
                            .Select(guardian => guardian.FullName)
                            .FirstOrDefault(),
                        student.GuardianId,
                        context.Groups
                            .Where(groupRow => groupRow.Id == context.Enrollments
                                .Where(enrollment => enrollment.StudentId == student.Id
                                    && enrollment.Status == EnrollmentStatuses.Active)
                                .OrderBy(enrollment => enrollment.GroupId)
                                .Select(enrollment => enrollment.GroupId)
                                .FirstOrDefault())
                            .Select(groupRow => context.AcademicLevels
                                .Where(level => level.Id == groupRow.AcademicLevelId)
                                .Select(level => level.Name)
                                .FirstOrDefault())
                            .FirstOrDefault(),
                        context.Groups
                            .Where(groupRow => groupRow.Id == context.Enrollments
                                .Where(enrollment => enrollment.StudentId == student.Id
                                    && enrollment.Status == EnrollmentStatuses.Active)
                                .OrderBy(enrollment => enrollment.GroupId)
                                .Select(enrollment => enrollment.GroupId)
                                .FirstOrDefault())
                            .Select(groupRow => context.Grades
                                .Where(grade => grade.Id == groupRow.GradeId)
                                .Select(grade => grade.Name)
                                .FirstOrDefault())
                            .FirstOrDefault(),
                        context.Groups
                            .Where(groupRow => groupRow.Id == context.Enrollments
                                .Where(enrollment => enrollment.StudentId == student.Id
                                    && enrollment.Status == EnrollmentStatuses.Active)
                                .OrderBy(enrollment => enrollment.GroupId)
                                .Select(enrollment => enrollment.GroupId)
                                .FirstOrDefault())
                            .Select(groupRow => groupRow.Name)
                            .FirstOrDefault(),
                        student.RegistrationFee))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<Result<int>> CountAsync(
        StudentListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return RunAsync(
            "Students.CountQuery",
            async context => await ApplyFilters(context.Students.AsNoTracking(), context, query)
                .CountAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
    }

    public Task<Result<StudentDetails?>> GetDetailsAsync(
        int id,
        CancellationToken cancellationToken = default)
        => RunAsync<StudentDetails?>(
            "Students.GetDetails",
            async context => await context.Students
                .AsNoTracking()
                .Where(student => student.Id == id)
                .Select(student => new StudentDetails(
                    student.Id,
                    student.StudentCode,
                    student.FirstName,
                    student.LastName,
                    student.DateOfBirth,
                    student.Gender,
                    student.Address,
                    student.Phone,
                    student.PhotoPath,
                    student.Status,
                    student.Notes,
                    student.RegistrationFee,
                    student.GuardianId,
                    context.Guardians
                        .Where(guardian => guardian.Id == student.GuardianId)
                        .Select(guardian => guardian.FullName)
                        .FirstOrDefault(),
                    context.Guardians
                        .Where(guardian => guardian.Id == student.GuardianId)
                        .Select(guardian => guardian.Phone)
                        .FirstOrDefault(),
                    context.Guardians
                        .Where(guardian => guardian.Id == student.GuardianId)
                        .Select(guardian => guardian.Email)
                        .FirstOrDefault(),
                    context.Guardians
                        .Where(guardian => guardian.Id == student.GuardianId)
                        .Select(guardian => guardian.Relationship)
                        .FirstOrDefault()))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Result<int>> CountAsync(bool onlyActive, CancellationToken cancellationToken = default)
        => RunAsync(
            "Students.Count",
            async context =>
            {
                var query = context.Students.AsNoTracking();
                if (onlyActive)
                {
                    query = query.Where(student => student.Status == StudentStatuses.Active);
                }

                return await query.CountAsync(cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    public Task<Result<bool>> CodeExistsAsync(
        string studentCode,
        int? excludeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studentCode))
        {
            return Task.FromResult(Result<bool>.Success(false));
        }

        var code = studentCode.Trim();
        var lowered = code.ToLowerInvariant();

        return RunAsync(
            "Students.CodeExists",
            async context =>
            {
                var query = context.Students.AsNoTracking().Where(
                    student => student.StudentCode == code
                        || student.StudentCode.ToLower() == lowered);

                if (excludeId is not null)
                {
                    var excluded = excludeId.Value;
                    query = query.Where(student => student.Id != excluded);
                }

                return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<Result<StudentSaveOutcome>> SaveAsync(
        StudentSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RunResultAsync(
            "Students.Save",
            async context =>
            {
                await using var transaction = await context.Database
                    .BeginTransactionAsync(cancellationToken)
                    .ConfigureAwait(false);

                await CodeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var now = Clock.LocalNow;
                    var guardianId = await ResolveGuardianAsync(context, request, now, cancellationToken)
                        .ConfigureAwait(false);

                    Student student;
                    var created = request.StudentId is null or <= 0;

                    if (created)
                    {
                        student = new Student();
                        RepositoryHelper.ApplyCreatedTimestamps(student, now);
                        context.Students.Add(student);
                    }
                    else
                    {
                        var existing = await context.Students
                            .FirstOrDefaultAsync(x => x.Id == request.StudentId!.Value, cancellationToken)
                            .ConfigureAwait(false);

                        if (existing is null)
                        {
                            return Result<StudentSaveOutcome>.Fail(
                                Failure.NotFound(TranslationKeys.Errors.NotFound));
                        }

                        student = existing;
                    }

                    student.SchoolId = student.SchoolId <= 0 ? 1 : student.SchoolId;
                    student.GuardianId = guardianId;
                    student.FirstName = request.FirstName.Trim();
                    student.LastName = request.LastName.Trim();
                    student.DateOfBirth = request.DateOfBirth;
                    student.Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim();
                    student.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
                    student.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
                    student.Status = string.IsNullOrWhiteSpace(request.Status)
                        ? StudentStatuses.Active
                        : request.Status.Trim();
                    student.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
                    student.RegistrationFee = request.RegistrationFee;

                    var suppliedCode = request.StudentCode?.Trim();
                    if (!string.IsNullOrEmpty(suppliedCode))
                    {
                        student.StudentCode = suppliedCode;
                    }
                    else if (string.IsNullOrWhiteSpace(student.StudentCode))
                    {
                        student.StudentCode = await GenerateCodeAsync(context, cancellationToken).ConfigureAwait(false);
                    }

                    RepositoryHelper.ApplyUpdatedTimestamp(student, now);
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                    return Result<StudentSaveOutcome>.Success(
                        new StudentSaveOutcome(student.Id, created, student.StudentCode));
                }
                finally
                {
                    CodeGate.Release();
                }
            },
            cancellationToken,
            TranslationKeys.Errors.SaveError);
    }

    public Task<Result<StudentDependencies>> GetDependenciesAsync(
        int studentId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "Students.GetDependencies",
            async context =>
            {
                var enrollments = await context.Enrollments
                    .AsNoTracking()
                    .CountAsync(x => x.StudentId == studentId, cancellationToken)
                    .ConfigureAwait(false);
                var payments = await context.Payments
                    .AsNoTracking()
                    .CountAsync(x => x.StudentId == studentId, cancellationToken)
                    .ConfigureAwait(false);
                var attendance = await context.Attendance
                    .AsNoTracking()
                    .CountAsync(x => x.StudentId == studentId, cancellationToken)
                    .ConfigureAwait(false);

                return new StudentDependencies(enrollments, payments, attendance);
            },
            cancellationToken);

    public Task<Result<IReadOnlyList<StudentExportRow>>> GetExportRowsAsync(
        CancellationToken cancellationToken = default)
        => RunAsync<IReadOnlyList<StudentExportRow>>(
            "Students.GetExportRows",
            async context =>
            {
                var rows = await context.Students
                    .AsNoTracking()
                    .OrderBy(student => student.LastName)
                    .ThenBy(student => student.FirstName)
                    .Select(student => new
                    {
                        student.Id,
                        student.StudentCode,
                        student.FirstName,
                        student.LastName,
                        student.DateOfBirth,
                        student.Gender,
                        student.Phone,
                        student.Address,
                        student.Status,
                        student.Notes,
                        student.RegistrationFee,
                        GuardianName = context.Guardians
                            .Where(guardian => guardian.Id == student.GuardianId)
                            .Select(guardian => guardian.FullName)
                            .FirstOrDefault(),
                        GuardianPhone = context.Guardians
                            .Where(guardian => guardian.Id == student.GuardianId)
                            .Select(guardian => guardian.Phone)
                            .FirstOrDefault(),
                        GuardianEmail = context.Guardians
                            .Where(guardian => guardian.Id == student.GuardianId)
                            .Select(guardian => guardian.Email)
                            .FirstOrDefault(),
                        GuardianRelationship = context.Guardians
                            .Where(guardian => guardian.Id == student.GuardianId)
                            .Select(guardian => guardian.Relationship)
                            .FirstOrDefault(),
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var placements = await LoadPlacementsAsync(
                        context,
                        rows.Select(row => row.Id).ToList(),
                        cancellationToken)
                    .ConfigureAwait(false);

                return rows
                    .Select(row => new StudentExportRow(
                        row.Id,
                        row.StudentCode,
                        row.FirstName,
                        row.LastName,
                        row.DateOfBirth,
                        row.Gender,
                        row.Phone,
                        row.Address,
                        row.Status,
                        row.Notes,
                        row.RegistrationFee,
                        row.GuardianName,
                        row.GuardianPhone,
                        row.GuardianEmail,
                        row.GuardianRelationship,
                        placements.TryGetValue(row.Id, out var placement) ? placement.LevelName : null,
                        placement?.GradeName,
                        placement?.GroupName))
                    .ToList();
            },
            cancellationToken);

    public Task<Result<IReadOnlyList<StudentCardData>>> GetCardDataAsync(
        IReadOnlyCollection<int> studentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(studentIds);

        // A List<int>, not an int[]: under C# 14 the compiler binds
        // array.Contains(value) to MemoryExtensions.Contains(ReadOnlySpan<int>, int),
        // and EF Core's expression interpreter cannot compile a ref-struct return
        // type, so the query fails at parameter-extraction time. List<int>.Contains
        // stays an ordinary instance method and still translates to SQL IN (...).
        var ids = studentIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return Task.FromResult(Result<IReadOnlyList<StudentCardData>>.Success(
                Array.Empty<StudentCardData>()));
        }

        return RunAsync<IReadOnlyList<StudentCardData>>(
            "Students.GetCardData",
            async context =>
            {
                var schoolName = await context.Schools
                    .AsNoTracking()
                    .OrderBy(school => school.Id)
                    .Select(school => school.Name)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false) ?? string.Empty;

                var academicYear = await context.AcademicYears
                    .AsNoTracking()
                    .OrderByDescending(year => year.IsCurrent)
                    .ThenByDescending(year => year.StartDate)
                    .Select(year => year.Name)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                var students = await context.Students
                    .AsNoTracking()
                    .Where(student => ids.Contains(student.Id))
                    .OrderBy(student => student.LastName)
                    .ThenBy(student => student.FirstName)
                    .Select(student => new
                    {
                        student.Id,
                        student.StudentCode,
                        student.FirstName,
                        student.LastName,
                        GuardianName = context.Guardians
                            .Where(guardian => guardian.Id == student.GuardianId)
                            .Select(guardian => guardian.FullName)
                            .FirstOrDefault(),
                        GuardianPhone = context.Guardians
                            .Where(guardian => guardian.Id == student.GuardianId)
                            .Select(guardian => guardian.Phone)
                            .FirstOrDefault(),
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var placements = await LoadPlacementsAsync(context, ids, cancellationToken)
                    .ConfigureAwait(false);

                return students
                    .Select(student =>
                    {
                        placements.TryGetValue(student.Id, out var placement);
                        return new StudentCardData(
                            student.Id,
                            student.StudentCode,
                            student.FirstName + " " + student.LastName,
                            placement?.LevelName,
                            placement?.GradeName,
                            placement?.GroupName,
                            student.GuardianName,
                            student.GuardianPhone,
                            schoolName,
                            academicYear);
                    })
                    .ToList();
            },
            cancellationToken);
    }

    public Task<Result<StudentAbsenceSummary>> GetAbsenceSummaryAsync(
        int studentId,
        CancellationToken cancellationToken = default)
        => RunAsync(
            "Students.GetAbsenceSummary",
            async context =>
            {
                var limit = await ReadAbsenceLimitAsync(context, cancellationToken).ConfigureAwait(false);
                var absenceCount = await context.Attendance
                    .AsNoTracking()
                    .CountAsync(
                        record => record.StudentId == studentId
                            && (record.Status == AttendanceStatuses.Absent
                                || record.Status == AttendanceStatuses.Unexcused),
                        cancellationToken)
                    .ConfigureAwait(false);

                var status = AbsenceLimitPolicy.Evaluate(absenceCount, limit);
                return new StudentAbsenceSummary(
                    studentId,
                    absenceCount,
                    limit,
                    status.HasReachedLimit,
                    status.IsExceeded);
            },
            cancellationToken);

    private static IQueryable<Student> ApplyFilters(
        IQueryable<Student> query,
        SchoolManagementDbContext context,
        StudentListQuery filters)
    {
        var term = string.IsNullOrWhiteSpace(filters.SearchTerm) ? null : filters.SearchTerm.Trim();
        if (term is not null)
        {
            var pattern = $"%{term}%";
            query = query.Where(student =>
                EF.Functions.Like(student.FirstName, pattern)
                || EF.Functions.Like(student.LastName, pattern)
                || EF.Functions.Like(student.StudentCode, pattern)
                || EF.Functions.Like(student.FirstName + " " + student.LastName, pattern)
                || (student.Phone != null && EF.Functions.Like(student.Phone, pattern))
                || context.Guardians.Any(guardian =>
                    guardian.Id == student.GuardianId && EF.Functions.Like(guardian.FullName, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filters.Status))
        {
            var status = filters.Status.Trim();
            query = query.Where(student => student.Status == status);
        }

        if (filters.AcademicLevelId is > 0)
        {
            var levelId = filters.AcademicLevelId.Value;
            query = query.Where(student => context.Enrollments.Any(enrollment =>
                enrollment.StudentId == student.Id
                && enrollment.Status == EnrollmentStatuses.Active
                && context.Groups.Any(group =>
                    group.Id == enrollment.GroupId && group.AcademicLevelId == levelId)));
        }

        if (filters.GradeId is > 0)
        {
            var gradeId = filters.GradeId.Value;
            query = query.Where(student => context.Enrollments.Any(enrollment =>
                enrollment.StudentId == student.Id
                && enrollment.Status == EnrollmentStatuses.Active
                && context.Groups.Any(group =>
                    group.Id == enrollment.GroupId && group.GradeId == gradeId)));
        }

        if (filters.GroupId is > 0)
        {
            var groupId = filters.GroupId.Value;
            query = query.Where(student => context.Enrollments.Any(enrollment =>
                enrollment.StudentId == student.Id
                && enrollment.Status == EnrollmentStatuses.Active
                && enrollment.GroupId == groupId));
        }

        return query;
    }

    /// <summary>
    /// Resolves the active group, level and grade names for a set of students.
    /// Runs as a second, translation-safe query instead of a correlated join so
    /// SQLite never needs the APPLY operator.
    /// </summary>
    private static async Task<Dictionary<int, StudentPlacement>> LoadPlacementsAsync(
        SchoolManagementDbContext context,
        List<int> studentIds,
        CancellationToken cancellationToken)
    {
        if (studentIds.Count == 0)
        {
            return new Dictionary<int, StudentPlacement>();
        }

        var rows = await (
                from enrollment in context.Enrollments.AsNoTracking()
                where studentIds.Contains(enrollment.StudentId)
                    && enrollment.Status == EnrollmentStatuses.Active
                join groupRow in context.Groups.AsNoTracking() on enrollment.GroupId equals groupRow.Id
                select new
                {
                    enrollment.StudentId,
                    enrollment.GroupId,
                    GroupName = groupRow.Name,
                    LevelName = context.AcademicLevels
                        .Where(level => level.Id == groupRow.AcademicLevelId)
                        .Select(level => level.Name)
                        .FirstOrDefault(),
                    GradeName = context.Grades
                        .Where(grade => grade.Id == groupRow.GradeId)
                        .Select(grade => grade.Name)
                        .FirstOrDefault(),
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(row => row.StudentId)
            .ToDictionary(
                groupRows => groupRows.Key,
                groupRows =>
                {
                    var primary = groupRows.OrderBy(row => row.GroupId).First();
                    return new StudentPlacement(primary.LevelName, primary.GradeName, primary.GroupName);
                });
    }

    private sealed record StudentPlacement(string? LevelName, string? GradeName, string? GroupName);

    private async Task<int?> ResolveGuardianAsync(
        SchoolManagementDbContext context,
        StudentSaveRequest request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var name = request.GuardianName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        Guardian? guardian = null;
        if (request.GuardianId is > 0)
        {
            guardian = await context.Guardians
                .FirstOrDefaultAsync(x => x.Id == request.GuardianId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (guardian is null)
        {
            guardian = new Guardian();
            RepositoryHelper.ApplyCreatedTimestamps(guardian, now);
            context.Guardians.Add(guardian);
        }
        else
        {
            RepositoryHelper.ApplyUpdatedTimestamp(guardian, now);
        }

        guardian.SchoolId = guardian.SchoolId <= 0 ? 1 : guardian.SchoolId;
        guardian.FullName = name;
        guardian.Phone = string.IsNullOrWhiteSpace(request.GuardianPhone) ? null : request.GuardianPhone.Trim();
        guardian.Email = string.IsNullOrWhiteSpace(request.GuardianEmail) ? null : request.GuardianEmail.Trim();
        guardian.Relationship = string.IsNullOrWhiteSpace(request.GuardianRelationship)
            ? null
            : request.GuardianRelationship.Trim();
        guardian.IsActive = true;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return guardian.Id;
    }

    private async Task<string> GenerateCodeAsync(
        SchoolManagementDbContext context,
        CancellationToken cancellationToken)
    {
        var year = Clock.LocalNow.Year;
        var prefix = $"{StudentCodeGenerator.Prefix}-{year:D4}-";
        var codes = await context.Students
            .AsNoTracking()
            .Where(student => student.StudentCode.StartsWith(prefix))
            .Select(student => student.StudentCode)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sequence = StudentCodeGenerator.NextSequence(codes, year);
        return StudentCodeGenerator.Build(year, sequence);
    }

    private static async Task<int> ReadAbsenceLimitAsync(
        SchoolManagementDbContext context,
        CancellationToken cancellationToken)
    {
        var storedValue = await context.SystemSettings
            .AsNoTracking()
            .Where(setting => setting.Key == AbsenceLimitPolicy.SettingKey)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return AbsenceLimitPolicy.ParseLimit(storedValue);
    }
}
