namespace SchoolManagement.Application.Localization;

/// <summary>
/// Compile-time translation keys used from C#. XAML uses {loc:Tr ...} directly.
/// A test reflects over every constant here and verifies it exists in all three
/// language files, so a typo becomes a failing build instead of a runtime
/// "missing key" in front of the user.
/// </summary>
public static class TranslationKeys
{
    public static class App
    {
        public const string Name = "app.name";
        public const string Subtitle = "app.subtitle";
    }

    public static class Common
    {
        public const string Retry = "common.retry";
        public const string Error = "common.error";
        public const string Loading = "common.loading";
        public const string NoData = "common.noData";
        public const string Save = "common.save";
        public const string Cancel = "common.cancel";
        public const string Confirm = "common.confirm";
        public const string Delete = "common.delete";
        public const string Currency = "common.currency";
        public const string Required = "common.required";
        public const string Active = "common.active";
        public const string Inactive = "common.inactive";
        public const string NotSpecified = "common.notSpecified";
        public const string SortName = "common.sortName";
        public const string SortOrder = "common.sortOrder";
        public const string SortLinked = "common.sortLinked";
        public const string SortDescending = "common.sortDescending";
        public const string IncludeInactive = "common.includeInactive";
        public const string Actions = "common.actions";
        public const string Yes = "common.yes";
        public const string No = "common.no";
        public const string Edit = "common.edit";
        public const string Details = "common.details";
        public const string Filters = "common.filters";
        public const string Selected = "common.selected";
    }

    public static class Menu
    {
        public const string Dashboard = "menu.dashboard";
        public const string Students = "menu.students";
        public const string Groups = "menu.groups";
        public const string Attendance = "menu.attendance";
        public const string Academics = "menu.academics";
        public const string Finance = "menu.finance";
        public const string Settings = "menu.settings";
        public const string Homework = "menu.homework";
        public const string Notifications = "menu.notifications";
        public const string Messages = "menu.messages";
    }

    public static class Dashboard
    {
        public const string Title = "dashboard.title";
        public const string Welcome = "dashboard.welcome";
        public const string Statistics = "dashboard.statistics";
        public const string StudentsCount = "dashboard.studentsCount";
        public const string GroupsCount = "dashboard.groupsCount";
        public const string TodaySessions = "dashboard.todaySessions";
        public const string Outstanding = "dashboard.outstanding";
        public const string QuickActions = "dashboard.quickActions";
        public const string GreetingMorning = "dashboard.greetingMorning";
        public const string GreetingAfternoon = "dashboard.greetingAfternoon";
        public const string GreetingEvening = "dashboard.greetingEvening";
        public const string GreetingWithName = "dashboard.greetingWithName";
        public const string Today = "dashboard.today";
        public const string Overview = "dashboard.overview";
        public const string MonthRevenue = "dashboard.monthRevenue";
        public const string Attention = "dashboard.attention";
        public const string NoAttention = "dashboard.noAttention";
        public const string NoAttentionHint = "dashboard.noAttentionHint";
        public const string Analytics = "dashboard.analytics";
        public const string RevenueTrend = "dashboard.revenueTrend";
        public const string RevenueTrendHint = "dashboard.revenueTrendHint";
        public const string WeeklyAttendance = "dashboard.weeklyAttendance";
        public const string WeeklyAttendanceHint = "dashboard.weeklyAttendanceHint";
        public const string LevelDistribution = "dashboard.levelDistribution";
        public const string LevelDistributionHint = "dashboard.levelDistributionHint";
        public const string NoLevel = "dashboard.noLevel";
        public const string SetupTitle = "dashboard.setupTitle";
        public const string SetupHint = "dashboard.setupHint";
        public const string SetupLevels = "dashboard.setupLevels";
        public const string SetupSubjects = "dashboard.setupSubjects";
        public const string SetupTeachers = "dashboard.setupTeachers";
        public const string SetupGroups = "dashboard.setupGroups";
        public const string SetupStudents = "dashboard.setupStudents";
        public const string SetupDone = "dashboard.setupDone";
        public const string SetupPending = "dashboard.setupPending";
        public const string SetupOpen = "dashboard.setupOpen";
        public const string SetupCount = "dashboard.setupCount";
        public const string NewSchoolTitle = "dashboard.newSchoolTitle";
        public const string NewSchoolHint = "dashboard.newSchoolHint";
        public const string ViewAll = "dashboard.viewAll";
        public const string TakeAttendance = "dashboard.takeAttendance";
        public const string AttendanceDone = "dashboard.attendanceDone";
        public const string NoSessionsToday = "dashboard.noSessionsToday";
        public const string NoSessionsHint = "dashboard.noSessionsHint";
        public const string SessionStudents = "dashboard.sessionStudents";
        public const string AlertArrearsTitle = "dashboard.alertArrearsTitle";
        public const string AlertArrearsBody = "dashboard.alertArrearsBody";
        public const string ArrearsCount = "dashboard.arrearsCount";
        public const string AlertAbsenceTitle = "dashboard.alertAbsenceTitle";
        public const string AlertAbsenceBody = "dashboard.alertAbsenceBody";
        public const string AlertMissingAttendanceTitle = "dashboard.alertMissingAttendanceTitle";
        public const string AlertMissingAttendanceBody = "dashboard.alertMissingAttendanceBody";
        public const string AlertLicenseTitle = "dashboard.alertLicenseTitle";
        public const string AlertLicenseBody = "dashboard.alertLicenseBody";
        public const string CategoryPayments = "dashboard.categoryPayments";
        public const string CategoryStudents = "dashboard.categoryStudents";
        public const string CategoryAttendance = "dashboard.categoryAttendance";
        public const string CategoryLicense = "dashboard.categoryLicense";
        public const string OpenFinance = "dashboard.openFinance";
        public const string OpenStudents = "dashboard.openStudents";
        public const string OpenAttendance = "dashboard.openAttendance";
        public const string RenewLicense = "dashboard.renewLicense";
        public const string QuickNewStudent = "dashboard.quickNewStudent";
        public const string QuickNewStudentHint = "dashboard.quickNewStudentHint";
        public const string QuickPayment = "dashboard.quickPayment";
        public const string QuickPaymentHint = "dashboard.quickPaymentHint";
        public const string QuickAttendance = "dashboard.quickAttendance";
        public const string QuickAttendanceHint = "dashboard.quickAttendanceHint";
        public const string QuickReport = "dashboard.quickReport";
        public const string QuickReportHint = "dashboard.quickReportHint";
        public const string RevenueSummary = "dashboard.revenueSummary";
        public const string AttendanceSummary = "dashboard.attendanceSummary";
        public const string LevelSummary = "dashboard.levelSummary";
        public const string NoChartData = "dashboard.noChartData";
        public const string Refresh = "dashboard.refresh";
    }

