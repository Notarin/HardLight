using System.Numerics;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Medical.Components;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared._HL.PoolToy;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Sprite;
using Content.Shared.Stacks;
using Robust.Server.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._HL.PoolToy;

/// <summary>
/// Lets air out of inflatable bodies that get cut or punctured until the breach is patched up.
/// </summary>
public sealed class PoolToyInflationSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _scale = default!;
    [Dependency] private readonly StackSystem _stacks = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PoolToyInflationComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<PoolToyInflationComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<PoolToyInflationComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PoolToyInflationComponent, PoolToySealDoAfterEvent>(OnSealDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PoolToyInflationComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            StopBleeding(uid);

            if (!comp.Breached || _timing.CurTime < comp.NextDeflate)
                continue;

            var elapsed = (float) comp.DeflateInterval.TotalSeconds;
            comp.NextDeflate = _timing.CurTime + comp.DeflateInterval;

            // Nothing left to lose once they are flat.
            if (IsIncapacitated(uid))
                continue;

            _damageable.TryChangeDamage(uid,
                new DamageSpecifier { DamageDict = { [comp.AirlossDamageType] = comp.AirlossPerSecond * elapsed } },
                ignoreResistances: true,
                interruptsDoAfters: false);

            UpdateScale((uid, comp));

            if (_timing.CurTime < comp.NextWarning)
                continue;

            comp.NextWarning = _timing.CurTime + comp.WarningInterval;
            _popup.PopupEntity(Loc.GetString(comp.DeflatingPopup), uid, uid, PopupType.MediumCaution);
            _audio.PlayPvs(comp.DeflatingSound, uid);
        }
    }

    /// <summary>
    /// Air, not blood, is what an inflatable body is full of, so it leaks the former and never the latter.
    /// </summary>
    private void StopBleeding(EntityUid uid)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream) || bloodstream.BleedAmount <= 0f)
            return;

        _bloodstream.TryModifyBleedAmount(uid, -bloodstream.BleedAmount, bloodstream);
    }

    private void OnDamageChanged(Entity<PoolToyInflationComponent> ent, ref DamageChangedEvent args)
    {
        UpdateScale(ent);

        if (!args.DamageIncreased || args.DamageDelta == null || ent.Comp.Breached)
            return;

        var puncture = FixedPoint2.Zero;
        foreach (var type in ent.Comp.PunctureDamageTypes)
        {
            if (args.DamageDelta.DamageDict.TryGetValue(type, out var amount))
                puncture += amount;
        }

        if (puncture < ent.Comp.PunctureThreshold)
            return;

        Breach(ent);
    }

    private void OnMobStateChanged(Entity<PoolToyInflationComponent> ent, ref MobStateChangedEvent args)
    {
        UpdateScale(ent);

        if (!ent.Comp.Breached || args.NewMobState == MobState.Alive)
            return;

        // All the way flat: nothing left to leak.
        _popup.PopupEntity(Loc.GetString(ent.Comp.FlatPopup), ent, ent, PopupType.LargeCaution);
        _audio.PlayPvs(ent.Comp.FlatSound, ent);
    }

    /// <summary>
    /// Patching a breach is handled here rather than through healing, since the escaping air leaves no wound
    /// for a bandage to actually heal.
    /// </summary>
    private void OnInteractUsing(Entity<PoolToyInflationComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !ent.Comp.Breached)
            return;

        if (!TryComp<HealingComponent>(args.Used, out var healing) || !SealsBreaches(healing))
            return;

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.SealDelay,
            new PoolToySealDoAfterEvent(),
            ent.Owner,
            target: ent.Owner,
            used: args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
        });
    }

    private void OnSealDoAfter(Entity<PoolToyInflationComponent> ent, ref PoolToySealDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !ent.Comp.Breached)
            return;

        args.Handled = true;

        if (args.Used is { } used)
        {
            if (TryComp<StackComponent>(used, out var stack))
                _stacks.Use(used, 1, stack);
            else
                QueueDel(used);
        }

        ent.Comp.Breached = false;
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString(ent.Comp.SealPopup), ent, ent);
        _audio.PlayPvs(ent.Comp.SealSound, ent);
        UpdateScale(ent);
    }

    /// <summary>
    /// Whether an item is the sort of patch - bandage, gauze, ointment - that can close a breach.
    /// </summary>
    private static bool SealsBreaches(HealingComponent healing)
    {
        if (healing.BloodlossModifier < 0)
            return true;

        foreach (var (type, amount) in healing.Damage.DamageDict)
        {
            if (amount < 0 && type is "Slash" or "Piercing" or "Blunt")
                return true;
        }

        return false;
    }

    private void Breach(Entity<PoolToyInflationComponent> ent)
    {
        ent.Comp.Breached = true;
        ent.Comp.NextDeflate = _timing.CurTime + ent.Comp.DeflateInterval;
        ent.Comp.NextWarning = _timing.CurTime + ent.Comp.WarningInterval;
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString(ent.Comp.BreachPopup), ent, ent, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString(ent.Comp.BreachPopupOthers, ("target", ent.Owner)), ent, Filter.PvsExcept(ent.Owner), true);
        _audio.PlayPvs(ent.Comp.BreachSound, ent);
    }

    /// <summary>
    /// Shrinks the sprite as air is lost, and squashes it flat once they are incapacitated.
    /// </summary>
    private void UpdateScale(Entity<PoolToyInflationComponent> ent)
    {
        if (IsIncapacitated(ent))
        {
            _scale.SetSpriteScale(ent, ent.Comp.FlatScale);
            return;
        }

        var progress = 0f;
        if (TryComp<DamageableComponent>(ent, out var damageable) &&
            _thresholds.TryGetIncapThreshold(ent, out var threshold) &&
            threshold > FixedPoint2.Zero)
        {
            progress = Math.Clamp((float) (damageable.TotalDamage / threshold.Value), 0f, 1f);
        }

        _scale.SetSpriteScale(ent, Vector2.Lerp(ent.Comp.InflatedScale, ent.Comp.DeflatedScale, progress));
    }

    private bool IsIncapacitated(EntityUid uid)
        => TryComp<MobStateComponent>(uid, out var state) && state.CurrentState != MobState.Alive;
}
