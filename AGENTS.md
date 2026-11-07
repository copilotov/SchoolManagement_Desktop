# School Management System - Desktop (WPF)

Windows desktop port of the Flutter/Android private-school management app.
The application is offline-only. Phases 1 (foundation), 2 (data model +
repositories), 3 (licensing: Ed25519 + DPAPI + trusted clock), 4 (academic
structure: levels, grades, subjects, teachers), 5 (students: list, shared form,
file, card, bulk QR, CSV/Excel), 6 (groups: list, form, enrollment, weekly
schedule, sessions, timetable, printable report) and 7 (sessions, attendance,
QR scanning, extra sessions, homeworks, attendance report) are complete. Phase
D1, the design-system foundation (Fluent 2 palette, typography, tokens, control
styles, motion, RTL mirroring, Debug-only design gallery), D2, the shell and
navigation, and D3, the reusable component library (page header, stat cards,
data table, states, forms, overlays, badges, indicators, pickers, weekly grid,
QR card, charts), are also complete; later phases redesign the individual
screens on top of them.

## Solution map

| Project | Target | Purpose |
| --- | --- | --- |
| `SchoolManagement.Domain` | `net8.0` | Pure business rules and entities. No UI, EF Core or I/O. |
| `SchoolManagement.Application` | `net8.0` | `Result<T>`, `Failure`, `UiState<T>`, interfaces, use cases. |
| `SchoolManagement.Infrastructure` | `net8.0` | EF Core + SQLite, JSON settings, localization files, licensing. |
| `SchoolManagement.Desktop` | `net8.0-windows` | WPF shell, views, ViewModels, themes, startup composition root. |
| `SchoolManagement.Tests` | `net8.0-windows` | xUnit, FluentAssertions, NSubstitute. |
| `tools/SchoolManagement.LicenseTool` | `net8.0` | Offline license issuer/verifier. Never shipped. |

References: Domain -> none, Application -> Domain, Infrastructure -> Application,
Desktop -> Domain + Application + Infrastructure, Tests -> all four. The issuer
tool references Domain only; no shipped project references it.

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
- License: `%LOCALAPPDATA%\SchoolManagement\license.dat` (DPAPI, CurrentUser)
- Trusted time: `%LOCALAPPDATA%\SchoolManagement\trusted_time.dat` (DPAPI, CurrentUser)
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
6. Run the license bootstrap (verify + trusted clock). A missing or invalid
   license opens the activation window; every other state opens the shell.
7. Apply the theme, create `ShellWindow`, close the splash. The shell refreshes
   the license whenever the main window regains focus.

If any step throws, `BootstrapGate` shows the icon-only retry screen. That screen
contains no text because translation may be the failed step. `bootstrap.log`
records the exception. Retry re-runs the whole order and disposes a partially
built host first.

## Phase 1 behavior

- Seven shell destinations: Dashboard, Students, Groups, Academics, Attendance,
  Finance, Settings (`Academics` arrived in Phase 4, `Students` in Phase 5 and
  `Attendance` in Phase 7; Dashboard, Finance and Settings remain placeholders
  until their own phases).
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

## Phase 3 behavior (licensing)

- **Format**: `base64url(payload) + "." + base64url(Ed25519 signature)`, byte-for-byte
  compatible with `tool/license_tool.dart`. Payload members are always written in
  the order `v, fp, exp, to, iat, [nbf], kid` with compact separators and raw
  UTF-8 (Dart `jsonEncode` output). `LicensePayload.WriteCanonicalJson` is
  signature-sensitive; fixed vectors in `LicenseTestKit` guard it.
- **Trust root**: the Ed25519 public key is the compile-time constant
  `LicensePublicKey.Base64`. There is no environment-variable override. Key
  rotation means shipping a new build; `kid` identifies the issuer.
- **Verification order**: envelope structure, then the Ed25519 signature over the
  raw payload bytes, then semantics (version policy, dates, fingerprint). A
  tampered payload is `BadSignature`, never a misleading `Malformed`. Accepted
  versions are 1 and 2; 3+ is `UnsupportedVersion`; unknown `kid` is accepted.
- **Seven states** (`LicenseStatusCalculator`): `Active`, `ExpiringSoon`
  (`exp - 30` inclusive), `GracePeriod` (`exp + 1 .. exp + 30`, writing allowed),
  `ReadOnly` (`exp + 31`), `NotStarted` (before `nbf`; the `nbf` day itself is
  active), `Unlicensed`, `Invalid`. The grace banner states the days left and the
  read-only start date.
- **First screen**: only `Unlicensed` and `Invalid` (`LicenseInfo.RequiresActivation`)
  open `ActivationWindow`; `ReadOnly` and `NotStarted` still open the shell
  read-only. `CanWrite` is never used for that decision.
- **Guard**: every write use case derives from `WriteUseCase<T>` and carries
  `[WriteOperation]`. `WriteUseCase<T>.ExecuteAsync` calls
  `ILicenseGuard.EnsureCanWrite()` before `ExecuteCoreAsync`, so a blocked write
  returns `Failure.License(license.errorWriteBlocked)` and never reaches the
  repository. Reflection tests enforce the convention.
- **Trusted time**: `TrustedTimeEngine` merges the device clock, the highest
  instant stored in DPAPI (`trusted_time.dat`) and the license `iat` anchor. The
  trusted instant never goes backwards; >5 minutes behind the stored instant is a
  rollback; the anchor is compared by local date with a 1-day grace so a time-zone
  change is not tampering. `ITrustedClock` is the only allowed clock in validity
  code; `SystemLocalClock` is the single raw-clock wrapper.
- **Storage**: `license.dat` and `trusted_time.dat` are DPAPI-encrypted with
  `DataProtectionScope.CurrentUser`. Reading never deletes an invalid license;
  only explicit deactivation clears it.
- **UI**: `LicenseBannerView` in the status bar shows one message per state with a
  renew button; `ActivationPage`/`ActivationWindow` show the full fingerprint,
  a 4x4 short code, paste/copy helpers and the exact failure reason. The shell
  re-evaluates the license on window focus. `settings.json` still holds only
  language/theme.
- **Issuer**: `tools/SchoolManagement.LicenseTool` supports `keygen`, `issue` and
  `verify`. The private key lives outside the repo
  (`%LOCALAPPDATA%\SchoolManagement\license_private.key`) and is never committed.
  Full procedure: `docs/LICENSING.md`.

## Phase 4 behavior (academic structure)

- **Scope**: academic levels, grades (with the `grade_subjects` many-to-many),
  subjects and teachers. One `Academics` shell destination hosts the four lists in
  a `TabControl`; the child ViewModels are transient and the host is cached by
  `NavigationService`, so search, sort and editor state survive tab and shell
  navigation. Tabs load lazily on first selection.
- **Models**: `Application/Academic/AcademicModels.cs` holds `AcademicListQuery`
  (search + include inactive + sort field + direction), the four input records,
  the update-request records, `GradeSubjectAssignment` and the pure
  `AcademicValidator`. Rules: name required and length-limited, order index
  `>= 0`, optional code length-limited, email/phone patterns checked only when a
  value is supplied, and the optional fields normalized to `null` when blank.
- **Projections**: `AcademicLevelListItem`, `GradeListItem`, `SubjectListItem`,
  `TeacherListItem` and `SubjectSelectionItem` carry only displayed columns plus
  linked counts (`GradesCount`, `GroupsCount`, `SubjectsCount`). Linked counts
  are correlated subqueries, so one row per entity arrives without a second
  round-trip; no full-table load exists.
- **Use cases**: read use cases delegate to the repositories. Every write derives
  from `WriteUseCase<T>` or the request-object template
  `WriteUseCase<TRequest, TResult>` (both live in `UseCases/WriteUseCase.cs`) and
  carries `[WriteOperation]`. Add/update/delete are exposed for all four entities,
  plus `SetGradeSubjectsUseCase`. `CodeExistsAsync` is called before adding or
  updating a subject, excluding the current id on update.
- **Delete policy**: hard delete when unlinked, blocked with a localized, counted
  message when linked. Levels block on grades then groups; grades block on groups
  and otherwise remove their own `grade_subjects` rows inside the same
  transaction; subjects block on groups then grade links; teachers block on
  groups. Deactivation remains the `IsActive` flag because the phone schema has no
  `deleted_at` column.
- **Repositories**: `AcademicLevelRepository`, `GradeRepository`,
  `SubjectRepository` and `TeacherRepository`. Subject codes compare
  case-insensitively. `GradeRepository.SetSubjectsAsync` filters to existing
  subject ids, diffs the links and returns `Failure.NotFound` for a missing grade.
  `GenericRepository.DeleteAsync` is virtual so the grade override can cascade in
  one transaction.
- **Localization**: every label, column header, editor title, status message,
  validation message and delete-blocked message comes from
  `TranslationKeys.{Levels,Grades,Subjects,Teachers,Common,Dialogs,Validators,Errors}`;
  the ar/en/fr key sets stay identical. Required fields show a translated `*`
  whose tooltip is `common.required`.
- **ViewModels**: `AcademicListViewModelBase` owns the toolbar, `UiState`, sort
  options, include-inactive toggle, search, editor state, status message, renew
  action and `IDisposable` language hook; the four derived ViewModels own their
  entity-specific editor and delete rules. A license-blocked write shows the
  unified message and the renew button; other failures do not.
- **Views**: one tab view per entity with a toolbar, a `DataGrid`, an in-page
  modal editor overlay and keyboard shortcuts (`F2` edit, `Delete` remove,
  `Enter` save, `Escape` cancel) plus a deliberate tab order. Delete confirmation
  uses the shared `ConfirmationWindow` through `IUserDialogService`; the
  ViewModel passes already-localized text, so the WPF layer needs no localization
  and the ViewModels stay testable without a WPF message loop.

## Phase 5 behavior (students)

- **Scope**: the `Students` shell destination (previously a placeholder) is a
  full list with instant search and filters, one shared add/edit form, a
  read-only student file with three tabs, a printable card and a bulk QR sheet,
  plus CSV/Excel import and export. Group, finance, attendance, session and
  homework screens remain later phases; the attendance tab of the student file
  therefore shows data but has no "open attendance" link yet.
- **Models**: `Application/Students/StudentModels.cs` holds
  `StudentListQuery` (search, status, level, grade, group, sort field,
  direction, skip, take), `StudentSaveRequest`/`StudentSaveOutcome`,
  `StudentDependencies`, `StudentCardData`, `StudentQrItem`,
  `StudentExportRow`, the import records (`StudentImportRow`,
  `StudentImportError`, `ParsedStudentFile`, `StudentImportResult`), the
  `AlgeriaPhone` normalizer and the pure `StudentValidator`.
- **Validation/phone**: first/last name required and length-limited, optional
  code restricted to letters/digits/dashes and length-limited, birth date must
  be `DateTimeKind.Utc` and plausible, gender restricted to `male`/`female`
  case-insensitively, status restricted to the stored values, fee `>= 0`, the
  guardian name required as soon as a guardian phone/email is supplied.
  `AlgeriaPhone` strips spaces/dashes/parentheses/dots, converts `0XXXXXXXXX`,
  `XXXXXXXXX`, `00213...` and `213...` to `+213XXXXXXXX` (8–9 digits after the
  country code), rejects letters and keeps an invalid value unchanged so the
  validator reports it; blank stays `null`.
- **Student code**: `StudentCodeGenerator` (Domain, pure) builds
  `STU-{year}-{sequence:0000}` with `ParseSequence`, `IsCodeForYear` and
  `NextSequence` (highest of the year + 1, refuses 9999). The code is generated
  only when the request carries none and is never rewritten on update.
  `StudentRepository.SaveAsync` serializes generation with a static
  `SemaphoreSlim` and a SQLite transaction; the unique `students.student_code`
  index remains the final safety net, so concurrent saves can never share a
  code.
- **Guardian**: the guardian row is created or updated inside the same
  transaction as the student, so a failed save leaves neither row behind.
- **Repository**: `StudentRepository` implements filtered/sorted/paged
  `GetListAsync` + `CountAsync`, `GetDetailsAsync`, `CodeExistsAsync`
  (case-insensitive, excluding the edited id), `SaveAsync`, the dependency
  counts used by delete, `GetExportRowsAsync`, `GetCardDataAsync` and
  `GetAbsenceSummaryAsync`. Every projection is deliberate. SQLite cannot
  translate the SQL `APPLY` operator, so active group/level/grade names are
  resolved by a second `LoadPlacementsAsync` query (enrollment→group join with
  scalar subqueries for level/grade) instead of correlated `let`/`DefaultIfEmpty`
  joins; sorting by level/group uses fully inlined correlated subqueries because
  EF cannot translate helper method calls.
- **Delete policy**: a student with enrollments, payments or attendance is
  blocked with a localized, counted `students.deleteBlocked` message (keys
  `enrollments`, `payments`, `attendance`); an unlinked student is hard deleted.
- **Use cases**: `GetStudentLookupsUseCase`, `GetStudentsUseCase`,
  `GetStudentDetailsUseCase`, `GetStudentActivityUseCase` (groups, finance and
  attendance in one call), `GetStudentCardDataUseCase`,
  `SaveStudentUseCase`, `DeleteStudentUseCase`, `ImportStudentsUseCase`,
  `ExportStudentsUseCase`, `RenderStudentQrUseCase`/`RenderStudentsQrUseCase`,
  `RenderStudentCardPreviewUseCase`, `BuildStudentCardPdfUseCase` and
  `BuildStudentsQrSheetPdfUseCase`. The write use cases derive from
  `WriteUseCase<TRequest, TResult>` and carry `[WriteOperation]`; export and the
  rendering use cases are reads.