    public static class Students
    {
        public const string Title = "students.title";
        public const string Add = "students.add";
        public const string Edit = "students.edit";
        public const string Details = "students.details";
        public const string SearchHint = "students.searchHint";
        public const string Empty = "students.empty";
        public const string EmptyHint = "students.emptyHint";
        public const string DeleteConfirm = "students.deleteConfirm";
        public const string DeleteMessage = "students.deleteMessage";
        public const string DeleteBlocked = "students.deleteBlocked";
        public const string FirstName = "students.firstName";
        public const string LastName = "students.lastName";
        public const string FullName = "students.fullName";
        public const string StudentCode = "students.studentCode";
        public const string DateOfBirth = "students.dateOfBirth";
        public const string Gender = "students.gender";
        public const string Male = "students.male";
        public const string Female = "students.female";
        public const string Phone = "students.phone";
        public const string Address = "students.address";
        public const string Status = "students.status";
        public const string Active = "students.active";
        public const string Inactive = "students.inactive";
        public const string Withdrawn = "students.withdrawn";
        public const string Notes = "students.notes";
        public const string PersonalInfo = "students.personalInfo";
        public const string ContactInfo = "students.contactInfo";
        public const string AdditionalInfo = "students.additionalInfo";
        public const string FinancialStatus = "students.financialStatus";
        public const string GuardianSection = "students.guardianSection";
        public const string GuardianName = "students.guardianName";
        public const string GuardianPhone = "students.guardianPhone";
        public const string GuardianEmail = "students.guardianEmail";
        public const string GuardianRelationship = "students.guardianRelationship";
        public const string RegistrationFee = "students.registrationFee";
        public const string GenerateCode = "students.generateCode";
        public const string FilterLevel = "students.filterLevel";
        public const string FilterGrade = "students.filterGrade";
        public const string FilterGroup = "students.filterGroup";
        public const string FilterStatus = "students.filterStatus";
        public const string AllLevels = "students.allLevels";
        public const string AllGrades = "students.allGrades";
        public const string AllGroups = "students.allGroups";
        public const string AllStatuses = "students.allStatuses";
        public const string PrintCard = "students.printCard";
        public const string PrintQrCodes = "students.printQrCodes";
        public const string Import = "students.import";
        public const string Export = "students.export";
        public const string ImportCsv = "students.importCsv";
        public const string ImportExcel = "students.importExcel";
        public const string ExportCsv = "students.exportCsv";
        public const string ExportExcel = "students.exportExcel";
        public const string ImportReport = "students.importReport";
        public const string ImportSuccess = "students.importSuccess";
        public const string ImportNoRows = "students.importNoRows";
        public const string ImportInvalidFile = "students.importInvalidFile";
        public const string ImportRow = "students.importRow";
        public const string ImportErrors = "students.importErrors";
        public const string ImportedCount = "students.importedCount";
        public const string ExportSuccess = "students.exportSuccess";
        public const string ExportEmpty = "students.exportEmpty";
        public const string ExportFailed = "students.exportFailed";
        public const string DetailsData = "students.detailsData";
        public const string DetailsGroups = "students.detailsGroups";
        public const string DetailsFinance = "students.detailsFinance";
        public const string DetailsAttendance = "students.detailsAttendance";
        public const string NoGroups = "students.noGroups";
        public const string NoPayments = "students.noPayments";
        public const string NoAttendance = "students.noAttendance";
        public const string OpenGroups = "students.openGroups";
        public const string OpenFinance = "students.openFinance";
        public const string OpenAttendance = "students.openAttendance";
        public const string CardTitle = "students.cardTitle";
        public const string CardOwnerNote = "students.cardOwnerNote";
        public const string CardReturnNote = "students.cardReturnNote";
        public const string CardPrint = "students.cardPrint";
        public const string CardSavePdf = "students.cardSavePdf";
        public const string CardPreview = "students.cardPreview";
        public const string CardClose = "students.cardClose";
        public const string QrPageTitle = "students.qrPageTitle";
        public const string QrPageOf = "students.qrPageOf";
        public const string QrPrintAll = "students.qrPrintAll";
        public const string QrEmpty = "students.qrEmpty";
        public const string QrColumns = "students.qrColumns";
        public const string SortCode = "students.sortCode";
        public const string SortStatus = "students.sortStatus";
        public const string SortName = "students.sortName";
        public const string SortGroup = "students.sortGroup";
        public const string SortLevel = "students.sortLevel";
        public const string GroupsTabEmpty = "students.groupsTabEmpty";
        public const string FinanceTabEmpty = "students.financeTabEmpty";
        public const string AttendanceTabEmpty = "students.attendanceTabEmpty";
        public const string CodeHint = "students.codeHint";
        public const string ImportHint = "students.importHint";

        // Phase D5 - list views, bulk actions and the redesigned student file.
        public const string ViewTable = "students.viewTable";
        public const string ViewCards = "students.viewCards";
        public const string ActiveFilters = "students.activeFilters";
        public const string ClearFilters = "students.clearFilters";
        public const string FeePendingFilter = "students.feePendingFilter";
        public const string FeePendingChip = "students.feePendingChip";
        public const string BulkQr = "students.bulkQr";
        public const string BulkMessage = "students.bulkMessage";
        public const string MessageBody = "students.messageBody";
        public const string MessageNoPhone = "students.messageNoPhone";
        public const string MessageOpened = "students.messageOpened";
        public const string MessageFailed = "students.messageFailed";
        public const string Overview = "students.overview";
        public const string Homework = "students.homework";
        public const string HomeworkTabEmpty = "students.homeworkTabEmpty";
        public const string NoHomework = "students.noHomework";
        public const string TotalPaid = "students.totalPaid";
        public const string AttendanceRateLabel = "students.attendanceRate";
        public const string QuickProfile = "students.quickProfile";
        public const string NoSelection = "students.noSelection";
        public const string Zoom = "students.zoom";
        public const string CardTemplate = "students.cardTemplate";
        public const string CardTemplateStandard = "students.cardTemplateStandard";
        public const string Placement = "students.placement";
        public const string Guardian = "students.guardian";
        public const string ValidationFirstName = "students.validationFirstName";
        public const string ValidationLastName = "students.validationLastName";
        public const string ValidationGuardian = "students.validationGuardian";
        public const string ValidationFee = "students.validationFee";
        public const string OpenFile = "students.openFile";
    }

