using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.GameStates;

namespace Content.Shared._HL.Insurance.Components;

/// <summary>
/// Marks an item as belonging to a server-side insurance policy.
/// The generation lets ship loads discard stale copies after a policy has been claimed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class InsuredItemComponent : Component
{
    [DataField]
    public string PolicyId = string.Empty;

    [DataField]
    public long Generation;

    [DataField]
    public string OwnerUserId = string.Empty;

    [DataField]
    public string OwnerCharacterKey = string.Empty;
}
