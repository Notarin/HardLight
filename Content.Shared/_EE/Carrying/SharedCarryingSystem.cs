using Content.Shared.Hands.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nyanotrasen.Item.PseudoItem;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Map.Components;

namespace Content.Shared.Carrying;

/*
    HL
    Moved CarryingSystem to a shared system so that the AddVerb can be called clientside to deal with lag when right-clicking objects/players.
    CarriableComponent and CarryingComponent are now shared, and we had to network a few fields to keep the verb code the same.
    The CarryingSystem on the Server is mostly handling everything, with an empty class of the same name on the Client so that the client can run the AddVerb code locally.
*/
public abstract class SharedCarryingSystem : EntitySystem
{
    [Dependency]
    private readonly SharedPseudoItemSystem _pseudoItem = default!;

    [Dependency]
    private readonly MobStateSystem _mobStateSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CarriableComponent, GetVerbsEvent<AlternativeVerb>>(AddCarryVerb);
        SubscribeLocalEvent<CarryingComponent, GetVerbsEvent<InnateVerb>>(AddInsertCarriedVerb);
    }

    private void AddCarryVerb(EntityUid uid, CarriableComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (
            !args.CanInteract
            || !args.CanAccess
            || !_mobStateSystem.IsAlive(args.User)
            || !CanCarry(args.User, uid, component)
            || HasComp<CarryingComponent>(args.User)
            || HasComp<BeingCarriedComponent>(args.User)
            || HasComp<BeingCarriedComponent>(args.Target)
            || args.User == args.Target
        )
            return;

        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                StartCarryDoAfter(args.User, uid, component);
            },
            Text = Loc.GetString("carry-verb"),
            Priority = 2,
        };
        args.Verbs.Add(verb);
    }

    private void AddInsertCarriedVerb(EntityUid uid, CarryingComponent component, GetVerbsEvent<InnateVerb> args)
    {
        // If the person is carrying someone, and the carried person is a pseudo-item, and the target entity is a storage,
        // then add an action to insert the carried entity into the target
        var toInsert = args.Using;
        if (
            toInsert is not { Valid: true }
            || !args.CanAccess
            || !TryComp<PseudoItemComponent>(toInsert, out var pseudoItem)
            || !TryComp<StorageComponent>(args.Target, out var storageComp)
            || !_pseudoItem.CheckItemFits((toInsert.Value, pseudoItem), (args.Target, storageComp))
        )
            return;

        InnateVerb verb = new()
        {
            Act = () =>
            {
                DropCarried(uid, toInsert.Value);
                _pseudoItem.TryInsert(args.Target, toInsert.Value, pseudoItem, storageComp);
            },
            Text = Loc.GetString("action-name-insert-other", ("target", toInsert)),
            Priority = 2,
        };
        args.Verbs.Add(verb);
    }

    public bool CanCarry(EntityUid carrier, EntityUid carried, CarriableComponent? carriedComp = null)
    {
        if (
            !Resolve(carried, ref carriedComp, false)
            || carriedComp.HasCancelToken
            || !HasComp<MapGridComponent>(Transform(carrier).ParentUid)
            || HasComp<BeingCarriedComponent>(carrier)
            || HasComp<BeingCarriedComponent>(carried)
            || !TryComp<HandsComponent>(carrier, out var hands)
            || hands.CountFreeHands() < carriedComp.FreeHandsRequired
        )
            return false;

        return true;
    }

    // HL: Stubs for the verb actions so the server can still handle the actual code but the client can see the actions
    protected virtual void StartCarryDoAfter(EntityUid carrier, EntityUid carried, CarriableComponent component) { }

    public virtual void DropCarried(EntityUid carrier, EntityUid carried) { }
}
