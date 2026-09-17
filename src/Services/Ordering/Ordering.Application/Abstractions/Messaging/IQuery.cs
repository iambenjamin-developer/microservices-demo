namespace Ordering.Application.Abstractions.Messaging;

/// <summary>A read-only request (the read side of CQRS): it never loads aggregates, it projects to DTOs.</summary>
public interface IQuery<TResponse>;
