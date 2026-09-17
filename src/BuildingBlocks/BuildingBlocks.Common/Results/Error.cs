namespace BuildingBlocks.Common.Results;

/// <summary>
/// An expected failure. <see cref="Type"/> lets the API layer translate it to an HTTP status code
/// without the domain or application layers knowing anything about HTTP.
/// </summary>
public record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Unavailable(string code, string description) => new(code, description, ErrorType.Unavailable);

    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);
}

/// <summary>Several invalid input fields reported at once (property name → messages).</summary>
public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Errors)
    : Error("Validation.Failed", "One or more validation errors occurred.", ErrorType.Validation);

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,

    /// <summary>A dependency (another service, the broker) cannot be reached right now; the caller may retry.</summary>
    Unavailable,
}
