using Content.Shared._DV.Abilities;
using Content.Shared._Funkystation.Genetics.Mutations.Components;
using Content.Shared._Funkystation.Genetics.Mutations.Systems;
using Content.Shared.Stunnable; // HardLight

namespace Content.Client._Funkystation.Genetics.Systems;

public sealed class CrawlSpeedBoostSystem : SharedCrawlSpeedBoostSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrawlSpeedBoostComponent, KnockedDownRefreshEvent>(OnRefresh); // HardLight: RefreshMovementSpeedModifiersEvent>KnockedDownRefreshEvent
    }

    private void OnRefresh(EntityUid uid, CrawlSpeedBoostComponent comp, ref KnockedDownRefreshEvent args) // HardLight: RefreshMovementSpeedModifiersEvent>ref KnockedDownRefreshEvent
    {
        if (!TryComp<CrawlerComponent>(uid, out var crawler)) // HardLight
            return;

        args.SpeedModifier *= comp.TargetSpeedMult / crawler.SpeedModifier; // HardLight
    }
}
