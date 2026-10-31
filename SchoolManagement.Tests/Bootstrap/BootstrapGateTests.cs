using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SchoolManagement.Desktop.Bootstrap;

namespace SchoolManagement.Tests.Bootstrap;

public sealed class BootstrapGateTests
{
    [Fact]
    public async Task Splash_is_shown_before_initialization_starts()
    {
        var view = Substitute.For<IBootstrapView>();
        var splashShown = false;
        var initializationSawSplash = false;

        view.When(call => call.ShowSplash()).Do(_ => splashShown = true);

        var gate = new BootstrapGate(
            view,
            _ =>
            {
                initializationSawSplash = splashShown;
                return Task.CompletedTask;
            },
            NullLogger<BootstrapGate>.Instance);

        var succeeded = await gate.RunAsync();

        succeeded.Should().BeTrue();
        initializationSawSplash.Should().BeTrue();
        view.Received(1).ShowSplash();
        view.DidNotReceive().ShowError();
    }

    [Fact]
    public async Task Initialization_failure_shows_the_icon_only_error_screen()
    {
        var view = Substitute.For<IBootstrapView>();

        var gate = new BootstrapGate(
            view,
            _ => throw new InvalidOperationException("database unavailable"),
            NullLogger<BootstrapGate>.Instance);

        var succeeded = await gate.RunAsync();

        succeeded.Should().BeFalse();
        view.Received(1).ShowSplash();
        view.Received(1).ShowError();
    }

    [Fact]
    public async Task Cancellation_does_not_show_the_error_screen()
    {
        var view = Substitute.For<IBootstrapView>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var gate = new BootstrapGate(
            view,
            token => Task.FromCanceled(token),
            NullLogger<BootstrapGate>.Instance);

        var succeeded = await gate.RunAsync(cancellation.Token);

        succeeded.Should().BeFalse();
        view.Received(1).ShowSplash();
        view.DidNotReceive().ShowError();
    }
}
