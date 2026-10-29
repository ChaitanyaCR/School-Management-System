namespace SchoolMs.SharedKernel;

/// <summary>Category of a failure; the API layer maps each type to an HTTP status code.</summary>
public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unauthorized,
}

/// <summary>An expected business failure (not an exception). <see cref="Code"/> is stable and machine-readable.</summary>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>Messages per input field (e.g. "password"), for forms. Null when the error is not field-specific.</summary>
    public IReadOnlyDictionary<string, string[]>? Fields { get; init; }

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]> fields) =>
        new(code, message, ErrorType.Validation) { Fields = fields };

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
}
