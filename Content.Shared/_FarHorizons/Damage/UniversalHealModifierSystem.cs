using Content.Shared.Damage;

namespace Content.Shared._FarHorizons.Damage;

public sealed class UniversalHealModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalHealModifierComponent, HealModifyEvent>(OnHealModify);
    }

    private void OnHealModify(Entity<UniversalHealModifierComponent> ent, ref HealModifyEvent args)
    {
        var damage = new DamageSpecifier(args.Damage); // HardLight
        foreach (var (key, value) in args.Damage.DamageDict)
            if (value < 0)
                damage.DamageDict[key] = value * ent.Comp.Modifier;

        args.Damage = damage;
    }
}