    public static class Groups
    {
        public const string Title = "groups.title";
        public const string Add = "groups.add";
        public const string Edit = "groups.edit";
        public const string Details = "groups.details";
        public const string SearchHint = "groups.searchHint";
        public const string Name = "groups.name";
        public const string Grade = "groups.grade";
        public const string Subject = "groups.subject";
        public const string Teacher = "groups.teacher";
        public const string MonthlyFee = "groups.monthlyFee";
        public const string MaxStudents = "groups.maxStudents";
        public const string StudentsCount = "groups.studentsCount";
        public const string EnrolledStudents = "groups.enrolledStudents";
        public const string ManageStudents = "groups.manageStudents";
        public const string Empty = "groups.empty";
        public const string WeeklySchedule = "groups.weeklySchedule";
        public const string AddTimeSlot = "groups.addTimeSlot";
        public const string EditTimeSlot = "groups.editTimeSlot";
        public const string NoSchedule = "groups.noSchedule";
        public const string EnrollmentDate = "groups.enrollmentDate";
        public const string FullReport = "groups.fullReport";
        public const string AttendanceReport = "groups.attendanceReport";
        public const string HomeworkReport = "groups.homeworkReport";
        public const string DayOfWeek = "groups.dayOfWeek";
        public const string StartTime = "groups.startTime";
        public const string EndTime = "groups.endTime";
        public const string Location = "groups.location";
        public const string DeleteTitle = "groups.deleteTitle";
        public const string DeleteMessage = "groups.deleteMessage";
        public const string DeleteTimeSlotConfirm = "groups.deleteTimeSlotConfirm";
        public const string DeleteBlocked = "groups.deleteBlocked";
        public const string NoGrades = "groups.noGrades";
        public const string StudentsCountValue = "groups.studentsCountValue";
        public const string NoTeacher = "groups.noTeacher";
        public const string Schedule = "groups.schedule";
        public const string ScheduleTitle = "groups.scheduleTitle";
        public const string ScheduleSubtitle = "groups.scheduleSubtitle";
        public const string ScheduleEmpty = "groups.scheduleEmpty";
        public const string ScheduleSaved = "groups.scheduleSaved";
        public const string ScheduleDeleted = "groups.scheduleDeleted";
        public const string SlotsCount = "groups.slotsCount";
        public const string SlotsCountShort = "groups.slotsCountShort";
        public const string NoSlotsShort = "groups.noSlotsShort";
        public const string BackToGroup = "groups.backToGroup";
        public const string GenerateSessions = "groups.generateSessions";
        public const string GenerationCreated = "groups.generationCreated";
        public const string GenerationNone = "groups.generationNone";
        public const string GenerationAlreadyExists = "groups.generationAlreadyExists";
        public const string SessionsGenerated = "groups.sessionsGenerated";
        public const string ScheduledDays = "groups.scheduledDays";
        public const string NoDaysScheduled = "groups.noDaysScheduled";
        public const string InvalidTimeRange = "groups.invalidTimeRange";
        public const string Capacity = "groups.capacity";
        public const string Full = "groups.full";
        public const string CapacityShort = "groups.capacityShort";
        public const string Saved = "groups.saved";
        public const string Deleted = "groups.deleted";
        public const string SortName = "groups.sortName";
        public const string SortSubject = "groups.sortSubject";
        public const string SortTeacher = "groups.sortTeacher";
        public const string SortGrade = "groups.sortGrade";
        public const string SortCapacity = "groups.sortCapacity";
        public const string SortFee = "groups.sortFee";

        // Phase D5 - card/table views, grouping and the interactive schedule.
        public const string ViewTable = "groups.viewTable";
        public const string ViewCards = "groups.viewCards";
        public const string GroupBy = "groups.groupBy";
        public const string GroupByNone = "groups.groupByNone";
        public const string GroupByLevel = "groups.groupByLevel";
        public const string GroupByTeacher = "groups.groupByTeacher";
        public const string NoTeacherSection = "groups.noTeacherSection";
        public const string CompactTimetable = "groups.compactTimetable";
        public const string Shortcuts = "groups.shortcuts";
        public const string OpenAttendance = "groups.openAttendance";
        public const string OpenHomework = "groups.openHomework";
        public const string NoGroupsSection = "groups.noGroupsSection";
        public const string SlotsBadge = "groups.slotsBadge";
        public const string GridHint = "groups.gridHint";
        public const string MoveSaved = "groups.moveSaved";
        public const string NameRequired = "groups.nameRequired";
        public const string GradeRequired = "groups.gradeRequired";
        public const string SubjectRequired = "groups.subjectRequired";
        public const string CapacityInvalid = "groups.capacityInvalid";
        public const string FeeInvalid = "groups.feeInvalid";
        public const string ConflictTitle = "groups.conflictTitle";
        public const string ConflictMessage = "groups.conflictMessage";
        public const string ConflictSameGroup = "groups.conflictSameGroup";
        public const string ConflictSameTeacher = "groups.conflictSameTeacher";
        public const string ConflictProceed = "groups.conflictProceed";
        public const string ConflictCancel = "groups.conflictCancel";
    }

