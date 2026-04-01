namespace MunicipiosApi.Domain.Models;

public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public IReadOnlyList<string> Errors { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Errors = [];
    }

    private Result(IReadOnlyList<string> errors)
    {
        IsSuccess = false;
        Value = default;
        Errors = errors;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new([error]);
    public static Result<T> Failure(IReadOnlyList<string> errors) => new(errors);
}
