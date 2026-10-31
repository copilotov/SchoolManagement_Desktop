using FluentAssertions;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;

namespace SchoolManagement.Tests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Success_exposes_value_and_has_no_failure()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
        result.ValueOrDefault.Should().Be(42);
        result.Failure.Should().BeNull();
    }

    [Fact]
    public void Failure_returns_the_failure_and_never_exposes_a_value()
    {
        var failure = Failure.NotFound("students.notFound", "Student 7");

        var result = Result<string>.Fail(failure);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Failure.Should().BeSameAs(failure);
        result.ValueOrDefault.Should().BeNull();
        result.Invoking(value => _ = value.Value)
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Map_keeps_a_failure_without_calling_the_mapper()
    {
        var mapperCalled = false;
        var failure = Failure.Database();

        var mapped = Result<int>.Fail(failure).Map(value =>
        {
            mapperCalled = true;
            return value * 2;
        });

        mapperCalled.Should().BeFalse();
        mapped.IsFailure.Should().BeTrue();
        mapped.Failure.Should().BeSameAs(failure);
    }

    [Fact]
    public void Bind_short_circuits_on_failure()
    {
        var binderCalled = false;
        var failure = Failure.License(TranslationKeys.Errors.Unexpected);

        var bound = Result<int>.Fail(failure).Bind(value =>
        {
            binderCalled = true;
            return Result<string>.Success(value.ToString());
        });

        binderCalled.Should().BeFalse();
        bound.Failure.Should().BeSameAs(failure);
    }

    [Fact]
    public void Match_runs_exactly_one_branch()
    {
        var success = Result<int>.Success(5).Match(value => value + 1, _ => 0);
        var failure = Result<int>.Fail(Failure.Unexpected()).Match(value => value + 1, _ => -1);

        success.Should().Be(6);
        failure.Should().Be(-1);
    }

    [Fact]
    public void Validation_failure_adds_the_field_parameter()
    {
        var failure = Failure.Validation("students.nameRequired", field: "name");

        failure.Kind.Should().Be(FailureKind.Validation);
        failure.MessageKey.Should().Be("students.nameRequired");
        failure.Parameters.Should().ContainKey("field").WhoseValue.Should().Be("name");
    }

    [Fact]
    public void Unexpected_failure_uses_a_translation_key_present_in_the_files()
    {
        var failure = Failure.Unexpected();

        failure.Kind.Should().Be(FailureKind.Unexpected);
        failure.MessageKey.Should().Be("errors.unexpected");
    }

    [Fact]
    public void Failure_rejects_an_empty_message_key()
    {
        var action = () => new Failure(FailureKind.Unexpected, " ");

        action.Should().Throw<ArgumentException>();
    }
}