    public static class Enrollment
    {
        public const string Title = "enrollment.title";
        public const string SelectGroup = "enrollment.selectGroup";
        public const string SearchStudent = "enrollment.searchStudent";
        public const string AvailableStudents = "enrollment.availableStudents";
        public const string EnrolledStudents = "enrollment.enrolledStudents";
        public const string EnrollDate = "enrollment.enrollDate";
        public const string Enroll = "enrollment.enroll";
        public const string Unenroll = "enrollment.unenroll";
        public const string UnenrollConfirm = "enrollment.unenrollConfirm";
        public const string EnrolledSuccess = "enrollment.enrolledSuccess";
        public const string UnenrolledSuccess = "enrollment.unenrolledSuccess";
        public const string AlreadyEnrolled = "enrollment.alreadyEnrolled";
        public const string NoResults = "enrollment.noResults";
        public const string NoStudents = "enrollment.noStudents";
        public const string SelectGroupFirst = "enrollment.selectGroupFirst";
        public const string CapacityLabel = "enrollment.capacityLabel";
        public const string CapacityFull = "enrollment.capacityFull";
        public const string CapacityExceededTitle = "enrollment.capacityExceededTitle";
        public const string CapacityExceededMessage = "enrollment.capacityExceededMessage";
        public const string CapacityExceededWarning = "enrollment.capacityExceededWarning";
        public const string Group = "enrollment.group";

        // Phase D5 - quick add board.
        public const string QuickAdd = "enrollment.quickAdd";
        public const string AvailableHint = "enrollment.availableHint";
        public const string SearchPlaceholder = "enrollment.searchPlaceholder";
        public const string CapacityWarning = "enrollment.capacityWarning";
    }

    public static class Schedule
    {
        public const string Title = "schedule.title";
        public const string Subtitle = "schedule.subtitle";
        public const string AddSlot = "schedule.addSlot";
        public const string EditSlot = "schedule.editSlot";
        public const string Empty = "schedule.empty";
        public const string Saved = "schedule.saved";
        public const string Deleted = "schedule.deleted";
        public const string InvalidTimeRange = "schedule.invalidTimeRange";
        public const string ConflictTitle = "schedule.conflictTitle";
        public const string ConflictMessage = "schedule.conflictMessage";
        public const string ConflictSameGroup = "schedule.conflictSameGroup";
        public const string ConflictSameTeacher = "schedule.conflictSameTeacher";
        public const string ConflictProceed = "schedule.conflictProceed";
        public const string ConflictCancel = "schedule.conflictCancel";
    }

    public static class Timetable
    {
        public const string Title = "timetable.title";
        public const string Group = "timetable.group";
        public const string Teacher = "timetable.teacher";
        public const string AllGroups = "timetable.allGroups";
        public const string AllTeachers = "timetable.allTeachers";
        public const string Empty = "timetable.empty";
        public const string Hour = "timetable.hour";
    }

    public static class Reports
    {
        public const string GroupReportTitle = "reports.groupReportTitle";
        public const string PrintDate = "reports.printDate";
        public const string SessionsCount = "reports.sessionsCount";
        public const string AttendanceSummary = "reports.attendanceSummary";
        public const string TotalSessions = "reports.totalSessions";
        public const string PresentCount = "reports.presentCount";
        public const string AbsentCount = "reports.absentCount";
        public const string LateCount = "reports.lateCount";
        public const string AttendanceRate = "reports.attendanceRate";
        public const string FinancialSummary = "reports.financialSummary";
        public const string PeriodLabel = "reports.periodLabel";
        public const string TotalAmount = "reports.totalAmount";
        public const string PaidLabel = "reports.paidLabel";
        public const string RemainingLabel = "reports.remainingLabel";
        public const string NoDataPeriod = "reports.noDataPeriod";
        public const string ManagerSignature = "reports.managerSignature";
        public const string AttendanceReportTitle = "reports.attendanceReport";
        public const string FromDate = "reports.fromDate";
        public const string ToDate = "reports.toDate";
        public const string ExportPdf = "reports.exportPdf";
        public const string ExportExcel = "reports.exportExcel";
        public const string FilterStudent = "reports.filterStudent";
        public const string FilterGroup = "reports.filterGroup";
        public const string AllStudents = "reports.allStudents";
        public const string NoRecords = "reports.noRecords";
        public const string GeneratedAt = "reports.generatedAt";
        public const string TotalStudents = "reports.totalStudents";
        public const string TotalRecords = "reports.totalRecords";
        public const string ExportSuccess = "reports.exportSuccess";
        public const string AttendanceReportFile = "reports.attendanceReportFile";
        public const string Summary = "reports.summary";
        public const string Print = "reports.print";
    }

    public static class Levels
    {
        public const string Title = "levels.title";
        public const string Add = "levels.add";
        public const string Edit = "levels.edit";
        public const string DeleteMessage = "levels.deleteMessage";
        public const string DeleteBlockedGrades = "levels.deleteBlockedGrades";
        public const string DeleteBlockedGroups = "levels.deleteBlockedGroups";
    }

    public static class Grades
    {
        public const string Title = "grades.title";
        public const string Add = "grades.add";
        public const string Edit = "grades.edit";
        public const string ManageSubjects = "grades.manageSubjects";
        public const string DeleteMessage = "grades.deleteMessage";
        public const string DeleteBlockedGroups = "grades.deleteBlockedGroups";
    }

    public static class Subjects
    {
        public const string Title = "subjects.title";
        public const string Add = "subjects.add";
        public const string Edit = "subjects.edit";
        public const string DeleteMessage = "subjects.deleteMessage";
        public const string DeleteBlockedGroups = "subjects.deleteBlockedGroups";
        public const string DeleteBlockedGrades = "subjects.deleteBlockedGrades";
    }

    public static class Teachers
    {
        public const string Title = "teachers.title";
        public const string Add = "teachers.add";
        public const string Edit = "teachers.edit";
        public const string DeleteMessage = "teachers.deleteMessage";
        public const string DeleteBlockedGroups = "teachers.deleteBlockedGroups";
    }

    public static class Validators
    {
        public const string Required = "validators.required";
        public const string MaxLength = "validators.maxLength";
        public const string InvalidEmail = "validators.invalidEmail";
        public const string InvalidPhone = "validators.invalidPhone";
        public const string InvalidNumber = "validators.invalidNumber";
        public const string OrderInvalid = "validators.orderInvalid";
        public const string CodeExists = "validators.codeExists";
        public const string InvalidDate = "validators.invalidDate";
        public const string InvalidField = "validators.invalidField";
        public const string InvalidCode = "validators.invalidCode";
        public const string PositiveNumber = "validators.positiveNumber";
    }

