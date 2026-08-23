using Content.Shared.Mind.Components;
using Content.Shared.Teleportation.Systems;
using Content.Shared.Xenoarchaeology.Artifact.XAE.Components;
using Robust.Shared.Collections;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Xenoarchaeology.Artifact.XAE;

/// <summary>
/// System for xeno artifact effect that creates temporary portal between places on station.
/// </summary>
public sealed class XAEPortalSystem : BaseXAESystem<XAEPortalComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly LinkedEntitySystem _link = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <inheritdoc />
    protected override void OnActivated(Entity<XAEPortalComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var map = Transform(ent).MapID;
        var validMinds = new ValueList<EntityUid>();
        var mindQuery = EntityQueryEnumerator<MindContainerComponent, TransformComponent, MetaDataComponent>();
        while (mindQuery.MoveNext(out var uid, out var mc, out var xform, out var meta))
        {
            // Cheap-first short-circuit: most MindContainers on a populated server are not on
            // the same map as the artifact, and IsEntityOrParentInContainer walks the parent
            // chain. Filter by map, then HasMind, then container check. Same set of valid minds.
            if (xform.MapID != map)
                continue;
            if (!mc.HasMind)
                continue;
            if (_container.IsEntityOrParentInContainer(uid, meta: meta, xform: xform))
                continue;

            validMinds.Add(uid);
        }
        // this would only be 0 if there were a station full of AIs and no one else, in that case just stop this function
        if (validMinds.Count == 0)
            return;

        var offset = _random.NextVector2(2, 3);
        var originWithOffset = args.Coordinates.Offset(offset);
        var firstPortal = Spawn(ent.Comp.PortalProto, originWithOffset);

        var target = _random.Pick(validMinds);

        var secondPortal = Spawn(ent.Comp.PortalProto, _transform.GetMapCoordinates(target));

        // Manual position swapping, because the portal that opens doesn't trigger a collision, and doesn't teleport targets the first time.
        _transform.SwapPositions(target, ent.Owner);

        _link.TryLink(firstPortal, secondPortal, true);
    }
}