- **Import**: `StudentFileGateway` writes/reads CSV (UTF-8 with BOM, RFC 4180
  quoting) and `.xlsx` through ClosedXML with the canonical header row
  (`FirstName, LastName, StudentCode, DateOfBirth, Gender, Phone, Address,
  Status, Notes, RegistrationFee, GuardianName, GuardianPhone, GuardianEmail,
  GuardianRelationship`). A headerless CSV falls back to that column order.
  Malformed dates/numbers and corrupt workbooks become `Failure`/per-row
  `StudentImportError` values (row number, student reference, translation key),
  never exceptions; valid rows are still saved. Export always covers the whole
  student table with the guardian and placement columns, and refuses to write an
  empty file (`students.exportEmpty`).
- **Cards/QR**: `StudentCardService` renders the card preview (`GenerateImages`),
  the card PDF and the A4 QR sheet with QuestPDF and the QR PNG with ZXing.Net +
  SkiaSharp, using the same layout for preview and print. QuestPDF is configured
  once (`LicenseType.Community`, environment fonts, glyph check disabled,
  `Segoe UI`). The QR payload is the student code.
- **Desktop**: `StudentsPage` (toolbar, filter bar, `DataGrid`, in-page modal
  `StudentFormView`, import report overlay, status banner, keyboard shortcuts
  F2/Delete/Enter/Escape), `StudentDetailsPage` (header, guardian card, three
  tabs, Edit, Print card, Back), `StudentCardPage` (preview + Print + Save PDF)
  and `StudentsQrPrintPage` (tiles in a `Viewbox` + `UniformGrid`, Print all +
  Save A4 PDF). Details, card and QR pages are transient pages reached through
  `NavigationService.NavigateTo<T>()`; the list stays cached.
- **Testability seams**: WPF dialogs/printing live behind `IFileDialogService`
  and `IPdfPrintService`, so the ViewModels pass already-localized text and never
  reference `Microsoft.Win32` or a printer. `BitmapHelper` freezes the PNG into a
  `BitmapSource` off the UI thread.
- **Messages**: a successful save/delete/import/export sets the localized status
  banner, and a list reload keeps it (same behaviour as the Phase 4 lists); only
  an explicit new action, a failure or a license block replaces it.
- **Localization**: every Phase 5 label, column, hint, status, validation message
  and delete-blocked message comes from
  `TranslationKeys.{Students,Common,Validators,Errors,Dialogs}`; the ar/en/fr
  key sets stay identical and Arabic stays the default (RTL).

## Phase 6 behavior (groups, enrollment, schedule, sessions, timetable, report)

- **Scope**: the `Groups` shell destination hosts the group table, the shared
  add/edit form, the enrollment board, the weekly schedule of one group, the
  visual weekly timetable and the printable full group report. The `Groups`
  placeholder is gone; dashboard/groups/finance/attendance/sessions/homework
  screens beyond this list remain later phases.
- **Models**: `Application/Groups/GroupModels.cs` holds `GroupListQuery`
  (search, include inactive, sort field/direction), `GroupSaveRequest`/
  `GroupSaveOutcome`, `GroupDependencies`, `GroupSubjectOption`,
  `ScheduleSaveRequest`/`ScheduleSaveOutcome`, `ScheduleConflictItem`,
  `ScheduleCheckResult`, `EnrollmentSaveRequest`/`EnrollmentSaveOutcome`,
  `SessionGenerationRequest`/`SessionGenerationResult`, `GroupLookups`,
  `EnrollmentBoardData`, `GroupDetailsData`, `GroupReportStudent`/
  `GroupReportData`, the pure `GroupValidator`, `ScheduleValidator` and
  `ScheduleTimeParser`.
- **Group form cascade**: the grade list drives the subject list through
  `grade_subjects` (`GetGroupSubjectsUseCase` → `IGradeRepository.GetSubjectIdsAsync`
  then the subject lookup filtered to the linked ids); switching the grade
  clears and reloads the subject selection while the editor is open. A group
  requires a grade and a subject, and `GroupRepository.SaveAsync` always writes
  `academic_level_id`: the request value when supplied, otherwise the level
  derived from the selected grade (an update keeps the stored level when the
  request has none). Subject and teacher names are resolved in the same
  projection as the group row (correlated scalar subqueries), never by a N+1
  query per row.
- **Group list**: `GroupRepository.GetListAsync` filters on entity columns
  before projecting (`EF.Functions.Like` for case-insensitive ASCII search over
  group name, subject, teacher, grade and level names; searching by `Contains`
  would be case-sensitive `instr` in SQLite). `GroupSummary` carries the
  displayed columns plus `ActiveEnrollments`, `WeeklySlots` and the computed
  `IsFull`, `IsOverCapacity`, `RemainingSeats`, `HasSchedule` and
  `CapacityText` (`"12/30"`). Capacity is a warning only: the badge turns to the
  full colour and the enrollment screen asks for confirmation, but nothing is
  ever blocked (`GroupCapacityPolicy` unchanged).
- **Capacity confirmation**: `EnrollmentViewModel` compares
  `Group.IsFull` (`ActiveEnrollments >= MaxStudents`) and confirms at `>=`, the
  same inclusive boundary as `GroupCapacityPolicy`. Declining leaves the roster
  unchanged. The board only offers active students that are not already in an
  active enrollment of that group, and the success message distinguishes
  "enrolled", "capacity exceeded warning" and "already enrolled".
- **Enrollment**: a student may hold at most one active enrollment per group.
  `EnrollmentRepository.EnrollAsync` checks for an existing active row inside a
  static `SemaphoreSlim` plus a SQLite transaction and returns
  `AlreadyEnrolled` instead of creating a duplicate, so concurrent clicks cannot
  double-enroll. A withdrawn row does not count, so re-enrolling creates a new
  row. Withdrawing (`WithdrawEnrollmentUseCase`) only flips the status to
  `withdrawn`; rows are never hard deleted.
- **Schedule**: one `schedule_templates` row per weekly slot, stored as the fixed
  reference date `2000-01-01` plus a time-of-day so the phone schema and the
  Unix-seconds converter stay compatible. The page groups slots under the seven
  school days (`0 = Saturday … 6 = Friday`), and the shared dialog adds or edits
  one slot. `ScheduleTimeParser` reads `HH:mm`; a malformed or inverted range is
  a validation failure (`groups.invalidTimeRange`) before any query.
- **Conflicts are warnings, not blocks**: `ScheduleConflictPolicy` (Domain, pure)
  detects half-open overlaps (`start < otherEnd && end > otherStart`) for the
  same group or the same teacher on the same weekday, and
  `GetScheduleConflictsUseCase` classifies each hit as `SameGroup` or
  `SameTeacher`. The page lists the lines and asks for confirmation; confirming
  saves through a license-guarded write, cancelling discards the pending
  request. This matches the phone app's advisory behaviour and prevents a
  silent double-booking without making a legitimate reschedule impossible.
- **Sessions**: `GenerateSessionsForDate(date, groupId?)` creates one `sessions`
  row per active template of that school day (optionally limited to one group),
  combining the date with the template time through
  `SessionSchedulePolicy.Combine`, and returns
  `{createdCount, alreadyExisted}` with no duplicate. Existing
  group/date/start rows are counted as `alreadyExisted`; a static gate plus a
  transaction serializes concurrent callers. The ViewModel displays the created
  count, the all-existing case or the no-template case with distinct keys.
- **Timetable**: `GetTimetableUseCase` reads the joined slot projection filtered
  by group and/or teacher. `TimetableViewModel` builds the whole grid (one
  corner cell, 12 hour headers 08:00–19:00, seven day labels and 7×12 slot
  cells = 104 cells) and the view renders it with an `ItemsControl` over a
  `UniformGrid` sized by `ColumnCount` (13) and `RowCount` (8). Each occupied
  cell shows the group, the time range and the room.
- **Group file**: `GetGroupDetailsUseCase` joins the summary, the group roster,
  the ordered weekly slots and the attendance counters in one call.
  `GroupAttendanceStats.AttendanceRate` is `(present + late) / records`; the
  ViewModel formats it as `"2 / 3 (66.7%)"`. Shortcuts open enrollment, the
  schedule, the shared edit form (on the cached list ViewModel) and the report.
  The attendance tab remains read-only because the attendance screen is a later
  phase.
- **Printable report**: `BuildGroupFullReportUseCase` (read, no license gate)
  joins the group, its active students, attendance and the current month's dues
  from `GetMonthlyDuesAsync(monthYear, groupId)`, then hands a `GroupReportData`
  to the `IGroupReportService` port. `GroupReportService` (Infrastructure,
  QuestPDF A4) renders the report; `GroupFullReportViewModel` reuses the same
  PDF bytes for print, open and save (`group-report-{id}.pdf`), so the printed
  page cannot drift from the saved file.
- **Use cases**: reads are `GetGroupLookupsUseCase`, `GetGroupSubjectsUseCase`,
  `GetGroupsUseCase`, `GetGroupSummaryUseCase`, `GetGroupDetailsUseCase`,
  `GetGroupScheduleUseCase`, `GetScheduleConflictsUseCase`,
  `GetTimetableUseCase`, `GetEnrollmentBoardUseCase` and
  `BuildGroupFullReportUseCase`. Writes are `SaveGroupUseCase`,
  `RemoveGroupUseCase`, `EnrollStudentInGroupUseCase`,
  `WithdrawEnrollmentUseCase`, `SaveScheduleSlotUseCase`,
  `DeleteScheduleSlotUseCase` and `GenerateSessionsUseCase`; every one derives
  from `WriteUseCase<T>`/`WriteUseCase<TRequest, TResult>` and carries
  `[WriteOperation]`.
- **Delete policy**: a group with enrollments, sessions or schedule slots is
  blocked with a localized, counted `groups.deleteBlocked` message; an unlinked
  group is hard deleted.
- **Repositories**: `GroupRepository` (list/summary/save/dependencies/active
  count), `EnrollmentRepository` (roster, active count, idempotent enroll,
  status-only withdraw), `ScheduleTemplateRepository` (slot projection, save,
  delete, same-day conflict query),
  `SessionRepository.GenerateForDateAsync` and
  `AttendanceRepository.GetGroupStatisticsAsync`. Every projection and filter is
  deliberate; filters and orderings are applied to entity columns before the
  projection because EF cannot translate predicates over records that contain
  correlated subqueries.
- **Localization**: every Phase 6 label, column, hint, status, validation
  message and delete-blocked message comes from
  `TranslationKeys.{Groups,Enrollment,Timetable,Reports,Common,Validators,Errors}`,
  including the new `groups.editTimeSlot`, `groups.scheduleSaved`,
  `groups.scheduleDeleted`, `groups.sessionsGenerated`, `groups.slotsPerWeek`
  and `enrollment.unenrollConfirm` keys; the ar/en/fr key sets stay identical.
  `TranslationKeys.Reports` maps the existing `reports.*` keys used by the PDF.

## Phase 7 behavior (sessions, attendance, homeworks)

- **Scope**: the `Attendance` shell destination hosts the session list (today /
  all), the take-attendance screen, the QR scanner window and the attendance
  report. The homework report is reached from the attendance screen and hosts
  its add/edit and submissions overlays. Extra/makeup sessions, their payment or
  exemption, the guarded status transitions with the optional SMS suggestion,
  the automatic absence notification, the homework report and the PDF/Excel
  attendance report all ship in this phase. Finance, dashboard, settings,
  notifications center, SMS-template management and login remain later phases.
- **Models**: `Application/Attendance/AttendanceModels.cs` holds
  `SessionListQuery`, `SessionStatusChangeRequest`/`Result`, `AttendanceEntry`,
  `SaveSessionAttendanceRequest`/`Result`, `ExtraSessionRequest`/`Outcome`,
  `SessionPaymentRequest`, `AttendanceRosterItem`, `SessionAttendanceBoard`,
  `AttendanceReportQuery`/`Row`/`Data` and the pure `AttendanceValidator`.
  `Application/Homeworks/HomeworkModels.cs` holds `HomeworkSaveRequest`/
  `Outcome`, `HomeworkSubmissionRequest`, `HomeworkSubmissionBoard`,
  `HomeworkReportQuery`, `HomeworkListItem` and `HomeworkValidator`. The
  Application namespace is `Homeworks` (plural) because the Domain entity type is
  already named `Homework`.
- **Session status policy**: `SessionStatusPolicy` (Domain, pure) is the single
  state machine: `scheduled`, `in_progress`, `completed`, `cancelled`,
  `postponed`. `completed` and `cancelled` are terminal; `postponed` can be
  restored to `scheduled`; an unknown stored status is treated as restorable so
  a future phone build cannot lock a session out. `SuggestsSms` is true only for
  `cancelled` and `postponed`. `SessionStatusPolicy.CanTakeAttendance` gates the
  roster screen, so a closed session is read-only in both the ViewModel and the
  repository path.
- **Attendance list**: `AttendanceViewModel` has two tabs (`SelectedTabIndex` 0 =
  today, 1 = all), a group filter whose first entry means "all groups", a date
  field accepting `yyyy-MM-dd`, `yyyy/MM/dd` and `dd/MM/yyyy`, session
  generation, the cancel/postpone reason overlay, restore/start, the extra
  session overlay (group, date, start/end, type, topic, notes, price) and the
  payment/exemption overlay (paid amount, method, notes). `SessionRow` carries
  the localized text plus `CanTakeAttendance`, `CanCancel`, `CanPostpone`,
  `CanPay`, `IsExempted` and the payment text so the view never re-derives a
  rule. `AttendanceLabels` maps every TEXT status/type/attendance/homework
  status and the seven school-day names to translation keys; unknown values are
  shown as-is. Keyboard shortcuts: `F5` reload, `Ctrl+G` generate.
