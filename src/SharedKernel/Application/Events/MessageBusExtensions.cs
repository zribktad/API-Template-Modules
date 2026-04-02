using Wolverine;

namespace SharedKernel.Application.Events;

/// <summary>
/// Factory for <see cref="OutgoingMessages"/> instances when no cascading messages need to be dispatched.
/// </summary>
public static class OutgoingMessagesHelper
{
    /// <summary>
    /// Returns a new empty <see cref="OutgoingMessages"/> for handler paths that do not emit any cascading messages.
    /// </summary>
    public static OutgoingMessages Empty => new();
}