    public static class Dialogs
    {
        public const string ConfirmDelete = "dialogs.confirmDelete";
        public const string CannotDelete = "dialogs.cannotDelete";
        public const string Alert = "dialogs.alert";
    }

    public static class Finance
    {
        public const string Title = "finance.title";
    }

    public static class PaymentMethods
    {
        public const string Cash = "paymentMethods.cash";
        public const string BankTransfer = "paymentMethods.bankTransfer";
        public const string Check = "paymentMethods.check";
        public const string Other = "paymentMethods.other";
    }

    // Phase 7: sessions, attendance, homework and the SMS suggestions.
    public static class Attendance
    {
        public const string Title = "attendance.title";
        public const string Topic = "attendance.topic";
        public const string TodaySessions = "attendance.todaySessions";
        public const string AllSessions = "attendance.allSessions";
        public const string TakeAttendance = "attendance.takeAttendance";
        public const string SessionDetails = "attendance.sessionDetails";
        public const string Present = "attendance.present";
        public const string Absent = "attendance.absent";
        public const string Late = "attendance.late";
        public const string SetAllPresent = "attendance.setAllPresent";
        public const string SetAllAbsent = "attendance.setAllAbsent";
        public const string SetAllLate = "attendance.setAllLate";
        public const string SaveAttendance = "attendance.saveAttendance";
        public const string SaveSuccess = "attendance.saveSuccess";
        public const string ScanQr = "attendance.scanQr";
        public const string ScanQrTitle = "attendance.scanQrTitle";
        public const string ScanHint = "attendance.scanHint";
        public const string ScanSuccess = "attendance.scanSuccess";
        public const string ScanDuplicate = "attendance.scanDuplicate";
        public const string ScanWrongGroup = "attendance.scanWrongGroup";
        public const string ScanInvalid = "attendance.scanInvalid";
        public const string ScanSummary = "attendance.scanSummary";
        public const string Scanned = "attendance.scanned";
        public const string Waiting = "attendance.waiting";
        public const string FinishScan = "attendance.finishScan";
        public const string CancelScan = "attendance.cancelScan";
        public const string ContinueScan = "attendance.continueScan";
        public const string CreateSession = "attendance.createSession";
        public const string SessionType = "attendance.sessionType";
        public const string Regular = "attendance.regular";
        public const string Makeup = "attendance.makeup";
        public const string Extra = "attendance.extra";
        public const string ExtraPrice = "attendance.extraPrice";
        public const string NoSessions = "attendance.noSessions";
        public const string NoSessionsToday = "attendance.noSessionsToday";
        public const string NoSessionsDate = "attendance.noSessionsDate";
        public const string CancelSession = "attendance.cancelSession";
        public const string PostponeSession = "attendance.postponeSession";
        public const string RestoreSession = "attendance.restoreSession";
        public const string CancelReason = "attendance.cancelReason";
        public const string CancelReasonHint = "attendance.cancelReasonHint";
        public const string Cancelled = "attendance.cancelled";
        public const string Completed = "attendance.completed";
        public const string Scheduled = "attendance.scheduled";
        public const string Postponed = "attendance.postponed";
        public const string InProgress = "attendance.inProgress";
        public const string SessionDate = "attendance.sessionDate";
        public const string PickDate = "attendance.pickDate";
        public const string HomeworksDue = "attendance.homeworksDue";
        public const string Submissions = "attendance.submissions";
        public const string NoSubmissions = "attendance.noSubmissions";
        public const string NoHomeworks = "attendance.noHomeworks";
        public const string Students = "attendance.students";
        public const string PresentCount = "attendance.presentCount";
        public const string AbsentCount = "attendance.absentCount";
        public const string LateCount = "attendance.lateCount";
        public const string TotalCount = "attendance.totalCount";
        public const string InvalidTimeRange = "attendance.invalidTimeRange";
        public const string ScanWrongGroupDetailed = "attendance.scanWrongGroupDetailed";
        public const string PaidExtraSession = "attendance.paidExtraSession";
        public const string SessionNotFound = "attendance.sessionNotFound";
        public const string NoGroups = "attendance.noGroups";
        public const string SessionDeleted = "attendance.sessionDeleted";
        public const string SessionCancelled = "attendance.sessionCancelled";
        public const string SessionRestored = "attendance.sessionRestored";
        public const string SessionCreated = "attendance.sessionCreated";
        public const string SessionUpdated = "attendance.sessionUpdated";
        public const string CancelReasonDisplay = "attendance.cancelReasonDisplay";
        public const string Pay = "attendance.pay";
        public const string Exempt = "attendance.exempt";
        public const string Unpaid = "attendance.unpaid";
        public const string Paid = "attendance.paid";
        public const string Partial = "attendance.partial";
        public const string PaymentStatus = "attendance.paymentStatus";
        public const string SmsSuggestion = "attendance.smsSuggestion";
        public const string SessionClosed = "attendance.sessionClosed";
        public const string ManualEntry = "attendance.manualEntry";
        public const string BarcodeHint = "attendance.barcodeHint";
        public const string CameraUnavailable = "attendance.cameraUnavailable";
        public const string PaymentSaved = "attendance.paymentSaved";
        public const string NoStudents = "attendance.noStudents";
        public const string StartSession = "attendance.startSession";
        public const string SmsSent = "attendance.smsSent";
        public const string SessionPostponed = "attendance.sessionPostponed";
        public const string ScanAccepted = "attendance.scanAccepted";
        public const string PaymentAmount = "attendance.paymentAmount";
        public const string PaymentMethod = "attendance.paymentMethod";
        public const string SessionExists = "attendance.sessionExists";
        public const string SmsOpenFailed = "attendance.smsOpenFailed";
    }

