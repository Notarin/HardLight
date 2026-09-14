using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._DV.Body.Components;

/// <summary>
/// Marks this entity as a feather, that probably has a custom color
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FeatherComponent : Component
{
    // HardLight edit start - saving of feather colors

    /// <summary>
    /// Feather color.
    /// </summary>
    [DataField]
    public Color? FeatherColor;

    /// <summary>
    /// Colors of the blood that covers the feather.
    /// </summary>
    [DataField]
    public Color? BloodColor;
    // HardLight edit end
}

/// <summary>
/// Visual data for feathers
/// </summary>
[Serializable, NetSerializable]
public enum FeatherVisuals : byte
{
    FeatherColor,
    BloodColor,
}
