using Content.Shared.Singularity.Components;
using Robust.Shared.Containers;

namespace Content.Server.Singularity.Events;

/// <summary>
///     Event raised on the event horizon entity whenever an event horizon consumes an entity.
/// </summary>
/// <param name="Entity">The entity being consumed by the event horizon.</param>
/// <param name="EventHorizonUid">The uid of the event horizon consuming the entity.</param>
/// <param name="EventHorizon">The event horizon consuming the entity.</param>
/// <param name="Container">The innermost container of the entity being consumed by the event horizon that is not also in the process of being consumed by the event horizon. Used to correctly dump out the contents containers that are consumed by the event horizon.</param>
[ByRefEvent]
public readonly record struct EntityConsumedByEventHorizonEvent(
    EntityUid Entity,
    EntityUid EventHorizonUid,
    EventHorizonComponent EventHorizon,
    BaseContainer? Container);

