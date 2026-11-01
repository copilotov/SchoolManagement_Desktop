using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("students");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolId).HasColumnName("school_id");
        builder.Property(x => x.GuardianId).HasColumnName("guardian_id");
        builder.Property(x => x.StudentCode).HasColumnName("student_code");
        builder.Property(x => x.FirstName).HasColumnName("first_name");
        builder.Property(x => x.LastName).HasColumnName("last_name");
        builder.Property(x => x.DateOfBirth).HasColumnName("date_of_birth");
        builder.Property(x => x.Gender).HasColumnName("gender");
        builder.Property(x => x.Address).HasColumnName("address");
        builder.Property(x => x.Phone).HasColumnName("phone");
        builder.Property(x => x.PhotoPath).HasColumnName("photo_path");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.RegistrationFee).HasColumnName("registration_fee");

        // The phone schema has no FK from students to guardians, only the id.
        builder.HasIndex(x => x.StudentCode).IsUnique();
    }
}

public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SubjectId).HasColumnName("subject_id");
        builder.Property(x => x.TeacherId).HasColumnName("teacher_id");
        builder.Property(x => x.AcademicLevelId).HasColumnName("academic_level_id");
        builder.Property(x => x.GradeId).HasColumnName("grade_id");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.MaxStudents).HasColumnName("max_students");
        builder.Property(x => x.MonthlyFee).HasColumnName("monthly_fee");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("enrollments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolId).HasColumnName("school_id");
        builder.Property(x => x.StudentId).HasColumnName("student_id");
        builder.Property(x => x.GroupId).HasColumnName("group_id");
        builder.Property(x => x.EnrollmentDate).HasColumnName("enrollment_date");
        builder.Property(x => x.RegistrationFee).HasColumnName("registration_fee");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
