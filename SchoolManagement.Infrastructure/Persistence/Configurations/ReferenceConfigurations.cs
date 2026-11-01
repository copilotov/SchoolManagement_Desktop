using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public sealed class SchoolConfiguration : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> builder)
    {
        builder.ToTable("schools");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.Address).HasColumnName("address");
        builder.Property(x => x.Phone).HasColumnName("phone");
        builder.Property(x => x.Email).HasColumnName("email");
        builder.Property(x => x.LogoPath).HasColumnName("logo_path");
        builder.Property(x => x.DefaultLanguage).HasColumnName("default_language");
        builder.Property(x => x.Timezone).HasColumnName("timezone");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

public sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable("academic_years");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolId).HasColumnName("school_id");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.StartDate).HasColumnName("start_date");
        builder.Property(x => x.EndDate).HasColumnName("end_date");
        builder.Property(x => x.IsCurrent).HasColumnName("is_current");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(x => new { x.SchoolId, x.Name }).IsUnique();
    }
}

public sealed class AcademicLevelConfiguration : IEntityTypeConfiguration<AcademicLevel>
{
    public void Configure(EntityTypeBuilder<AcademicLevel> builder)
    {
        builder.ToTable("academic_levels");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.OrderIndex).HasColumnName("order_index");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

public sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("subjects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolId).HasColumnName("school_id");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.Code).HasColumnName("code");
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

public sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable("teachers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolId).HasColumnName("school_id");
        builder.Property(x => x.FullName).HasColumnName("full_name");
        builder.Property(x => x.Phone).HasColumnName("phone");
        builder.Property(x => x.Email).HasColumnName("email");
        builder.Property(x => x.Address).HasColumnName("address");
        builder.Property(x => x.PhotoPath).HasColumnName("photo_path");
        builder.Property(x => x.Specialization).HasColumnName("specialization");
        builder.Property(x => x.HireDate).HasColumnName("hire_date");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

public sealed class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> builder)
    {
        builder.ToTable("guardians");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolId).HasColumnName("school_id");
        builder.Property(x => x.FullName).HasColumnName("full_name");
        builder.Property(x => x.Phone).HasColumnName("phone");
        builder.Property(x => x.Email).HasColumnName("email");
        builder.Property(x => x.Relationship).HasColumnName("relationship");
        builder.Property(x => x.Address).HasColumnName("address");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

/// <summary>
/// The original schema has exactly one foreign key outside the join table:
/// <c>grades.academic_level_id</c>. No index was created for it, hence the
/// removal of EF's <c>ForeignKeyIndexConvention</c> in the context.
/// </summary>
public sealed class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.ToTable("grades");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.AcademicLevelId).HasColumnName("academic_level_id");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.OrderIndex).HasColumnName("order_index");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(x => x.AcademicLevel)
            .WithMany()
            .HasForeignKey(x => x.AcademicLevelId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class GradeSubjectConfiguration : IEntityTypeConfiguration<GradeSubject>
{
    public void Configure(EntityTypeBuilder<GradeSubject> builder)
    {
        builder.ToTable("grade_subjects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.GradeId).HasColumnName("grade_id");
        builder.Property(x => x.SubjectId).HasColumnName("subject_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(x => new { x.GradeId, x.SubjectId }).IsUnique();

        builder.HasOne(x => x.Grade)
            .WithMany()
            .HasForeignKey(x => x.GradeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Subject)
            .WithMany()
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
