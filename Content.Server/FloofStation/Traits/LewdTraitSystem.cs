using Content.Server.Fluids.EntitySystems;
using Content.Server.Popups;
using Content.Shared._Mono.Traits.Physical;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.FloofStation.Traits.Events;
using Content.Shared.FloofStation.Traits.Events.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.FloofStation.Traits;

public sealed class LewdTraitSystem : SharedLewdTraitSystem // HL: Move LewdTrait to shared system to fix slow UI loading
{
    [Dependency]
    private readonly HungerSystem _hunger = default!;

    [Dependency]
    private readonly IGameTiming _timing = default!;

    [Dependency]
    private readonly MobStateSystem _mobState = default!;

    [Dependency]
    private readonly SharedDoAfterSystem _doAfterSystem = default!;

    [Dependency]
    private readonly PopupSystem _popupSystem = default!;

    [Dependency]
    private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    [Dependency]
    private readonly PuddleSystem _puddle = default!; // HardLight

    [Dependency]
    private readonly SharedAudioSystem _audio = default!; // HardLight

    public override void Initialize()
    {
        base.Initialize();

        //Initializers
        SubscribeLocalEvent<CumProducerComponent, ComponentStartup>(OnComponentInitCum);
        SubscribeLocalEvent<MilkProducerComponent, ComponentStartup>(OnComponentInitMilk);
        //SubscribeLocalEvent<SquirtProducerComponent, ComponentStartup>(OnComponentInitSquirt); //Unused-Trait is WIP
        SubscribeLocalEvent<PissProducerComponent, ComponentStartup>(OnComponentInitPiss);

        //Events
        SubscribeLocalEvent<CumProducerComponent, CummingDoAfterEvent>(OnDoAfterCum);
        SubscribeLocalEvent<MilkProducerComponent, MilkingDoAfterEvent>(OnDoAfterMilk);
        SubscribeLocalEvent<MilkProducerComponent, DrinkMilkDoAfterEvent>(OnDoAfterDrinkMilk); // Hardlight
        //SubscribeLocalEvent<SquirtProducerComponent, SquirtingDoAfterEvent>(OnDoAfterSquirt); //Unused-Trait is WIP
        SubscribeLocalEvent<PissProducerComponent, PissingDoAfterEvent>(OnDoAfterPiss);
    }

    #region event handling
    private void OnComponentInitCum(Entity<CumProducerComponent> entity, ref ComponentStartup args)
    {
        if (!_solutionContainer.EnsureSolution(entity.Owner, entity.Comp.SolutionName, out var solutionCum))
            return;

        solutionCum.MaxVolume = entity.Comp.MaxVolume;

        solutionCum.AddReagent(entity.Comp.ReagentId, entity.Comp.MaxVolume - solutionCum.Volume);
    }

    private void OnComponentInitMilk(Entity<MilkProducerComponent> entity, ref ComponentStartup args)
    {
        if (!_solutionContainer.EnsureSolution(entity.Owner, entity.Comp.SolutionName, out var solutionMilk))
            return;

        solutionMilk.MaxVolume = entity.Comp.MaxVolume;

        solutionMilk.AddReagent(entity.Comp.ReagentId, entity.Comp.MaxVolume - solutionMilk.Volume);
    }

    //private void OnComponentInitSquirt(Entity<SquirtProducerComponent> entity, ref ComponentStartup args) //Unused-Trait is WIP
    //{
    //    var solutionSquirt = _solutionContainer.EnsureSolution(entity.Owner, entity.Comp.SolutionName);
    //    solutionSquirt.MaxVolume = entity.Comp.MaxVolume;

    //    solutionSquirt.AddReagent(entity.Comp.ReagentId, entity.Comp.MaxVolume - solutionSquirt.Volume);
    //}

