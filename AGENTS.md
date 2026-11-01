# School Management System - Desktop (WPF)

Windows desktop port of the Flutter/Android private-school management app.
The application is offline-only. Phase 1 (foundation) is complete. Phase 2
(data model + repositories) is complete; later phases add use cases and feature
screens.

## Solution map

| Project | Target | Purpose |
| --- | --- | --- |
| `SchoolManagement.Domain` | `net8.0` | Pure business rules and entities. No UI, EF Core or I/O. |
| `SchoolManagement.Application` | `net8.0` | `Result<T>`, `Failure`, `UiState<T>`, interfaces, use cases. |
| `SchoolManagement.Infrastructure` | `net8.0` | EF Core + SQLite, JSON settings, localization files, license bootstrap. |
| `SchoolManagement.Desktop` | `net8.0-windows` | WPF shell, views, ViewModels, themes, startup composition root. |
| `SchoolManagement.Tests` | `net8.0-windows` | xUnit, FluentAssertions, NSubstitute. |

References: Domain -> none, Application -> Domain, Infrastructure -> Application,
Desktop -> Domain + Application + Infrastructure, Tests -> all four.

## Commands

```powershell
dotnet restore SchoolManagement.sln
dotnet build SchoolManagement.sln -c Debug
dotnet test SchoolManagement.sln -c Debug
dotnet run --project SchoolManagement.Desktop
```

`Directory.Build.props` enables nullable + implicit usings and treats warnings as
errors in every project except `SchoolManagement.Desktop` (generated WPF code).

## Runtime locations

- Data: `%LOCALAPPDATA%\SchoolManagement`
- Settings: `%LOCALAPPDATA%\SchoolManagement\settings.json`
- Database: `%LOCALAPPDATA%\SchoolManagement\school_management_db.sqlite`
- Logs: `%LOCALAPPDATA%\SchoolManagement\logs\bootstrap.log`
- Translations: `<app output>\Resources\Translations\{ar,en,fr}.json`

The canonical translation files live in
`SchoolManagement.Desktop/Resources/Translations`; Infrastructure and Tests link
them into their own output folders.

## Startup order

1. Show the splash window immediately (logo + indeterminate progress only).
2. Read `settings.json` (language/theme).
3. Load the saved translation file, falling back to Arabic.
4. Build and start the Generic Host / DI container.
5. Open and integrity-check the SQLite database.
6. Run the license bootstrap hook (Phase 1: `Unlicensed` stub).
7. Apply the theme, create `ShellWindow`, close the splash.

If any step throws, `BootstrapGate` shows the icon-only retry screen. That screen
contains no text because translation may be the failed step. `bootstrap.log`
records the exception. Retry re-runs the whole order and disposes a partially
built host first.

## Phase 1 behavior

- Five shell destinations: Dashboard, Students, Groups, Finance, Settings.
- `NavigationService` caches one ViewModel per screen so tab state survives
  navigation. `NavigateToTab` is the desktop equivalent of `HomeTab.goToTab`.
- Light/dark themes use semantic `DynamicResource` brushes. `ThemeService`
  replaces the first merged theme dictionary in place; no restart.
- Arabic is the default language and sets `FlowDirection.RightToLeft`.
  `{loc:Tr key}` bindings update instantly on language change.
- `settings.json` stores only language and theme. It is separate from SQLite so
  preferences are readable before the database opens.
- The status bar reserves a banner host for the licensing phase and shows the
  application version.
- Feature pages are deliberate placeholders until their use cases exist.

## Phase 2 behavior (data model + repositories)

- **Schema compatibility**: The 28 tables from the original Flutter/Drift app are
  created exactly as in `app_database.dart`. Tables: `schools`,
  `academic_years`, `academic_levels`, `subjects`, `teachers`, `guardians`,
  `students`, `grades`, `grade_subjects`, `groups`, `enrollments`, `schedule_templates`,
  `sessions`, `attendance`, `invoices`, `payments`, `discounts`, `exemptions`,
  `exams`, `exam_results`, `notifications`, `audit_logs`, `extra_fees`,
  `fee_settings`, `system_settings`, `sms_logs`, `homeworks`, `homework_submissions`.
- **Date storage**: Drift stores `DateTime` as INTEGER Unix epoch seconds
  (local wall-clock). `UnixSecondsDateTimeConverter` writes local wall-clock
  components as seconds and reads them back as `DateTimeKind.Utc` to match the
  domain convention. `ITrustedClock.UtcNow` is for license validity;
  `ITrustedClock.LocalNow` is used for audit timestamps.
- **EF Core mapping**: `SchoolManagementDbContext` maps all 28 entities with
  exact snake_case column names. Foreign keys exist only where the original
  schema has them (`grades.academic_level_id`, `grade_subjects.grade_id/subject_id`,
  `exam_results.exam_id` navigation without enforcing FK, `homework_submissions.homework_id`
  navigation without enforcing FK). `ForeignKeyIndexConvention` is removed.
- **Initialization**: `DatabaseInitializer` runs the original DDL for a new file,
  sets `PRAGMA journal_mode = WAL`, `PRAGMA busy_timeout = 5000`,
  `PRAGMA foreign_keys = ON`, does `PRAGMA integrity_check`, and seeds once on
  new DB: school (name from `TranslationKeys.App.Name`), `fee_settings` (id=1),
  `system_settings` (`absence_limit=5`). `PRAGMA user_version` tracks schema
  (v1 before `enrollments.group_id`, v2 current). A file with user_version 0 and
  existing tables is inferred by checking `enrollments.group_id`. v1 is upgraded
  by adding `group_id INTEGER NOT NULL DEFAULT 0` (documented as requiring
  re-enrollment). user_version > 2 is refused without modifying the file.
