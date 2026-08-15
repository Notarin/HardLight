using Content.Server.Standing;
using Content.Shared._HL.Hands;
using Content.Shared.Standing;

namespace Content.Server._HL.Hands;

/// <summary>
/// Cancels the item drop that normally comes with falling over for entities with a
/// <see cref="SteadyGripComponent"/>.
/// </summary>
public sealed class SteadyGripSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SteadyGripComponent, DropHandItemsEvent>(OnDropHandItems,
            before: new[] { typeof(Content.Server.Standing.StandingStateSystem) });
    }

    private void OnDropHandItems(EntityUid uid, SteadyGripComponent component, DropHandItemsEvent args)
    {
        args.Cancel();
    }
}