    private void OnComponentInitPiss(Entity<PissProducerComponent> entity, ref ComponentStartup args)
    {
        if (!_solutionContainer.EnsureSolution(entity.Owner, entity.Comp.SolutionName, out var solutionPiss))
            return;

        solutionPiss.MaxVolume = entity.Comp.MaxVolume;

        solutionPiss.AddReagent(entity.Comp.ReagentId, entity.Comp.MaxVolume - solutionPiss.Volume);
    }

    private void OnDoAfterCum(Entity<CumProducerComponent> entity, ref CummingDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Used == null)
            return;

        if (
            !_solutionContainer.ResolveSolution(
                entity.Owner,
                entity.Comp.SolutionName,
                ref entity.Comp.Solution,
                out var solution
            )
        )
            return;

        // Try refillable solution first (containers like beakers)
        if (
            _solutionContainer.TryGetRefillableSolution(
                args.Args.Used.Value,
                out var targetSoln,
                out var targetSolution
            )
        )
        {
            args.Handled = true;
            var quantity = solution.Volume;
            if (quantity == 0)
            {
                _popupSystem.PopupEntity(Loc.GetString("cum-verb-dry"), entity.Owner, args.Args.User);
                return;
            }

            if (quantity > targetSolution.AvailableVolume)
                quantity = targetSolution.AvailableVolume;

            var split = _solutionContainer.SplitSolution(entity.Comp.Solution.Value, quantity);
            _solutionContainer.TryAddSolution(targetSoln.Value, split);
            _popupSystem.PopupEntity(
                Loc.GetString(
                    "cum-verb-success",
                    ("amount", quantity),
                    ("target", Identity.Entity(args.Args.Used.Value, EntityManager))
                ),
                entity.Owner,
                args.Args.User,
                PopupType.Medium
            );

            return;
        }

