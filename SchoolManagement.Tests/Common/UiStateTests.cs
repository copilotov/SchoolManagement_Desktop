using FluentAssertions;
using SchoolManagement.Application.Common;

namespace SchoolManagement.Tests.Common;

public sealed class UiStateTests
{
    [Fact]
    public void Success_exposes_its_data_and_is_completed()
    {
        var state = new UiState<int>.Success(7);

        state.IsSuccess.Should().BeTrue();
        state.IsCompleted.Should().BeTrue();
        state.Data.Should().Be(7);
        state.Failure.Should().BeNull();
    }

    [Fact]
    public void Error_exposes_its_failure()
    {
        var failure = Failure.Database();

        var state = new UiState<int>.Error(failure);

        state.IsError.Should().BeTrue();
        state.Failure.Should().BeSameAs(failure);
        state.Data.Should().Be(0);
    }

    [Fact]
    public void Empty_is_completed_but_has_no_data()
    {
        var state = new UiState<int>.Empty();

        state.IsEmpty.Should().BeTrue();
        state.IsCompleted.Should().BeTrue();
        state.Data.Should().Be(0);
    }

    [Fact]
    public void Match_selects_the_matching_branch()
    {
        UiState<int> state = new UiState<int>.Loading();

        var result = state.Match(
            onSuccess: value => $"success:{value}",
            onFailure: failure => $"error:{failure.Kind}",
            onInitial: () => "initial",
            onLoading: () => "loading",
            onEmpty: () => "empty");

        result.Should().Be("loading");
    }

    [Fact]
    public void Map_preserves_the_state_kind()
    {
        UiState<int> state = new UiState<int>.Success(3);

        var mapped = state.Map(value => value.ToString());

        mapped.Should().BeOfType<UiState<string>.Success>()
            .Which.Value.Should().Be("3");
    }

    [Fact]
    public void Map_preserves_a_failure()
    {
        var failure = Failure.NotFound("groups.notFound");

        var mapped = new UiState<int>.Error(failure).Map(value => value.ToString());

        mapped.Failure.Should().BeSameAs(failure);
    }
}
