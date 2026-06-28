using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FloofStation.Traits.Events.Components;
using Content.Shared.Verbs;

namespace Content.Shared.FloofStation.Traits.Events;

/*
    HL
    Moved LewdTrait to a shared system so that the AddVerb can be called clientside to deal with lag when right-clicking objects/players.
    CumProducerComponent, PissProducerComponent and MilkProducerComponent are now shared, but don't have any networked vars as it's not needed for what we're doing here.
    Technically we could do client-side prediction on cum now, but I don't think the networking overhead is worthwhile.
    The LewdTraitSystem on the Server is mostly handling everything, with an empty class of the same name on the Client so that the client can run the AddVerb code locally.
*/
public abstract class SharedLewdTraitSystem : EntitySystem
{
    [Dependency]
    private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();

        //Verbs
        SubscribeLocalEvent<CumProducerComponent, GetVerbsEvent<InnateVerb>>(AddCumVerb);
        SubscribeLocalEvent<RefillableSolutionComponent, GetVerbsEvent<AlternativeVerb>>(AddRefillableInsideVerbs);
        SubscribeLocalEvent<InjectableSolutionComponent, GetVerbsEvent<AlternativeVerb>>(AddInjectableInsideVerbs);
        SubscribeLocalEvent<MilkProducerComponent, GetVerbsEvent<AlternativeVerb>>(AddMilkVerbs); // Hardlight added AlternativeVerb and additional Verb
        //SubscribeLocalEvent<SquirtProducerComponent, GetVerbsEvent<InnateVerb>>(AddSquirtVerb); //Unused-Trait is WIP
    }

    public void AddCumVerb(Entity<CumProducerComponent> entity, ref GetVerbsEvent<InnateVerb> args)
    {
        if (
            args.Using == null
            || !args.CanInteract
            || args.User != args.Target
            || !HasComp<RefillableSolutionComponent>(args.Using.Value)
        ) //see if removing this part lets you milk on the ground.
            return;

        _solutionContainer.EnsureSolution(entity.Owner, entity.Comp.SolutionName, out _);

        var user = args.User;
        var used = args.Using.Value;

        InnateVerb verbCum = new()
        {
            Act = () => AttemptCum(entity, user, used),
            Text = Loc.GetString($"cum-verb-get-text"),
            Priority = 1,
        };
        args.Verbs.Add(verbCum);
    }

    // Combined handler for RefillableSolutionComponent verbs (cum and piss)
    public void AddRefillableInsideVerbs(
        EntityUid uid,
        RefillableSolutionComponent component,
        GetVerbsEvent<AlternativeVerb> args
    )
    {
        if (!args.CanInteract)
            return;

        var user = args.User;
        var target = uid;

        // Add cum verb if user has CumProducerComponent
        if (TryComp<CumProducerComponent>(args.User, out var cumProducer))
        {
            _solutionContainer.EnsureSolution(args.User, cumProducer.SolutionName, out _);

            AlternativeVerb verbCumInside = new()
            {
                Act = () => AttemptCum((args.User, cumProducer), user, target),
                Text = Loc.GetString("cum-verb-inside-text"),
                Priority = -50, // HardLight: 2<-50; Should never happen as an alt+click verb unless absolutely no other alt-click verbs are available.
            };
            args.Verbs.Add(verbCumInside);
        }

        // Add piss verb if user has PissProducerComponent
        if (TryComp<PissProducerComponent>(args.User, out var pissProducer))
        {
            _solutionContainer.EnsureSolution(args.User, pissProducer.SolutionName, out _);

            AlternativeVerb verbPissInside = new()
            {
                Act = () => AttemptPiss((args.User, pissProducer), user, target),
                Text = Loc.GetString("piss-verb-inside-text"),
                Priority = -50, // HardLight: 2<-50; Should never happen as an alt+click verb unless absolutely no other alt-click verbs are available.
            };
            args.Verbs.Add(verbPissInside);
        }
    }

    // Combined handler for InjectableSolutionComponent verbs (cum and piss)
    public void AddInjectableInsideVerbs(
        EntityUid uid,
        InjectableSolutionComponent component,
        GetVerbsEvent<AlternativeVerb> args
    )
    {
        if (!args.CanInteract)
            return;

        var user = args.User;
        var target = uid;

        // Add cum verb if user has CumProducerComponent
        if (TryComp<CumProducerComponent>(args.User, out var cumProducer))
        {
            _solutionContainer.EnsureSolution(args.User, cumProducer.SolutionName, out _);

            AlternativeVerb verbCumInside = new()
            {
                Act = () => AttemptCum((args.User, cumProducer), user, target),
                Text = Loc.GetString("cum-verb-inside-text"),
                Priority = -50, // HardLight: 2<-50; Should never happen as an alt+click verb unless absolutely no other alt-click verbs are available.
            };
            args.Verbs.Add(verbCumInside);
        }

        // Add piss verb if user has PissProducerComponent
        if (TryComp<PissProducerComponent>(args.User, out var pissProducer))
        {
            _solutionContainer.EnsureSolution(args.User, pissProducer.SolutionName, out _);

            AlternativeVerb verbPissInside = new()
            {
                Act = () => AttemptPiss((args.User, pissProducer), user, target),
                Text = Loc.GetString("piss-verb-inside-text"),
                Priority = -50, // HardLight: 2<-50; Should never happen as an alt+click verb unless absolutely no other alt-click verbs are available.
            };
            args.Verbs.Add(verbPissInside);
        }
    }

    // Hardlight Start

    public void AddMilkVerbs(Entity<MilkProducerComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        AddMilkVerb(entity, ref args);
        AddDrinkMilkVerb(entity, ref args);
    }

    // Hardlight End

    public void AddMilkVerb(Entity<MilkProducerComponent> entity, ref GetVerbsEvent<AlternativeVerb> args) // Hardlight Changed to AlternativeVerb
    {
        if (
            args.Using == null
            || !args.CanInteract
            ||
            // Hardlight removed self-cast only
            !HasComp<RefillableSolutionComponent>(args.Using.Value)
        ) //see if removing this part lets you milk on the ground.
            return;

        _solutionContainer.EnsureSolution(entity.Owner, entity.Comp.SolutionName, out _);

        var user = args.User;
        var used = args.Using.Value;

        AlternativeVerb verbMilk = new() // Hardlight Changed to AlternativeVerb
        {
            Act = () => AttemptMilk(entity, user, used),
            Text = Loc.GetString($"milk-verb-get-text"),
            Priority = 1,
        };
        args.Verbs.Add(verbMilk);
    }

    // Hardlight Start
    public void AddDrinkMilkVerb(Entity<MilkProducerComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract)
            return;

        _solutionContainer.EnsureSolution(entity.Owner, entity.Comp.SolutionName, out _);

        var user = args.User;

        AlternativeVerb verbDrinkMilk = new()
        {
            Act = () => AttemptDrinkMilk(entity, user),
            Text = Loc.GetString($"drink-milk-verb-get-text"),
            Priority = 1,
        };
        args.Verbs.Add(verbDrinkMilk);
    }

    // Note: AddPissInsideVerb and AddPissInsideInjectableVerb have been combined into
    // AddRefillableInsideVerbs and AddInjectableInsideVerbs above

    // Hardlight End

    //public void AddSquirtVerb(Entity<SquirtProducerComponent> entity, ref GetVerbsEvent<InnateVerb> args) //Unused-Trait is WIP
    //{
    //    if (args.Using == null ||
    //         !args.CanInteract ||
    //         !EntityManager.HasComponent<RefillableSolutionComponent>(args.Using.Value)) //see if removing this part lets you milk on the ground.
    //        return;

    //    _solutionContainer.EnsureSolution(entity.Owner, entity.Comp.SolutionName);

    //    var user = args.User;
    //    var used = args.Using.Value;

    //    InnateVerb verbSquirt = new()
    //    {
    //        Act = () => AttemptSquirt(entity, user, used),
    //        Text = Loc.GetString($"squirt-verb-get-text"),
    //        Priority = 1
    //    };
    //    args.Verbs.Add(verbSquirt);
    //}

    // Stubs for the rest of the actual code, is handled on the Server LewdTraitSystem and we don't need the client running it too.
    protected virtual void AttemptCum(Entity<CumProducerComponent> lewd, EntityUid userUid, EntityUid containerUid) { }

    protected virtual void AttemptMilk(
        Entity<MilkProducerComponent> lewd,
        EntityUid userUid,
        EntityUid containerUid
    ) { }

    protected virtual void AttemptDrinkMilk(Entity<MilkProducerComponent> lewd, EntityUid userUid) { }

    protected virtual void AttemptPiss(
        Entity<PissProducerComponent> lewd,
        EntityUid userUid,
        EntityUid containerUid
    ) { }
}
