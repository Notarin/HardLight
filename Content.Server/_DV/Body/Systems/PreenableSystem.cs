using Content.Server.Body.Components;
using Content.Server.Chat.Systems;
using Content.Server.Forensics;
using Content.Shared._DV.Body.Components;
using Content.Shared._DV.Body.Events;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Verbs;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._DV.Body.Systems;

public sealed class PreenableSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ForensicsSystem _forensics = default!; // HardLight: server-only in this codebase
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PreenableComponent, GetVerbsEvent<Verb>>(AddVerb);
        SubscribeLocalEvent<PreenableComponent, PreeningEvent>(OnPreened);
        // Hardlight edit - remove dropping of bloodied feathers
        // SubscribeLocalEvent<PreenableComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<PreenableComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<PreenableComponent, ComponentInit>(OnCompInit);

        // HardLight edit - re-apply persisted feather colors after a saved ship or room loads
        SubscribeLocalEvent<FeatherComponent, ComponentStartup>(OnFeatherStartup);
        SubscribeLocalEvent<FeatherComponent, MapInitEvent>(OnFeatherMapInit);
    }

    private void OnCompInit(Entity<PreenableComponent> ent, ref ComponentInit args)
    {
        ent.Comp.CurrentFeathers = ent.Comp.MaximumFeathers;
        Dirty(ent);
    }

    private void AddVerb(Entity<PreenableComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // can't preen with no feathers
        if (ent.Comp.CurrentFeathers <= 0)
            return;

        var user = args.User;

        Verb verb = new()
        {
            Act = () => AttemptDoAfter(ent, user),
            Text = Loc.GetString(ent.Comp.PreeningVerbString),
        };
        args.Verbs.Add(verb);
    }

    private void AttemptDoAfter(Entity<PreenableComponent> ent, EntityUid userUid)
    {
        var doArgs = new DoAfterArgs(EntityManager, userUid, TimeSpan.FromSeconds(5), new PreeningEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        // HardLight edit
        if (userUid == ent.Owner)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.SelfPreeningMessage), ent, ent);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.GettingPreenedMessage, ("preener", Identity.Entity(userUid, EntityManager))), ent, ent, PopupType.Medium);
            _popup.PopupEntity(Loc.GetString(ent.Comp.PreeningOtherMessage, ("preenee", Identity.Entity(ent, EntityManager))), userUid, userUid);
        }

        _doAfter.TryStartDoAfter(doArgs);
    }

    private void OnPreened(Entity<PreenableComponent> ent, ref PreeningEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (ent.Comp.CurrentFeathers <= 0)
            return;

        args.Handled = true;
        var feather = SpawnFeather(ent, false);

        _hands.TryPickupAnyHand(args.User, feather);
    }

    // Hardlight edit start - remove dropping of bloodied feathers
    // private void OnDamaged(Entity<PreenableComponent> ent, ref DamageChangedEvent args)
    // {
    //     if (args.DamageDelta == null || ent.Comp.ValidDamageTypes == null || !args.DamageIncreased)
    //         return;
    //
    //     if (ent.Comp.CurrentFeathers <= 0)
    //         return;
    //
    //     var totalApplicableDamage = FixedPoint2.Zero;
    //
    //     foreach (var (type, value) in args.DamageDelta.DamageDict)
    //     {
    //         if (!ent.Comp.ValidDamageTypes.Contains(type))
    //             continue;
    //
    //         totalApplicableDamage += value;
    //     }
    //
    //     if (totalApplicableDamage <= ent.Comp.ShedDamageThreshold)
    //         return;
    //
    //     // HardLight edit
    //     var triggerChance = Math.Clamp(totalApplicableDamage.Float() * ent.Comp.ShedScalingChance, 0f, 1f);
    //
    //     if (!_random.Prob(triggerChance))
    //         return;
    //
    //     var feather = SpawnFeather(ent, true);
    //
    //     // apply a random impulse so it's flying off the body. similar code to GibbingSystem
    //     var scatterVector = _random.NextAngle().ToVec() * (_random.NextFloat(10, 40));
    //     _physics.ApplyLinearImpulse(feather, scatterVector);
    //     _physics.ApplyAngularImpulse(feather, _random.NextFloat(-30, 30));
    //
    //     // update name/desc for increased validness
    //     var meta = MetaData(feather);
    //     _metaData.SetEntityName(feather, Loc.GetString(ent.Comp.FeatherBloodiedNameString, ("item", Name(feather))), meta);
    //     _metaData.SetEntityDescription(feather, Loc.GetString(ent.Comp.FeatherBloodiedDescString), meta);
    //
    //     // yeeeowch!
    //     _popup.PopupEntity(Loc.GetString(ent.Comp.DroppedFeatherString), ent, ent, PopupType.MediumCaution);
    //     _chat.TryEmoteWithoutChat(ent, ent.Comp.ScreamEmote);
    //
    //     _statusEffects.TryAddStatusEffect(ent, "Adrenaline", TimeSpan.FromSeconds(3), true);
    // }
    // Hardlight edit end

    private void OnDamageModify(Entity<PreenableComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.VulnerabilityModifier == null)
            return;

        // zero vulnerability at max feathers, full vulnerability at 0 feathers
        var vulnerabilityModifier = 1f - (ent.Comp.CurrentFeathers / (float)ent.Comp.MaximumFeathers);

        var damageSpecifier = new DamageModifierSet
        {
            Coefficients = new Dictionary<string, float>(ent.Comp.VulnerabilityModifier.Coefficients),
        };

        foreach (var key in damageSpecifier.Coefficients.Keys)
        {
            damageSpecifier.Coefficients[key] = 1f + ((damageSpecifier.Coefficients[key] - 1f) * vulnerabilityModifier);
        }

        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, damageSpecifier);
    }

    private EntityUid SpawnFeather(Entity<PreenableComponent> ent, bool bloody)
    {
        // HardLight edit
        var feather = Spawn(ent.Comp.FeatherPrototype.Id, Transform(ent).Coordinates);

        // HardLight edit
        var color = ent.Comp.Color;
        if (color == null && TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            color = humanoid.SkinColor;

        // HardLight edit
        var featherComp = EnsureComp<FeatherComponent>(feather);
        featherComp.FeatherColor = color ?? Color.White;

        // best be careful, no cleaning this
        _forensics.TransferDna(feather, ent, false);

        ent.Comp.CurrentFeathers -= 1;
        ent.Comp.ReplenishTime = _timing.CurTime + ent.Comp.ReplenishDelay;
        Dirty(ent);

        if (bloody && TryComp<BloodstreamComponent>(ent, out var bloodstream) && bloodstream.BloodSolution != null)
        {
            var solution = bloodstream.BloodSolution.Value.Comp.Solution;
            featherComp.BloodColor = solution.GetColor(_prototype);
        }

        ApplyFeatherColors((feather, featherComp));
        return feather;
    }

    // HardLight edit start - persisted feather colors
    private void OnFeatherStartup(Entity<FeatherComponent> ent, ref ComponentStartup args)
    {
        ApplyFeatherColors(ent);
    }

    private void OnFeatherMapInit(Entity<FeatherComponent> ent, ref MapInitEvent args)
    {
        ApplyFeatherColors(ent);
    }

    public void ApplyFeatherColors(Entity<FeatherComponent> ent)
    {
        if (ent.Comp.FeatherColor is { } featherColor)
            _appearance.SetData(ent, FeatherVisuals.FeatherColor, featherColor);

        if (ent.Comp.BloodColor is not { } bloodColor)
            return;

        _appearance.SetData(ent, FeatherVisuals.BloodColor, bloodColor);

        if (TryComp<StampComponent>(ent, out var stamp))
            stamp.StampedColor = bloodColor;
    }
    // HardLight edit end

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        var preenableQuery = EntityQueryEnumerator<PreenableComponent>();

        while (preenableQuery.MoveNext(out var uid, out var preenable))
        {
            if (preenable.ReplenishTime == null || _timing.CurTime < preenable.ReplenishTime)
                continue;

            if (preenable.CurrentFeathers >= preenable.MaximumFeathers)
                continue;

            preenable.CurrentFeathers += 1;

            if (preenable.CurrentFeathers >= preenable.MaximumFeathers)
            {
                preenable.ReplenishTime = null;
                Dirty(uid, preenable);
                continue;
            }

            preenable.ReplenishTime = _timing.CurTime + preenable.ReplenishDelay;
            Dirty(uid, preenable);
        }
    }
}
