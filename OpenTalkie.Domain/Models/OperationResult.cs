namespace OpenTalkie.Domain.Models;

public readonly record struct OperationResult(bool IsSuccess, string? ErrorMessage = null)
{
    public static OperationResult Success()
    {
        return new OperationResult(true);
    }

    public static OperationResult Fail(string errorMessage)
    {
        return new OperationResult(false, errorMessage);
    }
}

public readonly record struct OperationResult<T>(bool IsSuccess, T? Value = default, string? ErrorMessage = null)
{
    public static OperationResult<T> Success(T value)
    {
        return new OperationResult<T>(true, value);
    }

    public static OperationResult<T> Fail(string errorMessage)
    {
        return new OperationResult<T>(false, default, errorMessage);
    }
}

