using Content.Shared.StepTrigger.Systems;

namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Cancels certain step triggers for entities that have TrapAvoiderComponent. // HardLight: reworded
/// </summary>
public sealed class TrapAvoiderSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<AvoidableStepTriggerComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt); // HardLight: StepTriggerComponent>AvoidableStepTriggerComponent
    }

    private void OnStepTriggerAttempt(Entity<AvoidableStepTriggerComponent> ent, ref StepTriggerAttemptEvent args) // HardLight: StepTriggerComponent>AvoidableStepTriggerComponent
    {
        if (HasComp<TrapAvoiderComponent>(args.Tripper))
            args.Cancelled = true;
    }
}
