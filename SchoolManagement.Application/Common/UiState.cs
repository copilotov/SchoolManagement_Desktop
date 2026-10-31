namespace SchoolManagement.Application.Common;

/// <summary>
/// The single UI state model for every screen:
/// Initial / Loading / Success / Error / Empty.
/// ViewModels expose it; views render it with data templates.
/// </summary>
public abstract record UiState<T>
{
    public bool IsInitial => this is Initial;

    public bool IsLoading => this is Loading;

    public bool IsSuccess => this is Success;

    public bool IsError => this is Error;

    public bool IsEmpty => this is Empty;

    public bool IsCompleted => IsSuccess || IsEmpty;

    public T? Data => this is Success success ? success.Value : default;

    public Failure? Failure => this is Error error ? error.FailureValue : null;

    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Failure, TResult> onFailure,
        Func<TResult> onInitial,
        Func<TResult> onLoading,
        Func<TResult> onEmpty)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        ArgumentNullException.ThrowIfNull(onInitial);
        ArgumentNullException.ThrowIfNull(onLoading);
        ArgumentNullException.ThrowIfNull(onEmpty);

        return this switch
        {
            Initial => onInitial(),
            Loading => onLoading(),
            Success success => onSuccess(success.Value),
            Error error => onFailure(error.FailureValue),
            Empty => onEmpty(),
            _ => throw new InvalidOperationException($"Unknown UI state: {GetType().Name}"),
        };
    }

    public UiState<TOut> Map<TOut>(Func<T, TOut> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return this switch
        {
            Initial => new UiState<TOut>.Initial(),
            Loading => new UiState<TOut>.Loading(),
            Success success => new UiState<TOut>.Success(mapper(success.Value)),
            Error error => new UiState<TOut>.Error(error.FailureValue),
            Empty => new UiState<TOut>.Empty(),
            _ => throw new InvalidOperationException($"Unknown UI state: {GetType().Name}"),
        };
    }

    public sealed record Initial : UiState<T>;

    public sealed record Loading : UiState<T>;

    public sealed record Success(T Value) : UiState<T>;

    public sealed record Error(Failure FailureValue) : UiState<T>;

    public sealed record Empty : UiState<T>;
}
