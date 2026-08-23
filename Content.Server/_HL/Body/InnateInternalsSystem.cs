using Content.Server.Body.Components;
using Content.Shared._HL.Body;
using Content.Shared.Actions;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._HL.Body;

/// <summary>
/// Spawns the built in breathing apparatus of an <see cref="InnateInternalsComponent"/> and keeps it
/// wired into the entity's internals, filled with the gas the entity actually breathes and slowly
/// refilling whenever it isn't being breathed from.
/// </summary>
public sealed class InnateInternalsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedInternalsSystem _internals = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InnateInternalsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<InnateInternalsComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<InnateInternalsComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<InternalsComponent>(ent, out var internals))
            return;

        _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);

        var tool = SpawnInContainerOrDrop(ent.Comp.BreathTool, ent, ent.Comp.ContainerId);
        var tank = SpawnInContainerOrDrop(ent.Comp.GasTank, ent, ent.Comp.ContainerId);

        ent.Comp.BreathToolEntity = tool;
        ent.Comp.GasTankEntity = tank;
        ent.Comp.NextRegen = _timing.CurTime + ent.Comp.RegenInterval;
        Dirty(ent);

        _internals.ConnectBreathTool((ent, internals), tool);

        // The tank never ends up in hands or inventory, so hand out its internals toggle directly.
        if (TryComp<GasTankComponent>(tank, out var gasTank))
        {
            FillTank(ent, gasTank, ent.Comp.TankMoles);

            _actions.AddAction(ent, ref gasTank.ToggleActionEntity, gasTank.ToggleAction, tank);
            Dirty(tank, gasTank);
        }
    }

    private void OnShutdown(Entity<InnateInternalsComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<InternalsComponent>(ent, out var internals) && ent.Comp.BreathToolEntity is { } tool)
            _internals.DisconnectBreathTool((ent, internals), tool, forced: true);

        QueueDel(ent.Comp.BreathToolEntity);
        QueueDel(ent.Comp.GasTankEntity);

        ent.Comp.BreathToolEntity = null;
        ent.Comp.GasTankEntity = null;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<InnateInternalsComponent, InternalsComponent>();
        while (query.MoveNext(out var uid, out var comp, out var internals))
        {
            if (_timing.CurTime < comp.NextRegen)
                continue;

            var elapsed = (float) comp.RegenInterval.TotalSeconds;
            comp.NextRegen = _timing.CurTime + comp.RegenInterval;

            // The frame only tops the reservoir back up while it isn't being breathed from.
            if (comp.GasTankEntity is not { } tank || internals.GasTankEntity == tank)
                continue;

            if (!TryComp<GasTankComponent>(tank, out var gasTank))
                continue;

            var gas = GetBreathGas((uid, comp));
            var missing = comp.TankMoles - gasTank.Air.GetMoles(gas);

            if (missing <= 0f)
                continue;

            gasTank.Air.AdjustMoles(gas, MathF.Min(missing, comp.RegenMolesPerSecond * elapsed));
            Dirty(tank, gasTank);
        }
    }

    private void FillTank(Entity<InnateInternalsComponent> ent, GasTankComponent gasTank, float moles)
    {
        gasTank.Air.Clear();
        gasTank.Air.Temperature = Atmospherics.T20C;
        gasTank.Air.SetMoles(GetBreathGas(ent), moles);
    }

    /// <summary>
    /// Nitrogen breathers such as vox and slimes suffocate on the default oxygen fill, so the
    /// reservoir matches whatever their lungs want.
    /// </summary>
    private Gas GetBreathGas(Entity<InnateInternalsComponent> ent)
    {
        foreach (var lung in _body.GetBodyOrganEntityComps<LungComponent>((ent.Owner, null)))
        {
            if (lung.Comp1.Alert == ent.Comp.NitrogenBreatherAlert)
                return ent.Comp.NitrogenBreatherGas;
        }

        // Organs aren't always available yet, so fall back to the species.
        if (TryComp<HumanoidAppearanceComponent>(ent, out var humanoid) &&
            ent.Comp.NitrogenBreatherSpecies.Contains(humanoid.Species.Id))
        {
            return ent.Comp.NitrogenBreatherGas;
        }

        return ent.Comp.Gas;
    }
}
