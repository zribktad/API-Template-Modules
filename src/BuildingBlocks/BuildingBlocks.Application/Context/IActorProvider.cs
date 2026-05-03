namespace BuildingBlocks.Application.Context;

/// <summary>
///     Provides the identity of the currently authenticated user (actor) executing a request.
///     Consumed by Application-layer handlers and domain services that need the actor for auditing or authorization.
/// </summary>
public interface IActorProvider
{
    /// <summary>Gets the unique identifier of the acting user.</summary>
    public Guid ActorId { get; }
}