- **Take attendance**: `TakeAttendanceViewModel` loads the session board
  (`SessionAttendanceBoard` = session + roster + due homeworks), defaults an
  unmarked roster row to `present` (repository and ViewModel agree), and exposes
  per-row `MarkPresent`/`MarkAbsent`/`MarkLate` commands. Status clicks are
  ignored with `attendance.sessionClosed` when the session is not editable, and
  the live present/absent/late/total counters are recomputed by the parent so a
  row never owns a count. Select-all helpers, `Ctrl+S` save, `F9` scan and
  `Ctrl+1/2/3` select-all shortcuts are bound in the view. Saving calls
  `SaveSessionAttendanceUseCase` (guarded once), which upserts every row and then
  sets the session to `completed`.
- **Absence notification**: after the attendance write,
  `AutoNotificationService.CheckAttendanceLimitAsync` runs for the distinct
  students of the save. It reads the absence summary (`system_settings
  .absence_limit`, default 5), creates one in-app notification per student at or
  over the limit, and skips the student when an unread attendance notification
  already exists (`HasUnreadAsync`), so repeated saves cannot spam. It is a
  deliberate exception to the guarded-use-case rule: it runs inside
  `SaveSessionAttendanceUseCase` after the guard already passed, calls
  `INotificationRepository.AddAsync` directly, and a notification failure is
  best-effort (the roster stays saved). The exception is documented in the class
  comment and here.
- **QR scanning**: the payload is `CODE:<studentCode>`; `QrAttendanceCode
  .TryParseInput` also accepts the bare code and tolerates surrounding
  whitespace/case. `QrScanSession` (Desktop, pure) owns the duplicate guard, the
  wrong-group refusal and the malformed-input refusal, and returns the accepted
  student ids. `QrScannerWindow` shows a webcam preview, a manual/USB capture
  field and the accepted list, plays the confirmation/refusal sounds and colours
  the result; the USB reader works because the capture field is focused and
  submits on `Enter`. Webcam frames are captured with `OpenCvSharp4.Windows` and
  decoded through the Application port `IQrCodeDecoder` (Infrastructure:
  ZXing.Net + SkiaSharp), so the window never references a decoder library.
  Cancelling returns null and changes nothing.
- **Extra sessions and payment**: `CreateExtraSessionUseCase` (guarded) writes a
  `sessions` row of type `extra` or `makeup` through
  `ISessionRepository.CreateExtraAsync`, which serializes the insert with a
  static `SemaphoreSlim` and a SQLite transaction; an existing
  `(group, date, start)` returns `ExtraSessionOutcome(existingId, Created:false)`
  instead of a duplicate. `SetSessionPaymentUseCase` (guarded) writes a
  `payments` row of type `extra` with `StudentId = 0` for a paid extra session
  and updates the session's `is_paid`/`payment_status`/`paid_amount`/
  `remaining_amount`; exempting sets `payment_status = exempted` and writes no
  payment row. The price, paid amount and remaining amount are also projected on
  the session row so the list shows `paid`/`unpaid`/`exempt`.
- **SMS suggestion**: `SendSessionSmsUseCase` (guarded) resolves the guardians of
  the session's group, renders the localized template (cancelled, postponed,
  makeup/extra; `sms` is a cancelled alias), writes one `sms_logs` row per
  recipient through `ISmsService` and then asks `ISmsLauncher` (Desktop) to open
  the `sms:` URI with the numbers. There is no gateway and no network call. No
  guardian phone is a failure (`notifications.noPhoneNumber`); a launcher that
  refuses to open is reported (`attendance.smsOpenFailed`). Declining the
  confirmation keeps only the status change.
- **Homeworks**: `HomeworkViewModel` builds the report for the current month and
  the selected group, opens the shared add/edit overlay (group → session
  cascade; a target-session list whose first entry `0`/`homework.noSession`
  normalizes to null) and the submissions overlay with
  `pending`/`submitted`/`late`/`missing` buttons. Each save is a guarded
  `SaveHomeworkUseCase`; each status click is a guarded
  `SetHomeworkSubmissionStatusUseCase`, which resolves "late" with
  `HomeworkPolicy.Resolve(submittedAt, dueDate, today)`: an early late-click is
  stored as `submitted`, a click after the due date as `late`. The submissions
  board is read again after each write so the overlay and the report ratio cannot
  drift. `TakeAttendancePage` lists the homeworks due for the session
  (`GetDueForSessionAsync`).
- **Attendance report**: `AttendanceReportPage` filters by student, group and
  period (an inverted period is `validators.invalidDate` before any query),
  shows per-student session/present/absent/late counters and the attendance
  rate, and exports the same `AttendanceReportData` to PDF
  (`IAttendanceReportService`, Infrastructure QuestPDF) and Excel (ClosedXML).
  Save paths go through `IFileDialogService`; printing goes through
  `IPdfPrintService`, so the ViewModel never touches QuestPDF, ClosedXML or a
  printer. Shortcuts: `Ctrl+P` print, `Ctrl+E` Excel, `F5` reload.
- **Use cases**: reads are `GetSessionListUseCase`, `GetSessionSummaryUseCase`,
  `GetSessionAttendanceBoardUseCase`, `GetDueHomeworksUseCase`,
  `GetSessionGuardiansUseCase`, `GetHomeworkReportUseCase`,
  `GetHomeworkSubmissionBoardUseCase` and `GetAttendanceReportUseCase`. Writes
  are `ChangeSessionStatusUseCase`, `CreateExtraSessionUseCase`,
  `SaveSessionAttendanceUseCase`, `SetSessionPaymentUseCase`,
  `SendSessionSmsUseCase`, `SaveHomeworkUseCase` and
  `SetHomeworkSubmissionStatusUseCase`; every one derives from
  `WriteUseCase<TRequest, TResult>` and carries `[WriteOperation]`.
- **Repositories**: `SessionRepository` adds the filtered session list and
  summary, extra-session insert, status update, payment columns, sessions of one
  group and the session generation inherited from Phase 6.
  `AttendanceRepository` adds the joined roster (left-joined attendance, blank
  read as unset), the bulk upsert by `(session_id, student_id)`, the per-student
  report aggregate and the group statistics. `HomeworkRepository` adds the
  filtered report, the summary, the submission board (group roster left-joined
  to submissions), the due-for-session query and the status upsert. Every
  projection is deliberate and every filter/order is applied to entity columns
  before the projection, because EF cannot translate predicates over projections
  that contain correlated subqueries.
- **Desktop**: `AttendancePage`, `TakeAttendancePage`, `HomeworkReportPage`,
  `AttendanceReportPage` and `QrScannerWindow`; the pages are reached through
  `NavigationService` (`AttendancePage`/`HomeworkReportPage` cached, the
  take-attendance and report pages transient) and the four ViewModels are
  registered with the scanner dialog and SMS launcher. `ShellTab.Attendance = 6`
  is appended so the existing tab numbers do not move.
- **Localization**: every Phase 7 label, column, hint, status, validation
  message and notification body comes from
  `TranslationKeys.{Attendance,Homework,Reports,Common,Validators,Errors,
  Notifications,Dialogs}`; the ar/en/fr key sets stay identical, `reports
  .attendanceReport` is the report title and `attendance.topic` was added for
  the lesson-topic column.

## Design System (Phase D1 - foundation)

- **Scope**: Phase D1 is the visual foundation only. It ships the theme
  dictionaries, the token files, one style per basic control, the shared
  storyboards, the RTL/motion behaviours, a Debug-only design gallery and the
  tests that guard them. No feature screen is redesigned in this phase; the
  Phase 1-7 screens keep their markup and inherit the new palette through the
  legacy aliases.
- **Library**: WPF-UI `4.3.0` (`lepo.co`, Fluent 2 / Windows 11) supplies the
  library-level theme and control defaults and the Win11 backdrop plumbing. It is
  the only UI package; every app style still lives in this repository so the
  identity is not a library theme. `ApplicationThemeManager.Apply(theme,
  WindowBackdropType.None, updateAccent: false)` keeps the library dictionary in
  step with `ThemeService` without letting it repaint the accent. Mica/Acrylic is
  applied per window and falls back to the solid `Surface.Base` brush on Windows
  10 (`WindowBackdropType.None`), so no screen depends on compositor support.
- **Merge order** (`App.xaml`, and the order is asserted by a test):
  `ui:ThemesDictionary` -> `ui:ControlsDictionary` -> `Resources/Themes/Light.xaml`
  (the shim `ThemeService` replaces in place) -> `Design/Base`, `Typography`,
  `Spacing`, `Radii`, `Elevation`, `Icons`, `Animations` -> `Styles/Buttons`,
  `Inputs`, `DataViews`, `Navigation`, `Feedback`. Resource lookup walks merged
  dictionaries last-to-first, so component styles win over the library defaults
  and the tokens win over nothing: a token is never overridden by a style.
- **Theme switching**: `Resources/Themes/{Light,Dark}.xaml` are thin shims that
  merge `Resources/Design/Colors.{Light,Dark}.xaml` through a
  `/SchoolManagement.Desktop;component/...` pack URI. `ThemeService` still finds
  the dictionary whose `Source` contains `Resources/Themes/` and replaces it, so
  the swap path is unchanged and no view is recreated. The shims must never
  contain a colour value of their own.

### Files

| Path | Purpose |
| --- | --- |
| `Resources/Design/Colors.Light.xaml` / `Colors.Dark.xaml` | The palette: `Surface.*`, `Border.*`, `Text.*`, `Accent.*`, semantic, attendance, finance, `Overlay.Scrim`, plus the legacy `Brush.*` aliases. |
| `Resources/Design/Typography.xaml` | `Font.Family*`, `Font.Size.*`, the `Text.*` scale and the legacy text styles. |
| `Resources/Design/Spacing.xaml` | `Space.1..12` (4px grid), control heights, `Nav.RailWidth*`, `Page.MaxWidth`, `Inset.*` thicknesses. |
| `Resources/Design/Radii.xaml` | `Radius.Small/Element/Control/Card/Panel/Pill`. |
| `Resources/Design/Elevation.xaml` | `Shadow.Sm/Md/Lg`, soft and warm, `x:Shared="False"`. |
| `Resources/Design/Icons.xaml` | Segoe Fluent Icons glyph map (Segoe MDL2 Assets fallback) by destination, action, object and status. |
| `Resources/Design/Animations.xaml` | `Ease.*` and the `Anim.*` storyboards, all <= 250 ms. |
| `Resources/Design/Base.xaml` | Implicit `Window`, `TextBlock`, `Label`, `Hyperlink`, `Border`, `Image` styles. |
| `Resources/Styles/Buttons.xaml` | `Button` template + `Button.Primary/Secondary/Subtle/Link/Danger/Icon`, `FocusVisual`, legacy `PrimaryButton/SecondaryButton/LinkButton/IconButton`. |
| `Resources/Styles/Inputs.xaml` | Implicit `TextBox`, `PasswordBox`, `ComboBox`, `ComboBoxItem`, `DatePicker`, `DatePickerTextBox`, `CheckBox`, `RadioButton`, `ToggleButton` (toggle switch). |
| `Resources/Styles/DataViews.xaml` | Implicit `DataGrid` + header/row/cell, `DataGrid.Compact`, `ListView`/`ListViewItem`, `ListBox`/`ListBoxItem`, `ContextMenu`/`MenuItem`, `Separator`, thin `ScrollBar` + `RepeatButton` + `Thumb`. |
| `Resources/Styles/Navigation.xaml` | Implicit `TabControl`/`TabItem`, nav `ListBoxItem`, `NavGlyph`, `NavLabel`, legacy `NavRailItem`. |
| `Resources/Styles/Feedback.xaml` | `ToolTip`, `ProgressBar`, `ProgressRing`, `Card`/`Card.Elevated`, `Divider`, `Badge`, `StatusDot`, `Banner.{Base,Info,Success,Warning,Danger}`, `EditorOverlay`, `EditorCard`, `BoolToVisibility`. |
| `Controls/ProgressRing.cs` | The indeterminate spinner used by the splash and the gallery (the new `ProgressBar` template does not animate `IsIndeterminate`). |
| `Behaviors/MotionBehavior.cs` | `MotionBehavior.Play` (`FadeIn`/`SlideUp`/`ScaleIn`) and `MotionBehavior.HoverLift`. |
| `Behaviors/RtlMirrorBehavior.cs` | Flips a directional glyph when the element is RTL. |
| `Services/MotionSettings.cs` | `IsMotionEnabled` = `SystemParameters.ClientAreaAnimation`. |
| `Views/DesignGalleryWindow.xaml(.cs)` + `ViewModels/DesignGalleryViewModel.cs` | Debug-only gallery: every token and control in one page. |

### Palette

Warm light / deep dark, one teal accent in four steps, plus functional
attendance and finance colours. Contrast was measured before implementation and
is asserted by tests (WCAG AA >= 4.5:1 for normal text).

