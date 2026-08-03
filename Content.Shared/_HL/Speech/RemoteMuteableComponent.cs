using Robust.Shared.GameStates;

namespace Content.Shared._HL.Speech;

/// <summary>
/// Lets anyone who can reach this entity mute it through a verb, reducing its speech to mumbling.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RemoteMuteableComponent : Component
{
    /// <summary>
    /// Whether the entity is currently muted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Muted;

    [DataField]
    public LocId MuteVerb = "remote-mute-verb-mute";

    [DataField]
    public LocId UnmuteVerb = "remote-mute-verb-unmute";

    [DataField]
    public LocId MutedPopup = "remote-mute-muted";

    [DataField]
    public LocId UnmutedPopup = "remote-mute-unmuted";
}
