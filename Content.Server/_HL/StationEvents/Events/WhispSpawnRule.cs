using Content.Server._HL.StationEvents.Components;
using Content.Server._HL.Whisp;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;

namespace Content.Server._HL.StationEvents.Events;

/// <summary>
/// Station event rule that selects an isolated player on the station and initiates a whisp haunt.
/// Prioritizes the farthest living player from the station to target crew in deep space.
/// </summary>
public sealed class WhispSpawnRule : StationEventSystem<WhispSpawnRuleComponent>
{
    [Dependency] private readonly WhispHauntSystem _whispHaunt = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    /// <inheritdoc/>
    protected override void Started(EntityUid uid, WhispSpawnRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        if (!TryGetRandomStation(out var station))
        {
            var stations = StationSystem.GetStations();
            if (stations.Count > 0)
                station = stations[0];
            else
            {
                Sawmill.Warning("WhispSpawnRule: No station found.");
                ForceEndSelf(uid, gameRule);
                return;
            }
        }

        if (!TryComp<StationDataComponent>(station, out var stationData) ||
            StationSystem.GetLargestGrid(stationData) is not { } gridUid)
        {
            Sawmill.Warning($"WhispSpawnRule: No largest grid found for station {ToPrettyString(station.Value)}.");
            ForceEndSelf(uid, gameRule);
            return;
        }

        var gridXform = Transform(gridUid);
        if (gridXform.MapUid is not { } stationMap)
        {
            Sawmill.Warning($"WhispSpawnRule: Grid {ToPrettyString(gridUid)} is not on a valid map.");
            ForceEndSelf(uid, gameRule);
            return;
        }

        var anchorPos = _transform.GetWorldPosition(gridXform);

        EntityUid? farthest = null;
        var maxDistSq = -1f;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } player)
                continue;

            if (Deleted(player) || Terminating(player))
                continue;

            // Only living players
            if (TryComp<MobStateComponent>(player, out var mobState) && _mobState.IsDead(player, mobState))
                continue;

            // Ignore ghosts
            if (HasComp<GhostComponent>(player))
                continue;

            // Ignore players already being haunted, like multiple will ever happen at once
            if (_whispHaunt.IsHaunted(player))
                continue;

            var playerXform = Transform(player);
            if (playerXform.MapUid != stationMap)
                continue;

            var playerPos = _transform.GetWorldPosition(playerXform);
            var distSq = (playerPos - anchorPos).LengthSquared();
            if (!(distSq > maxDistSq))
                continue;
            maxDistSq = distSq;
            farthest = player;
        }

        if (farthest is not { } target)
        {
            Sawmill.Info("WhispSpawnRule: Found no eligible players on station map.");
            ForceEndSelf(uid, gameRule);
            return;
        }

        comp.HauntedPlayer = target;
        if (!_whispHaunt.StartHaunt(target, comp.WhispDistance, comp.WhispSpeed, comp.HauntSteps))
        {
            Sawmill.Warning($"WhispSpawnRule: Failed to start whisp haunt on player {ToPrettyString(target)}.");
            ForceEndSelf(uid, gameRule);
            return;
        }

        Sawmill.Info($"WhispSpawnRule: Selected farthest player {ToPrettyString(target)} at distance {MathF.Sqrt(maxDistSq):F1} tiles from station anchor.");
    }

    /// <inheritdoc/>
    protected override void ActiveTick(EntityUid uid, WhispSpawnRuleComponent comp, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, comp, gameRule, frameTime);

        if (comp.HauntedPlayer is not { } target || !_whispHaunt.IsHaunted(target))
            ForceEndSelf(uid, gameRule);
    }
}