| Role | Light | Dark |
| --- | --- | --- |
| `Surface.Base` | `#FBF9F5` | `#141C22` |
| `Surface.Card` | `#FFFFFF` | `#1D262F` |
| `Surface.Alt` | `#F4F0E8` | `#25323E` |
| `Surface.Sunken` | `#EFEAE1` | `#101820` |
| `Surface.RowAlt` | `#FCFAF7` | `#202B34` |
| `Surface.Hover` | `#EEF7F4` | `#26333D` |
| `Surface.Selected` | `#D8F3EE` | `#17423E` |
| `Border.Subtle` / `Border.Strong` | `#E8E2D8` / `#D5CBC0` | `#2D3D4B` / `#425769` |
| `Text.Primary` | `#181E1B` | `#F3F7FA` |
| `Text.Secondary` | `#635E58` | `#9AAEC0` |
| `Text.Disabled` | `#A09A93` | `#5A6C7C` |
| `Accent` / `Accent.Hover` / `Accent.Pressed` | `#0F766E` / `#0D655E` / `#0A4F4A` | `#2DD4BF` / `#5EEAD4` / `#14B8A6` |
| `Accent.Container` / `Accent.OnContainer` | `#CCFBF1` / `#0A4F4A` | `#113E3B` / `#5EEAD4` |
| `Success` / `Warning` / `Danger` / `Info` | `#166534` / `#92400E` / `#B91C1C` / `#075985` | `#4ADE80` / `#FBBF24` / `#F87171` / `#38BDF8` |

Measured ratios (the ones a reviewer will ask about): `Text.Primary` on
`Surface.Base` 16.10:1 light / 15.99:1 dark; `Text.Secondary` on `Surface.Base`
6.10:1 / 7.54:1; `Text.Secondary` on `Surface.Selected` 5.49:1 / 4.87:1;
`Text.OnAccent` on `Accent` 5.47:1 / 9.25:1; `Accent` on `Surface.Base` 5.20:1 /
9.25:1; `Danger` on `Surface.Base` 6.15:1 / 6.23:1. `Text.Disabled` is
WCAG-exempt and sits at 2.65:1 / 3.18:1, above the 2.5:1 floor the test
enforces. The dark `Surface.Selected` was darkened from `#1B4A46` to `#17423E`
specifically so `Text.Secondary` on a selected row still reaches AA.

### Typography

- Arabic: **Cairo**, already bundled in `Resources/Fonts`. It is a humanist Kufi
  face designed for UI text and it carries a complete Latin set, so an Arabic
  label and a Latin code or amount share one family and one baseline inside RTL
  tables; that removes the baseline jump a mixed Arabic/Latin pair produces in a
  dense `DataGrid`.
- Latin/numeric: **Segoe UI Variable Text** with a Segoe UI fallback
  (`Font.Family.Latin`), **Segoe UI Variable Display** for the display step
  (`Font.Family.Display`), **Segoe Fluent Icons** with Segoe MDL2 Assets fallback
  (`Font.Family.Icons`). Everything is a system font or a bundled file; nothing
  is downloaded at runtime.
- Fixed scale: Display 32, Title 24, Subtitle 18, Body 14, BodySmall 13,
  Caption 12, Micro 11. `Text.Numeric` uses `Font.Numeric` with tabular figures so
  money and counters align column to column.
- Styles: `Text.Base`, `Text.Display`, `Text.Title`, `Text.Subtitle`,
  `Text.Body`, `Text.BodyStrong`, `Text.Caption`, `Text.Label`, `Text.Numeric`,
  `Text.Numeric.Large`, `Icon.Glyph`. Legacy `PageTitle`, `PageSubtitle`,
  `SectionHeader`, `StatusText`, `FieldLabel` are `BasedOn` the new scale.

### Spacing, radii, elevation

- 4px grid: `Space.N` = N x 4 (`Space.1` = 4 ... `Space.12` = 48). Control height
  36, compact 28, large 44; icon 16; nav rail 240 / 64 compact; page max width
  1440. `Inset.*` gives ready-made `Thickness` values so no screen invents its own
  padding.
- Radii: 4 (chips), 8 (elements and controls), 12 (cards), 16 (panels), 999
  (pill badges and switches).
- Elevation: `Shadow.Sm` (blur 6, depth 1, 6% black), `Shadow.Md` (24 / 4 / 10%),
  `Shadow.Lg` (40 / 8 / 16%). All `x:Shared="False"` because WPF seals an effect
  on first use.

### Icons and motion

- `Icon.*` keys are glyph strings, not image files, so they inherit
  `Foreground` and cost nothing to theme. Directional glyphs (`Icon.Back`,
  `Forward`, `ChevronLeft/Right`, `Undo`, `Redo`, `Import`, `Export`) are the only
  ones that may carry `RtlMirrorBehavior`; symmetric glyphs must never opt in.
- `Anim.FadeIn`, `Anim.SlideUp`, `Anim.ScaleIn`, `Anim.HoverLift.Up/Down`, all
  120-200 ms with a decelerate curve. `MotionBehavior` is opt-in per element,
  animates only opacity and a render transform (never layout), and returns
  immediately when `MotionSettings.IsMotionEnabled` is false, so turning Windows
  "Animation effects" off disables every animation in the app at once. A table row
  must not use `HoverLift`; only cards and tiles may.

### Control coverage

- Buttons: Primary, Secondary, Subtle, Link, Danger, Icon, with hover/pressed/
  disabled/focus states and a visible focus ring. `AutomationProperties.Name` is
  required on every icon-only button.
- Inputs: TextBox, PasswordBox, ComboBox (+ item), DatePicker (+ text box),
  CheckBox, RadioButton, toggle switch, all with the same 8px radius, 36px
  height, focus ring and disabled treatment.
- `DataGrid`: sticky column header (`HeadersVisibility="Column"`, header height
  40), alternating rows via `Surface.RowAlt`, full-row selection, horizontal
  grid lines only, `IsReadOnly` and `CanUserResizeRows=False` by default,
  `EnableRowVirtualization` + `VirtualizingPanel.IsVirtualizing` +
  `VirtualizationMode="Recycling"` + `ScrollUnit="Pixel"`. Comfortable density is
  40/40; `DataGrid.Compact` is 32/34 and is opted into per screen.
- `ListView`/`ListBox` virtualize the same way. `ScrollBar` is thin (10px) with a
  rounded thumb. `TabControl`/`TabItem`, nav `ListBoxItem`, `ToolTip`,
  `ContextMenu`/`MenuItem`, `ProgressBar`, `ProgressRing`, `Card`, `Divider`,
  `Badge`, `StatusDot`, `Banner.*`, `EditorOverlay`/`EditorCard` and the
  `BoolToVisibility` converter are all defined once.

### Compatibility and rules

- The legacy `Brush.*` keys and the legacy style keys (`PrimaryButton`,
  `SecondaryButton`, `LinkButton`, `IconButton`, `PageTitle`, `PageSubtitle`,
  `SectionHeader`, `StatusText`, `Card`, `EditorOverlay`, `EditorCard`,
  `FieldLabel`, `NavRailItem`, `NavGlyph`, `NavLabel`, `BoolToVisibility`,
  `Font.Family`) still resolve. Phases 1-7 compile and render unchanged, and a
  test asserts every alias still mirrors its semantic token.
- `Resources/Styles.xaml` (the pre-D1 monolith) was deleted; its unique implicit
  styles moved to `Design/Base.xaml` and its `ListBox`/`ListBoxItem` styles to
  `Styles/DataViews.xaml`. No test referenced the file.
- Every screen colour must be `DynamicResource`; a literal hex in a view is a
  test failure. A `DynamicResource` key that no dictionary defines is also a test
  failure, because it resolves to nothing at runtime.
- Light and dark define exactly the same key set. A key added to one theme only
  is a test failure.
- The design gallery is Debug-only: its DI registrations sit inside `#if DEBUG`
  in `DesktopServiceCollectionExtensions` and the `F12` hook in
  `ShellWindow.xaml.cs` sits inside `#if DEBUG`, and a test asserts both. It
  shows every token and control in the current theme and direction, and its two
  switches preview the other theme and the other direction inside the window
  without changing the running app's language. Phase D2 added global shortcuts
  to the same `OnPreviewKeyDown`, so the test now scopes the assertion to the
  `F12` hook instead of the whole method.
- Gallery labels use `Tag` + a `RelativeSource` binding (or `{loc:Tr ...}`), never
  a literal `Text=` attribute, so the hardcoded-text translation test still
  passes.
- New strings go through `TranslationKeys.Design` and `design.*` in ar/en/fr
  (56 constants; the three JSON files carry the same key set - 1213 keys each
  after Phase D2 - and stay identical). `design.` keys with no constant, or a
  constant with no key, fail the suite.

## Design System (Phase D2 - shell and navigation)

- **Scope**: Phase D2 redesigns the shell chrome only - window frame, navigation,
  top bar, command palette, transitions, toasts, license banner and the shortcut
  help window. The Phase 1-7 feature pages keep their markup and inherit the new
  palette, typography and control styles. No Domain/Application/Infrastructure
  file, use case, schema or licensing rule changed.
- **Window**: `ShellWindow` derives from `ui:FluentWindow` with
  `WindowBackdropType="Mica"` and `ExtendsContentIntoTitleBar="True"`. Windows 11
  gets the Mica backdrop; Windows 10 gets the solid `Surface.Base` fallback,
  because WPF-UI reports `WindowBackdropType.None` when composition is not
  available. The custom title bar hosts the app mark (`Icon.School` on
  `Accent.Container`), the school name (`reference.schoolName`, falling back to
  `shell.schoolNameFallback`) and the library window buttons. Minimum size stays
  1000x650 and every window sets `UseLayoutRounding`.
- **Window placement**: `Services/WindowPlacement.cs` is a pure `Clamp` (virtual
  desktop bounds, >= 80 units of the title bar stay reachable, size capped to the
  desktop, minimum 1000x650 enforced, NaN/infinity fall back to the default).
  `WindowStateStore` persists size, position and maximized state to
  `%LOCALAPPDATA%\SchoolManagement\window.json` with an atomic temp-then-replace
  write; `App` calls `InitializePlacementAsync()` before `Show()`, and the window
  saves on closing. A saved position on an unplugged monitor is clamped back on
  screen, including negative multi-monitor origins.
- **Navigation rail**: 240 px expanded (`ShellViewModel.ExpandedNavWidth`) and
  64 px compact (`CompactNavWidth`), animated by a `GridLength` column. It
  auto-collapses below 1200 px (`AutoCollapseWidth`) and remembers whether the
  user had expanded it manually; `Ctrl+B` / the rail button toggles the visible
  state, so the first click after an automatic collapse expands. Five groups in
  fixed order, independent of language: Home (Dashboard), Academic (Students,
  Groups, Academics, Attendance, Homework), Finance, Communication
  (Notifications, Messages), System (Settings) - 10 items, counts 1/5/1/2/1.
  `Academics` (Phase 4) deliberately stays in the Academic group so an existing
  screen does not become unreachable. The active item uses `Surface.Selected`,
  an accent bar on the leading edge (mirrored in RTL) and the accent glyph; the
  Notifications item carries the unread count from
  `INotificationRepository.CountUnreadAsync` as a pill badge, refreshed on
  navigation and window focus. The footer shows a mini license status dot with
  its state text, the theme toggle and the language switch (AR/FR/EN badge).
- **Top bar**: back button (only when a sub-page can be popped), breadcrumb
  `group > page`, the page title, a contextual action host and the global search
  button. The search button shows `shell.search` plus a `KeyCap` with
  `shortcuts.keysSearch`; the help button opens the shortcuts window (`F1`).
- **Command palette**: `CommandPaletteViewModel` opens from `Ctrl+K` or the
  search button. It always lists eight commands (add student, new group, new
  homework, take attendance, record payment, open settings, toggle theme,
  keyboard shortcuts) and, once the query has at least two characters, merges
  students and groups through the existing `GetStudentsUseCase` /
  `GetGroupsUseCase` (no new repository method, no payment search). Up/Down move
  the selection with wrap-around, Enter runs it, Esc closes; a stale response is
  discarded. Running a command closes the palette and navigates or raises the
  shell-owned event (theme, shortcuts).
- **Page transitions and state**: `ShellWindow` materializes one page per
  ViewModel and caches it in `_pages`, so scroll position, filters and inline
  editors survive navigation and language/theme changes. The transition is
  Fade + 12 px slide (180 ms, `Anim.FadeIn`) and is skipped entirely when
  `MotionSettings.IsMotionEnabled` is false.
- **Toasts**: `IToastService`/`ToastService` + `ToastHostView` render
  success/error/warning/info cards in the bottom-trailing corner with a close
  button, a 4-second auto-dismiss and a four-toast cap, so a burst cannot grow
  the visual tree. `IUserDialogService` is unchanged; `ConfirmationWindow` is
  restyled as a `FluentWindow`, so every existing confirmation and information
  call gets the new look without a ViewModel change.
- **License banner**: `LicenseBannerView` is a card above the page host, never
  over it. `ExpiringSoon` is Info, `GracePeriod` is Warning, `ReadOnly` is Danger
  and cannot be dismissed, `NotStarted` is Warning, and `ClockTampered` wins over
  every state as Danger. Dismiss uses `license.dismiss`; a new `Info` restores a
  dismissed banner. Renew still calls the existing activation dialog.
- **Shortcuts**: `ShortcutsWindow` (F1) lists navigation, action and general
  shortcuts; the keycaps come from `shortcuts.keys*` so the physical key names
  follow the keyboard layout (English `Esc`, French `Échap`). Global map: F1
  help, `Ctrl+K` or `Ctrl+F` palette/global search, `Ctrl+N` new, `Ctrl+S` save,
  `Ctrl+P` print, `Ctrl+B` nav toggle, `Alt+Left` back, `Alt+1..5` first
  destination of each group (Dashboard, Students, Finance, Notifications,
  Settings), Esc closes the top overlay. Save and print stay page-owned.
- **Destinations**: `ShellTab` is `Dashboard=0`, `Students=1`, `Groups=2`,
  `Academics=3`, `Finance=4`, `Settings=5`, `Attendance=6`, `Homework=7`,
  `Notifications=8`, `Messages=9`; no earlier number moved.
  `NavigationService.ViewModelTypeFor(ShellTab)` exposes the mapping to the
  palette and the tests.
