using System.Globalization;
using System.Text.RegularExpressions;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;

namespace SchoolManagement.Application.Students;

/// <summary>Column a student list may be sorted by.</summary>
public enum StudentSortField
{
    Name = 0,
    Code = 1,
    Level = 2,
    Group = 3,
    Status = 4,
}

/// <summary>
/// Deliberate student query: every filter is applied in SQL, never in memory.
/// A null filter means "all". <see cref="Take"/> is capped by the repository.
/// </summary>
public sealed record StudentListQuery(
    string? SearchTerm = null,
    string? Status = null,
    int? AcademicLevelId = null,
    int? GradeId = null,
    int? GroupId = null,
    StudentSortField SortBy = StudentSortField.Name,
    bool Descending = false,
    int Skip = 0,
    int Take = 500);

/// <summary>All fields of the shared student form, including the guardian.</summary>
public sealed record StudentSaveRequest(
    string FirstName,
    string LastName,
    string? StudentCode,
    DateTime? DateOfBirth,
    string? Gender,
    string? Phone,
    string? Address,
    string Status,
    string? Notes,
    double RegistrationFee,
    string? GuardianName,
    string? GuardianPhone,
    string? GuardianEmail,
    string? GuardianRelationship,
    int? GuardianId = null,
    int? StudentId = null);

public sealed record StudentSaveOutcome(int StudentId, bool Created, string StudentCode);

/// <summary>Dependency counts used to block a destructive delete.</summary>
public sealed record StudentDependencies(int Enrollments, int Payments, int Attendance)
{
    public bool HasLinks => Enrollments > 0 || Payments > 0 || Attendance > 0;
}

/// <summary>Everything the printable card needs, already joined.</summary>
public sealed record StudentCardData(
    int StudentId,
    string StudentCode,
    string FullName,
    string? LevelName,
    string? GradeName,
    string? GroupName,
    string? GuardianName,
    string? GuardianPhone,
    string SchoolName,
    string? AcademicYear);

public enum StudentFileFormat
{
    Csv = 0,
    Excel = 1,
}

/// <summary>One raw row read from a CSV/Excel file, before validation.</summary>
public sealed record StudentImportRow(
    int RowNumber,
    string FirstName,
    string LastName,
    string? StudentCode,
    string? Phone,
    string? GuardianName,
    string? GuardianPhone,
    string? Status,
    double RegistrationFee,
    DateTime? DateOfBirth = null,
    string? Gender = null,
    string? Address = null,
    string? Notes = null,
    string? GuardianEmail = null,
    string? GuardianRelationship = null);

/// <summary>A per-row import failure. The message key stays translatable.</summary>
public sealed record StudentImportError(
    int RowNumber,
    string Reference,
    string MessageKey,
    IReadOnlyDictionary<string, string>? Parameters = null);

public sealed record StudentImportResult(
    int Created,
    IReadOnlyList<StudentImportError> Errors)
{
    public int Failed => Errors.Count;

    public bool HasErrors => Errors.Count > 0;
}

public sealed record ParsedStudentFile(
    IReadOnlyList<StudentImportRow> Rows,
    IReadOnlyList<StudentImportError> Errors);

/// <summary>One QR code of the bulk print page, already rendered.</summary>
public sealed record StudentQrItem(int StudentId, string StudentCode, string FullName, byte[] Png);

/// <summary>One fully joined row for CSV/Excel export.</summary>
public sealed record StudentExportRow(
    int Id,
    string StudentCode,
    string FirstName,
    string LastName,
    DateTime? DateOfBirth,
    string? Gender,
    string? Phone,
    string? Address,
    string Status,
    string? Notes,
    double RegistrationFee,
    string? GuardianName,
    string? GuardianPhone,
    string? GuardianEmail,
    string? GuardianRelationship,
    string? LevelName,
    string? GradeName,
    string? GroupName);

/// <summary>
/// Normalizes Algerian phone numbers. The desktop stores the international
/// form (+213XXXXXXXXX) so WhatsApp/sms links work without guessing the prefix.
/// </summary>
public static class AlgeriaPhone
{
    public const string CountryCode = "+213";

