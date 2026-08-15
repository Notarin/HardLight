using System.Numerics;
using Content.Server.Body.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Systems;
using Content.Server.Medical.Components;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared._HL.PoolToy;
using Content.Shared._Shitmed.Body.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Atmos.Components;
using Content.Shared.Humanoid;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Sprite;
using Content.Shared.Stacks;
using Robust.Server.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._HL.PoolToy;

/// <summary>
/// Lets air out of inflatable bodies that get cut or punctured until the breach is patched up.
/// </summary>
public sealed class PoolToyInflationSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly GasTankSystem _gasTank = default!;
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
        SubscribeLocalEvent<PoolToyInflationComponent, HealingDoAfterEvent>(OnHealed);
        SubscribeLocalEvent<PoolToyInflationComponent, PoolToyRefillDoAfterEvent>(OnRefillDoAfter);
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

            LoseAir((uid, comp), elapsed);
            UpdateScale((uid, comp));

            if (_timing.CurTime < comp.NextWarning)
                continue;

            comp.NextWarning = _timing.CurTime + comp.WarningInterval;
            _popup.PopupEntity(Loc.GetString(comp.DeflatingPopup), uid, uid, PopupType.MediumCaution);
            _popup.PopupEntity(Loc.GetString(comp.DeflatingPopupOthers, ("target", uid)), uid,
                Filter.PvsExcept(uid), true);
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

        // An airless body has no breath left to gasp with, and nothing left to suffocate on either, so it
        // holds wherever it ended up instead of decaying towards death.
        if (args.NewMobState == MobState.Alive)
            RemComp<BreathingImmunityComponent>(ent);
        else
            EnsureComp<BreathingImmunityComponent>(ent);

        if (!ent.Comp.Breached || args.NewMobState == MobState.Alive)
            return;

        // All the way flat: nothing left to leak.
        _popup.PopupEntity(Loc.GetString(ent.Comp.FlatPopup), ent, ent, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString(ent.Comp.FlatPopupOthers, ("target", ent.Owner)), ent,
            Filter.PvsExcept(ent.Owner), true);
        _audio.PlayPvs(ent.Comp.FlatSound, ent);
    }

    /// <summary>
    /// Patching a breach is handled here rather than through healing, since the escaping air leaves no wound
    /// for a bandage to actually heal.
    /// </summary>
    private void OnInteractUsing(Entity<PoolToyInflationComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<GasTankComponent>(args.Used, out var tank))
        {
            args.Handled = TryStartRefill(ent, args.User, (args.Used, tank));
            return;
        }

        if (!ent.Comp.Breached)
            return;

        if (!TryComp<HealingComponent>(args.Used, out var healing) || !SealsBreaches(healing))
            return;

        _popup.PopupEntity(
            Loc.GetString(ent.Comp.SealingPopupOthers,
                ("user", args.User),
                ("used", args.Used),
                ("target", ent.Owner)),
            ent,
            Filter.PvsExcept(args.User),
            true);

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
        _popup.PopupEntity(
            Loc.GetString(ent.Comp.SealPopupOthers, ("user", args.User), ("target", ent.Owner)),
            ent,
            Filter.PvsExcept(ent.Owner),
            true);
        _audio.PlayPvs(ent.Comp.SealSound, ent);
        UpdateScale(ent);
    }

    /// <summary>
    /// Deals the escaping air as the first damage type the body actually takes. Synthetic bodies have no
    /// airloss in their damage container at all, and shadekin zero out any airloss dealt to them, so airloss
    /// alone would leave those species leaking without ever going down.
    /// </summary>
    private void LoseAir(Entity<PoolToyInflationComponent> ent, float elapsed)
    {
        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return;

        foreach (var type in AirlossTypes(ent, damageable))
        {
            var rate = type == ent.Comp.AirlossDamageType
                ? ent.Comp.AirlossPerSecond
                : ent.Comp.AirlossPerSecond * ent.Comp.FallbackAirlossMultiplier;

            // Kept on the torso and unable to sever, since air leaving a body should not take its limbs off
            // the way an equivalent beating would.
            var delta = _damageable.TryChangeDamage(ent,
                new DamageSpecifier { DamageDict = { [type] = rate * elapsed } },
                ignoreResistances: true,
                interruptsDoAfters: false,
                damageable,
                canSever: false,
                targetPart: TargetBodyPart.Torso);

            // Nothing landed, so the species is outright immune to this type: try the next one.
            if (delta == null ||
                !delta.DamageDict.TryGetValue(type, out var applied) ||
                applied <= FixedPoint2.Zero)
            {
                continue;
            }

            ent.Comp.AirLost += applied;
            ent.Comp.AirlossType = type;
            return;
        }
    }

    /// <summary>
    /// The damage types worth trying for escaping air, best first, or just the one already known to work.
    /// </summary>
    private IEnumerable<ProtoId<DamageTypePrototype>> AirlossTypes(
        Entity<PoolToyInflationComponent> ent,
        DamageableComponent damageable)
    {
        if (ent.Comp.AirlossType is { } known)
        {
            yield return known;
            yield break;
        }

        if (damageable.Damage.DamageDict.ContainsKey(ent.Comp.AirlossDamageType))
            yield return ent.Comp.AirlossDamageType;

        foreach (var fallback in ent.Comp.AirlossFallbackDamageTypes)
        {
            if (damageable.Damage.DamageDict.ContainsKey(fallback))
                yield return fallback;
        }
    }

    /// <summary>
    /// Inflatable bodies can be topped back up straight from a gas tank, so long as the air has somewhere to
    /// stay. Nothing here runs for entities without the trait, so tanks behave as usual on everyone else.
    /// </summary>
    private bool TryStartRefill(Entity<PoolToyInflationComponent> ent, EntityUid user, Entity<GasTankComponent> tank)
    {
        if (ent.Comp.Breached)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.RefillBreachedPopup), ent, user);
            return false;
        }

        if (tank.Comp.Air.TotalMoles < ent.Comp.MinRefillMoles)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.RefillEmptyPopup, ("used", tank.Owner)), ent, user);
            return false;
        }

        if (ent.Comp.AirLost <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.RefillFullPopup, ("target", ent.Owner)), ent, user);
            return false;
        }

        if (user != ent.Owner)
        {
            _popup.PopupEntity(
                Loc.GetString(ent.Comp.RefillingPopupOthers,
                    ("user", user),
                    ("used", tank.Owner),
                    ("target", ent.Owner)),
                ent,
                Filter.PvsExcept(user),
                true);
        }

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            user,
            ent.Comp.RefillDelay,
            new PoolToyRefillDoAfterEvent(),
            ent.Owner,
            target: ent.Owner,
            used: tank.Owner)
        {
            NeedHand = true,
            BreakOnMove = true,
        });
    }

    private void OnRefillDoAfter(Entity<PoolToyInflationComponent> ent, ref PoolToyRefillDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } used)
            return;

        if (!TryComp<GasTankComponent>(used, out var tank) || tank.Air.TotalMoles < ent.Comp.MinRefillMoles)
            return;

        if (ent.Comp.AirLost <= FixedPoint2.Zero || ent.Comp.AirlossType is not { } airlossType)
            return;

        args.Handled = true;

        // Anything already patched up by other means is no longer theirs to reinflate.
        if (TryComp<DamageableComponent>(ent, out var damageable) &&
            damageable.Damage.DamageDict.TryGetValue(airlossType, out var current))
        {
            ent.Comp.AirLost = FixedPoint2.Min(ent.Comp.AirLost, current);
        }

        if (ent.Comp.AirLost <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.RefillFullPopup, ("target", ent.Owner)), ent, args.User);
            return;
        }

        // Only as much air as the body has room for, so a full tank tops them right up while leaving the rest
        // of the tank for later.
        var wanted = (float) (ent.Comp.AirLost / ent.Comp.AirlossHealedPerMole);
        var moles = Math.Min(wanted, tank.Air.TotalMoles);
        _gasTank.RemoveAir((used, tank), moles);

        var healed = FixedPoint2.Min(ent.Comp.AirLost, ent.Comp.AirlossHealedPerMole * moles);
        ent.Comp.AirLost -= healed;

        _damageable.TryChangeDamage(ent.Owner,
            new DamageSpecifier { DamageDict = { [airlossType] = -healed } },
            ignoreResistances: true,
            interruptsDoAfters: false,
            targetPart: TargetBodyPart.Torso);

        _popup.PopupEntity(Loc.GetString(ent.Comp.RefillPopup, ("used", used)), ent, ent);

        if (args.User != ent.Owner)
        {
            _popup.PopupEntity(
                Loc.GetString(ent.Comp.RefillPopupUser, ("used", used), ("target", ent.Owner)),
                ent,
                args.User);
        }

        _popup.PopupEntity(
            Loc.GetString(ent.Comp.RefillPopupOthers, ("target", ent.Owner), ("used", used)),
            ent,
            Filter.PvsExcept(ent.Owner).RemovePlayerByAttachedEntity(args.User),
            true);
        _audio.PlayPvs(ent.Comp.RefillSound, ent);

        UpdateScale(ent);
    }

    /// <summary>
    /// Ordinary healing only tells the patient about it, which leaves bystanders guessing whether the leak
    /// has been dealt with.
    /// </summary>
    private void OnHealed(Entity<PoolToyInflationComponent> ent, ref HealingDoAfterEvent args)
    {
        if (args.Cancelled || args.Used is not { } used || args.User == ent.Owner)
            return;

        _popup.PopupEntity(
            Loc.GetString(ent.Comp.HealedPopupOthers,
                ("user", args.User),
                ("used", used),
                ("target", ent.Owner)),
            ent,
            Filter.PvsExcept(args.User),
            true);
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
        Vector2 factor;
        if (IsIncapacitated(ent))
        {
            factor = ent.Comp.FlatScale;
        }
        else
        {
            var progress = 0f;
            if (TryComp<DamageableComponent>(ent, out var damageable) &&
                _thresholds.TryGetIncapThreshold(ent, out var threshold) &&
                threshold > FixedPoint2.Zero)
            {
                progress = Math.Clamp((float) (damageable.TotalDamage / threshold.Value), 0f, 1f);
            }

            factor = Vector2.Lerp(ent.Comp.InflatedScale, ent.Comp.DeflatedScale, progress);
        }

        factor = new Vector2(Math.Max(factor.X, ent.Comp.MinScale), Math.Max(factor.Y, ent.Comp.MinScale));

        // Humanoids get their sprite scale rewritten from HumanoidVisuals.Scale whenever their appearance
        // updates, so scaling them has to go through that instead of the generic sprite scale. The size that
        // is already there is the baseline, which is what keeps the size traits' own scaling intact.
        if (HasComp<HumanoidAppearanceComponent>(ent))
        {
            if (ent.Comp.BaseSize == null)
            {
                ent.Comp.BaseSize = _appearance.TryGetData<Vector2>(ent, HumanoidVisuals.Scale, out var current)
                    ? current
                    : Vector2.One;
            }

            var b = ent.Comp.BaseSize.Value;
            _appearance.SetData(ent, HumanoidVisuals.Scale, new Vector2(b.X * factor.X, b.Y * factor.Y));
            return;
        }

        _scale.SetSpriteScale(ent, factor);
    }

    private bool IsIncapacitated(EntityUid uid)
        => TryComp<MobStateComponent>(uid, out var state) && state.CurrentState != MobState.Alive;
}