    public static class Homework
    {
        public const string Title = "homework.title";
        public const string Add = "homework.add";
        public const string Edit = "homework.edit";
        public const string HomeworkTitle = "homework.homeworkTitle";
        public const string Content = "homework.content";
        public const string DueDate = "homework.dueDate";
        public const string Submitted = "homework.submitted";
        public const string Late = "homework.late";
        public const string Missing = "homework.missing";
        public const string Pending = "homework.pending";
        public const string SubmissionsCount = "homework.submissionsCount";
        public const string StudentsCount = "homework.studentsCount";
        public const string NoHomeworks = "homework.noHomeworks";
        public const string NoHomeworksPeriod = "homework.noHomeworksPeriod";
        public const string NoSessionsAvailable = "homework.noSessionsAvailable";
        public const string NoSessionsMessage = "homework.noSessionsMessage";
        public const string ReportTitle = "homework.reportTitle";
        public const string StudentsList = "homework.studentsList";
        public const string SubmissionsList = "homework.submissionsList";
        public const string NextSession = "homework.nextSession";
        public const string NoSession = "homework.noSession";
        public const string SessionInfo = "homework.sessionInfo";
        public const string AddedSuccess = "homework.addedSuccess";
        public const string UpdatedSuccess = "homework.updatedSuccess";
        public const string NotFound = "homework.notFound";
        public const string SubmissionNotFound = "homework.submissionNotFound";
        public const string FetchSessionsFailed = "homework.fetchSessionsFailed";
        public const string Session = "homework.session";
        public const string DueForSession = "homework.dueForSession";
        public const string NoSessionsTitle = "homework.noSessionsTitle";
        public const string Checking = "homework.checking";
        public const string NoSessionMessage = "homework.noSessionMessage";
        public const string AllGroups = "homework.allGroups";
        public const string SubmissionSaved = "homework.submissionSaved";
        public const string SaveFailed = "homework.saveFailed";
    }

    public static class Notifications
    {
        public const string Title = "notifications.title";
        public const string AbsenceAlert = "notifications.absenceAlert";
        public const string AbsenceAlertTitle = "notifications.absenceAlertTitle";
        public const string AbsenceBodyDetailed = "notifications.absenceBodyDetailed";
        public const string SendSms = "notifications.sendSms";
        public const string NoPhoneNumber = "notifications.noPhoneNumber";
        public const string TypeAttendance = "notifications.typeAttendance";
    }

    public static class SmsTemplates
    {
        public const string SessionCancelled = "smsTemplates.sessionCancelled";
        public const string MakeupSession = "smsTemplates.makeupSession";
        public const string PostponedSession = "smsTemplates.postponedSession";
        public const string AbsenceAlert = "smsTemplates.absenceAlert";
    }

    public static class Settings
    {
        public const string Title = "settings.title";
        public const string Appearance = "settings.appearance";
        public const string DarkMode = "settings.darkMode";
        public const string Language = "settings.language";
        public const string Arabic = "settings.arabic";
        public const string English = "settings.english";
        public const string French = "settings.french";
        public const string AppVersion = "settings.appVersion";
        public const string AboutDescription = "settings.aboutDescription";
    }

    public static class License
    {
        public const string Title = "license.title";
        public const string Subtitle = "license.subtitle";
        public const string DeviceFingerprint = "license.deviceFingerprint";
        public const string CopyFingerprint = "license.copyFingerprint";
        public const string FingerprintCopied = "license.fingerprintCopied";
        public const string FingerprintHint = "license.fingerprintHint";
        public const string FingerprintWeakWarning = "license.fingerprintWeakWarning";
        public const string LicenseKey = "license.licenseKey";
        public const string LicenseKeyHint = "license.licenseKeyHint";
        public const string Activate = "license.activate";
        public const string Activating = "license.activating";
        public const string Activated = "license.activated";
        public const string EnterKey = "license.enterKey";
        public const string PasteFromClipboard = "license.pasteFromClipboard";
        public const string ContactProvider = "license.contactProvider";
        public const string StatusTitle = "license.statusTitle";
        public const string StatusActive = "license.statusActive";
        public const string StatusExpiringSoon = "license.statusExpiringSoon";
        public const string StatusGracePeriod = "license.statusGracePeriod";
        public const string StatusReadOnly = "license.statusReadOnly";
        public const string StatusNotStarted = "license.statusNotStarted";
        public const string StatusUnlicensed = "license.statusUnlicensed";
        public const string StatusInvalid = "license.statusInvalid";
        public const string ExpiryDate = "license.expiryDate";
        public const string DaysRemaining = "license.daysRemaining";
        public const string Day = "license.day";
        public const string LicensedTo = "license.licensedTo";
        public const string NotLicensed = "license.notLicensed";
        public const string ErrorMalformed = "license.errorMalformed";
        public const string ErrorBadSignature = "license.errorBadSignature";
        public const string ErrorFingerprintMismatch = "license.errorFingerprintMismatch";
        public const string ErrorUnsupportedVersion = "license.errorUnsupportedVersion";
        public const string ErrorNotConfigured = "license.errorNotConfigured";
        public const string ErrorWriteBlocked = "license.errorWriteBlocked";
        public const string WarningExpiringSoon = "license.warningExpiringSoon";
        public const string WarningGracePeriod = "license.warningGracePeriod";
        public const string GracePeriodDetail = "license.gracePeriodDetail";
        public const string WarningReadOnly = "license.warningReadOnly";
        public const string NotStarted = "license.notStarted";
        public const string StartsOn = "license.startsOn";
        public const string RenewNow = "license.renewNow";
        public const string ClockTamperTitle = "license.clockTamperTitle";
        public const string ClockTamperBody = "license.clockTamperBody";
        public const string Dismiss = "license.dismiss";
        public const string ShowLicense = "license.showLicense";
        public const string Deactivate = "license.deactivate";
        public const string DeactivateConfirm = "license.deactivateConfirm";
        public const string Deactivated = "license.deactivated";
    }

    public static class Messages
    {
        public const string SavedSuccessfully = "messages.savedSuccessfully";
        public const string AddSuccess = "messages.addSuccess";
        public const string UpdateSuccess = "messages.updateSuccess";
        public const string DeleteSuccess = "messages.deleteSuccess";
    }

