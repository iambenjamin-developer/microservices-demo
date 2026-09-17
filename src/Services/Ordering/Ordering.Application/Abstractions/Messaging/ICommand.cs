namespace Ordering.Application.Abstractions.Messaging;

/// <summary>A request that changes state through the domain model (the write side of CQRS).</summary>
public interface ICommand<TResponse>;
