using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Buckle.Components;
using Content.Shared.CCVar;
using Content.Shared.DoAfter;
using Content.Shared._DV.Abilities;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Input;
using Content.Shared.Item;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Hands;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Gravity;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Rejuvenate;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Configuration;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Input.Binding;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Stunnable;

public abstract partial class SharedStunSystem : EntitySystem
{
    [Dependency] protected readonly ActionBlockerSystem Blocker = default!;
    [Dependency] private readonly SharedBroadphaseSystem _broadphase = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] protected readonly SharedAppearanceSystem Appearance = default!;
    [Dependency] private readonly IConfigurationManager _cfgManager = default!;
    [Dependency] private readonly EntityWhitelistSystem _entityWhitelist = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StandingStateSystem _standingState = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] protected readonly IGameTiming GameTiming = default!;

    private EntityQuery<CrawlerComponent> _crawlerQuery;
    private const int StandingCollisionLayer = (int) Content.Shared.Physics.CollisionGroup.MidImpassable;
    private static readonly ProtoId<ItemSizePrototype> MaxCrawlMeleeItemSize = "Small";
    public static readonly ProtoId<AlertPrototype> KnockdownAlert = "Knockdown";

    /// <summary>
    /// Friction modifier for knocked down players.
    /// Doesn't make them faster but makes them slow down... slower.
    /// </summary>
    public const float KnockDownModifier = 0.2f;

    public override void Initialize()
    {
        _crawlerQuery = GetEntityQuery<CrawlerComponent>();

        SubscribeLocalEvent<KnockedDownComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<KnockedDownComponent, ComponentInit>(OnKnockInit);
        SubscribeLocalEvent<KnockedDownComponent, ComponentShutdown>(OnKnockShutdown);
        SubscribeLocalEvent<KnockedDownComponent, BuckleAttemptEvent>(OnBuckleAttempt);
        SubscribeLocalEvent<KnockedDownComponent, StandAttemptEvent>(OnStandAttempt);
        SubscribeLocalEvent<KnockedDownComponent, ShotAttemptedEvent>(OnShootAttempt);
        SubscribeLocalEvent<KnockedDownComponent, AttemptMeleeEvent>(OnMeleeAttempt);
        SubscribeLocalEvent<KnockedDownComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshKnockedSpeed);
        SubscribeLocalEvent<KnockedDownComponent, RefreshFrictionModifiersEvent>(OnRefreshFriction);
        SubscribeLocalEvent<KnockedDownComponent, TryStandDoAfterEvent>(OnStandDoAfter);

        SubscribeLocalEvent<SlowedDownComponent, ComponentInit>(OnSlowInit);
        SubscribeLocalEvent<SlowedDownComponent, ComponentShutdown>(OnSlowRemove);

        SubscribeLocalEvent<StunnedComponent, ComponentStartup>(UpdateCanMove);
        SubscribeLocalEvent<StunnedComponent, ComponentShutdown>(OnStunShutdown);

        SubscribeLocalEvent<StunOnContactComponent, ComponentStartup>(OnStunOnContactStartup);
        SubscribeLocalEvent<StunOnContactComponent, StartCollideEvent>(OnStunOnContactCollide);

        // helping people up if they're knocked down
        SubscribeLocalEvent<SlowedDownComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);

        SubscribeLocalEvent<KnockedDownComponent, TileFrictionEvent>(OnKnockedTileFriction);
        SubscribeLocalEvent<CrawlerComponent, KnockedDownRefreshEvent>(OnKnockdownRefresh);
        SubscribeLocalEvent<CrawlerComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<KnockedDownComponent, DidEquipHandEvent>(OnHandChanged);
        SubscribeLocalEvent<KnockedDownComponent, DidUnequipHandEvent>(OnHandChanged);
        SubscribeLocalEvent<KnockedDownComponent, HandCountChangedEvent>(OnHandChanged);

        SubscribeAllEvent<ForceStandUpEvent>(OnForceStandup);
        SubscribeLocalEvent<KnockedDownComponent, KnockedDownAlertEvent>(OnKnockedDownAlert);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ToggleKnockdown, InputCmdHandler.FromDelegate(HandleToggleKnockdown, handle: false))
            .Register<SharedStunSystem>();

        // Attempt event subscriptions.
        SubscribeLocalEvent<StunnedComponent, ChangeDirectionAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, UpdateCanMoveEvent>(OnMoveAttempt);
        SubscribeLocalEvent<StunnedComponent, InteractionAttemptEvent>(OnAttemptInteract);
        SubscribeLocalEvent<StunnedComponent, UseAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, ThrowAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, DropAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, AttackAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, PickupAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<StunnedComponent, IsUnequippingAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<MobStateComponent, MobStateChangedEvent>(OnMobStateChanged);

        // Stun Appearance Data
        InitializeAppearance();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        CommandBinds.Unregister<SharedStunSystem>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<KnockedDownComponent>();

        while (query.MoveNext(out var uid, out var knockedDown))
        {
            if (!knockedDown.AutoStand || knockedDown.DoAfterId.HasValue || knockedDown.NextUpdate > GameTiming.CurTime)
                continue;

            TryStanding(uid);
        }
    }

    private void OnAttemptInteract(Entity<StunnedComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnMobStateChanged(EntityUid uid, MobStateComponent component, MobStateChangedEvent args)
    {
        if (!TryComp<StatusEffectsComponent>(uid, out var status))
        {
            return;
        }
        switch (args.NewMobState)
        {
            case MobState.Alive:
                {
                    break;
                }
            case MobState.Critical:
                {
                    _statusEffect.TryRemoveStatusEffect(uid, "Stun");
                    break;
                }
            case MobState.Dead:
                {
                    _statusEffect.TryRemoveStatusEffect(uid, "Stun");
                    break;
                }
            case MobState.Invalid:
            default:
                return;
        }

    }

    private void OnStunShutdown(Entity<StunnedComponent> ent, ref ComponentShutdown args)
    {
        // This exists so the client can end their funny animation if they're playing one.
        UpdateCanMove(ent, ent.Comp, args);
        Appearance.RemoveData(ent, StunVisuals.SeeingStars);
    }

    private void UpdateCanMove(EntityUid uid, StunnedComponent component, EntityEventArgs args)
    {
        Blocker.UpdateCanMove(uid);
    }

    private void OnStunOnContactStartup(Entity<StunOnContactComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<PhysicsComponent>(ent, out var body))
            _broadphase.RegenerateContacts((ent, body));
    }

    private void OnStunOnContactCollide(Entity<StunOnContactComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.FixtureId)
            return;

        if (_entityWhitelist.IsBlacklistPass(ent.Comp.Blacklist, args.OtherEntity))
            return;

        if (!TryComp<StatusEffectsComponent>(args.OtherEntity, out var status))
            return;

        TryStun(args.OtherEntity, ent.Comp.Duration, true, status);
        TryKnockdown(args.OtherEntity, ent.Comp.Duration, true, force: true, status);
    }

    private void OnKnockInit(EntityUid uid, KnockedDownComponent component, ComponentInit args)
    {
        _standingState.Down(uid, dropHeldItems: false);
        RefreshKnockedMovement((uid, component));
        _alerts.ShowAlert(uid, KnockdownAlert);
    }

    private void OnKnockShutdown(EntityUid uid, KnockedDownComponent component, ComponentShutdown args)
    {
        component.FrictionModifier = 1f;
        component.SpeedModifier = 1f;
        _standingState.Stand(uid);
        _alerts.ClearAlert(uid, KnockdownAlert);
        _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
    }

    private void OnRejuvenate(Entity<KnockedDownComponent> entity, ref RejuvenateEvent args)
    {
        SetKnockdownNextUpdate(entity, GameTiming.CurTime);

        if (entity.Comp.AutoStand)
            RemComp<KnockedDownComponent>(entity);
    }

    private void OnStandAttempt(EntityUid uid, KnockedDownComponent component, StandAttemptEvent args)
    {
        if (component.LifeStage <= ComponentLifeStage.Running)
            args.Cancel();
    }

    private void OnSlowInit(EntityUid uid, SlowedDownComponent component, ComponentInit args)
    {
        _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
    }

    private void OnSlowRemove(EntityUid uid, SlowedDownComponent component, ComponentShutdown args)
    {
        component.SprintSpeedModifier = 1f;
        component.WalkSpeedModifier = 1f;
        _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
    }

    private void OnRefreshMovespeed(EntityUid uid, SlowedDownComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.WalkSpeedModifier, component.SprintSpeedModifier);
    }

    // TODO STUN: Make events for different things. (Getting modifiers, attempt events, informative events...)

    /// <summary>
    ///     Stuns the entity, disallowing it from doing many interactions temporarily.
    /// </summary>
    public bool TryStun(EntityUid uid, TimeSpan time, bool refresh,
        StatusEffectsComponent? status = null)
    {
        if (time <= TimeSpan.Zero)
            return false;

        if (!Resolve(uid, ref status, false))
            return false;

        if (!_statusEffect.TryAddStatusEffect<StunnedComponent>(uid, "Stun", time, refresh))
            return false;

        var ev = new StunnedEvent();
        RaiseLocalEvent(uid, ref ev);

        _adminLogger.Add(LogType.Stamina, LogImpact.Medium, $"{ToPrettyString(uid):user} stunned for {time.Seconds} seconds");
        return true;
    }

    /// <summary>
    ///     Tries to knock an entity to the ground, but will fail if they aren't able to crawl.
    ///     Useful if you don't want to paralyze an entity that can't crawl, but still want to knockdown
    ///     entities that can.
    /// </summary>
    /// <param name="entity">Entity we're trying to knockdown.</param>
    /// <param name="time">Time of the knockdown.</param>
    /// <param name="refresh">Do we refresh their timer, or add to it if one exists?</param>
    /// <param name="autoStand">Whether we should automatically stand when knockdown ends.</param>
    /// <param name="drop">Should we drop what we're holding?</param>
    /// <param name="force">Should we force crawling? Even if something tried to block it?</param>
    /// <returns>Returns true if the entity is able to crawl, and was able to be knocked down.</returns>
    public bool TryCrawling(Entity<CrawlerComponent?> entity,
        TimeSpan? time,
        bool refresh = true,
        bool autoStand = true,
        bool drop = true,
        bool force = false)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        // Starlight edit start - add voluntary value
        return TryKnockdown(entity, time, refresh, autoStand, drop, force, voluntary: false);
        // Starlight edit end
    }

    /// <inheritdoc cref="TryCrawling(Entity{CrawlerComponent?},TimeSpan?,bool,bool,bool,bool)"/>
    /// <summary>An overload of TryCrawling which uses the default crawling time from the CrawlerComponent as its timespan.</summary>
    public bool TryCrawling(Entity<CrawlerComponent?> entity,
        bool refresh = true,
        bool autoStand = true,
        bool drop = true,
        bool force = false)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        // Starlight edit start - add voluntary value
        return TryKnockdown(entity, entity.Comp.DefaultKnockedDuration, refresh, autoStand, drop, force, voluntary: true);
        // Starlight edit end
    }

    /// <summary>
    ///     Checks if we can knock down an entity to the ground...
    /// </summary>
    /// <param name="entity">The entity we're trying to knock down</param>
    /// <param name="time">The time of the knockdown</param>
    /// <param name="autoStand">Whether we want to automatically stand when knockdown ends.</param>
    /// <param name="drop">Whether we should drop items.</param>
    /// <param name="force">Should we force the status effect?</param>
    /// <param name="voluntary">Indicates whether the knockdown is voluntary</param>
    // Starlight edit start - add voluntary value
    public bool CanKnockdown(Entity<StandingStateComponent?> entity, ref TimeSpan? time, ref bool autoStand, ref bool drop, bool force = false, bool voluntary = false)
    // Starlight edit end
    {
        if (time <= TimeSpan.Zero)
            return false;

        // Can't fall down if you can't actually be downed.
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        // Starlight edit start - add voluntary value
        var evAttempt = new KnockDownAttemptEvent(autoStand, drop, time, voluntary);
        // Starlight edit end
        RaiseLocalEvent(entity, ref evAttempt);

        autoStand = evAttempt.AutoStand;
        drop = evAttempt.Drop;

        return force || !evAttempt.Cancelled && !_gravity.IsWeightless(entity.Owner);
    }

    /// <summary>
    ///     Knocks down the entity, making it fall to the ground.
    /// </summary>
    /// <param name="entity">The entity we're trying to knock down</param>
    /// <param name="time">The time of the knockdown</param>
    /// <param name="refresh">Whether we should refresh a running timer or add to it, if one exists.</param>
    /// <param name="autoStand">Whether we want to automatically stand when knockdown ends.</param>
    /// <param name="drop">Whether we should drop items.</param>
    /// <param name="force">Should we force the status effect?</param>
    /// <param name="voluntary">Indicates whether the knockdown is voluntary</param>
    // Starlight edit start - add voluntary value
    public bool TryKnockdown(Entity<CrawlerComponent?> entity, TimeSpan? time, bool refresh = true, bool autoStand = true, bool drop = true, bool force = false, bool voluntary = false)
    {
        if (!CanKnockdown(entity.Owner, ref time, ref autoStand, ref drop, force, voluntary))
            return false;
    // Starlight edit end

        // If the entity can't crawl they also need to be stunned, and therefore we should be using paralysis status effect.
        // Also time shouldn't be null if we're and trying to add time but, we check just in case anyways.
        if (!Resolve(entity, ref entity.Comp, false))
        {
            if (time == null)
                return TryStun(entity.Owner, TimeSpan.FromSeconds(1), refresh);

            Knockdown(entity.Owner, null, refresh, autoStand, drop);
            return TryStun(entity.Owner, time.Value, refresh);
        }

        Knockdown(entity.Owner, time, refresh, autoStand, drop);
        return true;
    }

    public bool TryKnockdown(EntityUid uid, TimeSpan time, bool refresh, bool force = false,
        StatusEffectsComponent? status = null)
    {
        return TryKnockdown(uid, time, refresh, autoStand: true, drop: true, force);
    }

    public bool TryKnockdown(EntityUid uid,
        TimeSpan? time,
        bool refresh = true,
        bool autoStand = true,
        bool drop = true,
        bool force = false)
    {
        return TryKnockdown((uid, CompOrNull<CrawlerComponent>(uid)), time, refresh, autoStand, drop, force);
    }

    private void Knockdown(EntityUid uid, TimeSpan? time, bool refresh, bool autoStand, bool drop)
    {
        // Initialize our component with the relevant data we need if we don't have it
        if (EnsureComp<KnockedDownComponent>(uid, out var component))
        {
            RefreshKnockedMovement((uid, component));
            CancelKnockdownDoAfter((uid, component));
        }
        else
        {
            // Only drop items the first time we want to fall...
            if (drop)
                RaiseLocalEvent(uid, new DropHandItemsEvent(), false);

            // Only update Autostand value if it's our first time being knocked down...
            SetAutoStand((uid, component), autoStand);
        }

        var knockedEv = new KnockedDownEvent();
        RaiseLocalEvent(uid, ref knockedEv);

        if (time != null)
        {
            UpdateKnockdownTime((uid, component), time.Value, refresh);
            _statusEffect.TryAddStatusEffect(uid, "KnockedDown", time.Value, refresh);
            _adminLogger.Add(LogType.Stamina, LogImpact.Medium, $"{ToPrettyString(uid):user} was knocked down for {time.Value.TotalSeconds} seconds");
        }
        else
        {
            _adminLogger.Add(LogType.Stamina, LogImpact.Medium, $"{ToPrettyString(uid):user} was knocked down");
        }
    }

    /// <summary>
    ///     Applies knockdown and stun to the entity temporarily.
    /// </summary>
    public bool TryParalyze(EntityUid uid, TimeSpan time, bool refresh,
        StatusEffectsComponent? status = null)
    {
        if (!Resolve(uid, ref status, false))
            return false;

        Knockdown(uid, null, refresh, autoStand: true, drop: true);
        return TryStun(uid, time, refresh, status);
    }

    /// <summary>
    ///     Slows down the mob's walking/running speed temporarily
    /// </summary>
    public bool TrySlowdown(EntityUid uid, TimeSpan time, bool refresh,
        float walkSpeedMultiplier = 1f, float runSpeedMultiplier = 1f,
        StatusEffectsComponent? status = null)
    {
        if (!Resolve(uid, ref status, false))
            return false;

        if (time <= TimeSpan.Zero)
            return false;

        if (_statusEffect.TryAddStatusEffect<SlowedDownComponent>(uid, "SlowedDown", time, refresh, status))
        {
            var slowed = Comp<SlowedDownComponent>(uid);
            // Doesn't make much sense to have the "TrySlowdown" method speed up entities now does it?
            walkSpeedMultiplier = Math.Clamp(walkSpeedMultiplier, 0f, 1f);
            runSpeedMultiplier = Math.Clamp(runSpeedMultiplier, 0f, 1f);

            slowed.WalkSpeedModifier *= walkSpeedMultiplier;
            slowed.SprintSpeedModifier *= runSpeedMultiplier;

            _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Updates the movement speed modifiers of an entity by applying or removing the <see cref="SlowedDownComponent"/>.
    /// If both walk and run modifiers are approximately 1 (i.e. normal speed) and <see cref="StaminaComponent.StaminaDamage"/> is 0,
    /// or if the both modifiers are 0, the slowdown component is removed to restore normal movement.
    /// Otherwise, the slowdown component is created or updated with the provided modifiers,
    /// and the movement speed is refreshed accordingly.
    /// </summary>
    /// <param name="ent">Entity whose movement speed should be updated.</param>
    /// <param name="walkSpeedModifier">New walk speed modifier. Default is 1f (normal speed).</param>
    /// <param name="runSpeedModifier">New run (sprint) speed modifier. Default is 1f (normal speed).</param>
    public void UpdateStunModifiers(Entity<StaminaComponent?> ent,
        float walkSpeedModifier = 1f,
        float runSpeedModifier = 1f)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (
            (MathHelper.CloseTo(walkSpeedModifier, 1f) && MathHelper.CloseTo(runSpeedModifier, 1f) && ent.Comp.StaminaDamage == 0f) ||
            (walkSpeedModifier == 0f && runSpeedModifier == 0f)
        )
        {
            RemComp<SlowedDownComponent>(ent);
            return;
        }

        EnsureComp<SlowedDownComponent>(ent, out var comp);

        comp.WalkSpeedModifier = walkSpeedModifier;

        comp.SprintSpeedModifier = runSpeedModifier;

        _movementSpeedModifier.RefreshMovementSpeedModifiers(ent);

        Dirty(ent);
    }

    /// <summary>
    /// A convenience overload of <see cref="UpdateStunModifiers(EntityUid, float, float, StaminaComponent?)"/> that sets both
    /// walk and run speed modifiers to the same value.
    /// </summary>
    /// <param name="ent">Entity whose movement speed should be updated.</param>
    /// <param name="speedModifier">New walk and run speed modifier. Default is 1f (normal speed).</param>
    /// <param name="component">
    /// Optional <see cref="StaminaComponent"/> of the entity.
    /// </param>
    public void UpdateStunModifiers(Entity<StaminaComponent?> ent, float speedModifier = 1f)
    {
        UpdateStunModifiers(ent, speedModifier, speedModifier);
    }

    #region HardLight: Pre StatusEffectNew Merge
    public void SetAutoStand(Entity<KnockedDownComponent?> entity, bool autoStand = false)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        entity.Comp.AutoStand = autoStand;
        DirtyField(entity, entity.Comp, nameof(KnockedDownComponent.AutoStand));
    }

    public bool TryStanding(Entity<KnockedDownComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return true;

        if (!KnockdownOver((entity, entity.Comp)))
            return false;

        if (!_crawlerQuery.TryComp(entity, out var crawler) || !_cfgManager.GetCVar(CCVars.MovementCrawling))
        {
            RemComp<KnockedDownComponent>(entity);
            _statusEffect.TryRemoveStatusEffect(entity, "KnockedDown");
            _adminLogger.Add(LogType.Stamina, LogImpact.Medium, $"{ToPrettyString(entity):user} has stood up from knockdown.");
            return true;
        }

        if (StandingBlocked((entity, entity.Comp)))
            return false;

        var ev = new GetStandUpTimeEvent(crawler.StandTime);
        RaiseLocalEvent(entity, ref ev);

        var doAfterArgs = new DoAfterArgs(EntityManager, entity, ev.DoAfterTime, new TryStandDoAfterEvent(), entity, entity)
        {
            BreakOnDamage = true,
            DamageThreshold = 15,
            CancelDuplicate = true,
            RequireCanInteract = false,
            BreakOnHandChange = true
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs, out var doAfterId))
            return false;

        entity.Comp.DoAfterId = doAfterId.Value.Index;
        entity.Comp.GetUpDoAfter = ev.DoAfterTime;
        DirtyField(entity, entity.Comp, nameof(KnockedDownComponent.DoAfterId));
        DirtyField(entity, entity.Comp, nameof(KnockedDownComponent.GetUpDoAfter));
        return true;
    }

    public bool KnockdownOver(Entity<KnockedDownComponent> entity)
    {
        return entity.Comp.NextUpdate <= GameTiming.CurTime && Blocker.CanMove(entity);
    }

    private bool TryStand(Entity<KnockedDownComponent> entity)
    {
        if (!KnockdownOver(entity))
            return false;

        var ev = new StandUpAttemptEvent(entity.Comp.AutoStand);
        RaiseLocalEvent(entity, ref ev);

        if (ev.Autostand != entity.Comp.AutoStand)
            SetAutoStand((entity.Owner, entity.Comp), ev.Autostand);

        if (ev.Message != null)
            _popup.PopupClient(ev.Message.Value.Item1, entity, entity, ev.Message.Value.Item2);

        return !ev.Cancelled;
    }

    private bool StandingBlocked(Entity<KnockedDownComponent> entity)
    {
        if (!TryStand(entity))
            return true;

        if (_gravity.IsWeightless(entity.Owner))
            return false;

        if (!IntersectingStandingColliders(entity.Owner))
            return false;

        _popup.PopupClient(Loc.GetString("knockdown-component-stand-no-room"), entity, entity, PopupType.SmallCaution);
        SetAutoStand(entity.Owner);
        return true;
    }

    private void SetKnockdownNextUpdate(Entity<KnockedDownComponent> entity, TimeSpan time)
    {
        if (GameTiming.CurTime > time)
            time = GameTiming.CurTime;

        entity.Comp.NextUpdate = time;
        DirtyField(entity, entity.Comp, nameof(KnockedDownComponent.NextUpdate));
        _alerts.ShowAlert(entity.Owner, KnockdownAlert, cooldown: (GameTiming.CurTime, entity.Comp.NextUpdate));
    }

    private void UpdateKnockdownTime(Entity<KnockedDownComponent> entity, TimeSpan time, bool refresh)
    {
        var nextUpdate = GameTiming.CurTime + time;
        var current = entity.Comp.NextUpdate;

        if (refresh && current > nextUpdate)
            return;

        if (!refresh && current > GameTiming.CurTime)
            nextUpdate = current + time;

        SetKnockdownNextUpdate(entity, nextUpdate);
    }

    private void RefreshKnockdownTime(EntityUid uid, TimeSpan time, KnockedDownComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        UpdateKnockdownTime((uid, component), time, true);
    }

    private void CancelKnockdownDoAfter(Entity<KnockedDownComponent> entity)
    {
        if (entity.Comp.DoAfterId == null)
            return;

        _doAfter.Cancel(new DoAfterId(entity.Owner, entity.Comp.DoAfterId.Value));
        entity.Comp.DoAfterId = null;
        DirtyField(entity, entity.Comp, nameof(KnockedDownComponent.DoAfterId));
    }

    private void HandleToggleKnockdown(ICommonSession? session)
    {
        if (session is not { } playerSession)
            return;

        if (playerSession.AttachedEntity is not { Valid: true } playerEnt || !Exists(playerEnt))
            return;

        ToggleKnockdown(playerEnt);
    }

    private void ToggleKnockdown(Entity<CrawlerComponent?, KnockedDownComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp1, false) || !_cfgManager.GetCVar(CCVars.MovementCrawling))
            return;

        if (!Resolve(entity, ref entity.Comp2, false))
        {
            TryKnockdown(entity.Owner, entity.Comp1.DefaultKnockedDuration, true, false, false, false, true);
            return;
        }

        var stand = !entity.Comp2.DoAfterId.HasValue;
        SetAutoStand((entity, entity.Comp2), stand);

        if (!stand || !TryStanding((entity, entity.Comp2)))
            CancelKnockdownDoAfter((entity, entity.Comp2));

        SetAutoStand((entity, entity.Comp2), stand);
    }

    private void OnStandDoAfter(Entity<KnockedDownComponent> entity, ref TryStandDoAfterEvent args)
    {
        entity.Comp.DoAfterId = null;

        if (args.Cancelled || StandingBlocked(entity))
        {
            DirtyField(entity, entity.Comp, nameof(KnockedDownComponent.DoAfterId));
            return;
        }

        RemComp<KnockedDownComponent>(entity);
        _statusEffect.TryRemoveStatusEffect(entity, "KnockedDown");
        _adminLogger.Add(LogType.Stamina, LogImpact.Medium, $"{ToPrettyString(entity):user} has stood up from knockdown.");
    }

    private void RefreshKnockedMovement(Entity<KnockedDownComponent> entity)
    {
        var ev = new KnockedDownRefreshEvent();
        RaiseLocalEvent(entity, ref ev);

        entity.Comp.SpeedModifier = ev.SpeedModifier;
        entity.Comp.FrictionModifier = ev.FrictionModifier;
        Dirty(entity, entity.Comp);

        _movementSpeedModifier.RefreshMovementSpeedModifiers(entity);
        _movementSpeedModifier.RefreshFrictionModifiers(entity);
    }

    private void OnRefreshKnockedSpeed(Entity<KnockedDownComponent> entity, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(entity.Comp.SpeedModifier, entity.Comp.SpeedModifier);
    }

    private void OnRefreshFriction(Entity<KnockedDownComponent> entity, ref RefreshFrictionModifiersEvent args)
    {
        args.ModifyFriction(entity.Comp.FrictionModifier);
        args.ModifyAcceleration(entity.Comp.FrictionModifier);
    }

    private void OnDamaged(Entity<CrawlerComponent> entity, ref DamageChangedEvent args)
    {
        if (!args.InterruptsDoAfters || !args.DamageIncreased || args.DamageDelta == null || GameTiming.ApplyingState)
            return;

        if (args.DamageDelta.GetTotal() >= entity.Comp.KnockdownDamageThreshold)
            RefreshKnockdownTime(entity.Owner, entity.Comp.DefaultKnockedDuration);
    }

    private void OnKnockdownRefresh(Entity<CrawlerComponent> entity, ref KnockedDownRefreshEvent args)
    {
        args.FrictionModifier *= entity.Comp.FrictionModifier;
        args.SpeedModifier *= entity.Comp.SpeedModifier;
    }

    private void OnHandChanged(Entity<KnockedDownComponent> entity, ref DidEquipHandEvent args)
    {
        if (!GameTiming.ApplyingState)
            RefreshKnockedMovement(entity);
    }

    private void OnHandChanged(Entity<KnockedDownComponent> entity, ref DidUnequipHandEvent args)
    {
        if (!GameTiming.ApplyingState)
            RefreshKnockedMovement(entity);
    }

    private void OnHandChanged(Entity<KnockedDownComponent> entity, ref HandCountChangedEvent args)
    {
        if (!GameTiming.ApplyingState)
            RefreshKnockedMovement(entity);
    }

    private void OnForceStandup(ForceStandUpEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } user)
            ForceStandUp(user);
    }

    public void ForceStandUp(Entity<KnockedDownComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        SetAutoStand(entity, true);

        if (StandingBlocked((entity, entity.Comp)))
            return;

        if (!_hands.TryGetEmptyHand(entity.Owner, out _))
            return;

        if (!TryForceStand(entity.Owner))
            return;

        CancelKnockdownDoAfter((entity, entity.Comp));
        RemComp<KnockedDownComponent>(entity);
        _statusEffect.TryRemoveStatusEffect(entity, "KnockedDown");

        _adminLogger.Add(LogType.Stamina, LogImpact.Medium, $"{ToPrettyString(entity):user} has force stood up from knockdown.");
    }

    private void OnKnockedDownAlert(Entity<KnockedDownComponent> entity, ref KnockedDownAlertEvent args)
    {
        if (args.Handled)
            return;

        if (!TryStanding(entity.Owner))
            ForceStandUp((entity.Owner, entity.Comp));

        DirtyField(entity, entity.Comp, nameof(KnockedDownComponent.DoAfterId));
        args.Handled = true;
    }

    private bool TryForceStand(Entity<StaminaComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        var ev = new TryForceStandEvent(entity.Comp.ForceStandStamina);
        RaiseLocalEvent(entity, ref ev);

        if (!_stamina.TryTakeStamina(entity, ev.Stamina, entity.Comp, visual: true))
        {
            _popup.PopupClient(Loc.GetString("knockdown-component-pushup-failure"), entity, entity, PopupType.MediumCaution);
            return false;
        }

        _popup.PopupClient(Loc.GetString("knockdown-component-pushup-success"), entity, entity);
        _audio.PlayPredicted(entity.Comp.ForceStandSuccessSound, entity.Owner, entity.Owner, AudioParams.Default.WithVariation(0.025f).WithVolume(5f));
        return true;
    }

    private bool IntersectingStandingColliders(Entity<TransformComponent?> entity)
    {
        if (TryComp<CrawlUnderObjectsComponent>(entity, out var crawlUnder) && crawlUnder.Enabled)
            return false;

        if (!Resolve(entity, ref entity.Comp))
            return false;

        var intersecting = _physics.GetEntitiesIntersectingBody(entity, StandingCollisionLayer, false);
        if (intersecting.Count == 0)
            return false;

        var fixtureQuery = GetEntityQuery<FixturesComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();
        var ourAabb = _entityLookup.GetAABBNoContainer(entity, entity.Comp.LocalPosition, entity.Comp.LocalRotation);

        foreach (var ent in intersecting)
        {
            if (!fixtureQuery.TryGetComponent(ent, out var fixtures)
                || !xformQuery.TryComp(ent, out var xformComp))
                continue;

            var xform = new Transform(xformComp.LocalPosition, xformComp.LocalRotation);

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard || (fixture.CollisionMask & StandingCollisionLayer) != StandingCollisionLayer)
                    continue;

                for (var i = 0; i < fixture.Shape.ChildCount; i++)
                {
                    if (fixture.Shape.ComputeAABB(xform, i).IntersectPercentage(ourAabb) > 0.1f)
                        return true;
                }
            }
        }

        return false;
    }

    private void OnKnockedTileFriction(EntityUid uid, KnockedDownComponent component, ref TileFrictionEvent args)
    {
        args.Modifier *= component.FrictionModifier;
    }

    private void OnBuckleAttempt(Entity<KnockedDownComponent> entity, ref BuckleAttemptEvent args)
    {
        if (args.User == entity.Owner && entity.Comp.NextUpdate > GameTiming.CurTime)
            args.Cancelled = true;
    }

    private void OnShootAttempt(Entity<KnockedDownComponent> entity, ref ShotAttemptedEvent args)
    {
        args.Cancel();

        if (args.Used.Comp.NextFire <= GameTiming.CurTime)
        {
            _popup.PopupClient(Loc.GetString("knockdown-component-shoot-fail"), entity, entity, PopupType.MediumCaution);
            args.Used.Comp.NextFire = GameTiming.CurTime + TimeSpan.FromSeconds(0.5f);
            DirtyField(args.Used, args.Used.Comp, nameof(args.Used.Comp.NextFire));
        }
    }

    private void OnMeleeAttempt(Entity<KnockedDownComponent> entity, ref AttemptMeleeEvent args)
    {
        if (args.Weapon == args.User)
            return;

        if (TryComp<ItemComponent>(args.Weapon, out var item)
            && _item.GetSizePrototype(item.Size) <= _item.GetSizePrototype(MaxCrawlMeleeItemSize))
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("knockdown-component-melee-fail");
    }
    #endregion

    #region Attempt Event Handling

    private void OnMoveAttempt(EntityUid uid, StunnedComponent stunned, UpdateCanMoveEvent args)
    {
        if (stunned.LifeStage > ComponentLifeStage.Running)
            return;

        args.Cancel();
    }

    private void OnAttempt(EntityUid uid, StunnedComponent stunned, CancellableEntityEventArgs args)
    {
        args.Cancel();
    }

    private void OnEquipAttempt(EntityUid uid, StunnedComponent stunned, IsEquippingAttemptEvent args)
    {
        // is this a self-equip, or are they being stripped?
        if (args.Equipee == uid)
            args.Cancel();
    }

    private void OnUnequipAttempt(EntityUid uid, StunnedComponent stunned, IsUnequippingAttemptEvent args)
    {
        // is this a self-equip, or are they being stripped?
        if (args.Unequipee == uid)
            args.Cancel();
    }

    #endregion
}
