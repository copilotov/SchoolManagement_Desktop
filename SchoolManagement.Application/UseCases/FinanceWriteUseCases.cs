using SchoolManagement.Application.Common;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.Application.UseCases;

[WriteOperation]
public sealed class AddPaymentUseCase : WriteUseCase<int>
{
    private readonly IPaymentRepository _repository;
    private readonly Payment _payment;

    public AddPaymentUseCase(IPaymentRepository repository, ILicenseGuard guard, Payment payment)
        : base(guard)
    {
        _repository = repository;
        _payment = payment;
    }

    protected override Task<Result<int>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.AddAsync(_payment, cancellationToken);
}

[WriteOperation]
public sealed class AddHomeworkUseCase : WriteUseCase<int>
{
    private readonly IHomeworkRepository _repository;
    private readonly Homework _homework;

    public AddHomeworkUseCase(IHomeworkRepository repository, ILicenseGuard guard, Homework homework)
        : base(guard)
    {
        _repository = repository;
        _homework = homework;
    }

    protected override Task<Result<int>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.AddAsync(_homework, cancellationToken);
}

[WriteOperation]
public sealed class SaveHomeworkSubmissionUseCase : WriteUseCase<bool>
{
    private readonly IHomeworkRepository _repository;
    private readonly int _homeworkId;
    private readonly int _studentId;
    private readonly string _status;
    private readonly DateTime? _submittedAt;
    private readonly string? _notes;

    public SaveHomeworkSubmissionUseCase(
        IHomeworkRepository repository,
        ILicenseGuard guard,
        int homeworkId,
        int studentId,
        string status,
        DateTime? submittedAt = null,
        string? notes = null)
        : base(guard)
    {
        _repository = repository;
        _homeworkId = homeworkId;
        _studentId = studentId;
        _status = status;
        _submittedAt = submittedAt;
        _notes = notes;
    }

    protected override Task<Result<bool>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.SetSubmissionStatusAsync(
            _homeworkId,
            _studentId,
            _status,
            _submittedAt,
            _notes,
            cancellationToken);
}

[WriteOperation]
public sealed class AddNotificationUseCase : WriteUseCase<int>
{
    private readonly INotificationRepository _repository;
    private readonly Notification _notification;

    public AddNotificationUseCase(INotificationRepository repository, ILicenseGuard guard, Notification notification)
        : base(guard)
    {
        _repository = repository;
        _notification = notification;
    }

    protected override Task<Result<int>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.AddAsync(_notification, cancellationToken);
}

[WriteOperation]
public sealed class MarkNotificationReadUseCase : WriteUseCase<bool>
{
    private readonly INotificationRepository _repository;
    private readonly int _notificationId;

    public MarkNotificationReadUseCase(INotificationRepository repository, ILicenseGuard guard, int notificationId)
        : base(guard)
    {
        _repository = repository;
        _notificationId = notificationId;
    }

    protected override Task<Result<bool>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.MarkReadAsync(_notificationId, cancellationToken);
}

[WriteOperation]
public sealed class UpdateSystemSettingUseCase : WriteUseCase<bool>
{
    private readonly ISettingsRepository _repository;
    private readonly string _key;
    private readonly string _value;

    public UpdateSystemSettingUseCase(ISettingsRepository repository, ILicenseGuard guard, string key, string value)
        : base(guard)
    {
        _repository = repository;
        _key = key;
        _value = value;
    }

    protected override Task<Result<bool>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.SetSystemSettingAsync(_key, _value, cancellationToken);
}

[WriteOperation]
public sealed class UpdateFeeSettingsUseCase : WriteUseCase<bool>
{
    private readonly ISettingsRepository _repository;
    private readonly FeeSetting _settings;

    public UpdateFeeSettingsUseCase(ISettingsRepository repository, ILicenseGuard guard, FeeSetting settings)
        : base(guard)
    {
        _repository = repository;
        _settings = settings;
    }

    protected override Task<Result<bool>> ExecuteCoreAsync(CancellationToken cancellationToken) =>
        _repository.UpdateFeeSettingsAsync(_settings, cancellationToken);
}
