using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public sealed class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("exams");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolId).HasColumnName("school_id");
        builder.Property(x => x.GroupId).HasColumnName("group_id");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.ExamDate).HasColumnName("exam_date");
        builder.Property(x => x.StartTime).HasColumnName("start_time");
        builder.Property(x => x.DurationMinutes).HasColumnName("duration_minutes");
        builder.Property(x => x.Type).HasColumnName("type");
        builder.Property(x => x.TotalMarks).HasColumnName("total_marks");
        builder.Property(x => x.IsExternal).HasColumnName("is_external");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

/// <summary>
/// The phone schema has a unique key on (exam_id, student_id) but no foreign
/// key. The navigation is mapped for convenient queries only; no constraint is
/// created because <see cref="DatabaseInitializer"/> owns the physical schema.
/// </summary>
public sealed class ExamResultConfiguration : IEntityTypeConfiguration<ExamResult>
{
    public void Configure(EntityTypeBuilder<ExamResult> builder)
    {
        builder.ToTable("exam_results");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolId).HasColumnName("school_id");
        builder.Property(x => x.ExamId).HasColumnName("exam_id");
        builder.Property(x => x.StudentId).HasColumnName("student_id");
        builder.Property(x => x.Score).HasColumnName("score");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.RecordedBy).HasColumnName("recorded_by");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.ExamId, x.StudentId }).IsUnique();
        builder.HasOne(x => x.Exam)
            .WithMany()
            .HasForeignKey(x => x.ExamId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.Title).HasColumnName("title");
        builder.Property(x => x.Body).HasColumnName("body");
        builder.Property(x => x.Type).HasColumnName("type");
        builder.Property(x => x.RecipientType).HasColumnName("recipient_type");
        builder.Property(x => x.RecipientId).HasColumnName("recipient_id");
        builder.Property(x => x.Channel).HasColumnName("channel");
        builder.Property(x => x.IsRead).HasColumnName("is_read");
        builder.Property(x => x.Metadata).HasColumnName("metadata");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolId).HasColumnName("school_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Action).HasColumnName("action");
        builder.Property(x => x.EntityType).HasColumnName("entity_type");
        builder.Property(x => x.EntityId).HasColumnName("entity_id");
        builder.Property(x => x.OldValues).HasColumnName("old_values");
        builder.Property(x => x.NewValues).HasColumnName("new_values");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    }
}

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("system_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.Key).HasColumnName("key");
        builder.Property(x => x.Value).HasColumnName("value");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.Key).IsUnique();
    }
}

public sealed class SmsLogConfiguration : IEntityTypeConfiguration<SmsLog>
{
    public void Configure(EntityTypeBuilder<SmsLog> builder)
    {
        builder.ToTable("sms_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.NotificationId).HasColumnName("notification_id");
        builder.Property(x => x.Phone).HasColumnName("phone");
        builder.Property(x => x.Body).HasColumnName("body");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message");
        builder.Property(x => x.RecipientId).HasColumnName("recipient_id");
        builder.Property(x => x.RecipientName).HasColumnName("recipient_name");
        builder.Property(x => x.EventCode).HasColumnName("event_code");
        builder.Property(x => x.SentAt).HasColumnName("sent_at");
    }
}

public sealed class HomeworkConfiguration : IEntityTypeConfiguration<Homework>
{
    public void Configure(EntityTypeBuilder<Homework> builder)
    {
        builder.ToTable("homeworks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SessionId).HasColumnName("session_id");
        builder.Property(x => x.TargetSessionId).HasColumnName("target_session_id");
        builder.Property(x => x.GroupId).HasColumnName("group_id");
        builder.Property(x => x.Title).HasColumnName("title");
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.DueDate).HasColumnName("due_date");
        builder.Property(x => x.IsCompleted).HasColumnName("is_completed");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

public sealed class HomeworkSubmissionConfiguration : IEntityTypeConfiguration<HomeworkSubmission>
{
    public void Configure(EntityTypeBuilder<HomeworkSubmission> builder)
    {
        builder.ToTable("homework_submissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.HomeworkId).HasColumnName("homework_id");
        builder.Property(x => x.StudentId).HasColumnName("student_id");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.HomeworkId, x.StudentId }).IsUnique();
        builder.HasOne(x => x.Homework)
            .WithMany()
            .HasForeignKey(x => x.HomeworkId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
