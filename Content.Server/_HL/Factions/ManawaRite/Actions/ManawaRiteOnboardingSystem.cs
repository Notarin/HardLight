using Content.Shared.Actions.Events;
using Content.Shared.Hands.EntitySystems;

namespace Content.Server._HL.Factions.ManawaRite.Actions;

public sealed class ManawaRiteOnboardingSystem : EntitySystem
{
    [Dependency]
    private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ManawaRiteOnboardingActionEvent>(OnManawaRiteOnboarding);
    }

    private void OnManawaRiteOnboarding(ManawaRiteOnboardingActionEvent args)
    {
        if (args.Handled)
            return;

        var coords = Transform(args.Performer).Coordinates;
        var package = Spawn("ManawaRiteOnboardingPackage", coords);
        _hands.TryPickupAnyHand(args.Performer, package);
        args.Handled = true;
    }
}
