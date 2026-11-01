using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public sealed class ScheduleTemplateConfiguration : IEntityTypeConfiguration<ScheduleTemplate>
{
    public void Configure(EntityTypeBuilder<ScheduleTemplate> builder)
    {
        builder.ToTable("schedule_templates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.GroupId).HasColumnName("group_id");
        builder.Property(x => x.DayOfWeek).HasColumnName("day_of_week");
        builder.Property(x => x.StartTime).HasColumnName("start_time");
        builder.Property(x => x.EndTime).HasColumnName("end_time");
        builder.Property(x => x.Location).HasColumnName("location");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.GroupId).HasColumnName("group_id");
        builder.Property(x => x.TemplateId).HasColumnName("template_id");
        builder.Property(x => x.SessionDate).HasColumnName("session_date");
        builder.Property(x => x.StartTime).HasColumnName("start_time");
        builder.Property(x => x.EndTime).HasColumnName("end_time");
        builder.Property(x => x.Type).HasColumnName("type");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.LessonTopic).HasColumnName("lesson_topic");
        builder.Property(x => x.TeacherNotes).HasColumnName("teacher_notes");
        builder.Property(x => x.Homework).HasColumnName("homework");
        builder.Property(x => x.CancelReason).HasColumnName("cancel_reason");
        builder.Property(x => x.IsPaid).HasColumnName("is_paid");
        builder.Property(x => x.Price).HasColumnName("price");
        builder.Property(x => x.PaymentStatus).HasColumnName("payment_status");
        builder.Property(x => x.PaidAmount).HasColumnName("paid_amount");
        builder.Property(x => x.RemainingAmount).HasColumnName("remaining_amount");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("attendance");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SessionId).HasColumnName("session_id");
        builder.Property(x => x.StudentId).HasColumnName("student_id");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.CheckInTime).HasColumnName("check_in_time");
        builder.Property(x => x.RecordedBy).HasColumnName("recorded_by");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique();
    }
}
