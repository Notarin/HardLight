using Content.Shared._HL.Silicons.Synths.Battery;
using Content.Shared._HL.Railroading.Events;
using Content.Server.Power.EntitySystems;

namespace Content.Server._HL.Silicons.Synths.Battery;

public sealed partial class SynthBatteryMetabolismSystem : EntitySystem
{
    private const string NutrimentReagent = "Nutriment";

    [Dependency] private SynthBatteryEffectsSystem _effects = default!;
    [Dependency] private SynthBatterySystem _synthBattery = default!;
    [Dependency] private BatterySystem _battery = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SynthBatteryComponent, RailroadingReagentMetabolizedEvent>(OnReagentMetabolized);
    }

    private void OnReagentMetabolized(Entity<SynthBatteryComponent> ent, ref RailroadingReagentMetabolizedEvent args)
    {
        if (ent.Comp.NutrimentChargeMultiplier <= 0f ||
            args.Reagent.Reagent.Prototype != NutrimentReagent ||
            !_synthBattery.TryGetBattery(ent.Owner, out var battery, ent.Comp))
            return;

        var changed = _battery.ChangeCharge(
            battery.Value.Owner,
            args.Reagent.Quantity.Float() * ent.Comp.NutrimentChargeMultiplier,
            battery.Value.Comp);

        if (changed <= 0f || !ent.Comp.Unpowered)
            return;

        ent.Comp.Unpowered = false;
        Dirty(ent);
        _effects.RefreshUnpoweredEffects(ent);
    }
}
