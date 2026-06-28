using Content.Shared.Animals.Components;
using Content.Shared.Verbs;
using Robust.Shared.Player;

namespace Content.Shared.Animals.Systems;

/*
    HL
    Moved LewdEggLayingSystem to a shared system so that the AddVerb can be called clientside to deal with lag when right-clicking objects/players.
    LewdEggLayingComponent is now shared, and we had to network the eggs count.
    We also had to move the AddEgg to a helper function to keep any processing code out of the Component and in the Shared space
    The LewdEggLayingSystem on the Server is mostly handling everything, with an empty class of the same name on the Client so that the client can run the AddVerb code locally.
*/
public abstract class SharedLewdEggLayingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LewdEggLayingComponent, GetVerbsEvent<InnateVerb>>(AddLayEggInsideVerb);
    }

    private void AddLayEggInsideVerb(Entity<LewdEggLayingComponent> user, ref GetVerbsEvent<InnateVerb> args)
    {
        // Todo figure out how to only make verb appear for player mobs
        var target = args.Target;
        if (
            !args.CanInteract
            || user.Owner == target
            || !user.Comp.hasEggs()
            || !TryComp(target, out ActorComponent? actor)
        )
            return;

        InnateVerb verbLayEgg = new()
        {
            Act = () => AttemptLayInside(user, target),
            Text = Loc.GetString($"lay-egg-inside-verb-get-text"),
            Priority = 1,
        };
        args.Verbs.Add(verbLayEgg);
    }

    protected void AddEggs(EntityUid uid, LewdEggLayingComponent comp, float amt)
    {
        comp.eggs = Math.Clamp(comp.eggs + amt, 0, comp.MaxEggs);
        if (amt > 0)
        {
            comp.eggsFlavorAccum += amt;
        }
        DirtyField(uid, comp, nameof(LewdEggLayingComponent.eggs));
    }

    protected virtual void AttemptLayInside(Entity<LewdEggLayingComponent> user, EntityUid target) { }
}
