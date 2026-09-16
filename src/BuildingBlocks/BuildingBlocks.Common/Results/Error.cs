namespace BuildingBlocks.Common.Results;

/// <summary>
/// An expected failure. <see cref="Type"/> lets the API layer translate it to an HTTP status code
/// without the domain or application layers knowing anything about HTTP.
/// </summary>
public sealed record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);
}

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
}
