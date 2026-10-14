namespace ClinicManagement.Application.Common;

public sealed record Error(string Code, string Message, IReadOnlyDictionary<string, string[]>? Details = null);

public abstract class OperationResult
{
    protected OperationResult(bool succeeded, Error? error)
    {
        Succeeded = succeeded;
        Error = error;
    }

    public bool Succeeded { get; }

    public bool Failed => !Succeeded;

    public Error? Error { get; }
}

public sealed class Result : OperationResult
{
    private Result(bool succeeded, Error? error)
        : base(succeeded, error)
    {
    }

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) => new(false, error);

    public static Result Failure(string code, string message) => new(false, new Error(code, message));
}

public sealed class Result<T> : OperationResult
{
    private Result(bool succeeded, T? value, Error? error)
        : base(succeeded, error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(Error error) => new(false, default, error);

    public static Result<T> Failure(string code, string message) => new(false, default, new Error(code, message));
}