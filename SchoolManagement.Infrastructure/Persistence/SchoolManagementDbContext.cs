using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Infrastructure.Persistence.Converters;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core context mapped to the original Drift schema. The context never
/// creates or migrates tables: <see cref="DatabaseInitializer"/> runs the
/// original DDL so the file stays usable by the phone app.
/// </summary>
public sealed class SchoolManagementDbContext : DbContext
{
    public SchoolManagementDbContext(DbContextOptions<SchoolManagementDbContext> options)
        : base(options)
    {
    }

    public DbSet<School> Schools => Set<School>();

    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();

    public DbSet<AcademicLevel> AcademicLevels => Set<AcademicLevel>();

    public DbSet<Grade> Grades => Set<Grade>();

    public DbSet<GradeSubject> GradeSubjects => Set<GradeSubject>();

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<Teacher> Teachers => Set<Teacher>();

    public DbSet<Guardian> Guardians => Set<Guardian>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    public DbSet<ScheduleTemplate> ScheduleTemplates => Set<ScheduleTemplate>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<AttendanceRecord> Attendance => Set<AttendanceRecord>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Discount> Discounts => Set<Discount>();

    public DbSet<Exemption> Exemptions => Set<Exemption>();

    public DbSet<Exam> Exams => Set<Exam>();

    public DbSet<ExamResult> ExamResults => Set<ExamResult>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<ExtraFee> ExtraFees => Set<ExtraFee>();

    public DbSet<FeeSetting> FeeSettings => Set<FeeSetting>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<SmsLog> SmsLogs => Set<SmsLog>();

    public DbSet<Homework> Homeworks => Set<Homework>();

    public DbSet<HomeworkSubmission> HomeworkSubmissions => Set<HomeworkSubmission>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // The phone schema has no index on foreign keys. Keeping EF's convention
        // would create indexes that do not exist in the original file.
        configurationBuilder.Conventions.Remove<ForeignKeyIndexConvention>();

        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UnixSecondsDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>()
            .HaveConversion<NullableUnixSecondsDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchoolManagementDbContext).Assembly);
    }
}
