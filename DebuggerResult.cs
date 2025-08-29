using System.Diagnostics.CodeAnalysis;

namespace DbgEngMcp;

public class DebuggerResult(bool Success, string? ErrorMessage = null)
{
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool Success { get; } = Success;
    public string? ErrorMessage { get; } = ErrorMessage;

    public static DebuggerResult Ok()
    {
        return new DebuggerResult(true);
    }

    public static DebuggerResult Fail(string errorMessage)
    {
        return new DebuggerResult(false, errorMessage);
    }
}

public class DebuggerResult<T>
{
    [MemberNotNullWhen(true, nameof(Result))]
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool Success { get; private set; }
    public T? Result { get; private set; }
    public string? ErrorMessage { get; private set; }


    public DebuggerResult(bool success, T? result, string errorMessage)
    {
        Result = result;
        ErrorMessage = errorMessage;
        Success = success;
    }

    public static DebuggerResult<T> Ok(T result)
    {
        return new DebuggerResult<T>(true, result, string.Empty);
    }

    public static DebuggerResult<T?> Fail(string errorMessage)
    {
        return new DebuggerResult<T?>(false, default(T), errorMessage);
    }
}