        // Try injectable solution (entities like players with stomachs)
        if (
            _solutionContainer.TryGetInjectableSolution(
                args.Args.Used.Value,
                out var injectSoln,
                out var injectSolution
            )
        )
        {
            args.Handled = true;
            var quantity = solution.Volume;
            if (quantity == 0)
            {
                _popupSystem.PopupEntity(Loc.GetString("cum-verb-dry"), entity.Owner, args.Args.User);
                return;
            }

            // HardLight start
            var available = injectSolution.AvailableVolume;
            var injected = quantity > available ? available : quantity;

            if (injected > 0)
            {
                var splitInject = _solutionContainer.SplitSolution(entity.Comp.Solution.Value, injected);
                _solutionContainer.TryAddSolution(injectSoln.Value, splitInject);
            }

            var overflow = quantity - injected;
            if (overflow > 0)
            {
                var splitOverflow = _solutionContainer.SplitSolution(entity.Comp.Solution.Value, overflow);
                _puddle.TrySpillAt(args.Args.Used.Value, splitOverflow, out _, sound: false);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg"), args.Args.Used.Value);
                _popupSystem.PopupEntity(
                    Loc.GetString("cum-verb-overflow", ("amount", overflow)),
                    args.Args.Used.Value,
                    PopupType.MediumCaution
                );
            }

            _popupSystem.PopupEntity(
                Loc.GetString(
                    "cum-verb-success",
                    ("amount", injected),
                    ("target", Identity.Entity(args.Args.Used.Value, EntityManager))
                ),
                entity.Owner,
                args.Args.User,
                PopupType.Medium
            );
            _popupSystem.PopupEntity(
                Loc.GetString(
                    "cum-verb-success-other",
                    ("amount", injected),
                    ("target", Identity.Entity(args.Args.User, EntityManager))
                ),
                entity.Owner,
                args.Args.Used.Value,
                PopupType.Medium
            );
            // HardLight end
            return;
        }
    }

    private void OnDoAfterMilk(Entity<MilkProducerComponent> entity, ref MilkingDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Used == null)
            return;

        if (
            !_solutionContainer.ResolveSolution(
                entity.Owner,
                entity.Comp.SolutionName,
                ref entity.Comp.Solution,
                out var solution
            )
        )
            return;

        if (
            !_solutionContainer.TryGetRefillableSolution(
                args.Args.Used.Value,
                out var targetSoln,
                out var targetSolution
            )
        )
            return;

        args.Handled = true;
        var quantity = solution.Volume;
        if (quantity == 0)
        {
            // Hardlight Start
            if (entity.Owner == args.Args.User)
            {
                _popupSystem.PopupEntity(Loc.GetString("milk-verb-dry"), entity.Owner, args.Args.User);
            }
            else
            {
                _popupSystem.PopupEntity(
                    Loc.GetString("milk-verb-dry-other", ("person", Identity.Entity(entity.Owner, EntityManager))),
                    entity.Owner,
                    args.Args.User,
                    PopupType.Medium
                );
                _popupSystem.PopupEntity(Loc.GetString("milk-verb-dry"), entity.Owner, entity.Owner);
            }
            // Hardlight End
            return;
        }

        if (quantity > targetSolution.AvailableVolume)
            quantity = targetSolution.AvailableVolume;

        var split = _solutionContainer.SplitSolution(entity.Comp.Solution.Value, quantity);
        _solutionContainer.TryAddSolution(targetSoln.Value, split);
        // Hardlight Start
        if (entity.Owner == args.Args.User)
        {
            _popupSystem.PopupEntity(
                Loc.GetString(
                    "milk-verb-success",
                    ("amount", quantity),
                    ("target", Identity.Entity(args.Args.Used.Value, EntityManager))
                ),
                entity.Owner,
                args.Args.User,
                PopupType.Medium
            );
        }
        else
        {
            _popupSystem.PopupEntity(
                Loc.GetString(
                    "milk-verb-success-other",
                    ("amount", quantity),
                    ("target", Identity.Entity(args.Args.Used.Value, EntityManager)),
                    ("person", Identity.Entity(entity.Owner, EntityManager))
                ),
                entity.Owner,
                args.Args.User,
                PopupType.Medium
            );
            _popupSystem.PopupEntity(
                Loc.GetString(
                    "milk-verb-success-other-self",
                    ("amount", quantity),
                    ("target", Identity.Entity(args.Args.Used.Value, EntityManager)),
                    ("person", Identity.Entity(args.Args.User, EntityManager))
                ),
                entity.Owner,
                entity.Owner,
                PopupType.Medium
            );
        }
        // Hardlight End
    }

    // Hardlight Start
    private void OnDoAfterDrinkMilk(Entity<MilkProducerComponent> entity, ref DrinkMilkDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (
            !_solutionContainer.ResolveSolution(
                entity.Owner,
                entity.Comp.SolutionName,
                ref entity.Comp.Solution,
                out var solution
            )
        )
            return;

        if (!_solutionContainer.TryGetInjectableSolution(args.Args.User, out var injectSoln, out var injectSolution))
            return;

        args.Handled = true;
        var quantity = solution.Volume;
        if (quantity == 0)
        {
            // Hardlight Start
            if (entity.Owner == args.Args.User)
            {
                _popupSystem.PopupEntity(Loc.GetString("milk-verb-dry"), entity.Owner, args.Args.User);
            }
            else
            {
                _popupSystem.PopupEntity(
                    Loc.GetString("milk-verb-dry-other", ("person", Identity.Entity(entity.Owner, EntityManager))),
                    entity.Owner,
                    args.Args.User,
                    PopupType.Medium
                );
                _popupSystem.PopupEntity(Loc.GetString("milk-verb-dry"), entity.Owner, entity.Owner);
            }
            return;
        }

        if (quantity > 5)
            quantity = 5;

        var split = _solutionContainer.SplitSolution(entity.Comp.Solution.Value, quantity);
        _solutionContainer.TryAddSolution(injectSoln.Value, split);
        if (entity.Owner == args.Args.User)
        {
            _popupSystem.PopupEntity(
                Loc.GetString("drink-milk-verb-success", ("amount", quantity)),
                entity.Owner,
                args.Args.User,
                PopupType.Medium
            );
        }
        else
        {
            _popupSystem.PopupEntity(
                Loc.GetString(
                    "drink-milk-verb-success-other",
                    ("amount", quantity),
                    ("person", Identity.Entity(entity.Owner, EntityManager))
                ),
                entity.Owner,
                args.Args.User,
                PopupType.Medium
            );
            _popupSystem.PopupEntity(
                Loc.GetString(
                    "drink-milk-verb-success-other-self",
                    ("amount", quantity),
                    ("person", Identity.Entity(args.Args.User, EntityManager))
                ),
                entity.Owner,
                entity.Owner,
                PopupType.Medium
            );
        }
        AttemptDrinkMilk(entity, args.Args.User);
    }

    // Hardlight End

    //private void OnDoAfterSquirt(Entity<SquirtProducerComponent> entity, ref SquirtingDoAfterEvent args) //Unused-Trait is WIP
    //{
    //    if (args.Cancelled || args.Handled || args.Args.Used == null)
    //        return;

    //    if (!_solutionContainer.ResolveSolution(entity.Owner, entity.Comp.SolutionName, ref entity.Comp.Solution, out var solution))
    //        return;

    //    if (!_solutionContainer.TryGetRefillableSolution(args.Args.Used.Value, out var targetSoln, out var targetSolution))
    //        return;

    //    args.Handled = true;
    //    var quantity = solution.Volume;
    //    if (quantity == 0)
    //    {
    //        _popupSystem.PopupEntity(Loc.GetString("squirt-verb-dry"), entity.Owner, args.Args.User);
    //        return;
    //    }

    //    if (quantity > targetSolution.AvailableVolume)
    //        quantity = targetSolution.AvailableVolume;

    //    var split = _solutionContainer.SplitSolution(entity.Comp.Solution.Value, quantity);
    //    _solutionContainer.TryAddSolution(targetSoln.Value, split);
    //    _popupSystem.PopupEntity(Loc.GetString("squirt-verb-success", ("amount", quantity), ("target", Identity.Entity(args.Args.Used.Value, EntityManager))), entity.Owner, args.Args.User, PopupType.Medium);
    //}

    private void OnDoAfterPiss(Entity<PissProducerComponent> entity, ref PissingDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Used == null)
            return;

        if (
            !_solutionContainer.ResolveSolution(
                entity.Owner,
                entity.Comp.SolutionName,
                ref entity.Comp.Solution,
                out var solution
            )
        )
            return;

        // Try refillable solution first (containers like beakers)
        if (
            _solutionContainer.TryGetRefillableSolution(
                args.Args.Used.Value,
                out var targetSoln,
                out var targetSolution
            )
        )
        {
            args.Handled = true;
            var quantity = solution.Volume;
            if (quantity == 0)
            {
                _popupSystem.PopupEntity(Loc.GetString("piss-verb-dry"), entity.Owner, args.Args.User);
                return;
            }

            if (quantity > targetSolution.AvailableVolume)
                quantity = targetSolution.AvailableVolume;

            var split = _solutionContainer.SplitSolution(entity.Comp.Solution.Value, quantity);
            _solutionContainer.TryAddSolution(targetSoln.Value, split);
            _popupSystem.PopupEntity(
                Loc.GetString(
                    "piss-verb-success",
                    ("amount", quantity),
                    ("target", Identity.Entity(args.Args.Used.Value, EntityManager))
                ),
                entity.Owner,
                args.Args.User,
                PopupType.Medium
            );
            return;
        }

        // Try injectable solution (entities like players with stomachs)
        if (
            _solutionContainer.TryGetInjectableSolution(
                args.Args.Used.Value,
                out var injectSoln,
                out var injectSolution
            )
        )
        {
            args.Handled = true;
            var quantity = solution.Volume;
            if (quantity == 0)
            {
                _popupSystem.PopupEntity(Loc.GetString("piss-verb-dry"), entity.Owner, args.Args.User);
                return;
            }

            if (quantity > injectSolution.AvailableVolume)
                quantity = injectSolution.AvailableVolume;

            var split = _solutionContainer.SplitSolution(entity.Comp.Solution.Value, quantity);
            _solutionContainer.TryAddSolution(injectSoln.Value, split);
            _popupSystem.PopupEntity(
                Loc.GetString(
                    "piss-verb-success",
                    ("amount", quantity),
                    ("target", Identity.Entity(args.Args.Used.Value, EntityManager))
                ),
                entity.Owner,
                args.Args.User,
                PopupType.Medium
            );
            return;
        }
    }
    #endregion

    #region utilities
    protected override void AttemptCum(Entity<CumProducerComponent> lewd, EntityUid userUid, EntityUid containerUid)
    {
        if (!HasComp<CumProducerComponent>(userUid))
            return;

        var doargs = new DoAfterArgs(
            EntityManager,
            userUid,
            5,
            new CummingDoAfterEvent(),
            lewd,
            lewd,
            used: containerUid
        )
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            MovementThreshold = 1.0f,
        };

        _doAfterSystem.TryStartDoAfter(doargs);
    }

    protected override void AttemptMilk(Entity<MilkProducerComponent> lewd, EntityUid userUid, EntityUid containerUid)
    {
        if (!Resolve(lewd, ref lewd.Comp!))
            return;

        var doargs = new DoAfterArgs(
            EntityManager,
            userUid,
            5,
            new MilkingDoAfterEvent(),
            lewd,
            lewd,
            used: containerUid
        )
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            MovementThreshold = 1.0f,
        };

        _doAfterSystem.TryStartDoAfter(doargs);
    }

    protected override void AttemptDrinkMilk(Entity<MilkProducerComponent> lewd, EntityUid userUid)
    {
        if (!Resolve(lewd, ref lewd.Comp!))
            return;

        var drinkSpeed = 2f;
        if (HasComp<VoraciousComponent>(userUid))
        {
            drinkSpeed = 0.5f;
        }
        var doargs = new DoAfterArgs(EntityManager, userUid, drinkSpeed, new DrinkMilkDoAfterEvent(), lewd, lewd)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            MovementThreshold = 1.0f,
        };

        _doAfterSystem.TryStartDoAfter(doargs);
    }

    //private void AttemptSquirt(Entity<SquirtProducerComponent> lewd, EntityUid userUid, EntityUid containerUid) //Unused-Trait is WIP
    //{
    //    if (!HasComp<SquirtProducerComponent>(userUid))
    //        return;

    //    var doargs = new DoAfterArgs(EntityManager, userUid, 5, new SquirtingDoAfterEvent(), lewd, lewd, used: containerUid)
    //    {
    //        BreakOnUserMove = true,
    //        BreakOnDamage = true,
    //        BreakOnTargetMove = true,
    //        MovementThreshold = 1.0f,
    //    };

    //    _doAfterSystem.TryStartDoAfter(doargs);
    //}

    protected override void AttemptPiss(Entity<PissProducerComponent> lewd, EntityUid userUid, EntityUid containerUid)
    {
        if (!HasComp<PissProducerComponent>(userUid))
            return;

        var doargs = new DoAfterArgs(
            EntityManager,
            userUid,
            5,
            new PissingDoAfterEvent(),
            lewd,
            lewd,
            used: containerUid
        )
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            MovementThreshold = 1.0f,
        };

        _doAfterSystem.TryStartDoAfter(doargs);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var queryCum = EntityQueryEnumerator<CumProducerComponent>();
        var queryPiss = EntityQueryEnumerator<PissProducerComponent>();
        var queryMilk = EntityQueryEnumerator<MilkProducerComponent>();
        var now = _timing.CurTime;

        while (queryCum.MoveNext(out var uid, out var containerCum))
        {
            if (now < containerCum.NextGrowth)
                continue;

            containerCum.NextGrowth = now + containerCum.GrowthDelay;

            if (_mobState.IsDead(uid))
                continue;

            if (!_solutionContainer.ResolveSolution(uid, containerCum.SolutionName, ref containerCum.Solution))
                continue;

            if (TryComp(uid, out HungerComponent? hunger))
            {
                if (_hunger.GetHungerThreshold(hunger) < HungerThreshold.Okay)
                    continue;
                _solutionContainer.TryAddReagent(
                    containerCum.Solution.Value,
                    containerCum.ReagentId,
                    containerCum.QuantityPerUpdate,
                    out var quantity
                );
                if (quantity > 0)
                {
                    _hunger.ModifyHunger(uid, -containerCum.HungerUsage, hunger);
                }

                continue;
            }

            _solutionContainer.TryAddReagent(
                containerCum.Solution.Value,
                containerCum.ReagentId,
                containerCum.QuantityPerUpdate,
                out _
            );
        }

        while (queryMilk.MoveNext(out var uid, out var containerMilk))
        {
            if (now < containerMilk.NextGrowth)
                continue;

            containerMilk.NextGrowth = now + containerMilk.GrowthDelay;

            if (_mobState.IsDead(uid))
                continue;

            if (!_solutionContainer.ResolveSolution(uid, containerMilk.SolutionName, ref containerMilk.Solution))
                continue;

            if (TryComp(uid, out HungerComponent? hunger))
            {
                if (_hunger.GetHungerThreshold(hunger) < HungerThreshold.Okay)
                    continue;
                _solutionContainer.TryAddReagent(
                    containerMilk.Solution.Value,
                    containerMilk.ReagentId,
                    containerMilk.QuantityPerUpdate,
                    out var quantity
                );
                if (quantity > 0)
                {
                    _hunger.ModifyHunger(uid, -containerMilk.HungerUsage, hunger);
                    continue;
                }
            }
            _solutionContainer.TryAddReagent(
                containerMilk.Solution.Value,
                containerMilk.ReagentId,
                containerMilk.QuantityPerUpdate,
                out _
            );
        }

        while (queryPiss.MoveNext(out var uid, out var containerPiss))
        {
            if (now < containerPiss.NextGrowth)
                continue;

            containerPiss.NextGrowth = now + containerPiss.GrowthDelay;

            if (_mobState.IsDead(uid))
                continue;

            if (!_solutionContainer.ResolveSolution(uid, containerPiss.SolutionName, ref containerPiss.Solution))
                continue;
            if (TryComp(uid, out HungerComponent? hunger))
            {
                if (_hunger.GetHungerThreshold(hunger) < HungerThreshold.Okay)
                    continue;
                _solutionContainer.TryAddReagent(
                    containerPiss.Solution.Value,
                    containerPiss.ReagentId,
                    containerPiss.QuantityPerUpdate,
                    out var quantity
                );
                if (quantity > 0)
                {
                    _hunger.ModifyHunger(uid, -containerPiss.HungerUsage, hunger);
                }
                continue;
            }

            _solutionContainer.TryAddReagent(
                containerPiss.Solution.Value,
                containerPiss.ReagentId,
                containerPiss.QuantityPerUpdate,
                out _
            );
        }

        //if (!(now < containerSquirt.NextGrowth)) //Unused-Trait is WIP
        //{
        //    containerSquirt.NextGrowth = now + containerSquirt.GrowthDelay;

        //
        //    if (EntityManager.TryGetComponent(uid, out HungerComponent? hunger))
        //    {
        //
        //        if (!(_hunger.GetHungerThreshold(hunger) < HungerThreshold.Okay))
        //            _hunger.ModifyHunger(uid, -containerSquirt.HungerUsage, hunger);
        //    }

        //    if (_solutionContainer.ResolveSolution(uid, containerSquirt.SolutionName, ref containerSquirt.Solution))
        //        _solutionContainer.TryAddReagent(containerSquirt.Solution.Value, containerSquirt.ReagentId, containerSquirt.QuantityPerUpdate, out _);
        //}
    }
    #endregion
}
