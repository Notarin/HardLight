using Robust.Shared.GameStates;

namespace Content.Shared.FloofStation;

// Hardlight - Start
/// <summary>
/// Applied to entities inside a vore container, tracking which temporary survival immunities must be removed
/// once they leave.
/// </summary>
// Hardlight - End
[RegisterComponent, NetworkedComponent]
public sealed partial class DevouredComponent : Component
{
    public bool AddedPressure;
    public bool AddedBreathing;
    public bool AddedTemperature;
    public bool AddedRadiation;
}

