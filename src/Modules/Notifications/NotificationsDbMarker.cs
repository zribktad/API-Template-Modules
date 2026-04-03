namespace Notifications;

/// <summary>
/// Domain-layer marker type identifying the Notifications module's persistence boundary.
/// Used as the type parameter for <see cref="SharedKernel.Domain.Interfaces.IUnitOfWork{T}"/>
/// so that handlers can request the correct unit of work without referencing the Infrastructure layer.
/// </summary>
public abstract class NotificationsDbMarker;