    public static class Errors
    {
        public const string Unexpected = "errors.unexpected";
        public const string SaveError = "errors.saveError";
        public const string SaveGradeSubjects = "errors.saveGradeSubjects";
        public const string DatabaseFailure = "errors.databaseFailure";
        public const string NotFound = "errors.notFound";
        public const string RequiredFields = "errors.requiredFields";
        public const string ImportFile = "errors.importFile";
        public const string ExportFile = "errors.exportFile";
        public const string CardRender = "errors.cardRender";
    }

    public static class Days
    {
        public const string Saturday = "days.saturday";
        public const string Sunday = "days.sunday";
        public const string Monday = "days.monday";
        public const string Tuesday = "days.tuesday";
        public const string Wednesday = "days.wednesday";
        public const string Thursday = "days.thursday";
        public const string Friday = "days.friday";
    }

    /// <summary>
    /// Phase D1 - debug design gallery. These keys are only read by the
    /// F12 window, which never ships in a release build, but they live in the
    /// normal translation files so the parity test still guards them.
    /// </summary>
    public static class Design
    {
        public const string Title = "design.title";
        public const string Subtitle = "design.subtitle";
        public const string Theme = "design.theme";
        public const string Direction = "design.direction";
        public const string DarkMode = "design.darkMode";
        public const string RightToLeft = "design.rightToLeft";
        public const string Palette = "design.palette";
        public const string PaletteHint = "design.paletteHint";
        public const string Typography = "design.typography";
        public const string Spacing = "design.spacing";
        public const string Radii = "design.radii";
        public const string Elevation = "design.elevation";
        public const string Buttons = "design.buttons";
        public const string Inputs = "design.inputs";
        public const string DataViews = "design.dataViews";
        public const string Navigation = "design.navigation";
        public const string Feedback = "design.feedback";
        public const string Motion = "design.motion";
        public const string Icons = "design.icons";
        public const string Accessibility = "design.accessibility";
        public const string AccessibilityHint = "design.accessibilityHint";
        public const string SampleTitle = "design.sampleTitle";
        public const string SampleSubtitle = "design.sampleSubtitle";
        public const string SampleBody = "design.sampleBody";
        public const string SampleCaption = "design.sampleCaption";
        public const string SampleNumeric = "design.sampleNumeric";
        public const string SampleLabel = "design.sampleLabel";
        public const string SamplePlaceholder = "design.samplePlaceholder";
        public const string SampleCombo = "design.sampleCombo";
        public const string SampleOptionA = "design.sampleOptionA";
        public const string SampleOptionB = "design.sampleOptionB";
        public const string SampleCheck = "design.sampleCheck";
        public const string SampleRadio = "design.sampleRadio";
        public const string SampleToggle = "design.sampleToggle";
        public const string SampleColumnName = "design.sampleColumnName";
        public const string SampleColumnAmount = "design.sampleColumnAmount";
        public const string SampleColumnStatus = "design.sampleColumnStatus";
        public const string SampleStudentA = "design.sampleStudentA";
        public const string SampleStudentB = "design.sampleStudentB";
        public const string SampleStudentC = "design.sampleStudentC";
        public const string SampleCardTitle = "design.sampleCardTitle";
        public const string SampleCardBody = "design.sampleCardBody";
        public const string SampleBanner = "design.sampleBanner";
        public const string SampleBadge = "design.sampleBadge";
        public const string SampleTabA = "design.sampleTabA";
        public const string SampleTabB = "design.sampleTabB";
        public const string SampleTabC = "design.sampleTabC";
        public const string SampleTooltip = "design.sampleTooltip";
        public const string SampleRequired = "design.sampleRequired";
        public const string SampleDisabled = "design.sampleDisabled";
        public const string MotionHint = "design.motionHint";
        public const string MotionOn = "design.motionOn";
        public const string MotionOff = "design.motionOff";
        public const string ShortcutHint = "design.shortcutHint";

        // Phase D3 - reusable component gallery.
        public const string Components = "design.components";
        public const string PageHeader = "design.pageHeader";
        public const string StatCards = "design.statCards";
        public const string DataTable = "design.dataTable";
        public const string States = "design.states";
        public const string Forms = "design.forms";
        public const string Overlays = "design.overlays";
        public const string Badges = "design.badges";
        public const string Indicators = "design.indicators";
        public const string Pickers = "design.pickers";
        public const string Schedule = "design.schedule";
        public const string QrCard = "design.qrCard";
        public const string Charts = "design.charts";
        public const string SampleBreadcrumb = "design.sampleBreadcrumb";
        public const string SamplePageDescription = "design.samplePageDescription";
        public const string SampleStatStudents = "design.sampleStatStudents";
        public const string SampleStatGroups = "design.sampleStatGroups";
        public const string SampleStatAttendance = "design.sampleStatAttendance";
        public const string SampleStatOutstanding = "design.sampleStatOutstanding";
        public const string SampleTrendUp = "design.sampleTrendUp";
        public const string SampleTrendDown = "design.sampleTrendDown";
        public const string SampleDetailTitle = "design.sampleDetailTitle";
        public const string SampleDetailBody = "design.sampleDetailBody";
        public const string SampleEmptyTitle = "design.sampleEmptyTitle";
        public const string SampleEmptyDescription = "design.sampleEmptyDescription";
        public const string SampleEmptyAction = "design.sampleEmptyAction";
        public const string SampleErrorTitle = "design.sampleErrorTitle";
        public const string SampleErrorMessage = "design.sampleErrorMessage";
        public const string SampleFieldName = "design.sampleFieldName";
        public const string SampleFieldDescription = "design.sampleFieldDescription";
        public const string SampleFieldError = "design.sampleFieldError";
        public const string SampleSectionTitle = "design.sampleSectionTitle";
        public const string SampleSectionDescription = "design.sampleSectionDescription";
        public const string SampleStepA = "design.sampleStepA";
        public const string SampleStepB = "design.sampleStepB";
        public const string SampleStepC = "design.sampleStepC";
        public const string SamplePanelTitle = "design.samplePanelTitle";
        public const string SamplePanelBody = "design.samplePanelBody";
        public const string SampleDialogTitle = "design.sampleDialogTitle";
        public const string SampleDialogDescription = "design.sampleDialogDescription";
        public const string OpenPanel = "design.openPanel";
        public const string OpenDialog = "design.openDialog";
        public const string SampleCapacityCaption = "design.sampleCapacityCaption";
        public const string SampleProgressTitle = "design.sampleProgressTitle";
        public const string SampleProgressValue = "design.sampleProgressValue";
        public const string SampleRating = "design.sampleRating";
        public const string SampleSearchPlaceholder = "design.sampleSearchPlaceholder";
        public const string SampleTagPlaceholder = "design.sampleTagPlaceholder";
        public const string SampleTagA = "design.sampleTagA";
        public const string SampleTagB = "design.sampleTagB";
        public const string SampleTagC = "design.sampleTagC";
        public const string SampleScheduleTitle = "design.sampleScheduleTitle";
        public const string SampleScheduleDescription = "design.sampleScheduleDescription";
        public const string SampleScheduleEmpty = "design.sampleScheduleEmpty";
        public const string SampleQrTitle = "design.sampleQrTitle";
        public const string SampleQrSubtitle = "design.sampleQrSubtitle";
        public const string SampleQrFooter = "design.sampleQrFooter";
        public const string SampleChartTitle = "design.sampleChartTitle";
        public const string SampleChartDescription = "design.sampleChartDescription";
        public const string SampleChartSummary = "design.sampleChartSummary";
    }

