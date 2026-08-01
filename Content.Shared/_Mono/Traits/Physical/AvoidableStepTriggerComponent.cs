// HardLight

using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Traits.Physical;

/// <summary>
/// Marks a step trigger as something that TrapAvoiderComponent can avoid.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AvoidableStepTriggerComponent : Component;
