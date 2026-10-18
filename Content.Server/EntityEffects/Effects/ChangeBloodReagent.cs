using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server.EntityEffects.Effects;

public sealed partial class ChangeBloodReagent : EntityEffect
{
    [DataField("bloodReagent", required: true)]
    public string BloodReagent = string.Empty;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-change-blood-reagent", ("reagent", BloodReagent));

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args.EntityManager.TryGetComponent<BloodstreamComponent>(args.TargetEntity, out var bloodstream))
        {
            var sys = args.EntityManager.System<BloodstreamSystem>();
            sys.ChangeBloodReagent(args.TargetEntity, BloodReagent, bloodstream);

            // Add or increment blood modification tracker
            var tracker = args.EntityManager.EnsureComponent<BloodModificationTrackerComponent>(args.TargetEntity);
            tracker.ActiveEffects++;
        }
    }
}
