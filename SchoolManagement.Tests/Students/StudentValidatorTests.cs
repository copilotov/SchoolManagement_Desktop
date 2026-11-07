using FluentAssertions;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Students;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Tests.Students;

/// <summary>
/// Pure validation of the shared student form and of the Algerian phone
/// normalizer used by both the editor and the importer.
/// </summary>
public sealed class StudentValidatorTests
{
    private static StudentSaveRequest Valid() => new(
        "Alice",
        "Anderson",
        null,
        new DateTime(2010, 5, 4, 0, 0, 0, DateTimeKind.Utc),
        "female",
        "+213555123456",
        "12 Rue des Ecoles",
        StudentStatuses.Active,
        "Note",
        1500,
        "Guardian One",
        "+213661123456",
        "guardian@example.com",
        "mother");

    [Fact]
    public void A_complete_form_is_valid()
    {
        StudentValidator.Validate(Valid()).Should().BeNull();
        StudentValidator.Validate(Valid() with
        {
            StudentCode = "STU-2026-0001",
            Gender = "male",
            Phone = null,
            GuardianPhone = null,
            GuardianEmail = null,
            Notes = null,
        }).Should().BeNull();
    }

    [Fact]
    public void The_validator_requires_a_request()
    {
        var act = () => StudentValidator.Validate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("", "Anderson", "firstName")]
    [InlineData("   ", "Anderson", "firstName")]
    [InlineData("Alice", "", "lastName")]
    [InlineData("Alice", "  ", "lastName")]
    public void First_and_last_name_are_required(string first, string last, string field)
    {
        var failure = StudentValidator.Validate(Valid() with { FirstName = first, LastName = last });

        failure.Should().NotBeNull();
        failure!.MessageKey.Should().Be(TranslationKeys.Validators.Required);
        failure.Parameters["field"].Should().Be(field);
    }

    [Fact]
    public void Names_are_length_limited()
    {
        var failure = StudentValidator.Validate(Valid() with
        {
            FirstName = new string('a', StudentValidator.NameMaxLength + 1),
        });

        failure.Should().NotBeNull();
        failure!.MessageKey.Should().Be(TranslationKeys.Validators.MaxLength);
        failure.Parameters["field"].Should().Be("firstName");
        failure.Parameters["count"].Should().Be(StudentValidator.NameMaxLength.ToString());
    }

