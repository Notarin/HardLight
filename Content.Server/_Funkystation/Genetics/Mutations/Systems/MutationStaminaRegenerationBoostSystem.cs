using Content.Server._Funkystation.Genetics.Mutations.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;

namespace Content.Server._Funkystation.Genetics.Mutations.Systems;

public sealed class MutationStaminaRegenerationBoostSystem : EntitySystem
{
    [Dependency] private readonly SharedStaminaSystem _staminaSystem = default!; // HardLight: StaminSystem<SharedStaminaSystem
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MutationStaminaRegenerationBoostComponent, StaminaComponent>();
        while (query.MoveNext(out var uid, out var boost, out var stamina))
        {
            if (stamina.ActiveDrains.Count == 0)
            {
                _staminaSystem.TakeStaminaDamage(uid, -boost.RegenBonus * frameTime, stamina, visual: false);
            }
        }
    }
}
