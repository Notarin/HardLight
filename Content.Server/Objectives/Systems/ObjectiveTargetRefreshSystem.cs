using Content.Server._Starlight.Objectives.Components;
using Content.Server.Antag;
using Content.Server.EventScheduler;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.HL.CCVar;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Objectives.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Rerolls kill/maroon objectives whose target has left the round (cryo, ghost, disconnect, cleanup)
/// while the traitor has made 0% progress against them.
/// </summary>
public sealed class ObjectiveTargetRefreshSystem : EntitySystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ObjectivesSystem _objectives = default!;
    [Dependency] private TargetObjectiveSystem _target = default!;
    [Dependency] private EventSchedulerSystem _eventScheduler = default!; // HL
    [Dependency] private IGameTiming _timing = default!; // HL
    [Dependency] private AntagSelectionSystem _antag = default!; // HL
    [Dependency] private IConfigurationManager _cfg = default!; // HL

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        // Run after MindSystem so the mind's OwnedEntity is already cleared (victory state settled)
        // before we decide whether a kill/maroon objective should be rerolled.
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating, after: new[] { typeof(MindSystem) });
        SubscribeLocalEvent<MindComponent, ObjectiveTargetTimeoutEvent>(OnObjectiveTargetTimeout); // HL: Target Re-Roll timeout
    }

    // HL FUNCTION
    // After the delay from the timer, check if the player has re-connected, or if the mind has re-connected to a non-ghost entity.
    private void OnObjectiveTargetTimeout(EntityUid uid, MindComponent comp, ObjectiveTargetTimeoutEvent args)
    {
        if (comp.UserId.HasValue && !_playerManager.ValidSessionId(comp.UserId.Value))
        {
            HandleTargetLeft(uid);
            return;
        }

        if (comp.OwnedEntity != null && comp.OwnedEntity.HasValue && !HasComp<GhostComponent>(comp.OwnedEntity)) // If we're attached to something that isn't a ghost
            return;
        HandleTargetLeft(uid); // We don't need to handle if the mind changes bodies because that'll auto-update.
    }

    // HL FUNCTION
    // Looks up any minds attached to a users session and schedules them for re-rolling
    private void ScheduleMindDetatch(NetUserId user)
    {
        // Minds can exist after the player disconnects, so find the unlinked mind
        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var mindUid, out var mind))
        {
            if (mind.OriginalOwnerUserId == user)
            {
                var ev = new ObjectiveTargetTimeoutEvent();
                _eventScheduler.ScheduleEvent(mindUid, ref ev, _timing.CurTime + TimeSpan.FromMinutes(_cfg.GetCVar(HLCCVars.AntagTargetDisconnectRerollTimeout)));
            }
        }
    }

    /// <summary>
    /// Fires when a player session leaves any entity (ghosting, cryo, body swap).
    /// We only care if the player is not attached to an entity, or to a ghost.
    /// </summary>
    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        // HL: Swap this to using the scheduler system to avoid quick disconnect and reconnects, don't run for aghosts.
        if (ev.Player.AttachedEntity == null || TryComp<GhostComponent>(ev.Player.AttachedEntity, out var c) && c.CanGhostInteract == false)
            ScheduleMindDetatch(ev.Player.Channel.UserId);
    }

    /// <summary>
    /// Fallback for disconnected players whose body entity is deleted by admin or timer cleanup.
    /// Connected players always auto-ghost first and are caught by <see cref="OnPlayerDetached"/>.
    /// </summary>
    private void OnEntityTerminating(ref EntityTerminatingEvent args)
    {
        var uid = args.Entity.Owner;

        // Only care about entities that can hold a mind.
        if (!HasComp<MindContainerComponent>(uid))
            return;

        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
            return;

        // Skip connected players — they will auto-ghost and be handled by OnPlayerDetached.
        if (mind.UserId != null && _playerManager.ValidSessionId(mind.UserId.Value))
            return;

        HandleTargetLeft(mindId);
    }

    private void HandleTargetLeft(EntityUid targetMindId)
    {
        var targetName = TryComp<MindComponent>(targetMindId, out var targetMind) ? targetMind.CharacterName ?? "Unknown" : "Unknown";
        Log.Info($"ObjectiveTargetRefresh: target mind {ToPrettyString(targetMindId)} ({targetName}) left the round, scanning traitor objectives for rerolls.");

        var traitorQuery = EntityQueryEnumerator<TraitorRuleComponent, GameRuleComponent>();
        while (traitorQuery.MoveNext(out var ruleUid, out var traitorComp, out var gameRule))
        {
            if (!_gameTicker.IsGameRuleActive(ruleUid, gameRule))
                continue;

            foreach (var traitorMindId in traitorComp.TraitorMinds)
            {
                if (traitorMindId == targetMindId)
                    continue;

                if (!TryComp<MindComponent>(traitorMindId, out var traitorMind))
                    continue;

                // Iterate in reverse so removal by index stays valid.
                for (var i = traitorMind.Objectives.Count - 1; i >= 0; i--)
                {
                    var objUid = traitorMind.Objectives[i];

                    if (!TryComp<KillPersonConditionComponent>(objUid, out _) && !TryComp<TeachALessonConditionComponent>(objUid, out _)) // HL: Add Teach a lesson to re-rolls
                        continue;

                    if (!_target.GetTarget(objUid, out var currentTarget))
                        continue;

                    if (currentTarget.Value != targetMindId)
                        continue;

                    // Don't reroll if the traitor already has any progress (partial kills, etc.).
                    var progress = _objectives.GetProgress(objUid, (traitorMindId, traitorMind));
                    if (progress is null or >= 0.01f)
                    {
                        Log.Info($"ObjectiveTargetRefresh: traitor {ToPrettyString(traitorMindId)} has progress {progress} on objective {ToPrettyString(objUid)} targeting the leaver, skipping reroll.");
                        continue;
                    }

                    Log.Info($"ObjectiveTargetRefresh: traitor {ToPrettyString(traitorMindId)} has objective {ToPrettyString(objUid)} targeting the leaver with no progress, rerolling.");
                    TryRerollObjectiveTarget(objUid, i, traitorMindId, traitorMind);
                }
            }
        }
    }

    private void TryRerollObjectiveTarget(EntityUid objUid, int objIndex, EntityUid traitorMindId, MindComponent traitorMind)
    {
        if (!TryComp<PickRandomPersonComponent>(objUid, out var pickComp))
        {
            // Can't pick a new target without a pool — remove the unachievable objective.
            Log.Info($"ObjectiveTargetRefresh: objective {ToPrettyString(objUid)} has no PickRandomPersonComponent pool, removing it from traitor {ToPrettyString(traitorMindId)}.");
            _mind.TryRemoveObjective(traitorMindId, traitorMind, objIndex);
            NotifyTraitor(traitorMind, Loc.GetString("objective-target-refresh-removed"));
            return;
        }

        var newTarget = _mind.PickFromPool(pickComp.Pool, pickComp.Filters, traitorMindId);

        if (newTarget == null)
        {
            // No valid replacement found — remove the objective rather than leave it stuck.
            Log.Info($"ObjectiveTargetRefresh: no valid replacement target found for objective {ToPrettyString(objUid)}, removing it from traitor {ToPrettyString(traitorMindId)}.");
            _mind.TryRemoveObjective(traitorMindId, traitorMind, objIndex);
            NotifyTraitor(traitorMind, Loc.GetString("objective-target-refresh-removed"));
            return;
        }

        _target.SetTarget(objUid, newTarget.Value);

        // Refresh the entity name on the objective to match the new target.
        var afterEv = new ObjectiveAfterAssignEvent(traitorMindId, traitorMind, Comp<ObjectiveComponent>(objUid), MetaData(objUid));
        RaiseLocalEvent(objUid, ref afterEv);

        var newName = TryComp<MindComponent>(newTarget.Value, out var newMind) ? newMind.CharacterName ?? "Unknown" : "Unknown";
        Log.Info($"ObjectiveTargetRefresh: rerolled objective {ToPrettyString(objUid)} for traitor {ToPrettyString(traitorMindId)} to new target {ToPrettyString(newTarget.Value)} ({newName}).");
        NotifyTraitor(traitorMind, Loc.GetString("objective-target-refresh-rerolled", ("name", newName)));
    }

    private void NotifyTraitor(MindComponent traitorMind, string message)
    {
        if (traitorMind.UserId == null)
            return;

        if (!_playerManager.TryGetSessionById(traitorMind.UserId.Value, out var session))
            return;

        _antag.SendBriefing(session, message, null, null); // HL: Use the SendBriefing command so the colors don't get messed up
    }

    public record struct ObjectiveTargetTimeoutEvent() { } // HL: Event raised when a mind splits from the body, check after a timer to see if we should re-roll
}
