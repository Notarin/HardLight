using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._HL.Rescue.Rescue;

/// <summary>
/// Receives <see cref="Content.Shared.Salvage.Fulton.FultonedComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RescueBeaconComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("soundLink"), AutoNetworkedField]
    public SoundSpecifier? LinkSound = new SoundPathSpecifier("/Audio/Items/beep.ogg");
}
