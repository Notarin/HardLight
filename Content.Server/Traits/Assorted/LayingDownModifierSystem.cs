using Content.Server.Traits.Assorted;
using Content.Shared.Stunnable; // HardLight

namespace Content.Shared.Traits.Assorted.Systems;

public sealed class LayingDownModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LayingDownModifierComponent, GetStandUpTimeEvent>(OnGetStandUpTime); // HardLight
        SubscribeLocalEvent<LayingDownModifierComponent, KnockedDownRefreshEvent>(OnKnockedDownRefresh); // HardLight
    }

    // HardLight-edit start
    private void OnGetStandUpTime(EntityUid uid, LayingDownModifierComponent component, ref GetStandUpTimeEvent args)
    {
        args.DoAfterTime *= component.LayingDownCooldownMultiplier;
    }

    private void OnKnockedDownRefresh(EntityUid uid, LayingDownModifierComponent component, ref KnockedDownRefreshEvent args)
    {
        args.SpeedModifier *= component.DownedSpeedMultiplierMultiplier;
    }
    // HardLight-edit end
}
