using Content.Shared._Funkystation.Genetics.Mutations.Components;
using Content.Shared._Funkystation.Genetics.Mutations.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable; // HardLight

namespace Content.Server._Funkystation.Genetics.Mutations.Systems;

public sealed class CrawlSpeedBoostSystem : SharedCrawlSpeedBoostSystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movespeed = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrawlSpeedBoostComponent, KnockedDownRefreshEvent>(OnRefresh); // HardLight
        SubscribeLocalEvent<CrawlSpeedBoostComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, CrawlSpeedBoostComponent comp, ComponentInit args)
    {
        _movespeed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnRefresh(EntityUid uid, CrawlSpeedBoostComponent comp, ref KnockedDownRefreshEvent args) // HardLight: RefreshMovementSpeedModifiersEvent>ref KnockedDownRefreshEvent
    {
        if (!TryComp<CrawlerComponent>(uid, out var crawler)) // HardLight
            return;

        args.SpeedModifier *= comp.TargetSpeedMult / crawler.SpeedModifier; // HardLight
    }
}