    /// <summary>
    /// Phase D3 - labels and hints owned by the reusable controls. A control
    /// never contains a literal string, so these keys are the single place a
    /// table, schedule or badge label is translated.
    /// </summary>
    public static class Components
    {
        public const string Columns = "components.columns";
        public const string Density = "components.density";
        public const string Export = "components.export";
        public const string Selected = "components.selected";
        public const string Page = "components.page";
        public const string Conflict = "components.conflict";
        public const string ScheduleLegend = "components.scheduleLegend";
    }

    /// <summary>Phase D3 - month names for the YYYY-MM picker.</summary>
    public static class Months
    {
        public const string January = "months.january";
        public const string February = "months.february";
        public const string March = "months.march";
        public const string April = "months.april";
        public const string May = "months.may";
        public const string June = "months.june";
        public const string July = "months.july";
        public const string August = "months.august";
        public const string September = "months.september";
        public const string October = "months.october";
        public const string November = "months.november";
        public const string December = "months.december";
    }

    /// <summary>Phase D2 - side navigation groups.</summary>
    public static class Nav
    {
        public const string GroupHome = "nav.groupHome";
        public const string GroupAcademic = "nav.groupAcademic";
        public const string GroupFinance = "nav.groupFinance";
        public const string GroupCommunication = "nav.groupCommunication";
        public const string GroupSystem = "nav.groupSystem";
    }

    /// <summary>Phase D2 - shell chrome: title bar, nav rail footer, top bar.</summary>
    public static class Shell
    {
        public const string Search = "shell.search";
        public const string ExpandNav = "shell.expandNav";
        public const string CollapseNav = "shell.collapseNav";
        public const string Back = "shell.back";
        public const string Help = "shell.help";
        public const string Unread = "shell.unread";
        public const string ComingSoon = "shell.comingSoon";
        public const string NotificationsComingSoon = "shell.notificationsComingSoon";
        public const string MessagesComingSoon = "shell.messagesComingSoon";
        public const string Theme = "shell.theme";
        public const string Language = "shell.language";
        public const string LicenseStatus = "shell.licenseStatus";
        public const string SchoolNameFallback = "shell.schoolNameFallback";
    }

    /// <summary>Phase D2 - Ctrl+K command palette.</summary>
    public static class Palette
    {
        public const string Title = "palette.title";
        public const string Placeholder = "palette.placeholder";
        public const string NoResults = "palette.noResults";
        public const string Students = "palette.students";
        public const string Groups = "palette.groups";
        public const string Commands = "palette.commands";
        public const string AddStudent = "palette.addStudent";
        public const string NewGroup = "palette.newGroup";
        public const string NewHomework = "palette.newHomework";
        public const string TakeAttendance = "palette.takeAttendance";
        public const string RecordPayment = "palette.recordPayment";
        public const string OpenSettings = "palette.openSettings";
        public const string ToggleTheme = "palette.toggleTheme";
        public const string ShowShortcuts = "palette.showShortcuts";
        public const string Hint = "palette.hint";
    }

    /// <summary>Phase D2 - F1 shortcuts help window.</summary>
    public static class Shortcuts
    {
        public const string Title = "shortcuts.title";
        public const string Subtitle = "shortcuts.subtitle";
        public const string GroupNavigation = "shortcuts.groupNavigation";
        public const string GroupActions = "shortcuts.groupActions";
        public const string GroupGeneral = "shortcuts.groupGeneral";
        public const string New = "shortcuts.new";
        public const string Search = "shortcuts.search";
        public const string Save = "shortcuts.save";
        public const string Print = "shortcuts.print";
        public const string Close = "shortcuts.close";
        public const string NavigateTabs = "shortcuts.navigateTabs";
        public const string CommandPalette = "shortcuts.commandPalette";
        public const string Help = "shortcuts.help";
        public const string Back = "shortcuts.back";
        public const string ToggleNav = "shortcuts.toggleNav";
        public const string KeysNew = "shortcuts.keysNew";
        public const string KeysSearch = "shortcuts.keysSearch";
        public const string KeysSearchAlt = "shortcuts.keysSearchAlt";
        public const string KeysSave = "shortcuts.keysSave";
        public const string KeysPrint = "shortcuts.keysPrint";
        public const string KeysClose = "shortcuts.keysClose";
        public const string KeysNavigateTabs = "shortcuts.keysNavigateTabs";
        public const string KeysHelp = "shortcuts.keysHelp";
        public const string KeysBack = "shortcuts.keysBack";
        public const string KeysToggleNav = "shortcuts.keysToggleNav";
    }

    /// <summary>Phase D2 - in-app toast notifications.</summary>
    public static class Toast
    {
        public const string ThemeChanged = "toast.themeChanged";
        public const string LanguageChanged = "toast.languageChanged";
        public const string LicenseRenewed = "toast.licenseRenewed";
        public const string LicenseRenewFailed = "toast.licenseRenewFailed";
        public const string Dismiss = "toast.dismiss";
    }
}
