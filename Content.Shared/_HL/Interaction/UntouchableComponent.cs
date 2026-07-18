using Robust.Shared.GameStates;

namespace Content.Shared._HL.Interaction;

/// <summary>
/// This component disallows other entities from biting, hugging, kissing, licking, passively pulling, and petting entities that have this trait!
/// </summary>
[RegisterComponent]
[NetworkedComponent]
public sealed partial class UntouchableComponent : Component
{
    /// <summary>
    /// Local spam gate for lean-away popups (pull + left-click). Not networked.
    /// </summary>
    [ViewVariables]
    public TimeSpan LastPopupTime;
}