- **Repositories**: Generic `IGenericRepository<TEntity>` (get-by-id/add/update/delete)
  and feature repositories (`IStudentRepository`, `IGroupRepository`,
  `IEnrollmentRepository`, `ISessionRepository`, `IAttendanceRepository`,
  `IScheduleTemplateRepository`, `IPaymentRepository`, `IHomeworkRepository`,
  `INotificationRepository`, `ISettingsRepository`, `IReferenceDataRepository`).
  All return `Result<T>`; exceptions are caught into `Failure.Database` but
  `OperationCanceledException` is rethrown. Queries use projections/`AsNoTracking`
  and deliberate filters; no full-table loads.
- **Policies**: `GroupCapacityPolicy` (full/warn at `>=`, enrollment never
  blocked), `AbsenceLimitPolicy` (default 5, triggers at `>=`, parses tolerantly),
  `MonthlyDuesCalculator` (full/partial exemption, never negative outstanding).
- **Finance**: `PaymentRepository.GetMonthlyDuesAsync` computes dues from active
  enrollments joined with payments (grouped by student/group/month) and
  exemptions (month-level), combining in memory via `MonthlyDuesCalculator`.
  `monthYear` is validated (empty -> `Failure.Validation` with
  `TranslationKeys.Errors.RequiredFields`).
- **Attendance**: `AttendanceRepository` upserts by `(session_id, student_id)`;
  absence counts include both `absent` and `unexcused`. `StudentRepository`
  provides absence summaries using `system_settings.absence_limit`.
- **Homework**: `HomeworkRepository` tracks submission counts and supports
  upsert of `homework_submissions` with status/notes/submitted_at.
- **Notifications**: `NotificationRepository` provides recent/unread counts,
  `HasUnreadAsync` for deduplication (attendance notifications), and `MarkReadAsync`.
- **Settings/Lookups**: `SettingsRepository` manages system settings and fee
  settings (single row). `ReferenceDataRepository` returns sorted lookup lists.

## Conventions

- Repositories and use cases return `Result<T>`; expected failures are `Failure`
  values with translation keys, never exceptions thrown at the UI.
- ViewModels expose `UiState<T>` (Initial/Loading/Success/Error/Empty).
- No user-visible hardcoded strings. XAML uses `{loc:Tr key}`; C# uses
  `TranslationKeys` constants. Missing keys return their last segment and are
  recorded in `ILocalizationService.MissingKeys`.
- Every `TranslationKeys` constant and every XAML `{loc:Tr ...}` key must exist
  in all three JSON files; tests enforce this.
- One ViewModel instance per screen. Do not recreate screens for navigation or
  language/theme changes.
- Dates use explicit `DateTimeKind.Utc` constructors. Never use `DateTime.Now`
  or `DateTime.UtcNow` for license validity. Use `ITrustedClock` (`UtcNow` for
  license, `LocalNow` for audit).
- School week: `0 = Saturday ... 6 = Friday`; convert with
  `((int)date.DayOfWeek + 1) % 7` via `WeekDayCalculator`.
- Write operations must call `ILicenseGuard.EnsureCanWrite()` once the licensing
  phase lands.
- The desktop minimum window size is 1000x650.
- Money maps to `double` (SQLite REAL) to match the original phone schema.
- Statuses remain TEXT strings (not enums) to tolerate unknown values.
- No EF Core migrations redefine the original schema; `DatabaseInitializer` owns
  schema creation/upgrade.

## Test coverage (Phase 1 + Phase 2)

- `Result<T>` / `Failure` / `UiState<T>` behavior.
- Weekday conversion and the broken `% 7` formula regression.
- Localization loading, switching, missing-key fallback and parameters.
- Shell RTL/LTR and label refresh after a language switch.
- Navigation cache, programmatic tab switching and events.
- Splash-before-initialization ordering, failure and cancellation.
- Translation key parity, used XAML keys, hardcoded XAML text and Arabic text.
- **Phase 2**: New DB creates all 28 tables with `user_version=2`, WAL mode,
  seeds school/fee_settings/system_settings, idempotent initialization,
  refusal of schema version > 2 without changes, v1→v2 upgrade adding
  `enrollments.group_id` with default 0 (old rows need re-enrollment), unique
  constraints (`students.studentCode`, `system_settings.key`), opening an existing
  v2 file with Unix-second dates and round-trip through EF Core, repository
  projections/filters/counts, monthly dues with payments/exemptions,
  absence counts (absent+unexcused) and limits, settings updates, notification
  dedup/mark-read, homework submission counts/upsert, reference lookups, and
  policy tests (group capacity, absence limits, monthly dues).

## Next phases

Use cases, students/groups/finance/attendance/sessions/homework screens,
Ed25519 + DPAPI licensing, QuestPDF reports, ZXing QR and SMS via `sms:` /
Phone Link. Do not add these to Phase 1/2.

## Phase 2 results (actual)

- **Build**: `dotnet build SchoolManagement.sln -c Debug` -> succeeded (0 warnings, 0 errors).
- **Tests**: `dotnet test SchoolManagement.sln -c Debug --no-build` -> 83 passed, 0 failed, 0 skipped.
- **Smoke test (desktop)**: Started the desktop app; it ran successfully for several
  seconds. The database in `%LOCALAPPDATA%\SchoolManagement` was initialized
  to user_version 2 with 29 SQLite objects (28 tables + indexes) in WAL mode,
  with 1 school row and `system_settings.absence_limit = 5`.
