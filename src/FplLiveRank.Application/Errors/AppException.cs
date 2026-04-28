namespace FplLiveRank.Application.Errors;

public class AppException : Exception
{
    public string Code { get; }
    public string? Details { get; }

    public AppException(string code, string message, string? details = null, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
        Details = details;
    }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message, string? details = null)
        : base("NOT_FOUND", message, details) { }
}

public sealed class ExternalServiceException : AppException
{
    public ExternalServiceException(string code, string message, string? details = null, Exception? inner = null)
        : base(code, message, details, inner) { }
}

public sealed class ValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("VALIDATION_FAILED", "Request validation failed.")
    {
        Errors = errors;
    }
}
