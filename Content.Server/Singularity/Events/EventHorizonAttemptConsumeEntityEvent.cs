using Content.Shared.Singularity.Components;

namespace Content.Server.Singularity.Events;

/// <summary>
/// Event raised on the target entity whenever an event horizon attempts to consume an entity.
/// Can be cancelled to prevent the target entity from being consumed.
/// </summary>
/// <param name="Entity">The entity that the event horizon is attempting to consume.</param>
/// <param name="EventHorizonUid">The uid of the event horizon consuming the entity.</param>
/// <param name="EventHorizon">The event horizon consuming the target entity.</param>
[ByRefEvent]
public record struct EventHorizonAttemptConsumeEntityEvent(
    EntityUid Entity,
    EntityUid EventHorizonUid,
    EventHorizonComponent EventHorizon)
{
    /// <summary>
    /// Whether the event horizon has been prevented from consuming the target entity.
    /// </summary>
    public bool Cancelled = false;
}

