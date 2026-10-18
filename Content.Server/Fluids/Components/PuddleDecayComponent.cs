using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Fluids.Components;

/// <summary>
/// Tracks when a puddle should decay into water.
/// </summary>
[RegisterComponent]
public sealed partial class PuddleDecayComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("decayAt", customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan DecayAt = TimeSpan.Zero;

    [ViewVariables(VVAccess.ReadWrite), DataField("decayed")]
    public bool Decayed;
}
