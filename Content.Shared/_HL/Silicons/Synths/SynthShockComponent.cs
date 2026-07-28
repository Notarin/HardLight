using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._HL.Silicons.Synths;

[RegisterComponent]
public sealed partial class SynthShockComponent : Component
{
    /// <summary>
    /// Damage type that grants battery charge when taken.
    /// </summary>
    [DataField]
    public ProtoId<DamageTypePrototype> ShockDamageType = "Shock";

    /// <summary>
    /// Battery charge gained per point of matching shock damage taken.
    /// </summary>
    [DataField]
    public float BatteryChargeMultiplier = 2f;

    /// <summary>
    /// Damage type applied as a side effect of matching shock damage.
    /// </summary>
    [DataField]
    public ProtoId<DamageTypePrototype> CellularDamageType = "Cellular";

    /// <summary>
    /// Cellular damage dealt per point of matching shock damage taken.
    /// </summary>
    [DataField]
    public float CellularDamageMultiplier = 0.2f;
}