- **Placeholders**: Notifications and Messages are Desktop-only pages that show
  the localized `shell.notificationsComingSoon` / `shell.messagesComingSoon`
  text. They exist so the Communication group is complete; their real screens
  remain later work.

### Files (Phase D2)

| Path | Purpose |
| --- | --- |
| `Views/ShellWindow.xaml(.cs)` | FluentWindow shell: title bar, nav rail host, top bar, banner, cached page host, overlays, transitions, placement, global key map, Debug `F12` gallery. |
| `Views/NavRailView.xaml(.cs)` | Grouped collapsible rail: captions, active bar, unread badge, footer (license, theme, language). |
| `Views/CommandPaletteView.xaml(.cs)` | `Ctrl+K` overlay: query box, result list, keyboard navigation. |
| `Views/ToastHostView.xaml(.cs)` | Toast stack in the bottom-trailing corner. |
| `Views/ShortcutsWindow.xaml(.cs)` | F1 shortcut reference, grouped with keycaps. |
| `Views/NotificationsPage.xaml(.cs)` / `Views/MessagesPage.xaml(.cs)` | Localized coming-soon placeholders for the Communication group. |
| `Views/LicenseBannerView.xaml` | Per-severity banner with Renew and Dismiss. |
| `Views/ConfirmationWindow.xaml(.cs)` | Shared confirm/info dialog restyled as `FluentWindow`, API unchanged. |
| `ViewModels/ShellViewModel.cs` | Groups/items, collapse state, width threshold, shortcuts, breadcrumb, badge, theme/language, title bar. |
| `ViewModels/NavigationItemViewModel.cs` | One rail item: tab, group, label, glyph, badge. |
| `ViewModels/CommandPaletteViewModel.cs` | Palette state, merged results, command list, search. |
| `ViewModels/ToastItem.cs` / `ViewModels/NotificationsViewModel.cs` / `ViewModels/MessagesViewModel.cs` | Toast model and the two placeholders. |
| `Services/WindowPlacement.cs` / `IWindowStateStore.cs` / `WindowStateStore.cs` | Pure clamp and the atomic `window.json` store. |
| `Services/IToastService.cs` / `ToastService.cs` / `ShortcutAction.cs` | Toast port/implementation and the shell shortcut enum. |
| `Resources/Styles/Navigation.xaml` | Adds `NavGroupCaption`, `NavBadge`, `NavBadgeText`, `ShellSearchButton`, `KeyCap`, `KeyCapText`. |
| `Resources/Design/Typography.xaml` | Adds the missing `Text.BodySmall` step (13 px, line height 18). |
| `Resources/Design/Icons.xaml` | Adds `Icon.ThemeLight`, `Icon.ThemeDark`, `Icon.NavCollapse`, `Icon.NavExpand`, `Icon.Globe`, `Icon.School`. |

### Rules added in D2

- The shell never re-creates a page for navigation, language or theme. Pages
  are cached per ViewModel; only the transient detail pages keep their existing
  lifetime.
- `Academics` must stay in the rail while it owns a Phase 4 screen. The group
  order is fixed data, not a localized list.
- `IUserDialogService` keeps its signatures; new user feedback goes through
  `IToastService`.
- A `StaticResource` key that no dictionary defines is now a test failure, like
  a missing `DynamicResource`. `Text.BodySmall` was exactly that bug and is the
  reason the check exists.
- Window geometry lives in `window.json`, never in `settings.json`, so the
  existing settings contract stays language/theme only.

## Design System (Phase D3 - reusable components)

- **Scope**: Phase D3 adds the component library the screen redesigns build on.
  Every control lives in `SchoolManagement.Desktop/Controls`, is theme/RTL/DPI
  aware, exposes dependency properties, has one live sample in the Debug design
  gallery, and is covered by tests. No Domain/Application/Infrastructure file,
  use case, schema or licensing rule changed.
- **Package**: `LiveChartsCore.SkiaSharpView.WPF` **2.0.0-rc3.3** is the only
  package added. It pulls `SkiaSharp.Views.WPF` 2.88.8, which matches the
  `SkiaSharp` 2.88.8 already used by Infrastructure, so the process loads one
  native Skia build. It is used only by `ChartCard`; every other component is
  plain WPF, so a chart failure cannot take a screen down.
- **Shape of a component**: a `UserControl` with a `x:Name="Root"` and internal
  bindings written as `{Binding ..., ElementName=Root}`. A `UserControl.Content`
  is already occupied by its template, so every slot is a named dependency
  property (`PageHeader.ActionsContent`, `FormField.FieldContent`,
  `FormSection.SectionContent`, `SidePanel.PanelContent`/`ActionsContent`,
  `Dialog.DialogContent`, `DataTable.ToolbarContent`/`BulkActionsContent`,
  `ChartCard.ActionsContent`, `WeekScheduleGrid.ActionsContent`). A screen sets
  `<controls:FormField.FieldContent>`; it must never set `Content`.
- **Colour contract**: a component never owns a colour. It names a semantic
  token through `ToneResources.For(StatusTone)` (four keys: `Background`,
  `Border`, `Foreground`, `Solid`) and applies it with `SetResourceReference`,
  so the element keeps following the live theme. `StatusTone` is the shared
  vocabulary: Neutral, Accent, Success, Warning, Danger, Info.
- **Shared styles**: `Resources/Styles/Components.xaml` is merged last (after
  `Feedback.xaml`) and defines `Component.Card`, `Component.DetailPanel`,
  `Component.ScheduleSlot`, `Component.QrPaper`, the section/heading styles and
  the shared converters (`StringToVisibility`, `InverseStringToVisibility`,
  `ObjectToVisibility`, `NullToVisibility`, `InverseBoolToVisibility`,
  `InverseBool`, `FlowDirectionToAlignment`, `CountFormat`).
- **Charts and theme switching**: `ChartCard` hands LiveCharts2 a concrete
  `SKColor`, so a `DynamicResource` cannot repaint it. `ThemeService
  .SetCurrentTheme` raises the static `ThemeMonitor.ThemeChanged` pulse and
  `ChartCard` rebuilds its series from the new palette; it unsubscribes on
  `Unloaded`, so a closed page cannot leak. Every custom-drawn control
  (`Sparkline`, `ProgressRing`) still paints from a `DynamicResource` brush.

### Component catalogue

| Component | Size and colour |
| --- | --- |
| `PageHeader` | Breadcrumb `Text.Caption` (12 px) in `Text.Secondary`, title `Text.Title` (24 px), description `Text.Body` (14 px), trailing `ActionsContent`; 4 px between rows, actions mirrored by `FlowDirection`. |
| `StatCard` | 36x36 icon tile with `Radius.Element` (8) on `Accent.Container` / `Accent.OnContainer`; value `Text.Display` (32 px) with tabular figures; trend glyph 12 px in `Success` (Up), `Danger` (Down) or `Text.Secondary` (Flat); `Sparkline` 32 px high, stroke from the card tone. |
| `DataTable` | Toolbar (search max 320 px, density toggle, column chooser, refresh, export, add), 56 px header with the filter row, `SelectionMode=Extended`, row virtualization + recycling, `DataGrid.Compact` at 32/34, pager (`PageSize` 25, shown only when `PageCount > 1`), 340 px detail panel, bulk bar, skeleton while `IsBusy`, empty state. |
| `EmptyState` | 96x96 vector illustration in a `Viewbox` (two `Surface.Alt`/`Border.Subtle` ellipses + a 48 px accent glyph), title `Text.Subtitle`, description `Text.Body`, one action button; max width 420. |
| `ErrorState` | `Danger.Container` card with `Radius.Card` and `Inset.Card`, 28 px `Danger` glyph, message and a retry button. |
| `SkeletonLoader` | 14 px bars on `Surface.Alt` with `Radius.Small`, 12 px spacing, six-step width pattern (100/82/92/74/88/68 %), one 1200 ms shimmer translate; no animation when `MotionSettings.IsMotionEnabled` is false. |
| `StatusBadge` | Pill (`Radius.Pill`), padding 8,2, 11 px text, optional 6 px dot or 12 px glyph; every entity status maps through `StatusBadgeMapper`. |
| `Chip` | Pill, 11 px text, optional 18 px glyph, optional 18 px close button that runs `CloseCommand`; used for tags and filters. |
| `FormField` | Label `Text.BodyStrong` + translated `*` whose tooltip is `common.required`, `FieldContent`, 4 px description, 4 px error in `Danger`; the error row collapses when `ErrorText` is blank. |
| `FormSection` | Title `Text.Subtitle`, description `Text.BodySmall`, divider, then `SectionContent`. |
| `Wizard` | Numbered step pills (accent for the current step, `Surface.Alt` for the rest), `Steps`, `CurrentIndex`, Back/Next/Finish, `ShowNavigation`; `FinishCommand` runs on the last step. |
| `SidePanel` | Trailing drawer (right in LTR, left in RTL), `PanelWidth` 420, `Overlay.Scrim`, 180 ms slide skipped when motion is off; `ShowScrim=false` gives a docked panel. |
| `Dialog` | 480 px card, max height 640, `Radius.Card`, tone-coloured header glyph, primary/secondary buttons, 32 px close button, scrim click closes. |
| `CapacityBar` | 6 px bar, `Success` below 80 %, `Warning` at >= 80 %, `Danger` at >= 100 %; `CapacityText` is the invariant `current/maximum` label; never blocks anything. |
| `ProgressStat` | `ProgressRing` + percentage, `Tone` picks the ring token, `DisplayText` is the secondary line. |
| `Avatar` | Circle at `Size` (default 40), initials derived from the first two words, stable tone from a name hash (`AutoTone`), or an `ImageSource` photo. |
| `RatingDots` | 12 px dots, `Maximum` clamped to 1..20, click sets `Value` and mirrors in RTL, `IsReadOnly` disables the hand cursor. |
| `DateRangePicker` | Two `DatePicker`s with localized start/end labels and a direction-neutral arrow; `DateTime?` in UTC. |
| `MonthPicker` | `YYYY-MM` with twelve caller-supplied month names (from `TranslationKeys.Months`), `StartYear`/`EndYear` default now +/-5, `SelectedMonth` is UTC first-of-month. `MonthYearParser` owns parse/format. |
| `SearchBox` | 36 px, `Radius.Control`, magnifier glyph, instant two-way `Text`, clear button, Enter runs `SearchCommand`. |
| `TagInput` | Pills in a 36 px surface, Enter adds, duplicates are ignored case-insensitively, `MaxTags` default 10, each pill has a remove button. |
| `WeekScheduleGrid` | Seven day columns (`DayLabels`, 0 = Saturday ... 6 = Friday) x `StartHour..EndHour` at `HourHeight`, `IsCompact` reduces the labels and the header, `IsEditable` enables drag-and-drop and raises `SlotMoved`, `WeekScheduleConflictPolicy` marks same-day half-open overlaps of the same `ResourceKey` in `Danger`; empty text and a conflict legend come from `components.*`. |
| `QrCardPreview` | 320x420 paper (`Component.QrPaper`) inside a `Viewbox`, so a 300x400 host scales it down without clipping; title, subtitle, QR image, monospace code and footer. |
| `ChartCard` | LiveCharts2 wrapper for `Line`/`Area`/`Bar`, chart area min height 160, 32 px summary, tone colours, `ShowEmptyState` + `EmptyText`, `ActionsContent`. |

### Pure logic (tested without a message loop)

- `DataTableFilter` - `Matches` (global search over visible columns + per-column
  filters, `OrdinalIgnoreCase`), `GetValue` (cached, case-insensitive, nested
  paths), `Format` (UTC dates as `yyyy-MM-dd`) and `Compare` (nulls first,
  culture-aware case-insensitive strings, numeric otherwise).
- `StatusBadgeMapper.MapTone` - normalizes case, spaces, `-` and `_`, then maps
  the attendance/finance/session/license/homework vocabulary to a `StatusTone`.
- `WeekScheduleConflictPolicy` - half-open overlap (`Start < other.End &&
  End > other.Start`), same day, same or blank `ResourceKey`; touching ranges
  never conflict; a stale flag is cleared on every pass.
- `MonthYearParser` - `yyyy-MM`, `yyyy/MM`, `yyyy-M`, `yyyy/M`; refuses anything
  else; returns a UTC first-of-month date.
- `ToneResources` - the only bridge between a tone and a palette key.

### Gallery and tests

- The Debug gallery has one live sample of every component (named
  `HeaderSample`, `TableSample`, `PanelSample`, `DialogSample`, ...), plus the
  overlay launchers, a deliberate same-teacher overlap in the schedule and
  Line/Area/Bar charts. `DesignGalleryWindowTests` resolves each sample by name,
  so a deleted component fails the suite.
- `ComponentTests` covers the pure helpers, every `StatusTone` palette key in
  both themes, the converters, the `components.*`/`months.*` translation parity
  and a reflection check over the public dependency-property surface.
- `DesignResourceTests` now scans `Controls/` as well as `Views/` for literal
  hex, unresolved `DynamicResource` keys and unresolved `StaticResource` keys,
  and asserts `Components.xaml` is merged after `Feedback.xaml`.

### Files (Phase D3)

