namespace CineSched.Core.Common;

public sealed record Error(
    string Code,
    string Message,
    IReadOnlyDictionary<string, object?>? Details = null)
{
    public string MessageKey => Code;
}

public readonly record struct Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(string code, string message, IReadOnlyDictionary<string, object?>? details = null) =>
        new(default, new Error(code, message, details));
}

public readonly record struct Unit
{
    public static Unit Value { get; } = new();
}
