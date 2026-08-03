using Content.Shared._HL.Movement;
using Content.Shared.Gravity;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._HL.Movement;

/// <summary>
/// Rolls for a stumble every step an entity with <see cref="HeavyFootingComponent"/> takes.
/// </summary>
public sealed class HeavyFootingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeavyFootingComponent, MoveEvent>(OnMove);
    }

    private void OnMove(Entity<HeavyFootingComponent> ent, ref MoveEvent args)
    {
        if (args.ParentChanged
            || _timing.CurTime < ent.Comp.GraceEndTime
            || _standing.IsDown(ent)
            || _gravity.IsWeightless(ent)
            || !args.NewPosition.TryDistance(EntityManager, args.OldPosition, out var distance))
        {
            return;
        }

        if (distance > ent.Comp.MaxStepDistance)
            return;

        ent.Comp.Accumulator += distance;

        if (ent.Comp.Accumulator < ent.Comp.StepDistance)
            return;

        ent.Comp.Accumulator = 0f;

        if (!_random.Prob(ent.Comp.StumbleChance))
            return;

        if (!_stun.TryKnockdown(ent, ent.Comp.KnockdownTime, refresh: true))
            return;

        ent.Comp.GraceEndTime = _timing.CurTime + ent.Comp.GracePeriod;

        _audio.PlayPvs(ent.Comp.StumbleSound, ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Popup), ent, ent);
    }
}