| Path | Purpose |
| --- | --- |
| `Controls/ComponentModels.cs` | `StatusTone`, `TrendDirection`, `DataTableDensity`, `ChartKind`, `DataTableColumn`, `DataTableFilter`, `StatusBadgeMapper`, `WeekScheduleItem`, `WeekScheduleConflictPolicy`, `MonthYearParser`, `WizardStep`, `RatingDot`, `MonthOption`, `YearOption`. |
| `Controls/ComponentConverters.cs` | The eight shared converters. |
| `Controls/ToneResources.cs` | `TonePalette` + `ToneResources.For`. |
| `Controls/PageHeader.xaml(.cs)` | Breadcrumb/title/description/actions header. |
| `Controls/StatCard.xaml(.cs)` + `Controls/Sparkline.cs` | KPI card and the dependency-free sparkline. |
| `Controls/DataTable.xaml(.cs)` | The full list surface (search, filters, chooser, density, bulk, paging, detail, context menu). |
| `Controls/EmptyState.xaml(.cs)` / `ErrorState.xaml(.cs)` / `SkeletonLoader.xaml(.cs)` | The three page states. |
| `Controls/StatusBadge.xaml(.cs)` / `Chip.xaml(.cs)` | Status pills and tags. |
| `Controls/FormField.xaml(.cs)` / `FormSection.xaml(.cs)` / `Wizard.xaml(.cs)` | Form primitives and the stepper. |
| `Controls/SidePanel.xaml(.cs)` / `Dialog.xaml(.cs)` | Drawer and modal. |
| `Controls/CapacityBar.xaml(.cs)` / `ProgressStat.xaml(.cs)` / `Avatar.xaml(.cs)` / `RatingDots.xaml(.cs)` | Indicators. |
| `Controls/DateRangePicker.xaml(.cs)` / `MonthPicker.xaml(.cs)` / `SearchBox.xaml(.cs)` / `TagInput.xaml(.cs)` | Inputs. |
| `Controls/WeekScheduleGrid.xaml(.cs)` | Weekly grid, compact variant, drag, conflicts. |
| `Controls/QrCardPreview.xaml(.cs)` | Viewbox-scaled student QR card. |
| `Controls/ChartCard.xaml(.cs)` | LiveCharts2 wrapper with a theme pulse. |
| `Services/ThemeMonitor.cs` | Static theme-changed event raised by `ThemeService`. |
| `Resources/Styles/Components.xaml` | Component styles + converter keys, merged after `Feedback.xaml`. |
| `Resources/Design/Icons.xaml` | D3 glyphs (`Icon.Columns`, `Icon.Density`, `Icon.Chart*`, `Icon.Clear`, `Icon.Qr`, `Icon.Trend*`, ...). |
| `Views/DesignGalleryWindow.xaml(.cs)` + `ViewModels/DesignGalleryViewModel.cs` | Every component sample and its overlay/state. |

### Rules added in D3

- A component takes content through a named dependency property, never through
  `Content`; `UserControl.Content` belongs to the template.
- A component names a semantic tone or a `DynamicResource` token; a literal
  colour in `Controls/` is a test failure. The only non-XAML colours are the
  `Sparkline`/`ProgressRing` defaults and the LiveCharts `SKColor` built from
  the palette at runtime.
- A new status string goes through `StatusBadgeMapper`, never through an ad-hoc
  colour in a screen.
- Table filtering and sorting stay in `DataTableFilter` / `ListCollectionView`;
  a screen supplies columns and data, not filtering code.
- The weekly grid keeps `0 = Saturday ... 6 = Friday`, and conflicts are
  warnings: `SlotMoved` lets the caller decide whether to persist.
- A new component ships with a gallery sample, a named `x:Name`, and a line in
  `ComponentTests` (pure logic and/or dependency-property surface).
- Every new string goes through `TranslationKeys.Components`/`Months` (or the
  owning entity section) and all three JSON files; the parity tests enforce it.

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
- Write operations must derive from `WriteUseCase<T>` (or call
  `ILicenseGuard.EnsureCanWrite()`) and be marked `[WriteOperation]`; the base
  template runs the guard exactly once before the concrete operation.
- License validity logic must not contain `DateTime.Now`/`DateTime.UtcNow`; use
  `ITrustedClock`. `SystemLocalClock` is the only place that reads the raw clock.
  A source-scanning test enforces both rules.
- Never commit private key material. `.gitignore` covers `*.key`, `*.pem`,
  `*.pfx`, `*.p12`, `.secrets/`, `license_private.key`, `license.dat` and
  `trusted_time.dat`.
- The desktop minimum window size is 1000x650.
- Money maps to `double` (SQLite REAL) to match the original phone schema.
- Statuses remain TEXT strings (not enums) to tolerate unknown values.
- No EF Core migrations redefine the original schema; `DatabaseInitializer` owns
  schema creation/upgrade.
- Student codes are generated by `StudentCodeGenerator` inside the repository
  transaction only when the request has none, and are never rewritten on update.
  Never build one by hand in a ViewModel or a use case.
- Phones are normalized with `AlgeriaPhone.Normalize` in the use case, never in
  the UI; invalid values are preserved so validation can report them.
- File formats, PDF and QR rendering are application ports
  (`IStudentFileGateway`, `IStudentCardService`). Domain/Application never
  reference ClosedXML, QuestPDF, ZXing.Net or SkiaSharp; Desktop never formats a
  student file itself.
- WPF dialogs, printing and image decoding sit behind `IFileDialogService`,
  `IPdfPrintService` and `BitmapHelper`, so ViewModels stay testable off the UI
  thread.
- Capacity and weekly-schedule conflicts never block a write: `GroupCapacityPolicy`
  warns at `ActiveEnrollments >= MaxStudents` and `ScheduleConflictPolicy`
  returns overlaps as `SameGroup`/`SameTeacher` warnings; the ViewModel asks for
  a confirmation and only then runs the guarded write.
- Weekly slots store a time-of-day on the fixed reference date `2000-01-01` so
  the phone `schedule_templates` schema and the Unix-seconds converter keep
  working; never store a real calendar date in a template.
- Sessions are generated only through `GenerateSessionsForDate`, which returns
  `{createdCount, alreadyExisted}`, never duplicates a
  (group, date, start-time) row and combines the template time with the date via
  `SessionSchedulePolicy.Combine`.
- An enrollment is unique per (group, student) while it is `active`; a
  `withdrawn` row may be re-created, and withdraw only changes the status.
- EF cannot translate predicates or orderings over a projection that contains
  correlated subqueries: filter and order on entity columns first, then
  `Project(...)`, then materialize. Group name search uses `EF.Functions.Like`
  (case-insensitive ASCII in SQLite), not `string.Contains`.
- Session status changes go through the pure `SessionStatusPolicy` state machine
  (`scheduled`, `in_progress`, `completed`, `cancelled`, `postponed`); never
  compare status strings in a ViewModel. `completed`/`cancelled` are terminal and
  `CanTakeAttendance` is the single read-only gate for the roster screen.
- An unmarked attendance row defaults to `present` in both the repository and
  the ViewModel; keep the two defaults in sync. Batch attendance saves upsert by
  `(session_id, student_id)` and set the session to `completed` in one guarded
  write.
- `AutoNotificationService` is the documented exception to the guarded-use-case
  rule: it runs inside an already-guarded attendance write, may call
  `INotificationRepository` directly, and must never duplicate an unread absence
  notification (`HasUnreadAsync`). A notification failure never fails the roster
  save.
- QR attendance payloads are `CODE:<studentCode>`, but `QrAttendanceCode
  .TryParseInput` also accepts the bare code and ignores surrounding whitespace
  and case. Duplicate, wrong-group and malformed refusals live in the pure
  `QrScanSession`, never in the WPF window. Webcam capture is Desktop-only
  (`OpenCvSharp4.Windows`); decoding sits behind the `IQrCodeDecoder` port.
- Extra/makeup sessions are created only through `CreateExtraSessionUseCase` /
  `ISessionRepository.CreateExtraAsync`, which serializes inserts and returns the
  existing id instead of duplicating `(group, date, start)`. A paid extra session
  writes a `payments` row of type `extra` with `StudentId = 0`; an exemption
  writes no payment row and only sets `payment_status = exempted`.
- SMS never leaves the machine: `SendSessionSmsUseCase` logs one `sms_logs` row
  per guardian through `ISmsService` and then opens the `sms:` URI through
  `ISmsLauncher`. No gateway, no network call; no phone number is a `Failure`
  (`notifications.noPhoneNumber`).
- `Homeworks` is the Application namespace (plural) because the Domain entity is
  named `Homework`. A submission marked "late" is resolved by the pure
  `HomeworkPolicy.Resolve(submittedAt, dueDate, today)`, never by the ViewModel.
- Report/PDF/Excel export stays behind ports (`IAttendanceReportService`,
  `IFileDialogService`, `IPdfPrintService`); the ViewModel passes the same
  `AttendanceReportData` to print and to save so the outputs cannot drift.

## Test coverage (Phases 1-7)

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
- **Phase 3 (licensing)**: exact canonical JSON bytes and Dart-compatible
  escaping/ordering, padded base64url encode/decode, fixed tool vectors that
  reproduce a complete license string, signature tampering (payload, signature,
  foreign key, truncation), verify-before-parse ordering, version policy
  (v1/v2/v3/v0), `nbf > exp`, unknown `kid`, fingerprint mismatch, unconfigured
  key, all seven states at every boundary (last active day, warning window,
  expiry day, grace first/last day, read-only day, `nbf` day), grace countdown
  and read-only date, banner keys/parameters, `CanWrite`/`RequiresActivation`
  matrices, trusted-time rollback before and after activation, 5-minute
  tolerance, anchor one-day grace, time-zone shifts forward/behind, persisted
  tamper flag and recovery, corrupt pre-2000 values, full activation through the
  real repository with fake fingerprint/storage, rejected activation never
  deleting or replacing a license, DPAPI storage failure handling, guard matrix,
  repository short-circuit and reflection checks over every `[WriteOperation]`,
  a source scan forbidding `DateTime.Now`/`UtcNow` in license code, and checks
  that the issuer tool is not referenced and no key files are tracked.
- **Phase 4 (academic structure)**: validator rules (required/length/order,
  optional code and contact patterns, blank normalization); every one of the 13
  academic writes stops at the license gate without touching its repository for
  a blocked write and runs the guard exactly once when allowed; validation
  short-circuits before the repository; update reports `NotFound` for a missing
  row; trimmed names/optional fields and case-insensitive subject code
  uniqueness (own id excluded); grade-subject assignment drops blank/duplicate
  ids and propagates repository failures; delete-blocked counts for levels,
  grades, subjects and teachers. Real SQLite tests cover the deliberate
  projections, search/sort/include-inactive, correlated linked counts,
  case-insensitive `CodeExistsAsync`, subject-id round-trip, and the
  transactional grade delete that removes its `grade_subjects` rows. ViewModel
  tests cover default search/sort query, load-once, read errors without the renew
  button, add/edit/delete flows, declined confirmation, counted delete blocks,
  license-blocked writes showing renew, duplicate subject code, teacher field
  mapping, grade level options and subject linking, the lazy tab host, and
  language-switch refresh of sort labels and the open editor title. Static XAML
  tests assert every `...Command` binding exists on its ViewModel (they caught
  `SaveCommand`/`CloseEditorCommand`) and that all four views keep the
  F2/Delete/Enter/Escape shortcuts.
- **Phase 5 (students)**: `StudentCodeGenerator` format/padding, year and
  sequence bounds, round-trip parsing, case/whitespace tolerance, per-year next
  sequence, exhaustion and null input; validator rules (required/length, code
  charset and length, UTC and plausible birth date, case-insensitive gender,
  stored status, negative fee, text lengths, guardian name requirement, email
  and phone patterns) plus every `AlgeriaPhone` normalization/validity/rejection
  case. Use-case tests prove the license gate runs first for save, delete and
  import (repository/gateway untouched, guard exactly once), validation
  short-circuits before the repository, duplicate codes are rejected before the
  write, code-lookup failures propagate, requests are trimmed and phones
  normalized, delete reports counted links, and import keeps one error per bad
  row while saving the valid ones (gateway parse errors, save failures, empty
  file and gateway failures included). Export tests cover rows, file name,
  empty database and write failure; card/QR use cases cover missing students and
  the student code as QR payload. Real SQLite tests cover
  search/filter/sort/paging/count, the card and export projections with
  guardian and placement, case-insensitive `CodeExistsAsync`, the transactional
  student+guardian save, generated sequential codes, `NotFound` on update, 12
  concurrent saves with unique codes, dependency counts and joined details.
  `StudentFileGateway` tests round-trip CSV and XLSX, assert the BOM/headers/
  quoting and headerless fallback, per-row date/number errors, skipped blank
  lines, empty files, a corrupt workbook returning a failure and the default
  extensions. ViewModel tests cover the default query, load-once, filter query,
  read error without renew, empty state, add/save, license-blocked save keeping
  the form open, edit loading the details, declined confirmation, confirmed
  delete with its status message, counted delete block, import report, export
  path and message, language-switch labels, navigation to the details page,
  the three detail tabs with the absence summary, missing student error, details
  edit opening the shared form, card preview error/success/print, QR tiles,
  empty print, save and close. Static XAML tests assert that every
  `...Command` binding exists on the five Phase 5 ViewModels (they caught
  `OpenAttendanceCommand`), that the list keeps F2/Delete/Enter/Escape and that
  the bulk page derives its size from a `Viewbox` + `UniformGrid`.
