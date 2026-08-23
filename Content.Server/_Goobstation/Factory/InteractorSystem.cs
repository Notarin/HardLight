using Content.Shared._Goobstation.Factory;
using Content.Server.Construction.Components;
using Content.Goobstation.Shared.Factory;

namespace Content.Server._Goobstation.Factory;

public sealed class InteractorSystem : SharedInteractorSystem
{
    private EntityQuery<ConstructionComponent> _constructionQuery;

    public override void Initialize()
    {
        base.Initialize();

        _constructionQuery = GetEntityQuery<ConstructionComponent>();

        SubscribeLocalEvent<InteractorComponent, MachineStartedEvent>(OnStarted);
    }

    private void OnStarted(Entity<InteractorComponent> ent, ref MachineStartedEvent args)
    {
        // Try to find a single valid target in front of the interactor.
        if (HasDoAfter(ent))
        {
            Machine.Failed(ent.Owner);
            return;
        }

        var targetUid = FindTarget(ent);
        if (targetUid == null)
        {
            Machine.Failed(ent.Owner);
            return;
        }

        var target = targetUid.Value;
        _constructionQuery.TryComp(target, out var construction);
        var originalCount = construction?.InteractionQueue?.Count ?? 0;

        if (!InteractWith(ent, target))
        {
            Machine.Failed(ent.Owner);
            return;
        }

        // construction supercode queues it instead of starting a doafter now, assume that queuing means it has started
        var newCount = construction?.InteractionQueue?.Count ?? 0;
        if (newCount > originalCount || HasDoAfter(ent))
        {
            Machine.Started(ent.Owner);
            UpdateAppearance(ent, InteractorState.Active);
        }
        else
        {
            // no doafter, complete it immediately
            Machine.Completed(ent.Owner);
            UpdateAppearance(ent);
        }
    }
}
