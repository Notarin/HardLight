using Robust.Shared.GameStates;

namespace Content.Shared._HL.Hands;

/// <summary>
/// Keeps the entity from dropping and scattering whatever it is holding when it falls over.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SteadyGripComponent : Component
{
}