- **Phase 6 (groups, enrollment, schedule, sessions, timetable, report)**:
  `ScheduleConflictPolicy` half-open overlap boundaries (touching ranges do not
  overlap), same-group vs same-teacher detection, self-slot exclusion, null
  teacher, other weekday, invalid day/range; `SessionSchedulePolicy.Combine`
  and invalid ranges; `GroupSummary` capacity/`HasSchedule`/`CapacityText`;
  `GroupAttendanceStats.AttendanceRate`; `SessionGenerationResult.Total`;
  `GroupDependencies.HasLinks`; the full `GroupValidator` (`name`
  required/max, grade and subject required, capacity `0`/negative/`>500` and
  the `500` boundary, negative fee, normalization) and `ScheduleValidator`/
  `ScheduleTimeParser` (day/range/location/normalization, parse/format).
  Use-case tests prove every one of the seven Phase 6 writes runs the license
  gate first, exactly once, and never touches its repository for a blocked
  write; validation short-circuits before the repository; delete reports the
  counted links; withdraw reports `NotFound` and only changes the status;
  generation validates the date and honours the group filter; lookups, the
  grade→subject cascade, details joins, conflicts classification, timetable
  filters, enrollment availability, report joins/outstanding totals and a
  failing report port propagate their failures. Real SQLite tests cover the
  deliberate group projections, search across every joined name (including the
  level), include-inactive, every sort column both ways, grade-derived and
  explicit level saving, update keeping the stored level, `NotFound` on a
  missing group, idempotent enrollment, withdrawn re-enrollment, 8-way
  concurrent enrollment yielding one active row, `ExistsActive`, status-only
  withdraw, slot round-trip/update/`NotFound`, same-group and same-teacher
  conflict selection with exclusion and other-day cases, slot filters, slot
  delete, duplicate-free session generation with and without a group filter and
  on a day without templates, dependency/active counts, group delete, attendance
  statistics and the roster projection. ViewModel tests cover the default group
  query, load-once, filter-bar query, read error without renew, empty state, add
  defaults, edit with the grade→subject cascade, missing group, trimmed save
  request, license-blocked save keeping the form open, delete confirm/decline
  and counted block, the three generation status keys and a generation failure,
  navigation to details/enrollment/schedule/timetable, language refresh of sort
  labels and the open editor title, schedule loading by day, conflict-free save,
  conflict confirm/cancel, invalid time ranges, delete confirm/decline, blocked
  save, language rebuild of weekday labels, enrollment board split/capacity
  text/percent/search/full-group confirmation/already-enrolled/withdraw, the
  104-cell timetable with slot placement and filters, the group file joins and
  attendance text, its shortcuts and edit hand-off, and report build/failure/
  print/open/save/cancel/back. Static XAML tests assert that every
  `...Command` binding exists on the seven Phase 6 ViewModels (they caught the
  dead `ReloadCommand` on the schedule page), that the group list and schedule
  keep F2/Delete/Enter/Escape and that the timetable grid is a `UniformGrid`
  sized by `ColumnCount`/`RowCount`.
- **Phase 7 (sessions, attendance, homeworks)**:
  `SessionStatusPolicy` transitions (every allowed/refused pair, terminal
  `completed`/`cancelled`, `postponed` restore, unknown status restorable,
  `SuggestsSms` only for `cancelled`/`postponed`, `CanTakeAttendance` matrix);
  `HomeworkPolicy.Resolve` (early click → `submitted`, on-time, past due →
  `late`, null dates); `QrAttendanceCode` parsing (`CODE:` prefix, bare code,
  whitespace/case tolerance, malformed refusal) and `QrScanSession` (duplicate
  refusal, wrong-group refusal, malformed refusal, accepted ids, cancelled scan
  changes nothing). `AutoNotificationService` tests prove the absence limit is
  read from `system_settings` (default 5, triggers at `>=`), one notification is
  created per student at/over the limit, and an existing unread attendance
  notification prevents a duplicate while a read one does not. Use-case tests
  prove every Phase 7 write (`ChangeSessionStatus`, `CreateExtraSession`,
  `SaveSessionAttendance`, `SetSessionPayment`, `SendSessionSms`, `SaveHomework`,
  `SetHomeworkSubmissionStatus`) stops at the license gate exactly once without
  touching its repository, that validation short-circuits, that an inverted
  report period is `validators.invalidDate` before any query, that extra-session
  duplicates return `Created:false`, that a paid extra session writes a `payments`
  row with `StudentId = 0` and an exemption writes none, that no guardian phone
  is `notifications.noPhoneNumber`, and that a failing report/SMS/launcher port
  propagates its failure. Real SQLite tests cover the filtered session list and
  summary, extra-session insert and duplicate, status update, payment columns,
  the joined attendance roster with `present` as the unmarked default, bulk
  upsert by `(session_id, student_id)`, the attendance report aggregate, the
  homework report/summary/submission board, due-for-session filtering and the
  submission upsert. ViewModel tests cover `AttendanceViewModel` (defaults,
  today/all tabs, group/date filters, generation status keys, cancel/postpone
  with and without SMS, restore/start, extra session, payment/exemption,
  license-blocked writes, language refresh), `TakeAttendanceViewModel` (roster
  load, one-click marking, counters, select-all, closed-session refusal, scanned
  duplicates, save → `completed`), `HomeworkViewModel` (report build, add/edit
  cascade, submission board, late resolution, reload after write) and
  `AttendanceReportViewModel` (filters, empty/error states, PDF print/save, Excel
  export, inverted period). Static XAML tests assert that every `...Command`
  binding exists on the four Phase 7 ViewModels (row-level commands through the
  `RowCommands` dictionary) and that the attendance pages keep their F5/Ctrl+G,
  Ctrl+S/F9/Ctrl+1-3 and Ctrl+N/Ctrl+P/Ctrl+E shortcuts.

## Next phases

Screen redesign on top of the Phase D1 foundation and the Phase D3 components
(Phase D2 rebuilt the shell and navigation, Phase D3 shipped the reusable
controls; the feature screens are next), then Finance (payments, dues,
registration fee and 3/6/12-month discounts, exemptions, extra fees), the
dashboard, settings, the notifications center, SMS-template management and the
documented simple login screen. QuestPDF reports beyond the student card, the
group report and the attendance report remain later work. Do not add these to
Phases 1-7 or to Phases D1-D3.

## Phase 2 results (actual)

- **Build**: `dotnet build SchoolManagement.sln -c Debug` -> succeeded (0 warnings, 0 errors).
- **Tests**: `dotnet test SchoolManagement.sln -c Debug --no-build` -> 83 passed, 0 failed, 0 skipped.
- **Smoke test (desktop)**: Started the desktop app; it ran successfully for several
  seconds. The database in `%LOCALAPPDATA%\SchoolManagement` was initialized
  to user_version 2 with 29 SQLite objects (28 tables + indexes) in WAL mode,
  with 1 school row and `system_settings.absence_limit = 5`.

## Phase 3 results (actual)

- **Build**: `dotnet build SchoolManagement.sln -c Debug` -> succeeded (0 warnings, 0 errors),
  including `tools/SchoolManagement.LicenseTool` (added to the solution and
  excluded from every shipped project reference).
- **Tests**: `dotnet test SchoolManagement.sln -c Debug --no-build` -> 317 passed,
  0 failed, 0 skipped (83 from Phase 1/2 + 234 new licensing tests).
- **Issuer tool**: `keygen` produced the public key embedded in
  `LicensePublicKey.Base64`; `issue` produced the fixed vectors in
  `LicenseTestKit`; `verify` accepted the vector with
  `result: Valid (v2, exp 2027-06-30)`.
- **Smoke test (desktop)**: Started the debug build of the desktop app; the
  process stayed alive for 7 seconds with no entry in `bootstrap.log`, then was
  stopped for the test. With no license stored the first screen is the
  activation window; activated machines go straight to the shell.
- **Key hygiene**: the private key is outside the repository
  (`%LOCALAPPDATA%\SchoolManagement\license_private.key`), `.gitignore` covers
  `*.key`, `*.pem`, `*.pfx`, `*.p12`, `.secrets/`, `license_private.key`,
  `license.dat` and `trusted_time.dat`, and a test fails if any key file is
  found in the source tree.

## Phase 4 results (actual)

- **Build**: `dotnet build SchoolManagement.sln -c Debug` -> succeeded (0 warnings, 0 errors).
- **Tests**: `dotnet test SchoolManagement.sln -c Debug --no-build` -> 428 passed,
  0 failed, 0 skipped (317 from Phases 1-3 + 111 new academic tests). The
  academic-only filter (`FullyQualifiedName~Academic`) reports 110 passed after
  the two static XAML binding/shortcut theories expand to 8 cases.
- **UI regression caught by tests**: generated commands are `SaveEditorCommand`,
  `CancelEditorCommand`, `DeleteSelectedCommand` and `EditSelectedCommand`; the
  views originally bound `SaveCommand`/`CloseEditorCommand`, which fails silently
  in WPF. The static binding test now fails the build if a view names a command
  its ViewModel does not expose.
- **Smoke test (desktop)**: Started the debug build of the desktop app; the
  process stayed alive for 8 seconds with no new `bootstrap.log` entry and the
  SQLite WAL file was touched, then it was stopped (PowerShell 5.1 has no
  `Process.Kill(bool)` overload; `Stop-Process -Force` was used).
- **Schema**: no change - Phase 4 uses the existing 28-table original schema
  (`academic_levels`, `grades`, `grade_subjects`, `subjects`, `teachers`), and the
  phone SQLite file still opens without migration.

## Phase 5 results (actual)

- **Build**: `dotnet build SchoolManagement.sln -c Debug` -> succeeded
  (0 warnings, 0 errors).
- **Tests**: `dotnet test SchoolManagement.sln -c Debug --no-build` -> 567 passed,
  0 failed, 0 skipped (428 from Phases 1-4 + 139 new student tests: 129 in
  `SchoolManagement.Tests.Students` and 10 in `StudentRepositoryTests`). The
  student-only filter (`FullyQualifiedName~Students`) reports 129 passed.
- **UI regression caught by tests**: the details view bound
  `OpenAttendanceCommand`, but the attendance screen is a later phase and the
  `ShellTab` enum has no Attendance destination; the dead link was removed from
  the attendance tab instead of adding a ViewModel command that cannot navigate.
  The same run showed the status banner being cleared by the refresh that follows
  a save/delete/import; `RefreshAsync` no longer clears it (Phase 4 behaviour),
  so the success message survives.
- **Smoke test (desktop)**: Started the debug build of the desktop app; the
  process stayed alive for 8 seconds with no new `bootstrap.log` entry, then it
  was stopped with `Stop-Process -Force`.
- **Schema**: no change - Phase 5 uses the existing 28-table original schema
  (`students`, `guardians`, `enrollments`, `payments`, `attendance`), and the
  phone SQLite file still opens without migration.

## Phase 6 results (actual)

- **Build**: `dotnet build SchoolManagement.sln -c Debug` -> succeeded
  (0 warnings, 0 errors).
- **Tests**: `dotnet test SchoolManagement.sln -c Debug --no-build` -> 788 passed,
  0 failed, 0 skipped (567 from Phases 1-5 + 221 new Phase 6 tests). The
  group-namespace filter (`FullyQualifiedName~Groups`) reports 201 passed and
  `FullyQualifiedName~GroupRepositoryTests` reports 25 passed (226 combined).
- **Repository regressions caught and fixed**: EF could not translate a
  predicate or an ordering applied after a projection containing correlated
  subqueries (`GetSummaryAsync`, `GetSlotsAsync`, `GetSlotAsync`,
  `GetConflictsAsync`); filters and orderings now run on entity columns before
  `Project(...)`. Group search used `string.Contains`, which translates to a
  case-sensitive `instr` in SQLite; it now uses `EF.Functions.Like` and also
  searches the level name. The Phase 6 use-case test had a wrong teacher id in a
  conflict mock and the slot-filter test shadowed a failed result; both were
  corrected.
- **UI regression caught by tests**: `GroupSchedulePage.xaml` bound
  `ReloadCommand`, which `GroupScheduleViewModel` did not expose (a silent WPF
  no-op). A license-guarded `ReloadAsync` command was added to the ViewModel.
- **Smoke test (desktop)**: Started the debug build of the desktop app; the
  process stayed alive for 8 seconds with no `bootstrap.log` entry, then it was
  stopped with `Stop-Process -Force`.
- **Schema**: no change - Phase 6 uses the existing 28-table original schema
  (`groups`, `enrollments`, `schedule_templates`, `sessions`, `attendance`,
  `payments`), and the phone SQLite file still opens without migration.

## Phase 7 results (actual)

- **Build**: `dotnet build SchoolManagement.sln -c Debug` -> succeeded
  (0 warnings, 0 errors).
- **Tests**: `dotnet test SchoolManagement.sln -c Debug --no-build` -> 1030
  passed, 0 failed, 0 skipped (788 from Phases 1-6 + 242 new Phase 7 tests).
  The new tests live in `SchoolManagement.Tests/Attendance` (models, use cases,
  `AttendanceRepository`, `SessionRepository`, `HomeworkRepository`,
  `AttendanceViewModel`, `TakeAttendanceViewModel`, `HomeworkViewModel`,
  `AttendanceReportViewModel`, the static XAML binding/shortcut test) and
  `SchoolManagement.Tests/Domain` (`SessionStatusPolicy`, `HomeworkPolicy`,
  `QrAttendanceCode`), plus `AutoNotificationService` and `QrScanSession` tests.
- **Failing-test proofs (deliberately broken first, then restored)**:
  - Disabling the `HasUnreadAsync` short-circuit in `AutoNotificationService`
    made `An_unread_absence_notification_prevents_a_duplicate` fail.
  - Removing the duplicate guard from `QrScanSession` made
    `The_same_student_is_never_accepted_twice` fail.
  Both behaviours are back in place and the suite is green.
- **UI regressions caught by tests**: the Phase 7 static binding test
  (`AttendanceViewBindingTests`) walks every `...Command` bound in the five new
  views; row-level commands are resolved through the `RowCommands` dictionary so
  `AttendanceStudentRow`/`HomeworkSubmissionRow` commands are checked too. It
  also asserts the F5/Ctrl+G, Ctrl+S/F9/Ctrl+1-3 and Ctrl+N/Ctrl+P/Ctrl+E
  shortcuts are still bound.
