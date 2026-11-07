using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Policies;

namespace SchoolManagement.Application.Homeworks;

/// <summary>Create or update one homework row.</summary>
public sealed record HomeworkSaveRequest(
    int GroupId,
    int SessionId,
    int? TargetSessionId,
    string Title,
    string Description,
    DateTime DueDate,
    string? Notes = null,
    int? HomeworkId = null);

public sealed record HomeworkSaveOutcome(int HomeworkId, bool Created);

/// <summary>One submission status change from the submissions overlay.</summary>
public sealed record HomeworkSubmissionRequest(
    int HomeworkId,
    int StudentId,
    string Status,
    DateTime? SubmittedAt = null,
    string? Notes = null);

/// <summary>One homework plus the roster of its group, pending rows included.</summary>
public sealed record HomeworkSubmissionBoard(
    HomeworkListItem Homework,
    IReadOnlyList<HomeworkSubmissionItem> Submissions)
{
    public int SubmittedCount => Submissions.Count(row =>
        HomeworkPolicy.CountsAsSubmitted(row.Status));

    public int PendingCount => Submissions.Count - SubmittedCount;
}

public sealed record HomeworkReportQuery(
    int? GroupId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null);

/// <summary>Everything the homework report page shows in one read.</summary>
public sealed record HomeworkReportData(
    string SchoolName,
    int? GroupId,
    string? GroupName,
    DateTime? FromDate,
    DateTime? ToDate,
    IReadOnlyList<HomeworkListItem> Homeworks,
    DateTime GeneratedAt)
{
    public int TotalCount => Homeworks.Count;

    public int CompletedCount => Homeworks.Count(homework => homework.IsCompleted);

    public int SubmittedTotal => Homeworks.Sum(homework => homework.SubmittedCount);

    public int PendingTotal => Homeworks.Sum(homework => homework.PendingCount);
}

/// <summary>Pure validation of the shared homework form.</summary>
public static class HomeworkValidator
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 4000;

    public static Failure? Validate(HomeworkSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.GroupId <= 0)
        {
            return Failure.Validation(TranslationKeys.Validators.Required, "groupId");
        }

        if (request.SessionId <= 0)
        {
            return Failure.Validation(TranslationKeys.Validators.Required, "sessionId");
        }

        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return Failure.Validation(TranslationKeys.Validators.Required, "title");
        }

        if (title.Length > TitleMaxLength)
        {
            return Failure.Validation(
                TranslationKeys.Validators.MaxLength,
                "title",
                parameters: MaxLengthParameters(TitleMaxLength));
        }

        if (request.Description is { Length: > DescriptionMaxLength })
        {
            return Failure.Validation(
                TranslationKeys.Validators.MaxLength,
                "description",
                parameters: MaxLengthParameters(DescriptionMaxLength));
        }

        if (request.DueDate == default || request.DueDate.Kind != DateTimeKind.Utc)
        {
            return Failure.Validation(TranslationKeys.Validators.InvalidDate, "dueDate");
        }

        return null;
    }

    public static HomeworkSaveRequest Normalize(HomeworkSaveRequest request) => request with
    {
        Title = request.Title.Trim(),
        Description = request.Description.Trim(),
        TargetSessionId = request.TargetSessionId is > 0 ? request.TargetSessionId : null,
        Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        DueDate = new DateTime(
            request.DueDate.Year,
            request.DueDate.Month,
            request.DueDate.Day,
            0,
            0,
            0,
            DateTimeKind.Utc),
    };

    public static IReadOnlyDictionary<string, string> MaxLengthParameters(int count)
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["count"] = count.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
}
