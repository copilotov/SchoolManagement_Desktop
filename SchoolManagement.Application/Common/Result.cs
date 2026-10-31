namespace SchoolManagement.Application.Common;

/// <summary>
/// The only return type of repositories and use cases. A failure is data, not
/// an exception; repositories must never leak exceptions to the UI.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, Failure? failure)
    {
        IsSuccess = isSuccess;
        _value = value;
        Failure = failure;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Failure? Failure { get; }

    /// <summary>Throws only when the caller ignores <see cref="IsSuccess"/>.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read the value of a failed result ({Failure}).");

    public T? ValueOrDefault => _value;

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Fail(Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new Result<T>(false, default, failure);
    }

    public Result<TOut> Map<TOut>(Func<T, TOut> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return IsSuccess
            ? Result<TOut>.Success(mapper(_value!))
            : Result<TOut>.Fail(Failure!);
    }

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        if (!IsSuccess)
        {
            return Result<TOut>.Fail(Failure!);
        }

        return binder(_value!) ?? throw new InvalidOperationException("A Bind continuation returned null.");
    }

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Failure, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess ? onSuccess(_value!) : onFailure(Failure!);
    }

    public void Match(Action<T> onSuccess, Action<Failure> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (IsSuccess)
        {
            onSuccess(_value!);
        }
        else
        {
            onFailure(Failure!);
        }
    }
}
