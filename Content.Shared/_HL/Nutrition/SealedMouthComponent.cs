using Robust.Shared.GameStates;

namespace Content.Shared._HL.Nutrition;

/// <summary>
/// Prevents the entity from eating or drinking anything, as if its mouth were permanently covered.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SealedMouthComponent : Component
{
    /// <summary>
    /// Popup shown to the entity when it tries to ingest something.
    /// </summary>
    [DataField]
    public LocId Popup = "sealed-mouth-cannot-ingest";
}
