using System.Globalization;
using System.Text.RegularExpressions;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.Academic;

/// <summary>Columns a screen may sort a reference list by.</summary>
public enum AcademicSortField
{
    Name = 0,
    Order = 1,
    LinkedItems = 2,
}

public sealed record AcademicListQuery(
    string? SearchTerm,
    bool IncludeInactive,
    AcademicSortField SortBy = AcademicSortField.Name,
    bool Descending = false);

public sealed record AcademicLevelInput(string Name, int OrderIndex, bool IsActive);

public sealed record AcademicGradeInput(string Name, int? AcademicLevelId, int OrderIndex, bool IsActive);

public sealed record AcademicSubjectInput(string Name, string? Code, string? Description, bool IsActive);

public sealed record AcademicTeacherInput(
    string FullName,
    string? Phone,
    string? Email,
    string? Address,
    string? Specialization,
    DateTime? HireDate,
    bool IsActive);

/// <summary>
/// Pure validation shared by the academic write use cases and the ViewModels.
/// It never touches the database, so a screen can show the same translated
/// message before the write is attempted. Uniqueness of a subject code is the
/// only rule that needs I/O and lives in the use case instead.
/// </summary>
public static class AcademicValidator
{
    public const int NameMaxLength = 100;
    public const int CodeMaxLength = 20;
    public const int DescriptionMaxLength = 500;
    public const int AddressMaxLength = 250;
    public const int SpecializationMaxLength = 100;
    public const int EmailMaxLength = 150;

    private static readonly Regex PhonePattern = new(
        @"^[0-9+\s().-]{6,20}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex EmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static Failure? ValidateLevel(AcademicLevelInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return ValidateName(input.Name) ?? ValidateOrder(input.OrderIndex);
    }

    public static Failure? ValidateGrade(AcademicGradeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return ValidateName(input.Name) ?? ValidateOrder(input.OrderIndex);
    }

    public static Failure? ValidateSubject(AcademicSubjectInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return ValidateName(input.Name)
            ?? ValidateCode(input.Code)
            ?? ValidateDescription(input.Description);
    }

    public static Failure? ValidateTeacher(AcademicTeacherInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return ValidateName(input.FullName)
            ?? ValidatePhone(input.Phone)
            ?? ValidateEmail(input.Email)
            ?? ValidateAddress(input.Address)
            ?? ValidateSpecialization(input.Specialization);
    }

    public static Failure? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Failure.Validation(TranslationKeys.Validators.Required);
        }

        return name.Trim().Length > NameMaxLength
            ? LengthFailure(NameMaxLength)
            : null;
    }

    public static Failure? ValidateOrder(int orderIndex) =>
        orderIndex < 0 ? Failure.Validation(TranslationKeys.Validators.OrderInvalid) : null;

    public static Failure? ValidateCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return code.Trim().Length > CodeMaxLength
            ? LengthFailure(CodeMaxLength)
            : null;
    }

    public static Failure? ValidateDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        return description.Trim().Length > DescriptionMaxLength
            ? LengthFailure(DescriptionMaxLength)
            : null;
    }

    public static Failure? ValidatePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        return PhonePattern.IsMatch(phone.Trim())
            ? null
            : Failure.Validation(TranslationKeys.Validators.InvalidPhone);
    }

    public static Failure? ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var trimmed = email.Trim();
        return trimmed.Length <= EmailMaxLength && EmailPattern.IsMatch(trimmed)
            ? null
            : Failure.Validation(TranslationKeys.Validators.InvalidEmail);
    }

    public static Failure? ValidateAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        return address.Trim().Length > AddressMaxLength
            ? LengthFailure(AddressMaxLength)
            : null;
    }

    public static Failure? ValidateSpecialization(string? specialization)
    {
        if (string.IsNullOrWhiteSpace(specialization))
        {
            return null;
        }

        return specialization.Trim().Length > SpecializationMaxLength
            ? LengthFailure(SpecializationMaxLength)
            : null;
    }

    public static AcademicLevel ToEntity(AcademicLevelInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new AcademicLevel
        {
            Name = input.Name.Trim(),
            OrderIndex = input.OrderIndex,
            IsActive = input.IsActive,
        };
    }

    public static Grade ToEntity(AcademicGradeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new Grade
        {
            Name = input.Name.Trim(),
            AcademicLevelId = input.AcademicLevelId,
            OrderIndex = input.OrderIndex,
            IsActive = input.IsActive,
        };
    }

    public static Subject ToEntity(AcademicSubjectInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new Subject
        {
            Name = input.Name.Trim(),
            Code = string.IsNullOrWhiteSpace(input.Code) ? null : input.Code.Trim(),
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            IsActive = input.IsActive,
        };
    }

    public static Teacher ToEntity(AcademicTeacherInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new Teacher
        {
            FullName = input.FullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim(),
            Address = string.IsNullOrWhiteSpace(input.Address) ? null : input.Address.Trim(),
            Specialization = string.IsNullOrWhiteSpace(input.Specialization) ? null : input.Specialization.Trim(),
            HireDate = input.HireDate,
            IsActive = input.IsActive,
        };
    }

    private static Failure LengthFailure(int maxLength) =>
        Failure.Validation(
            TranslationKeys.Validators.MaxLength,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["count"] = maxLength.ToString(CultureInfo.InvariantCulture),
            });
}
