using System.Reflection;
using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Licensing;
using SchoolManagement.Infrastructure.Licensing;

namespace SchoolManagement.Tests.Licensing;

public sealed class LicenseGuardTests
{
    private static LicenseGuard GuardFor(LicenseStatus status)
    {
        var service = Substitute.For<ILicenseService>();
        service.LoadAndVerifyAsync(Arg.Any<CancellationToken>())
            .Returns(new LicenseInfo(status, LicenseTestKit.Fingerprint));
        return new LicenseGuard(service);
    }

    [Theory]
    [InlineData(LicenseStatus.Active)]
    [InlineData(LicenseStatus.ExpiringSoon)]
    [InlineData(LicenseStatus.GracePeriod)]
    public void Writable_states_pass_the_gate(LicenseStatus status)
    {
        var result = GuardFor(status).EnsureCanWrite();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Theory]
    [InlineData(LicenseStatus.ReadOnly)]
    [InlineData(LicenseStatus.NotStarted)]
    [InlineData(LicenseStatus.Unlicensed)]
    [InlineData(LicenseStatus.Invalid)]
    public void All_other_states_block_writing_with_the_shared_key(LicenseStatus status)
    {
        var result = GuardFor(status).EnsureCanWrite();

        result.IsFailure.Should().BeTrue();
        result.Failure!.Kind.Should().Be(FailureKind.License);
        result.Failure.MessageKey.Should().Be(TranslationKeys.License.ErrorWriteBlocked);
        result.Failure.Detail.Should().Be(status.ToString());
    }

    [Fact]
    public void A_license_failure_is_reported_in_the_detail()
    {
        var service = Substitute.For<ILicenseService>();
        service.LoadAndVerifyAsync(Arg.Any<CancellationToken>())
            .Returns(LicenseInfo.Invalid(LicenseFailureReason.BadSignature, "f"));

        var result = new LicenseGuard(service).EnsureCanWrite();

        result.Failure!.Detail.Should().Be(nameof(LicenseFailureReason.BadSignature));
    }

    [Fact]
    public void The_guard_never_throws_for_a_blocked_write()
    {
        var act = () => GuardFor(LicenseStatus.ReadOnly).EnsureCanWrite();

        act.Should().NotThrow();
    }
}

public sealed class WriteUseCaseGuardTests
{
    private static ILicenseGuard AllowingGuard()
    {
        var guard = Substitute.For<ILicenseGuard>();
        guard.EnsureCanWrite().Returns(Result<bool>.Success(true));
        return guard;
    }

    private static ILicenseGuard BlockingGuard()
    {
        var guard = Substitute.For<ILicenseGuard>();
        guard.EnsureCanWrite().Returns(Result<bool>.Fail(
            Failure.License(TranslationKeys.License.ErrorWriteBlocked)));
        return guard;
    }

    [Fact]
    public async Task Add_student_writes_when_the_guard_allows_it()
    {
        var repository = Substitute.For<IStudentRepository>();
        repository.AddAsync(Arg.Any<Student>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(42));

        var result = await new AddStudentUseCase(repository, AllowingGuard(), new Student())
            .ExecuteAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        await repository.Received(1).AddAsync(Arg.Any<Student>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Add_student_never_reaches_the_repository_when_the_license_is_read_only()
    {
        var repository = Substitute.For<IStudentRepository>();

        var result = await new AddStudentUseCase(repository, BlockingGuard(), new Student())
            .ExecuteAsync();

        result.IsFailure.Should().BeTrue();
        result.Failure!.MessageKey.Should().Be(TranslationKeys.License.ErrorWriteBlocked);
        await repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task The_guard_is_called_exactly_once_per_write()
    {
        var repository = Substitute.For<IStudentRepository>();
        repository.GetDependenciesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<StudentDependencies>.Success(new StudentDependencies(0, 0, 0)));
        repository.DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));
        var guard = AllowingGuard();

        await new DeleteStudentUseCase(repository, guard).ExecuteAsync(7);

        guard.Received(1).EnsureCanWrite();
    }

    [Fact]
    public async Task A_second_write_use_case_is_also_gated()
    {
        var repository = Substitute.For<IAttendanceRepository>();

        var result = await new SaveAttendanceUseCase(
                repository, BlockingGuard(), sessionId: 1, studentId: 2, status: "present")
            .ExecuteAsync();

        result.Failure!.MessageKey.Should().Be(TranslationKeys.License.ErrorWriteBlocked);
        await repository.DidNotReceiveWithAnyArgs().UpsertAsync(default, default, default!, default, default);
    }

    [Fact]
    public async Task Settings_writes_are_gated_too()
    {
        var repository = Substitute.For<ISettingsRepository>();

        var result = await new UpdateSystemSettingUseCase(
                repository, BlockingGuard(), "absence_limit", "5")
            .ExecuteAsync();

        result.IsFailure.Should().BeTrue();
        await repository.DidNotReceiveWithAnyArgs()
            .SetSystemSettingAsync(default!, default!, default);
    }

    [Fact]
    public async Task A_settings_write_passes_through_when_allowed()
    {
        var repository = Substitute.For<ISettingsRepository>();
        repository.SetSystemSettingAsync("absence_limit", "6", Arg.Any<CancellationToken>())
            .Returns(Result<bool>.Success(true));

        var result = await new UpdateSystemSettingUseCase(
                repository, AllowingGuard(), "absence_limit", "6")
            .ExecuteAsync();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Payments_are_gated()
    {
        var repository = Substitute.For<IPaymentRepository>();

        var result = await new AddPaymentUseCase(repository, BlockingGuard(), new Payment())
            .ExecuteAsync();

        result.IsFailure.Should().BeTrue();
        await repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }
}

/// <summary>
/// Reflection invariants: a new use case cannot silently skip the guard, and
/// no use case can be registered as a write without deriving from the template.
/// </summary>
public sealed class WriteOperationConventionTests
{
    private static readonly Assembly ApplicationAssembly = typeof(WriteUseCaseBase).Assembly;

    private static Type[] WriteOperations() =>
        ApplicationAssembly.GetTypes()
            .Where(type => type.GetCustomAttribute<WriteOperationAttribute>() is not null)
            .ToArray();

    [Fact]
    public void At_least_seventeen_write_operations_are_declared()
    {
        WriteOperations().Should().HaveCountGreaterThanOrEqualTo(17);
    }

    [Fact]
    public void Every_write_operation_derives_from_the_guarded_template()
    {
        WriteOperations().Should().OnlyContain(type => typeof(WriteUseCaseBase).IsAssignableFrom(type));
    }

    [Fact]
    public void Every_write_operation_is_a_concrete_class_with_execute_async()
    {
        foreach (var type in WriteOperations())
        {
            type.IsAbstract.Should().BeFalse($"{type.Name} must be instantiable");
            type.IsClass.Should().BeTrue($"{type.Name} must be a class");
            type.GetMethod("ExecuteAsync", BindingFlags.Public | BindingFlags.Instance)
                .Should().NotBeNull($"{type.Name} must expose ExecuteAsync");
        }
    }

    [Fact]
    public void Every_guarded_use_case_is_marked_as_a_write_operation()
    {
        var unmarked = ApplicationAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => typeof(WriteUseCaseBase).IsAssignableFrom(type))
            .Where(type => type.GetCustomAttribute<WriteOperationAttribute>() is null)
            .ToArray();

        unmarked.Should().BeEmpty();
    }
}
