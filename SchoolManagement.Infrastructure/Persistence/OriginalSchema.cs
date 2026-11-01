namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// The original Drift schema, reproduced verbatim so a database created on the
/// desktop can be opened by the phone app. Dates are INTEGER Unix epoch seconds
/// (Drift's default) and booleans are INTEGER 0/1.
///
/// No EF migration ever defines these tables: this script is the single source
/// of truth and <see cref="DatabaseInitializer"/> runs it only for a new file.
/// </summary>
public static class OriginalSchema
{
    public const int CurrentVersion = 2;

    /// <summary>Every table the phone app expects, in creation order.</summary>
    public static readonly IReadOnlyList<string> RequiredTables =
    [
        "schools",
        "academic_years",
        "academic_levels",
        "subjects",
        "teachers",
        "guardians",
        "students",
        "grades",
        "grade_subjects",
        "groups",
        "enrollments",
        "schedule_templates",
        "sessions",
        "attendance",
        "invoices",
        "payments",
        "discounts",
        "exemptions",
        "exams",
        "exam_results",
        "notifications",
        "audit_logs",
        "extra_fees",
        "fee_settings",
        "system_settings",
        "sms_logs",
        "homeworks",
        "homework_submissions",
    ];

    public const string CreateV2Script = """
        CREATE TABLE IF NOT EXISTS "schools" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "name" TEXT NOT NULL,
          "address" TEXT NULL,
          "phone" TEXT NULL,
          "email" TEXT NULL,
          "logo_path" TEXT NULL,
          "default_language" TEXT NOT NULL DEFAULT 'ar',
          "timezone" TEXT NOT NULL DEFAULT 'Africa/Algiers',
          "is_active" INTEGER NOT NULL DEFAULT 1,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "academic_years" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "school_id" INTEGER NOT NULL DEFAULT 1,
          "name" TEXT NOT NULL,
          "start_date" INTEGER NOT NULL,
          "end_date" INTEGER NOT NULL,
          "is_current" INTEGER NOT NULL DEFAULT 0,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          UNIQUE ("school_id", "name")
        );

        CREATE TABLE IF NOT EXISTS "academic_levels" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "name" TEXT NOT NULL,
          "order_index" INTEGER NOT NULL DEFAULT 0,
          "is_active" INTEGER NOT NULL DEFAULT 1,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "subjects" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "school_id" INTEGER NOT NULL DEFAULT 1,
          "name" TEXT NOT NULL,
          "code" TEXT NULL,
          "description" TEXT NULL,
          "is_active" INTEGER NOT NULL DEFAULT 1,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "teachers" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "school_id" INTEGER NOT NULL DEFAULT 1,
          "full_name" TEXT NOT NULL,
          "phone" TEXT NULL,
          "email" TEXT NULL,
          "address" TEXT NULL,
          "photo_path" TEXT NULL,
          "specialization" TEXT NULL,
          "hire_date" INTEGER NULL,
          "is_active" INTEGER NOT NULL DEFAULT 1,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "guardians" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "school_id" INTEGER NOT NULL DEFAULT 1,
          "full_name" TEXT NOT NULL,
          "phone" TEXT NULL,
          "email" TEXT NULL,
          "relationship" TEXT NULL,
          "address" TEXT NULL,
          "is_active" INTEGER NOT NULL DEFAULT 1,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "students" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "school_id" INTEGER NOT NULL DEFAULT 1,
          "guardian_id" INTEGER NULL,
          "student_code" TEXT NOT NULL UNIQUE,
          "first_name" TEXT NOT NULL,
          "last_name" TEXT NOT NULL,
          "date_of_birth" INTEGER NULL,
          "gender" TEXT NULL,
          "address" TEXT NULL,
          "phone" TEXT NULL,
          "photo_path" TEXT NULL,
          "status" TEXT NOT NULL DEFAULT 'active',
          "notes" TEXT NULL,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "registration_fee" REAL NOT NULL DEFAULT 0.0
        );

        CREATE TABLE IF NOT EXISTS "grades" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "academic_level_id" INTEGER NULL REFERENCES academic_levels (id),
          "name" TEXT NOT NULL,
          "order_index" INTEGER NOT NULL DEFAULT 0,
          "is_active" INTEGER NOT NULL DEFAULT 1,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "grade_subjects" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "grade_id" INTEGER NOT NULL REFERENCES grades (id),
          "subject_id" INTEGER NOT NULL REFERENCES subjects (id),
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          UNIQUE ("grade_id", "subject_id")
        );

        CREATE TABLE IF NOT EXISTS "groups" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "subject_id" INTEGER NULL,
          "teacher_id" INTEGER NULL,
          "academic_level_id" INTEGER NULL,
          "grade_id" INTEGER NULL,
          "name" TEXT NOT NULL,
          "max_students" INTEGER NOT NULL DEFAULT 30,
          "monthly_fee" REAL NOT NULL DEFAULT 0.0,
          "is_active" INTEGER NOT NULL DEFAULT 1,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "enrollments" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "school_id" INTEGER NOT NULL DEFAULT 1,
          "student_id" INTEGER NOT NULL,
          "group_id" INTEGER NOT NULL,
          "enrollment_date" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "registration_fee" REAL NOT NULL DEFAULT 0.0,
          "status" TEXT NOT NULL DEFAULT 'active',
          "notes" TEXT NULL,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "schedule_templates" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "group_id" INTEGER NOT NULL,
          "day_of_week" INTEGER NOT NULL,
          "start_time" INTEGER NOT NULL,
          "end_time" INTEGER NOT NULL,
          "location" TEXT NULL,
          "is_active" INTEGER NOT NULL DEFAULT 1,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "sessions" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "group_id" INTEGER NOT NULL,
          "template_id" INTEGER NULL,
          "session_date" INTEGER NOT NULL,
          "start_time" INTEGER NOT NULL,
          "end_time" INTEGER NOT NULL,
          "type" TEXT NOT NULL DEFAULT 'regular',
          "status" TEXT NOT NULL DEFAULT 'scheduled',
          "lesson_topic" TEXT NULL,
          "teacher_notes" TEXT NULL,
          "homework" TEXT NULL,
          "cancel_reason" TEXT NULL,
          "is_paid" INTEGER NOT NULL DEFAULT 0,
          "price" REAL NULL,
          "payment_status" TEXT NULL,
          "paid_amount" REAL NOT NULL DEFAULT 0.0,
          "remaining_amount" REAL NOT NULL DEFAULT 0.0,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "attendance" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "session_id" INTEGER NOT NULL,
          "student_id" INTEGER NOT NULL,
          "status" TEXT NOT NULL,
          "check_in_time" INTEGER NULL,
          "recorded_by" INTEGER NULL,
          "notes" TEXT NULL,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          UNIQUE ("session_id", "student_id")
        );

        CREATE TABLE IF NOT EXISTS "invoices" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "school_id" INTEGER NOT NULL DEFAULT 1,
          "invoice_number" TEXT NOT NULL UNIQUE,
          "student_id" INTEGER NOT NULL,
          "group_id" INTEGER NULL,
          "session_id" INTEGER NULL,
          "type" TEXT NOT NULL,
          "description" TEXT NOT NULL,
          "amount" REAL NOT NULL,
          "due_date" INTEGER NOT NULL,
          "status" TEXT NOT NULL DEFAULT 'unpaid',
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "payments" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "student_id" INTEGER NOT NULL,
          "group_id" INTEGER NULL,
          "session_id" INTEGER NULL,
          "type" TEXT NOT NULL,
          "month_year" TEXT NULL,
          "amount" REAL NOT NULL,
          "original_amount" REAL NOT NULL,
          "remaining_amount" REAL NOT NULL DEFAULT 0.0,
          "payment_date" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "method" TEXT NOT NULL DEFAULT 'cash',
          "receipt_number" TEXT NOT NULL UNIQUE,
          "notes" TEXT NULL,
          "recorded_by" INTEGER NULL,
          "parent_payment_id" INTEGER NULL,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "discounts" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "school_id" INTEGER NOT NULL DEFAULT 1,
          "name" TEXT NOT NULL,
          "description" TEXT NULL,
          "percentage" REAL NOT NULL,
          "conditions" TEXT NULL,
          "is_active" INTEGER NOT NULL DEFAULT 1,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "exemptions" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "student_id" INTEGER NOT NULL,
          "type" TEXT NOT NULL,
          "amount" REAL NOT NULL,
          "month_year" TEXT NULL,
          "extra_fee_id" INTEGER NULL,
          "reason" TEXT NOT NULL,
          "authorized_by" INTEGER NULL,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "exams" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "school_id" INTEGER NOT NULL DEFAULT 1,
          "group_id" INTEGER NOT NULL,
          "name" TEXT NOT NULL,
          "exam_date" INTEGER NOT NULL,
          "start_time" INTEGER NULL,
          "duration_minutes" INTEGER NULL,
          "type" TEXT NOT NULL,
          "total_marks" REAL NULL,
          "is_external" INTEGER NOT NULL DEFAULT 0,
          "notes" TEXT NULL,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "exam_results" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "school_id" INTEGER NOT NULL DEFAULT 1,
          "exam_id" INTEGER NOT NULL,
          "student_id" INTEGER NOT NULL,
          "score" REAL NULL,
          "notes" TEXT NULL,
          "recorded_by" INTEGER NULL,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          UNIQUE ("exam_id", "student_id")
        );

        CREATE TABLE IF NOT EXISTS "notifications" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "title" TEXT NOT NULL,
          "body" TEXT NOT NULL,
          "type" TEXT NOT NULL,
          "recipient_type" TEXT NOT NULL,
          "recipient_id" INTEGER NULL,
          "channel" TEXT NOT NULL DEFAULT 'in_app',
          "is_read" INTEGER NOT NULL DEFAULT 0,
          "metadata" TEXT NULL,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "audit_logs" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "school_id" INTEGER NOT NULL DEFAULT 1,
          "user_id" INTEGER NULL,
          "action" TEXT NOT NULL,
          "entity_type" TEXT NOT NULL,
          "entity_id" INTEGER NULL,
          "old_values" TEXT NULL,
          "new_values" TEXT NULL,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "extra_fees" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "student_id" INTEGER NOT NULL,
          "description" TEXT NOT NULL,
          "total_amount" REAL NOT NULL,
          "paid_amount" REAL NOT NULL DEFAULT 0.0,
          "remaining_amount" REAL NOT NULL,
          "status" TEXT NOT NULL DEFAULT 'unpaid',
          "due_date" INTEGER NULL,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "fee_settings" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "registration_fee_enabled" INTEGER NOT NULL DEFAULT 0,
          "registration_fee_amount" REAL NOT NULL DEFAULT 0.0,
          "discount_3_months" REAL NOT NULL DEFAULT 0.0,
          "discount_6_months" REAL NOT NULL DEFAULT 0.0,
          "discount_12_months" REAL NOT NULL DEFAULT 0.0,
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "system_settings" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "key" TEXT NOT NULL UNIQUE,
          "value" TEXT NOT NULL,
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "sms_logs" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "notification_id" INTEGER NULL,
          "phone" TEXT NOT NULL,
          "body" TEXT NOT NULL,
          "status" TEXT NOT NULL,
          "error_message" TEXT NULL,
          "recipient_id" INTEGER NULL,
          "recipient_name" TEXT NULL,
          "event_code" TEXT NULL,
          "sent_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "homeworks" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "session_id" INTEGER NOT NULL,
          "target_session_id" INTEGER NULL,
          "group_id" INTEGER NOT NULL,
          "title" TEXT NOT NULL,
          "description" TEXT NOT NULL,
          "due_date" INTEGER NOT NULL,
          "is_completed" INTEGER NOT NULL DEFAULT 0,
          "notes" TEXT NULL,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP))
        );

        CREATE TABLE IF NOT EXISTS "homework_submissions" (
          "id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          "homework_id" INTEGER NOT NULL,
          "student_id" INTEGER NOT NULL,
          "status" TEXT NOT NULL DEFAULT 'pending',
          "submitted_at" INTEGER NULL,
          "notes" TEXT NULL,
          "created_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          "updated_at" INTEGER NOT NULL DEFAULT (strftime('%s', CURRENT_TIMESTAMP)),
          UNIQUE ("homework_id", "student_id")
        );
        """;

    /// <summary>
    /// The v1 file has every table except <c>enrollments.group_id</c>. SQLite
    /// requires a default for a new NOT NULL column; the phone model declares
    /// none, so desktop-upgraded rows get 0 and must be re-enrolled. This is the
    /// only documented schema difference.
    /// </summary>
    public const string UpgradeV1ToV2Script = """
        ALTER TABLE "enrollments" ADD COLUMN "group_id" INTEGER NOT NULL DEFAULT 0;
        """;
}