    private static readonly Regex Allowed = new(
        @"^\+213[1-9][0-9]{7,8}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns the normalized number, or null when the input is blank. An
    /// invalid value is returned unchanged so validation can reject it.
    /// </summary>
    public static string? Normalize(string? raw)
        => TryNormalize(raw, out var normalized) ? normalized : raw?.Trim();

    public static bool TryNormalize(string? raw, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var compact = raw.Trim();
        foreach (var separator in new[] { " ", "-", "(", ")", ".", "\u00A0" })
        {
            compact = compact.Replace(separator, string.Empty, StringComparison.Ordinal);
        }

        if (compact.StartsWith("00", StringComparison.Ordinal))
        {
            compact = "+" + compact[2..];
        }

        string digits;
        if (compact.StartsWith(CountryCode, StringComparison.Ordinal))
        {
            digits = compact[CountryCode.Length..];
        }
        else if (compact.StartsWith("213", StringComparison.Ordinal) && compact.Length > 3)
        {
            digits = compact[3..];
        }
        else if (compact.StartsWith('0'))
        {
            digits = compact[1..];
        }
        else
        {
            digits = compact;
        }

        if (digits.Length is < 8 or > 9 || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        var candidate = CountryCode + digits;
        if (!Allowed.IsMatch(candidate))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    public static bool IsValid(string? raw)
        => TryNormalize(raw, out _);
}

/// <summary>Pure student-form validation shared by the editor and the importer.</summary>
public static class StudentValidator
{
    public const int NameMaxLength = 100;
    public const int CodeMaxLength = 40;
    public const int NotesMaxLength = 1000;
    public const int AddressMaxLength = 255;
    public const int GuardianRelationshipMaxLength = 50;

    private static readonly Regex CodePattern = new(
        @"^[A-Za-z0-9-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex EmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static Failure? Validate(StudentSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var first = request.FirstName?.Trim();
        var last = request.LastName?.Trim();

        if (string.IsNullOrWhiteSpace(first))
        {
            return Failure.Validation(TranslationKeys.Validators.Required, "firstName");
        }

        if (first.Length > NameMaxLength)
        {
            return Failure.Validation(
                TranslationKeys.Validators.MaxLength,
                "firstName",
                parameters: MaxLengthParameters(NameMaxLength));
        }

        if (string.IsNullOrWhiteSpace(last))
        {
            return Failure.Validation(TranslationKeys.Validators.Required, "lastName");
        }

        if (last.Length > NameMaxLength)
        {
            return Failure.Validation(
                TranslationKeys.Validators.MaxLength,
                "lastName",
                parameters: MaxLengthParameters(NameMaxLength));
        }

        var code = request.StudentCode?.Trim();
        if (!string.IsNullOrEmpty(code))
        {
            if (code.Length > CodeMaxLength)
            {
                return Failure.Validation(
                    TranslationKeys.Validators.MaxLength,
                    "studentCode",
                    parameters: MaxLengthParameters(CodeMaxLength));
            }

            if (!CodePattern.IsMatch(code))
            {
                return Failure.Validation(TranslationKeys.Validators.InvalidCode, "studentCode");
            }
        }

        if (request.DateOfBirth is { } birthDate)
        {
            if (birthDate.Kind != DateTimeKind.Utc)
            {
                return Failure.Validation(TranslationKeys.Validators.InvalidDate, "dateOfBirth");
            }

            if (birthDate.Year is < 1900 or > 2100)
            {
                return Failure.Validation(TranslationKeys.Validators.InvalidDate, "dateOfBirth");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Gender)
            && !string.Equals(request.Gender.Trim(), "male", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.Gender.Trim(), "female", StringComparison.OrdinalIgnoreCase))
        {
            return Failure.Validation(TranslationKeys.Validators.InvalidField, "gender");
        }

        if (!string.IsNullOrWhiteSpace(request.Phone) && !AlgeriaPhone.IsValid(request.Phone))
        {
            return Failure.Validation(TranslationKeys.Validators.InvalidPhone, "phone");
        }

        if (!string.IsNullOrWhiteSpace(request.Address) && request.Address.Trim().Length > AddressMaxLength)
        {
            return Failure.Validation(
                TranslationKeys.Validators.MaxLength,
                "address",
                parameters: MaxLengthParameters(AddressMaxLength));
        }

        if (!string.IsNullOrWhiteSpace(request.Notes) && request.Notes.Trim().Length > NotesMaxLength)
        {
            return Failure.Validation(
                TranslationKeys.Validators.MaxLength,
                "notes",
                parameters: MaxLengthParameters(NotesMaxLength));
        }

        if (request.RegistrationFee < 0)
        {
            return Failure.Validation(TranslationKeys.Validators.PositiveNumber, "registrationFee");
        }

        if (!string.IsNullOrWhiteSpace(request.Status)
            && request.Status.Trim() is not ("active" or "inactive" or "withdrawn"))
        {
            return Failure.Validation(TranslationKeys.Validators.InvalidField, "status");
        }

        var guardianName = request.GuardianName?.Trim();
        var guardianPhone = request.GuardianPhone?.Trim();
        if (string.IsNullOrEmpty(guardianName) && !string.IsNullOrEmpty(guardianPhone))
        {
            return Failure.Validation(TranslationKeys.Validators.Required, "guardianName");
        }

        if (!string.IsNullOrEmpty(guardianName) && guardianName.Length > NameMaxLength)
        {
            return Failure.Validation(
                TranslationKeys.Validators.MaxLength,
                "guardianName",
                parameters: MaxLengthParameters(NameMaxLength));
        }

        if (!string.IsNullOrEmpty(guardianPhone) && !AlgeriaPhone.IsValid(guardianPhone))
        {
            return Failure.Validation(TranslationKeys.Validators.InvalidPhone, "guardianPhone");
        }

        if (!string.IsNullOrWhiteSpace(request.GuardianEmail) && !EmailPattern.IsMatch(request.GuardianEmail.Trim()))
        {
            return Failure.Validation(TranslationKeys.Validators.InvalidEmail, "guardianEmail");
        }

        if (!string.IsNullOrWhiteSpace(request.GuardianRelationship)
            && request.GuardianRelationship.Trim().Length > GuardianRelationshipMaxLength)
        {
            return Failure.Validation(
                TranslationKeys.Validators.MaxLength,
                "guardianRelationship",
                parameters: MaxLengthParameters(GuardianRelationshipMaxLength));
        }

        return null;
    }

    public static string FormatDate(DateTime? date)
        => date is null
            ? string.Empty
            : date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static IReadOnlyDictionary<string, string> MaxLengthParameters(int count)
        => new Dictionary<string, string>(StringComparer.Ordinal) { ["count"] = count.ToString(CultureInfo.InvariantCulture) };
}
