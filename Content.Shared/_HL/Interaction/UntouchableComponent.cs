using Robust.Shared.GameStates;

namespace Content.Shared._HL.Interaction;

/// <summary>
/// This component disallows other entities from hugging, kissing, biting, petting, and licking mobs that have this trait!
/// </summary>
[RegisterComponent]
[NetworkedComponent]
public sealed partial class UntouchableComponent : Component;
