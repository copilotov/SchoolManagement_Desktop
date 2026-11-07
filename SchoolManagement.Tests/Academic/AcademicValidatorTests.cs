using FluentAssertions;
using SchoolManagement.Application.Academic;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;

namespace SchoolManagement.Tests.Academic;

public sealed class AcademicValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_name_is_required(string? name)
    {
        var failure = AcademicValidator.ValidateName(name);

        failure.Should().NotBeNull();
        failure!.Kind.Should().Be(FailureKind.Validation);
        failure.MessageKey.Should().Be(TranslationKeys.Validators.Required);
    }

    [Fact]
    public void A_name_longer_than_the_limit_is_rejected_with_the_count()
    {
        var failure = AcademicValidator.ValidateName(new string('a', AcademicValidator.NameMaxLength + 1));

        failure.Should().NotBeNull();
        failure!.MessageKey.Should().Be(TranslationKeys.Validators.MaxLength);
        failure.Parameters["count"].Should().Be(AcademicValidator.NameMaxLength.ToString());
    }

    [Fact]
    public void A_name_at_the_limit_is_accepted()
    {
        AcademicValidator.ValidateName(new string('a', AcademicValidator.NameMaxLength))
            .Should().BeNull();
    }

    [Theory]
    [InlineData(-1)]
    public void A_negative_order_is_rejected(int order)
    {
        var failure = AcademicValidator.ValidateLevel(new AcademicLevelInput("Level", order, true));

        failure.Should().NotBeNull();
        failure!.MessageKey.Should().Be(TranslationKeys.Validators.OrderInvalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    public void A_zero_or_positive_order_is_accepted(int order)
    {
        AcademicValidator.ValidateLevel(new AcademicLevelInput("Level", order, true))
            .Should().BeNull();
    }

    [Fact]
    public void A_grade_uses_the_same_name_and_order_rules()
    {
        AcademicValidator.ValidateGrade(new AcademicGradeInput("", null, 0, true))!
            .MessageKey.Should().Be(TranslationKeys.Validators.Required);

        AcademicValidator.ValidateGrade(new AcademicGradeInput("Grade", null, -2, true))!
            .MessageKey.Should().Be(TranslationKeys.Validators.OrderInvalid);

        AcademicValidator.ValidateGrade(new AcademicGradeInput("Grade", 1, 2, true))
            .Should().BeNull();
    }

    [Fact]
    public void A_code_longer_than_the_limit_is_rejected()
    {
        var failure = AcademicValidator.ValidateCode(new string('c', AcademicValidator.CodeMaxLength + 1));

        failure.Should().NotBeNull();
        failure!.MessageKey.Should().Be(TranslationKeys.Validators.MaxLength);
        failure.Parameters["count"].Should().Be(AcademicValidator.CodeMaxLength.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("MATH")]
    public void A_code_is_optional(string? code)
    {
        AcademicValidator.ValidateCode(code).Should().BeNull();
    }

    [Fact]
    public void A_description_longer_than_the_limit_is_rejected()
    {
        var failure = AcademicValidator.ValidateDescription(
            new string('d', AcademicValidator.DescriptionMaxLength + 1));

        failure.Should().NotBeNull();
        failure!.Parameters["count"].Should().Be(AcademicValidator.DescriptionMaxLength.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0555 12 34 56")]
    [InlineData("+213(0)555-12-34")]
    public void A_valid_or_empty_phone_is_accepted(string? phone)
    {
        AcademicValidator.ValidatePhone(phone).Should().BeNull();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("12ab34")]
    public void An_invalid_phone_is_rejected(string phone)
    {
        AcademicValidator.ValidatePhone(phone)!
            .MessageKey.Should().Be(TranslationKeys.Validators.InvalidPhone);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("teacher@example.com")]
    public void A_valid_or_empty_email_is_accepted(string? email)
    {
        AcademicValidator.ValidateEmail(email).Should().BeNull();
    }

    [Theory]
    [InlineData("teacher")]
    [InlineData("teacher@")]
    [InlineData("@example.com")]
    [InlineData("a b@example.com")]
    public void An_invalid_email_is_rejected(string email)
    {
        AcademicValidator.ValidateEmail(email)!
            .MessageKey.Should().Be(TranslationKeys.Validators.InvalidEmail);
    }

    [Fact]
    public void An_email_longer_than_the_limit_is_rejected()
    {
        var email = $"{new string('a', AcademicValidator.EmailMaxLength)}@example.com";

        AcademicValidator.ValidateEmail(email)!
            .MessageKey.Should().Be(TranslationKeys.Validators.InvalidEmail);
    }

    [Fact]
    public void Address_and_specialization_respect_their_limits()
    {
        AcademicValidator.ValidateAddress(new string('a', AcademicValidator.AddressMaxLength + 1))!
            .MessageKey.Should().Be(TranslationKeys.Validators.MaxLength);

        AcademicValidator.ValidateSpecialization(
                new string('s', AcademicValidator.SpecializationMaxLength + 1))!
            .MessageKey.Should().Be(TranslationKeys.Validators.MaxLength);
    }

    [Fact]
    public void A_teacher_validates_every_optional_contact_field()
    {
        var failure = AcademicValidator.ValidateTeacher(new AcademicTeacherInput(
            "Teacher",
            "not a phone",
            "bad email",
            null,
            null,
            null,
            true));

        failure.Should().NotBeNull();
        failure!.MessageKey.Should().Be(TranslationKeys.Validators.InvalidPhone);
    }

    [Fact]
    public void ToEntity_trims_values_and_turns_blanks_into_null()
    {
        var entity = AcademicValidator.ToEntity(new AcademicTeacherInput(
            "  Teacher  ",
            "  ",
            " t@example.com ",
            null,
            " Math ",
            null,
            true));

        entity.FullName.Should().Be("Teacher");
        entity.Phone.Should().BeNull();
        entity.Email.Should().Be("t@example.com");
        entity.Address.Should().BeNull();
        entity.Specialization.Should().Be("Math");
    }

    [Fact]
    public void Subject_and_level_entities_are_normalized()
    {
        var subject = AcademicValidator.ToEntity(
            new AcademicSubjectInput(" Math ", " M1 ", "  ", true));

        subject.Name.Should().Be("Math");
        subject.Code.Should().Be("M1");
        subject.Description.Should().BeNull();

        var level = AcademicValidator.ToEntity(new AcademicLevelInput(" Level ", 3, false));

        level.Name.Should().Be("Level");
        level.OrderIndex.Should().Be(3);
        level.IsActive.Should().BeFalse();
    }
}
