using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolId).HasColumnName("school_id");
        builder.Property(x => x.InvoiceNumber).HasColumnName("invoice_number");
        builder.Property(x => x.StudentId).HasColumnName("student_id");
        builder.Property(x => x.GroupId).HasColumnName("group_id");
        builder.Property(x => x.SessionId).HasColumnName("session_id");
        builder.Property(x => x.Type).HasColumnName("type");
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.Amount).HasColumnName("amount");
        builder.Property(x => x.DueDate).HasColumnName("due_date");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.InvoiceNumber).IsUnique();
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.StudentId).HasColumnName("student_id");
        builder.Property(x => x.GroupId).HasColumnName("group_id");
        builder.Property(x => x.SessionId).HasColumnName("session_id");
        builder.Property(x => x.Type).HasColumnName("type");
        builder.Property(x => x.MonthYear).HasColumnName("month_year");
        builder.Property(x => x.Amount).HasColumnName("amount");
        builder.Property(x => x.OriginalAmount).HasColumnName("original_amount");
        builder.Property(x => x.RemainingAmount).HasColumnName("remaining_amount");
        builder.Property(x => x.PaymentDate).HasColumnName("payment_date");
        builder.Property(x => x.Method).HasColumnName("method");
        builder.Property(x => x.ReceiptNumber).HasColumnName("receipt_number");
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.RecordedBy).HasColumnName("recorded_by");
        builder.Property(x => x.ParentPaymentId).HasColumnName("parent_payment_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.ReceiptNumber).IsUnique();
    }
}

public sealed class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> builder)
    {
        builder.ToTable("discounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolId).HasColumnName("school_id");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.Percentage).HasColumnName("percentage");
        builder.Property(x => x.Conditions).HasColumnName("conditions");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

public sealed class ExemptionConfiguration : IEntityTypeConfiguration<Exemption>
{
    public void Configure(EntityTypeBuilder<Exemption> builder)
    {
        builder.ToTable("exemptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.StudentId).HasColumnName("student_id");
        builder.Property(x => x.Type).HasColumnName("type");
        builder.Property(x => x.Amount).HasColumnName("amount");
        builder.Property(x => x.MonthYear).HasColumnName("month_year");
        builder.Property(x => x.ExtraFeeId).HasColumnName("extra_fee_id");
        builder.Property(x => x.Reason).HasColumnName("reason");
        builder.Property(x => x.AuthorizedBy).HasColumnName("authorized_by");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
    }
}

public sealed class ExtraFeeConfiguration : IEntityTypeConfiguration<ExtraFee>
{
    public void Configure(EntityTypeBuilder<ExtraFee> builder)
    {
        builder.ToTable("extra_fees");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.StudentId).HasColumnName("student_id");
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.TotalAmount).HasColumnName("total_amount");
        builder.Property(x => x.PaidAmount).HasColumnName("paid_amount");
        builder.Property(x => x.RemainingAmount).HasColumnName("remaining_amount");
        builder.Property(x => x.Status).HasColumnName("status");
        builder.Property(x => x.DueDate).HasColumnName("due_date");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

public sealed class FeeSettingConfiguration : IEntityTypeConfiguration<FeeSetting>
{
    public void Configure(EntityTypeBuilder<FeeSetting> builder)
    {
        builder.ToTable("fee_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.RegistrationFeeEnabled).HasColumnName("registration_fee_enabled");
        builder.Property(x => x.RegistrationFeeAmount).HasColumnName("registration_fee_amount");
        builder.Property(x => x.Discount3Months).HasColumnName("discount_3_months");
        builder.Property(x => x.Discount6Months).HasColumnName("discount_6_months");
        builder.Property(x => x.Discount12Months).HasColumnName("discount_12_months");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}
