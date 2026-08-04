namespace Content.Server._HL.Traits.Physical;

[RegisterComponent]
public sealed partial class TemperatureToleranceComponent : Component
{
    /// <summary>
    /// Raises the temperature where high-temperature damage starts, in Kelvin.
    /// </summary>
    [DataField]
    public float HeatDamageThresholdModifier;

    /// <summary>
    /// Lowers the temperature where low-temperature damage starts, in Kelvin.
    /// </summary>
    [DataField]
    public float ColdDamageThresholdModifier;
}
