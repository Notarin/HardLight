using Robust.Shared.Map;
using Robust.Shared.Map.Components;

using Content.Shared.Singularity.Components;

namespace Content.Server.Singularity.Events;

/// <summary>
/// Event raised on the event horizon entity whenever an event horizon consumes an entity.
/// </summary>
/// <param name="Tiles">The tiles that the event horizon is consuming.</param>
/// <param name="MapGridUid">The uid of the map grid the event horizon is consuming part of.</param>
/// <param name="MapGrid">The mapgrid that the event horizon is consuming tiles of.</param>
/// <param name="EventHorizonUid">The uid of the event horizon consuming the entity.</param>
/// <param name="EventHorizon">The event horizon consuming the tiles.</param>
[ByRefEvent]
public readonly record struct TilesConsumedByEventHorizonEvent(
    IReadOnlyList<(Vector2i, Tile)> Tiles,
    EntityUid MapGridUid,
    MapGridComponent MapGrid,
    EntityUid EventHorizonUid,
    EventHorizonComponent EventHorizon);