- **Test-infrastructure notes**: `AttendanceVmHarness` (internal) builds the four
  Phase 7 ViewModels with NSubstitute; `ITrustedClock.LocalNow` is stubbed before
  `Navigation.NavigateTo<T>()` is configured, otherwise the substitute hijacks the
  navigation call. `ShellViewModelTests` was updated for the seventh destination
  and `ShellTab.Attendance = 6`.
- **Smoke test (desktop)**: Started the debug build of the desktop app; the
  process stayed alive for 8 seconds with no new `bootstrap.log` entry, then it
  was stopped with `Stop-Process -Force`.
- **Schema**: no change - Phase 7 uses the existing 28-table original schema
  (`sessions`, `attendance`, `homeworks`, `homework_submissions`, `payments`,
  `notifications`, `sms_logs`, `system_settings`), and the phone SQLite file
  still opens without migration.
- **Dependencies added**: `OpenCvSharp4` + `OpenCvSharp4.Windows`
  (4.10.0.20240616, Desktop, webcam capture only); QR decoding uses the existing
  ZXing.Net + SkiaSharp in Infrastructure behind `IQrCodeDecoder`.

## Phase D1 results (actual)

- **Build**: `dotnet build SchoolManagement.sln -c Debug` -> succeeded
  (0 warnings, 0 errors). `WPF-UI 4.3.0` is the only package added.
- **Tests**: `dotnet test SchoolManagement.sln -c Debug --no-build` -> 1052
  passed, 0 failed, 0 skipped (1030 from Phases 1-7 + 22 new design-system
  tests). `FullyQualifiedName~DesignSystem` reports 22 passed. The new tests live
  in `SchoolManagement.Tests/DesignSystem`:
  - `DesignFiles.cs` - locates the real Desktop files, parses the palettes,
    implements the WCAG 2.1 ratio (with alpha compositing), and detects `#if
    DEBUG` membership.
  - `DesignPaletteTests.cs` - light/dark key parity, 43 required text/background
    pairs at >= 4.5:1 in both themes, the disabled-text 2.5-4.5 band, 25 legacy
    `Brush.*` aliases still mirroring their token, translucent `Surface.Mica` /
    `Overlay.Scrim`, and the theme shims staying colour-free shims.
  - `DesignResourceTests.cs` - no hex in `Views/`, every `DynamicResource` key in
    a view defined by some dictionary, the `App.xaml` merge order, well-formed
    XAML, the gallery and F12 hook staying inside `#if DEBUG`, and tokens named by
    role rather than by colour step.
  - `DesignTranslationTests.cs` - every `TranslationKeys.Design` constant present
    in ar/en/fr, all using the `design.` prefix, with no orphan `design.*` key.
  - `DesignGalleryWindowTests.cs` - STA integration test that loads the real
    `App.InitializeComponent()` resource tree and instantiates
    `DesignGalleryWindow`; this is the only test that resolves every
    `StaticResource` at runtime.
- **Failing-test proofs (deliberately broken first, then restored)**:
  - `Text.Secondary` light set to `#C8C2BA` -> `Every_used_text_pair_meets_wcag_aa`
    and `Legacy_brush_aliases_mirror_the_semantic_tokens` failed (3 failures).
  - `Background="#FF0000"` added to the gallery root -> 
    `Views_do_not_hardcode_hex_colours` failed.
  - Renaming `Text.Title` to `Text.TitleX` -> `Gallery_window_loads_with_the_real_application_resources`
    failed, proving the runtime test catches a missing style key that a static
    check cannot.
  All three behaviours are back in place and the suite is green.
- **Persistence regression found and fixed**: three repository tests
  (`StudentRepositoryTests` card/export, `AcademicRepositoryTests`
  `Set_subjects`) started failing with `Failure.Database`. Root cause was not the
  repositories: with `LangVersion=latest` on the .NET 11 preview SDK, the
  compiler binds `int[].Contains(value)` to
  `MemoryExtensions.Contains(ReadOnlySpan<int>, int)`, and EF Core's expression
  interpreter cannot compile a `ref struct` return type
  (`GenericArguments[1], 'System.ReadOnlySpan`1[System.Int32]', ... violates the
  constraint of type 'TRet'`). Fixed by materializing `List<int>` instead of
  `int[]` at the three sites that feed an EF query: `StudentRepository
  .GetCardDataAsync` (`ids`), `StudentRepository.LoadPlacementsAsync` (parameter
  is now `List<int>`, guard uses `.Count`) and `GradeRepository.SetSubjectsAsync`
  (`desired`). Both files carry a comment explaining the constraint. Diagnostic
  edits to `RepositoryBase.cs` were fully reverted; it shows no diff.
- **UI regressions avoided**: the splash previously relied on a `ProgressBar`
  with `IsIndeterminate`; the new template does not animate that state, so the
  splash now hosts `controls:ProgressRing`. `Resources/Styles.xaml` was deleted
  and its unique implicit styles were moved to `Design/Base.xaml` (and
  `ListBox`/`ListBoxItem` to `Styles/DataViews.xaml`); the full suite still
  passes, so nothing referenced the deleted file.
- **Smoke test (desktop)**: Started the debug build of the desktop app; the
  process stayed alive for 8 seconds with no `bootstrap.log` written, then it was
  stopped with `Stop-Process -Force`. This proves the new merged-dictionary tree
  and the WPF-UI theme load on the real startup path, not only under test.
- **Schema / domain**: no change - Phase D1 touches only
  `SchoolManagement.Desktop` (plus the three `List<int>` lines above). Domain,
  Application, Infrastructure persistence, the 28-table schema, licensing and
  `ILicenseGuard` are untouched.

## Phase D2 results (actual)

- **Build**: `dotnet build SchoolManagement.sln -c Debug` -> succeeded
  (0 warnings, 0 errors).
- **Tests**: `dotnet test SchoolManagement.sln -c Debug --no-build` -> 1091
  passed, 0 failed, 0 skipped (1052 from Phase D1 + 39 new/rewritten shell
  tests). New or rewritten files:
  - `Shell/ShellViewModelTests.cs` - 10 rail items, the five group keys and
    counts 1/5/1/2/1 in every language, the 1200 px auto-collapse threshold,
    first-click expand after an automatic collapse, shortcut-to-action mapping,
    unread badge, breadcrumb, and the language switch refreshing labels and
    `FlowDirection`.
  - `Shell/WindowPlacementTests.cs` - off-screen placement, unplugged monitor,
    minimum size, oversized window, NaN/infinity fallback, maximized state and
    negative multi-monitor origins.
  - `Shell/WindowStateStoreTests.cs` - missing/corrupt file fallback, round
    trip, atomic overwrite with no `.tmp` left behind, default path.
  - `Shell/LicenseBannerViewModelTests.cs` - one banner per state, severity
    mapping, read-only not dismissible, clock tamper winning, dismissal restored
    by a new state, language refresh.
  - `Shell/CommandPaletteViewModelTests.cs` - eight commands on open, close
    clears state, two-character search merges students and groups through the
    use cases, one character never hits a repository, command execution
    navigates and closes, theme/help events, wrap-around selection.
  - `Shell/ShellTranslationTests.cs` - the `nav`/`shell`/`palette`/`shortcuts`/
    `toast` sections exist and are non-empty in ar/en/fr, every shortcut keycap
    exists, the physical English keys are asserted, the nav groups are
    translated.
  - `DesignSystem/DesignResourceTests.cs` - added
    `Every_static_resource_key_used_in_views_is_defined` (global dictionaries
    plus per-view local resources).
- **Startup regression caught and fixed**: the first smoke test wrote
  `bootstrap.log` with `XamlParseException: Impossible de trouver la ressource
  nommée 'Text.BodySmall'` at `ShellWindow.xaml:189`. Phase D1 shipped
  `Font.Size.BodySmall` but never the `Text.BodySmall` style, and both
  `ShellWindow.xaml` and `LicenseBannerView.xaml` used it with
  `StaticResource`, so the real shell could not load (the app stayed alive only
  because `BootstrapGate` showed its retry screen). Added the 13 px
  `Text.BodySmall` style based on `Text.Body`, then added the static-resource
  test above.
- **Failing-test proof (deliberately broken first, then restored)**: renaming
  `Text.BodySmall` to `Text.BodySmallX` made
  `Every_static_resource_key_used_in_views_is_defined` fail with
  `LicenseBannerView.xaml -> Text.BodySmall`, and the real app crashed at
  startup. Both the style and the test are back in place and the suite is green.
- **Test updates with justification**:
  - `DesignResourceTests.Design_gallery_is_registered_and_hooked_only_in_debug_builds`
    now asserts `Key.F12` (not the whole `OnPreviewKeyDown`) is inside
    `#if DEBUG`, because the method legitimately hosts the global shortcuts in
    release.
  - `ShellTranslationTests` only asserts keycaps are non-empty across languages,
    because French uses `Échap`, not `Esc`.
  - `ShellViewModelTests.CreateShell` only stubs `CountUnreadAsync` when no
    repository substitute is passed, so the badge test can provide its own.
- **Deviation (justified)**: the requested nav groups list 9 destinations, but
  Phase 4 owns the `Academics` screen (levels, grades, subjects, teachers). It
  was kept as a 10th item inside the Academic group instead of becoming
  unreachable. Group counts are therefore 1/5/1/2/1.
- **Smoke test (desktop)**: Deleted `bootstrap.log`, started the debug build,
  kept it alive for 8 seconds, then `Stop-Process -Force`. The process stayed
  alive and **no `bootstrap.log` was written**, proving the FluentWindow,
  Mica/fallback, the merged dictionaries, the nav rail, the cached page host
  and the license banner all load on the real startup path.
- **Schema / domain**: no change - Phase D2 touches only
  `SchoolManagement.Desktop` (views, styles, services, ViewModels for display
  state) plus `TranslationKeys` and the three JSON files. Domain, Application,
  Infrastructure, the 28-table schema, licensing and `ILicenseGuard` are
  untouched.

## Phase D3 results (actual)

- **Build**: `dotnet build SchoolManagement.sln -c Debug` -> succeeded
  (0 warnings, 0 errors).
- **Tests**: `dotnet test SchoolManagement.sln -c Debug --no-build` -> 1160
  passed, 0 failed, 0 skipped (1091 from Phase D2 + 69 new component tests).
  `FullyQualifiedName~DesignSystem` reports 92 passed. New or extended files:
  - `DesignSystem/ComponentTests.cs` - 69 cases: `StatusBadgeMapper` over every
    attendance/finance/session/license/homework status plus case/separator
    tolerance and unknown/null fallback; every `StatusTone` naming palette keys
    that exist in both themes; `DataTableFilter` value access (nested,
    case-insensitive, null-safe), date formatting, null-first/numeric/string
    comparison, global search over visible columns only, combined column
    filters, blank-filter handling and the `DataTableColumn.Changed` property
    name; `WeekScheduleConflictPolicy` overlap, touching boundary, other day,
    other resource, blank keys, invalid ranges and stale-flag clearing;
    `MonthYearParser` accepted/refused formats and the UTC first-of-month
    round-trip; all eight converters; the public dependency-property surface of
    all 25 component types; and `components.*`/`months.*` parity plus orphan
    checks in ar/en/fr.
  - `DesignSystem/DesignGalleryWindowTests.cs` - the existing STA test now also
    resolves 32 named samples (`HeaderSample`, `TableSample`, `PanelSample`,
    `DialogSample`, every badge/indicator/picker/chart/state) through the real
    name scope, and asserts the table exposes three live columns and a
    `DetailTemplate`.
  - `DesignSystem/DesignFiles.cs` - added `ControlsDirectory`.
  - `DesignSystem/DesignResourceTests.cs` - the hex, `DynamicResource` and
    `StaticResource` scans now cover `Controls/` as well as `Views/`, and the
    merge-order assertion includes `Resources/Styles/Components.xaml` after
    `Feedback.xaml`.
- **Regression caught and fixed**: `DataTableFilter.Compare("Amina", "amina")`
  returned 1, because two same-type strings matched the generic `IComparable`
  branch before the culture-insensitive fallback, so a name column would sort
  uppercase and lowercase into two blocks. Added an explicit string branch
  (`CurrentCultureIgnoreCase`); the new test is green and every existing sort
  test still passes.
- **Verification**: the gallery test loads the real `App.xaml` resource tree and
  instantiates every component, so a missing style or converter key fails the
  suite instead of only showing up on screen. The static scans prove no literal
  hex and no unresolved resource key in `Controls/`.
- **Smoke test (desktop)**: Deleted `bootstrap.log`, started the debug build,
  kept it alive for 8 seconds, then `Stop-Process -Force`. The process stayed
  alive and **no `bootstrap.log` was written**, so the new
  `Resources/Styles/Components.xaml` merge and the WPF-UI theme still load on
  the real startup path.
- **Schema / domain**: no change - Phase D3 touches
  `SchoolManagement.Desktop` (`Controls/`, `Resources/Styles/Components.xaml`,
  `Resources/Design/Icons.xaml`, `Services/ThemeMonitor.cs`, the Debug gallery
  view/ViewModel, `TranslationKeys`, the three JSON files, the Desktop `.csproj`
  package reference) plus the new tests. Domain, Application, Infrastructure,
  the 28-table schema, licensing and `ILicenseGuard` are untouched.
- **Dependency**: `LiveChartsCore.SkiaSharpView.WPF` 2.0.0-rc3.3 (Desktop only),
  resolving to the same `SkiaSharp` 2.88.8 as Infrastructure.
