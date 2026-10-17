using Content.Server.Body.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server.EntityEffects.Effects;

/// <summary>
/// Decrements the blood modification tracker when a blood-changing reagent is finishing its effects.
/// This should be used by reagents that have ChangeBloodReagent effects to properly clean up tracking.
/// </summary>
public sealed partial class DecrementBloodModificationTracker : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-decrement-blood-modification-tracker");

    public override void Effect(EntityEffectBaseArgs args)
    {
        args.EntityManager.RemoveComponentDeferred<BloodModificationTrackerComponent>(args.TargetEntity);
    }
}
