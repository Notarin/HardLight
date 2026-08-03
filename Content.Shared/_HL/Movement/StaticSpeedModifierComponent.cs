using Robust.Shared.GameStates;

namespace Content.Shared._HL.Movement;

/// <summary>
/// Permanently scales the movement speed of whatever it is put on.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StaticSpeedModifierComponent : Component
{
    [DataField, AutoNetworkedField]
    public float WalkModifier = 1f;

    [DataField, AutoNetworkedField]
    public float SprintModifier = 1f;
}
