namespace BuildingBlocks;

public sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);

    public T GetValueOrThrow() => IsSuccess ? Value! : throw new InvalidOperationException(Error ?? "Operation failed.");
}

public sealed record Result
{
    public static Result Success() => new();
    public static Result Failure(string error) => new(error);

    private Result(string? error = null)
    {
        Error = error;
    }

    public bool IsSuccess => string.IsNullOrWhiteSpace(Error);
    public string? Error { get; }
}
