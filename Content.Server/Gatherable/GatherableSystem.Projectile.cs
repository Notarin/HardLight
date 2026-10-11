using Content.Server.Gatherable.Components;
using Content.Shared.Mining.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;

namespace Content.Server.Gatherable;

public sealed partial class GatherableSystem
{
    private void InitializeProjectile()
    {
        SubscribeLocalEvent<GatheringProjectileComponent, StartCollideEvent>(OnProjectileCollide);
    }

    private void OnProjectileCollide(Entity<GatheringProjectileComponent> gathering, ref StartCollideEvent args)
    {
        if (!args.OtherFixture.Hard ||
            args.OurFixtureId != SharedProjectileSystem.ProjectileFixture ||
            gathering.Comp.Amount <= 0 ||
            !TryComp<GatherableComponent>(args.OtherEntity, out var gatherable))
        {
            return;
        }

        // Frontier: gathering changes
        // bad gatherer - not strong enough
        if (_whitelistSystem.IsWhitelistFail(gatherable.ToolWhitelist, gathering.Owner))
        {
            QueueDel(gathering);
            return;
        }
        // Too strong (e.g. overpen) - gathers ore but destroys it
        // HL - Removed this because it's dumb and makes no sense
        // if (TryComp<OreVeinComponent>(args.OtherEntity, out var oreVein)
        //    && _whitelistSystem.IsWhitelistPass(oreVein.GatherDestructionWhitelist, gathering.Owner))
        // {
        //    oreVein.PreventSpawning = true;
        //}
        // End Frontier: gathering changes

        // HL START
        // Track weapons that have already hit this tile, as a single pellet is enough to destroy
        // We track the gun itself so that we don't sneak in any more hits between now and destruction.
        if (TryComp<ProjectileComponent>(gathering, out var proj) && proj.Weapon.HasValue)
        {
            if (gatherable.AlreadyHit.Contains(proj.Weapon.Value))
            {
                gathering.Comp.Amount--; // Decrease the Amount so that the projectile can still hit blocks behind correctly.
                return;
            }
            gatherable.AlreadyHit.Add(proj.Weapon.Value);
        }
        // HL END

        Gather(args.OtherEntity, gathering, gatherable);
        gathering.Comp.Amount--;

        if (gathering.Comp.Amount <= 0)
            QueueDel(gathering);
    }
}
