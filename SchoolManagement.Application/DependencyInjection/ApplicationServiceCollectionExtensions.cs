using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.UseCases;

namespace SchoolManagement.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers application use cases. Repositories are registered by
    /// Infrastructure, and ViewModels by Desktop; this method is the single
    /// place where use cases are added phase by phase.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Phase 4: academic structure (levels, grades, subjects, teachers).
        services.AddTransient<GetAcademicLevelsUseCase>();
        services.AddTransient<GetGradesUseCase>();
        services.AddTransient<GetSubjectsUseCase>();
        services.AddTransient<GetTeachersUseCase>();
        services.AddTransient<GetGradeSubjectsUseCase>();

        services.AddTransient<AddAcademicLevelUseCase>();
        services.AddTransient<UpdateAcademicLevelUseCase>();
        services.AddTransient<DeleteAcademicLevelUseCase>();

        services.AddTransient<AddGradeUseCase>();
        services.AddTransient<UpdateGradeUseCase>();
        services.AddTransient<DeleteGradeUseCase>();
        services.AddTransient<SetGradeSubjectsUseCase>();

        services.AddTransient<AddSubjectUseCase>();
        services.AddTransient<UpdateSubjectUseCase>();
        services.AddTransient<DeleteSubjectUseCase>();

        services.AddTransient<AddTeacherUseCase>();
        services.AddTransient<UpdateTeacherUseCase>();
        services.AddTransient<DeleteTeacherUseCase>();

        // Phase 5: students (list, details, card, import/export).
        services.AddTransient<GetStudentLookupsUseCase>();
        services.AddTransient<GetStudentsUseCase>();
        services.AddTransient<GetStudentDetailsUseCase>();
        services.AddTransient<GetStudentActivityUseCase>();
        services.AddTransient<GetStudentCardDataUseCase>();
        services.AddTransient<SaveStudentUseCase>();
        services.AddTransient<DeleteStudentUseCase>();
        services.AddTransient<ImportStudentsUseCase>();
        services.AddTransient<ExportStudentsUseCase>();
        services.AddTransient<RenderStudentQrUseCase>();
        services.AddTransient<RenderStudentCardPreviewUseCase>();
        services.AddTransient<BuildStudentCardPdfUseCase>();
        services.AddTransient<BuildStudentsCardsPdfUseCase>();
        services.AddTransient<RenderStudentsQrUseCase>();
        services.AddTransient<BuildStudentsQrSheetPdfUseCase>();

        // Phase 6: groups, enrollments, schedule and sessions.
        services.AddTransient<GetGroupLookupsUseCase>();
        services.AddTransient<GetGroupSubjectsUseCase>();
        services.AddTransient<GetGroupsUseCase>();
        services.AddTransient<GetGroupSummaryUseCase>();
        services.AddTransient<GetGroupDetailsUseCase>();
        services.AddTransient<GetGroupScheduleUseCase>();
        services.AddTransient<GetScheduleConflictsUseCase>();
        services.AddTransient<GetTimetableUseCase>();
        services.AddTransient<GetEnrollmentBoardUseCase>();
        services.AddTransient<BuildGroupFullReportUseCase>();

        services.AddTransient<SaveGroupUseCase>();
        services.AddTransient<RemoveGroupUseCase>();
        services.AddTransient<EnrollStudentInGroupUseCase>();
        services.AddTransient<WithdrawEnrollmentUseCase>();
        services.AddTransient<SaveScheduleSlotUseCase>();
        services.AddTransient<DeleteScheduleSlotUseCase>();
        services.AddTransient<GenerateSessionsUseCase>();

        // Phase 7: sessions, attendance, homework and SMS suggestions.
        services.AddTransient<Attendance.GetSessionListUseCase>();
        services.AddTransient<Attendance.GetSessionSummaryUseCase>();
        services.AddTransient<Attendance.GetSessionAttendanceBoardUseCase>();
        services.AddTransient<Attendance.GetAttendanceReportUseCase>();
        services.AddTransient<Attendance.ChangeSessionStatusUseCase>();
        services.AddTransient<Attendance.CreateExtraSessionUseCase>();
        services.AddTransient<Attendance.SaveSessionAttendanceUseCase>();
        services.AddTransient<Attendance.SetSessionPaymentUseCase>();
        services.AddTransient<Attendance.SendSessionSmsUseCase>();
        services.AddTransient<Services.AutoNotificationService>();

        services.AddTransient<Homeworks.GetHomeworkReportUseCase>();
        services.AddTransient<Homeworks.GetHomeworkSubmissionBoardUseCase>();
        services.AddTransient<Homeworks.SaveHomeworkUseCase>();
        services.AddTransient<Homeworks.SetHomeworkSubmissionStatusUseCase>();

        return services;
    }
}