    [Theory]
    [InlineData("STU 2026 1")]
    [InlineData("STU/2026/1")]
    [InlineData("STU_2026_1")]
    public void The_code_only_accepts_letters_digits_and_dashes(string code)
    {
        var failure = StudentValidator.Validate(Valid() with { StudentCode = code });

        failure.Should().NotBeNull();
        failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidCode);
        failure.Parameters["field"].Should().Be("studentCode");
    }

    [Fact]
    public void The_code_is_length_limited()
    {
        var failure = StudentValidator.Validate(Valid() with
        {
            StudentCode = new string('A', StudentValidator.CodeMaxLength + 1),
        });

        failure!.MessageKey.Should().Be(TranslationKeys.Validators.MaxLength);
        failure.Parameters["count"].Should().Be(StudentValidator.CodeMaxLength.ToString());
    }

    [Fact]
    public void The_birth_date_must_be_utc_and_plausible()
    {
        var local = StudentValidator.Validate(Valid() with
        {
            DateOfBirth = new DateTime(2010, 5, 4, 0, 0, 0, DateTimeKind.Local),
        });
        local!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidDate);

        var outOfRange = StudentValidator.Validate(Valid() with
        {
            DateOfBirth = new DateTime(1800, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        outOfRange!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidDate);
    }

    [Theory]
    [InlineData("boy")]
    [InlineData("F")]
    public void The_gender_is_restricted_to_the_two_stored_values(string gender)
    {
        var failure = StudentValidator.Validate(Valid() with { Gender = gender });

        failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidField);
        failure.Parameters["field"].Should().Be("gender");
    }

    [Fact]
    public void The_gender_is_case_insensitive()
    {
        StudentValidator.Validate(Valid() with { Gender = "MALE" }).Should().BeNull();
    }

    [Theory]
    [InlineData("0123")]
    [InlineData("+33 6 12 34 56 78")]
    [InlineData("0555-12-34-5A")]
    public void An_invalid_phone_is_rejected(string phone)
    {
        var failure = StudentValidator.Validate(Valid() with { Phone = phone });

        failure.Should().NotBeNull();
        failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidPhone);
        failure.Parameters["field"].Should().Be("phone");
    }

    [Fact]
    public void Text_fields_are_length_limited()
    {
        StudentValidator.Validate(Valid() with
        {
            Address = new string('a', StudentValidator.AddressMaxLength + 1),
        })!.Parameters["field"].Should().Be("address");

        StudentValidator.Validate(Valid() with
        {
            Notes = new string('a', StudentValidator.NotesMaxLength + 1),
        })!.Parameters["field"].Should().Be("notes");

        StudentValidator.Validate(Valid() with
        {
            GuardianRelationship = new string('a', StudentValidator.GuardianRelationshipMaxLength + 1),
        })!.Parameters["field"].Should().Be("guardianRelationship");
    }

    [Fact]
    public void A_negative_registration_fee_is_rejected()
    {
        var failure = StudentValidator.Validate(Valid() with { RegistrationFee = -1 });

        failure!.MessageKey.Should().Be(TranslationKeys.Validators.PositiveNumber);
        failure.Parameters["field"].Should().Be("registrationFee");
    }

    [Fact]
    public void An_unknown_status_is_rejected()
    {
        var failure = StudentValidator.Validate(Valid() with { Status = "graduated" });

        failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidField);
        failure.Parameters["field"].Should().Be("status");
    }

    [Fact]
    public void A_guardian_phone_requires_a_guardian_name()
    {
        var failure = StudentValidator.Validate(Valid() with { GuardianName = null });

        failure!.MessageKey.Should().Be(TranslationKeys.Validators.Required);
        failure.Parameters["field"].Should().Be("guardianName");
    }

    [Fact]
    public void A_guardian_email_must_look_like_an_email()
    {
        var failure = StudentValidator.Validate(Valid() with { GuardianEmail = "not-an-email" });

        failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidEmail);
        failure.Parameters["field"].Should().Be("guardianEmail");
    }

    [Fact]
    public void An_invalid_guardian_phone_is_rejected()
    {
        var failure = StudentValidator.Validate(Valid() with { GuardianPhone = "12" });

        failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidPhone);
        failure.Parameters["field"].Should().Be("guardianPhone");
    }

    [Fact]
    public void Format_date_is_invariant_and_empty_when_missing()
    {
        StudentValidator.FormatDate(null).Should().BeEmpty();
        StudentValidator.FormatDate(new DateTime(2010, 5, 4, 0, 0, 0, DateTimeKind.Utc))
            .Should().Be("2010-05-04");
    }
}

/// <summary>Algerian phone normalization: every stored number is +213XXXXXXXXX.</summary>
public sealed class AlgeriaPhoneTests
{
    [Theory]
    [InlineData("0555123456", "+213555123456")]
    [InlineData("555123456", "+213555123456")]
    [InlineData("+213555123456", "+213555123456")]
    [InlineData("00213555123456", "+213555123456")]
    [InlineData("213555123456", "+213555123456")]
    [InlineData(" 0555 12 34 56 ", "+213555123456")]
    [InlineData("0555-12-34-56", "+213555123456")]
    [InlineData("(0555) 12.34.56", "+213555123456")]
    [InlineData("055512345", "+21355512345")]
    [InlineData("55512345", "+21355512345")]
    public void Normalize_adds_the_country_code(string raw, string expected)
    {
        AlgeriaPhone.Normalize(raw).Should().Be(expected);
        AlgeriaPhone.IsValid(raw).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_keeps_a_blank_value_null(string? raw)
    {
        AlgeriaPhone.Normalize(raw).Should().BeNull();
        AlgeriaPhone.IsValid(raw).Should().BeTrue();
    }

    [Theory]
    [InlineData("05551234")]
    [InlineData("0555ABC456")]
    [InlineData("+33612345678")]
    [InlineData("+2130123456")]
    public void Invalid_numbers_are_kept_unchanged_and_rejected(string raw)
    {
        AlgeriaPhone.Normalize(raw).Should().Be(raw);
        AlgeriaPhone.IsValid(raw).Should().BeFalse();
    }

    [Fact]
    public void Try_normalize_reports_the_normalized_value()
    {
        var ok = AlgeriaPhone.TryNormalize("0555 12 34 56", out var normalized);

        ok.Should().BeTrue();
        normalized.Should().Be("+213555123456");
    }
}
