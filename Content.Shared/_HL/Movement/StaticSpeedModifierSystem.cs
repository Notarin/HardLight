using Content.Shared.Movement.Systems;

namespace Content.Shared._HL.Movement;

public sealed class StaticSpeedModifierSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StaticSpeedModifierComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<StaticSpeedModifierComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StaticSpeedModifierComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnRefreshSpeed(EntityUid uid, StaticSpeedModifierComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.WalkModifier, component.SprintModifier);
    }

    private void OnStartup(EntityUid uid, StaticSpeedModifierComponent component, ComponentStartup args)
    {
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    private void OnShutdown(EntityUid uid, StaticSpeedModifierComponent component, ComponentShutdown args)
    {
        _movement.RefreshMovementSpeedModifiers(uid);
    }
}
